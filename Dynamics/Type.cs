using System;
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
            Mutability = 0 < type.GetCustomAttributes(typeof(PureAttribute), false).Length ? Mutability.Immutable:
                         typeof(Delegate).IsAssignableFrom(type) || HasImpureMethods(type) ? Mutability.Mutable:
                                                                                             TransitiveMutability(type, out isMutable);
            
            deepCopy = Mutability == Mutability.Immutable ? null : GenerateCopy(type);
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
            foreach (var field in type.GetFields(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy))
            {
                if (!field.IsInitOnly)
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
                            chkMut = Expression.And(Expression.Call(Expression.Field(x, field), ftype.GetMethod("IsMutable")), chkMut);
                            break;
                    }
                }
            }
            if (mut == Mutability.Maybe)
            {
                if (!type.IsSealed)
                {
                    // perform a dynamic type check and dispatch to dynamic type for non-sealed types
                    var getType = type.GetMethod("GetType", BindingFlags.Instance | BindingFlags.Public | BindingFlags.FlattenHierarchy);
                    var typeCheck = Expression.Equal(Expression.Call(x, getType), Expression.Constant(type));
                    var subMut = type.GetMethod("IsSubtypeMutable", BindingFlags.NonPublic | BindingFlags.Static);
                    chkMut = Expression.Condition(typeCheck, chkMut, Expression.Call(subMut, x));
                }
                isMutable = Expression.Lambda<Func<T, bool>>(chkMut, x).Compile();
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

        static bool HasImpureMethods(Type type)
        {
            // T is impure if any method is not decorated with [Pure] or is a static method that does not accept a T
            // skip standard methods, ie. GetHashCode, Equals, ToString, CompareTo, get_* and private set_* tagged with [CompilerGenerated], etc.
            //FIXME: should also exclude operators?
            var ieq = type.GetInterface("IEquatable`1");
            var icompg = type.GetInterface(typeof(IComparable<>).MakeGenericType(type).Name);
            var icomp = type.GetInterface("IComparable");
            var icln = type.GetInterface("ICloneable");
            var iconv = type.GetInterface("IConvertible");
            var iconvm = typeof(IConvertible).GetMethods().ToDictionary(x => x.Name);
            var typeArgs = new[] { type };
            return type.GetMethods(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.FlattenHierarchy)
                       .All(x =>
                       {
                           var args = x.GetParameters();
                           MethodInfo m;
                           return x.Name.Equals("GetHashCode") && x.DeclaringType == typeof(object)
                               || x.Name.Equals("Equals") && (x.DeclaringType == typeof(object) || ieq != null && args.Length == 1 && args[0].ParameterType == type)
                               || x.Name.Equals("ToString")
                               || x.Name.Equals("Clone") && icln != null && args.Length == 0
                               || iconv != null && iconvm.TryGetValue(x.Name, out m) && args.Select(z => z.ParameterType).SequenceEqual(m.GetParameters().Select(z => z.ParameterType))
                               || x.Name.Equals("CompareTo") && args.Length == 1 && (icompg != null && args[0].ParameterType == type || icomp != null && args[0].ParameterType == typeof(object))
                               || x.Name.StartsWith("get_") && x.GetCustomAttributes(typeof(CompilerGeneratedAttribute), true).Length != 0
                               || x.Name.StartsWith("set_") && x.IsPrivate && x.GetCustomAttributes(typeof(CompilerGeneratedAttribute), true).Length != 0
                               || x.GetCustomAttributes(typeof(PureAttribute), false).Length != 0
                               || x.IsStatic && !Array.Exists(args, p => p.ParameterType == type); //FIXME: internal fields can bypass mutability analysis
                       });
        }

        static T DefaultCtor()
        {
            return default(T);
        }
        #endregion
    }
}
