using System;
using System.Linq;
using System.Linq.Expressions;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Reflection;

namespace Dynamics
{
    [Pure]
    public static class Mutable<T>
    {
        /// <summary>
        /// Exposes the conservative mutability of <typeparamref name="T"/>.
        /// </summary>
        public static readonly Mutability Mutability;

        /// <summary>
        /// Dynamically checks value state for mutability.
        /// </summary>
        static readonly Func<T, HashSet<object>, bool> isMutable;

        /// <summary>
        /// Used to dispatch to the dynamic type to check for mutability.
        /// </summary>
        static readonly ConcurrentDictionary<Type, Func<T, HashSet<object>, bool>> subtypeMutability = new ConcurrentDictionary<Type, Func<T, HashSet<object>, bool>>();

        static Mutable()
        {
            // Immutable: any types decorated with [Pure] || T has init-only fields whose types are immutable
            // Maybe: if any fields have types with Mutability.Maybe
            // Mutable: otherwise
            // Need a list of sanitized core immutable types: Int32, DateTime, decimal, Exception, Attribute, Tuple<*>, etc.
            var type = typeof(T);
            Mutability = ImmutableWhitelist(type) ? Mutability.Immutable:
                         MutableBlacklist(type)   ? Mutability.Mutable:
                                                    TransitiveMutability(type, out isMutable);
        }

        /// <summary>
        /// Checks a value's mutability.
        /// </summary>
        /// <param name="value">The value to check for mutability.</param>
        /// <returns>True if the current configuration is mutable, false otherwise.</returns>
        public static bool IsMutable(T value)
        {
            return Mutability == Mutability.Mutable
                || Mutability == Mutability.Maybe && IsMutable(value, new HashSet<object>());
        }

        #region Mutability helpers
        static bool IsMutable(T value, HashSet<object> visited)
        {
            //FIXME: can we exploit cycle detection to avoid adding to 'visited'? This
            //may create duplicate work in DAGs by possibly traversing same node twice.
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
                        var dispatch = new Func<T, HashSet<object>, bool>(DispatchIsMutable<T>).Method
                                       .GetGenericMethodDefinition()
                                       .MakeGenericMethod(type);
                        f = subtypeMutability[type] = (Func<T, HashSet<object>, bool>)Delegate.CreateDelegate(typeof(Func<T, HashSet<object>, bool>), dispatch);
                    }
                    return f(value, visited);
                default:
                    throw new InvalidOperationException("Unknown Mutability value.");
            }
        }

        static bool ImmutableWhitelist(Type type)
        {
            return type.GetCustomAttribute<PureAttribute>() != null
                || type.IsPrimitive
                || type == typeof(DateTime)
                || type == typeof(TimeSpan)
                || type == typeof(DateTimeOffset)
                || type == typeof(decimal)
                || type == typeof(string)
                || type == typeof(System.Linq.Expressions.Expression)
                || type.Subtypes(typeof(Enum))
                || type.Subtypes(typeof(MemberInfo));
        }

        static bool MutableBlacklist(Type type)
        {
            return type.Subtypes(typeof(Delegate))
                || type.IsArray;
        }

        static Mutability TransitiveMutability(Type type, out Func<T, HashSet<object>, bool> isMutable)
        {
            // sealed types are immutable if they have init-only fields and all fields are immutable
            // non-sealed types may not be immutable, and so generate a residual program to check the dynamic state
            var typeMutable = typeof(Mutable<>).GetField("Mutability");
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
                    var ftype = typeof(Mutable<>).MakeGenericType(field.FieldType);
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
            return Mutable<T0>.IsMutable((T0)value, visited);
        }

        static bool AllPureMethods(Type type)
        {
            // T is impure if any method is not decorated with [Pure] or is a static method that does not accept a T
            //FIXME: should also exclude operators?
            var mflags = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.FlattenHierarchy;
            var types = new[]
            {
                typeof(IFormattable), typeof(IConvertible), typeof(ICloneable), typeof(IComparable),
                typeof(IComparable<>).MakeGenericType(type), typeof(IEquatable<>).MakeGenericType(type),
                typeof(IFormatProvider), typeof(ICustomFormatter), typeof(ICustomAttributeProvider),
                typeof(IComparer<>).MakeGenericType(type),
                //Kind.Definition.Apply(typeof(IGrouping<>), type), Kind.Definition.Apply(typeof(ILookup<>), type),
                typeof(IOrderedQueryable<>).MakeGenericType(type), typeof(IOrderedQueryable),
                typeof(IOrderedEnumerable<>).MakeGenericType(type),
                typeof(IQueryable<>).MakeGenericType(type), typeof(IQueryable),
                typeof(IReflect), typeof(ISafeSerializationData), typeof(IServiceProvider),
                //Type.GetType("System.ITuple, mscorlib") ?? Type.GetType("System.ITuple, netstandard"),  //loading this dynamically because not yet available publicly
                typeof(IStructuralEquatable), typeof(IStructuralComparable),
                typeof(ISurrogateSelector), typeof(object), typeof(ValueType)
            }
            .Where(x => type.Subtypes(x));
            var methods = types.SelectMany(x => x.IsInterface ? type.GetInterfaceMap(x).TargetMethods : x.GetMethods(mflags))
                               .Aggregate(new HashSet<int>(), (acc, x) => { acc.Add(x.GetBaseDefinition().MetadataToken); return acc; });
            //FIXME: internal fields can bypass mutability analysis
            return type.GetMethods(mflags)
                       .All(x => methods.Contains(x.GetBaseDefinition().MetadataToken)
                              || null != x.GetCustomAttribute<PureAttribute>() || x.IsPureGetter() || x.IsPureSetter()
                              || x.IsStatic && !Array.Exists(x.GetParameters(), p => p.ParameterType == type));
        }
        #endregion
    }
}
