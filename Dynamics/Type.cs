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
        static readonly Func<T, Dictionary<object, object>, T> deepCopy;

        /// <summary>
        /// Dynamically checks value state for mutability.
        /// </summary>
        static readonly Func<T, HashSet<object>, bool> isMutable;

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
        static readonly ConcurrentDictionary<Type, Func<T, HashSet<object>, bool>> subtypeMutability = new ConcurrentDictionary<Type, Func<T, HashSet<object>, bool>>();

        /// <summary>
        /// Used to dispatch to the dynamic type to copy.
        /// </summary>
        static readonly ConcurrentDictionary<Type, Func<T, Dictionary<object, object>, T>> subtypeCopy = new ConcurrentDictionary<Type, Func<T, Dictionary<object, object>, T>>();

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
            
            deepCopy = Mutability == Mutability.Immutable ? null : GenerateCopy(type);
        }

        /// <summary>
        /// Performs a deep copy of an object, if necessary.
        /// </summary>
        /// <param name="value">The value to copy.</param>
        /// <returns>A deep copy of the value.</returns>
        public static T Copy(T value)
        {
            return Mutability == Mutability.Immutable ? value : Copy(value, new Dictionary<object, object>());
        }

        /// <summary>
        /// Checks a value's mutability.
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public static bool IsMutable(T value)
        {
            return Mutability == Mutability.Mutable
                || Mutability == Mutability.Maybe && IsMutable(value, new HashSet<object>());
        }

        #region Copy helpers
        internal static T Copy(T value, Dictionary<object, object> refs)
        {
            return Mutability == Mutability.Immutable ? value : deepCopy(value, refs);
        }

        static Func<T, Dictionary<object, object>, T> GenerateCopy(Type type)
        {
            var x = Expression.Parameter(type, "x");
            var refs = Expression.Parameter(typeof(Dictionary<object, object>), "refs");
            var dc = x as Expression;
            if (type.IsArray)
            {
                var acopy = new Func<int[], Dictionary<object, object>, int[]>(Runtime.Copy<int>).Method.GetGenericMethodDefinition();
                dc = Expression.Call(acopy.MakeGenericMethod(type.GetElementType()), x, refs);
            }
            else
            {
                var members = new List<MemberBinding>();
                var rofields = new Dictionary<string, Expression>();
                var noEmptyCtor = !type.IsValueType && !HasEmptyConstructor(type);
                foreach (var field in type.GetFields(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance))
                {
                    var access = Expression.Field(x, field);
                    var copy = typeof(Type<>).MakeGenericType(field.FieldType)
                                             .GetMethod("Copy", BindingFlags.Static | BindingFlags.NonPublic, null,
                                                        new[] { field.FieldType, typeof(Dictionary<object, object>) }, null);
                    var ecopy = Expression.Call(copy, access, refs);
                    if (field.IsInitOnly || noEmptyCtor)
                        rofields.Add(field.FieldName().ToLower(), ecopy);
                    else
                        members.Add(Expression.Bind(field, ecopy));
                }
                var newe = rofields.Count == 0 ? Expression.New(type):
                                                 ConstructNew(type, rofields);
                dc = Expression.MemberInit(newe, members);
                if (!type.IsValueType)
                {
                    var tgv = typeof(Dictionary<object, object>).GetMethod("TryGetValue");
                    var copied = Expression.Parameter(typeof(object), "copied");
                    var checkCache = Expression.Condition(
                            Expression.Call(refs, tgv, x, copied),
                            Expression.Convert(copied, type),
                            dc);
                    dc = Expression.Block(new[] { copied }, checkCache);
                }
            }
            return Expression.Lambda<Func<T, Dictionary<object, object>, T>>(dc, x, refs).Compile();
        }

        static NewExpression ConstructNew(Type type, Dictionary<string, Expression> copies)
        {
            var ctors = type.GetConstructors();
            ConstructorInfo ctor = null;
            var bindings = new List<Expression>();
            foreach (var x in ctors)
            {
                ctor = x;
                var args = x.GetParameters();
                if (args.Length != copies.Count)
                    continue;
                foreach (var p in args)
                {
                    // find the field whose name matches the constructor parameter, else find one matching the type
                    Expression e;
                    if (!copies.TryGetValue(p.Name.ToLower(), out e))
                        e = copies.Values.First(z => z.Type == p.ParameterType);
                    bindings.Add(e);
                }
            }
            //return ctor == null ? Expression.New(type) : Expression.New(ctor, bindings);
            return Expression.New(ctor, bindings);
        }
        #endregion
        #region Constructor helpers
        static bool HasEmptyConstructor(Type type)
        {
            return null != type.GetConstructor(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, Type.EmptyTypes, null);
        }

        static T DefaultCtor()
        {
            return default(T);
        }
        #endregion

        #region Mutability helpers
        static bool IsMutable(T value, HashSet<object> visited)
        {
            //FIXME: this actually needs a private overload that accepts a HashSet<object> to ensure we don't visit
            //a node more than once.
            switch (Mutability)
            {
                case Mutability.Immutable:
                    return false;
                case Mutability.Mutable:
                    return true;
                case Mutability.Maybe:
                    var type = value?.GetType();
                    if (type == typeof(T))
                        return (value is ValueType || visited.Add(value)) && isMutable(value, visited);
                    Func<T, HashSet<object>, bool> f;
                    if (!subtypeMutability.TryGetValue(type, out f))
                    {
                        var dispatch = new Func<T, HashSet<object>, bool>(DispatchIsMutable<T>).Method.GetGenericMethodDefinition();
                        f = subtypeMutability[type] = (Func<T, HashSet<object>, bool>)Delegate.CreateDelegate(typeof(Func<T, HashSet<object>, bool>), dispatch.MakeGenericMethod(type));
                    }
                    return f(value, visited);
                    //return value is ValueType || visited.Add(value)
                    //     ? isMutable(value, visited)
                    //     : false;
                default:
                    throw new InvalidOperationException("Unknown Mutability value.");
            }
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

        static Mutability TransitiveMutability(Type type, out Func<T, HashSet<object>, bool> isMutable)
        {
            // sealed types are immutable if they have init-only fields and all fields are immutable
            // non-sealed types may not be immutable, and so generate a residual program to check the dynamic state
            var typeMutable = typeof(Type<>).GetField("Mutability");
            var x = Expression.Parameter(typeof(T), "x");
            var visited = Expression.Parameter(typeof(HashSet<object>), "visited");
            var chkMut = Expression.Constant(false) as Expression;
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
                            var fmut = ftype.GetMethod("IsMutable", BindingFlags.Static | BindingFlags.NonPublic,
                                                       null, new[] { field.FieldType, typeof(HashSet<object>) }, null);
                            chkMut = Expression.Or(Expression.Call(fmut, Expression.Field(x, field), visited), chkMut);
                            break;
                    }
                }
            }
            isMutable = mut == Mutability.Maybe
                      ? Expression.Lambda<Func<T, HashSet<object>, bool>>(chkMut, x, visited).Compile()
                      : null;
            return mut;
        }
        
        static bool DispatchIsMutable<T0>(T value, HashSet<object> visited)
            where T0 : T
        {
            return Type<T0>.IsMutable((T0)value, visited);
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
        #endregion
    }
}
