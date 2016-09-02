using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Runtime.Serialization;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.CompilerServices;
using System.Diagnostics.Contracts;

namespace Dynamics
{
    /// <summary>
    /// A class exposing metadata about type <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The type to analyze.</typeparam>
    [Pure]
    public static class Type<T>
    {
        /// <summary>
        /// Empty constructor for <typeparamref name="T"/>.
        /// </summary>
        /// <remarks>
        /// This may throw a SecurityException if <typeparamref name="T"/> has no empty constructor and isn't a struct.
        /// </remarks>
        public static readonly Func<T> Create;

        /// <summary>
        /// Exposes the conservative mutability of <typeparamref name="T"/>.
        /// </summary>
        public static readonly Mutability Mutability;

        /// <summary>
        /// Exposes whether the type structure allows cycles.
        /// </summary>
        public static readonly Circularity Circularity;

        /// <summary>
        /// Performs a deep copy of any value.
        /// </summary>
        static readonly Func<T, T> deepCopy;

        /// <summary>
        /// Dynamically checks value state for mutability.
        /// </summary>
        static readonly Func<T, bool> isMutable;

        //FIXME: add structural equality?
        //FIXME: add structural comparison?

        /// <summary>
        /// The cached delegate for <see cref="EqualityComparer{T}.Default.Equals"/>.
        /// </summary>
        public static readonly Func<T, T, bool> DefaultEquals = EqualityComparer<T>.Default.Equals;

        /// <summary>
        /// The cached delegate for <see cref="EqualityComparer{T}.Default.GetHashCode"/>.
        /// </summary>
        public static readonly Func<T, int> DefaultHash = EqualityComparer<T>.Default.GetHashCode;

        /// <summary>
        /// Used to dispatch to the dynamic type to check for mutability.
        /// </summary>
        static readonly ConcurrentDictionary<Type, Func<T, bool>> subtypeMutability = new ConcurrentDictionary<Type, Func<T, bool>>();

        static Type()
        {
            var type = typeof(T);
            // initialize the default constructor: if T:new(), then invoke the parameterless constructor
            // else if it's an array or struct, then simply return default(T)
            Create = type.IsArray || type.IsValueType ? DefaultCtor:
                     type == typeof(string)           ? Expression.Lambda<Func<T>>(Expression.Constant(string.Empty)).Compile():
                     HasEmptyConstructor(type)        ? Expression.Lambda<Func<T>>(Expression.New(typeof(T))).Compile():
                                                        () => (T)FormatterServices.GetUninitializedObject(type);

            // Immutable: any types decorated with [Pure] || T has init-only fields whose types are immutable
            // Maybe: if any fields have types with Mutability.Maybe
            // Mutable: otherwise
            // Need a list of sanitized core immutable types: Int32, DateTime, decimal, Exception, Attribute, Tuple<*>, etc.
            Mutability = ImmutableWhitelist(type) ? Mutability.Immutable:
                         MutableBlacklist(type)   ? Mutability.Mutable:
                                                    TransitiveMutability(type, out isMutable);
            
            //deepCopy = Mutability == Mutability.Immutable ? null : GenerateCopy(type);
        }

        static bool ImmutableWhitelist(Type type)
        {
            return type.Has<PureAttribute>()
                || type.IsPrimitive
                || type == typeof(DateTime)
                || type == typeof(TimeSpan)
                || type == typeof(DateTimeOffset)
                || type == typeof(decimal)
                || type == typeof(string)
                || type == typeof(System.Linq.Expressions.Expression)
                || typeof(Enum).IsAssignableFrom(type);
        }

        static bool MutableBlacklist(Type type)
        {
            return typeof(Delegate).IsAssignableFrom(type)
                || type.IsArray;
        }

        /// <summary>
        /// Performs a deep copy of an object, if necessary.
        /// </summary>
        /// <param name="value">The value to copy.</param>
        /// <returns>A deep copy of the value.</returns>
        public static T Copy(T value)
        {
            //FIXME: this actually needs a private overload that accepts a Dictionary<object, object> to ensure
            //we don't visit to prevent infinite recursion in the presence of cycles and to preserve sharing. Structs
            //should not be added to the map.
            //FIXME: this should also consult circularity property to check whether to add to visited set.
            //FIXME: if struct, then just call new(), otherwise call MemberwiseClone().
            return IsMutable(value) ? deepCopy(value) : value;
        }

        /// <summary>
        /// Checks a value's mutability.
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public static bool IsMutable(T value)
        {
            //FIXME: this actually needs a private overload that accepts a HashSet<object> to ensure we don't visit
            //a node more than once. Actually, should probably use Dictionary<object, object> so we can share the
            //map with Copy so we don't need to track both a HashSet and a Dictionary.
            switch (Mutability)
            {
                case Mutability.Immutable:
                    return false;
                case Mutability.Mutable:
                    return true;
                case Mutability.Maybe:
                    return isMutable(value);
                default:
                    throw new InvalidOperationException("Unknown Mutability value.");
            }
        }

        #region Internal helper methods
        static Func<T, T> GenerateCopy(Type type)
        {
            var x = Expression.Parameter(type, "x");
            var dc = x as Expression;
            foreach (var field in type.GetFields(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance))
            {
                var access = Expression.Field(x, field);
                var copy = typeof(Type<>).MakeGenericType(field.FieldType)
                                         .GetMethod("Copy");
                dc = Expression.Assign(access, Expression.Call(copy, access));
            }
            return Expression.Lambda<Func<T, T>>(dc, x).Compile();
        }

        static bool HasEmptyConstructor(Type type)
        {
            return null != type.GetConstructor(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, Type.EmptyTypes, null);
        }

        static Mutability TransitiveMutability(Type type, out Func<T, bool> isMutable)
        {
            // sealed types are immutable if they have init-only fields and all fields are immutable
            // non-sealed types may not be immutable, and so generate a residual program to check the dynamic state
            var typeMutable = typeof(Type<>).GetField("Mutability");
            var x = Expression.Parameter(typeof(T), "x");
            var chkMut = Expression.Constant(true) as Expression;
            var mut = type.IsSealed ? Mutability.Immutable : Mutability.Maybe;
            var pureMethods = AllPureMethods(type);
            foreach (var field in type.GetFields(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy))
            {
                // since this type already has no impure methods, then only public fields should matter
                if (!field.IsInitOnly && (field.IsPublic || !pureMethods))
                {
                    isMutable = null;
                    return Mutability.Mutable;
                }
                else
                {
                    var ftype = typeof(Type<>).MakeGenericType(field.FieldType);
                    switch ((Mutability)ftype.GetField("Mutability").GetValue(null))
                    {
                        case Mutability.Mutable:
                            mut = Mutability.Mutable;
                            break;
                        case Mutability.Maybe:
                            mut = Mutability.Maybe;
                            //chkMut = Expression.And(Expression.Call(Expression.Field(x, field), ftype.GetMethod("IsMutable")), chkMut);
                            break;
                    }
                }
            }
            if (mut == Mutability.Maybe)
            {
                //if (!type.IsSealed)
                //{
                //    // perform a dynamic type check and dispatch to dynamic type for non-sealed types
                //    var getType = type.GetMethod("GetType", BindingFlags.Instance | BindingFlags.Public | BindingFlags.FlattenHierarchy);
                //    var typeCheck = Expression.Equal(Expression.Call(x, getType), Expression.Constant(type));
                //    var subMut = type.GetMethod("IsSubtypeMutable", BindingFlags.NonPublic | BindingFlags.Static);
                //    chkMut = Expression.Condition(typeCheck, chkMut, Expression.Call(subMut, x));
                //}
                //isMutable = Expression.Lambda<Func<T, bool>>(chkMut, x).Compile();
                isMutable = null;
            }
            else
            {
                isMutable = null;
            }
            return mut;
        }

        static readonly MethodInfo isMutableInfo = typeof(Type<T>).GetMethod("IsMutable`1");

        static bool IsSubtypeMutable(T value)
        {
            Func<T, bool> f;
            var type = value.GetType();
            if (!subtypeMutability.TryGetValue(type, out f))
                f = subtypeMutability[type] = (Func<T, bool>)Delegate.CreateDelegate(typeof(Func<T, bool>), isMutableInfo.MakeGenericMethod(type));
            return f(value);
        }

        static bool IsMutable<T0>(T value)
            where T0 : T
        {
            return Type<T0>.IsMutable((T0)value);
        }

        static bool AllPureMethods(Type type)
        {
            // T is impure if any method is not decorated with [Pure] or is a static method that does not accept a T
            //FIXME: should also exclude operators?
            var mflags = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.FlattenHierarchy;
            var types = new[]
            {
                typeof(IFormattable), typeof(IConvertible), typeof(ICloneable), typeof(IComparable),
                Kind.Definition.Apply(typeof(IComparable<>), type), Kind.Definition.Apply(typeof(IEquatable<>), type),
                typeof(IFormatProvider), typeof(ICustomFormatter), typeof(ICustomAttributeProvider),
                Kind.Definition.Apply(typeof(IComparer<>), type), 
                //Kind.Definition.Apply(typeof(IGrouping<>), type), Kind.Definition.Apply(typeof(ILookup<>), type),
                Kind.Definition.Apply(typeof(IOrderedQueryable<>), type), typeof(IOrderedQueryable),
                Kind.Definition.Apply(typeof(IOrderedEnumerable<>), type),
                Kind.Definition.Apply(typeof(IQueryable<>), type), typeof(IQueryable),
                typeof(IReflect), typeof(ISafeSerializationData), typeof(IServiceProvider),
                Type.GetType("System.ITuple, mscorlib"), typeof(IStructuralEquatable), typeof(IStructuralComparable),
                typeof(ISurrogateSelector), typeof(object), typeof(ValueType)
            }
            .Where(x => type.Subtypes(x));
            var methods = types.SelectMany(x => x.IsInterface ? type.GetInterfaceMap(x).TargetMethods : x.GetMethods(mflags))
                               .Aggregate(new HashSet<int>(), (acc, x) => { acc.Add(x.GetBaseDefinition().MetadataToken); return acc; });
            //FIXME: internal fields can bypass mutability analysis
            return type.GetMethods(mflags)
                       .All(x => methods.Contains(x.GetBaseDefinition().MetadataToken)
                              || x.Has<PureAttribute>() || x.IsPureGetter() || x.IsPureSetter()
                              || x.IsStatic && !Array.Exists(x.GetParameters(), p => p.ParameterType == type));
        }

        static T DefaultCtor()
        {
            return default(T);
        }
        #endregion
    }
}
