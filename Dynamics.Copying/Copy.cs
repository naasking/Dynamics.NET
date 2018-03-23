using System;
using System.Linq;
using System.Reflection;
using System.Linq.Expressions;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Dynamics
{
    public static class Copy<T>
    {
        /// <summary>
        /// Performs a deep copy of any value.
        /// </summary>
        static Func<T, Dictionary<object, object>, T> deepCopy;

        /// <summary>
        /// Used to dispatch to the dynamic type to copy.
        /// </summary>
        static readonly ConcurrentDictionary<Type, Func<T, Dictionary<object, object>, T>> subtypeCopy = new ConcurrentDictionary<Type, Func<T, Dictionary<object, object>, T>>();

        static Copy()
        {
            deepCopy = Mutable<T>.Mutability == Mutability.Immutable ? null : GenerateCopy(typeof(T));
        }

        /// <summary>
        /// Performs a deep copy of an object, if necessary.
        /// </summary>
        /// <param name="value">The value to copy.</param>
        /// <returns>A deep copy of the value.</returns>
        public static T DeepCopy(T value)
        {
            return Mutable<T>.Mutability == Mutability.Immutable ? value : DeepCopy(value, new Dictionary<object, object>());
        }

        /// <summary>
        /// The copy method that preserves sharing and circular references.
        /// </summary>
        /// <param name="value"></param>
        /// <param name="refs"></param>
        /// <returns></returns>
        public static T DeepCopy(T value, Dictionary<object, object> refs)
        {
            if (Mutable<T>.Mutability == Mutability.Immutable || value == null)
                return value;
            var type = value?.GetType();
            object x;
            if (type != typeof(T))
            {
                // type is a subtype of T, so dispatch to the appropriate handler
                Func<T, Dictionary<object, object>, T> f;
                if (!subtypeCopy.TryGetValue(type, out f))
                {
                    var dispatch = new Func<T, Dictionary<object, object>, T>(DispatchCopy<T>).Method
                                   .GetGenericMethodDefinition()
                                   .MakeGenericMethod(type);
                    f = subtypeCopy[type] = dispatch.Create<Func<T, Dictionary<object, object>, T>>();
                }
                return f(value, refs);
            }
            else if (refs.TryGetValue(value, out x))
            {
                return (T)x;
            }
            else
            {
                return deepCopy(value, refs);
            }
        }

        /// <summary>
        /// Override the default auto-generated copy method with a more efficient one.
        /// </summary>
        /// <param name="copy">The delegate that overrides deep copying behaviour.</param>
        public static void OverrideCopy(Func<T, Dictionary<object, object>, T> copy)
        {
            deepCopy = copy;
        }

        #region Copy helpers
        static T DispatchCopy<T0>(T value, Dictionary<object, object> refs)
            where T0 : T
        {
            return Copy<T0>.DeepCopy((T0)value, refs);
        }

        static Func<T, Dictionary<object, object>, T> GenerateCopy(Type type)
        {
            // if T is an interface type, generate nothing since the base copy code will already
            // dispatch to the concrete subtype for copying purposes
            if (type.IsInterface) return null;
            // if type implements ICopiable, return delegate dispatching to Copy method
            if (!type.IsValueType)
            {
                var icopy = typeof(ICopiable<>).MakeGenericType(type);
                if (type.Subtypes(icopy))
                    return icopy.GetMethod("Copy").Create<Func<T, Dictionary<object, object>, T>>();
            }
            // handle arrays and delegates specially
            if (type.IsArray)
                return new Func<int[], Dictionary<object, object>, int[]>(Copying.Array)
                       .Method
                       .GetGenericMethodDefinition()
                       .MakeGenericMethod(type.GetElementType())
                       .Create<Func<T, Dictionary<object, object>, T>>();
            if (type.Subtypes<Delegate>())
                return new Func<Action, Dictionary<object, object>, Action>(Copying.Delegate)
                       .Method
                       .GetGenericMethodDefinition()
                       .MakeGenericMethod(type)
                       .Create<Func<T, Dictionary<object, object>, T>>();
            // if type has a method in Copying static class, then dispatch to that
            var matchName = type.IsGenericType ? type.Name.Remove(type.Name.Length - 2):
                                                 type.Name;
            var match = Copying.Methods
                        .SingleOrDefault(m => m.Name.Equals(matchName, StringComparison.Ordinal)
                                           && (m.ReturnType == type || m.ReturnType.ContainsGenericParameters && type.IsGenericType));
            if (match != null)
            {
                if (match.IsGenericMethod)
                    match = match.MakeGenericMethod(type.GetGenericArguments());
                return match.Create<Func<T, Dictionary<object, object>, T>>();
            }
            // if we get here, we need to dynamically generate a deepCopy method
            var x = Expression.Parameter(type, "x");
            var refs = Expression.Parameter(typeof(Dictionary<object, object>), "refs");
            var dc = x as Expression;
            var y = Expression.Variable(type, "y");
            var members = new List<Expression>();
            var rofields = new Dictionary<string, Expression>();
            var noEmptyCtor = !type.IsValueType && !HasEmptyConstructor(type);
            members.Add(null); // reserve slot for: y = new T(read-only-fields)
            // need to add new obj to 'refs' before copying children in case of child->parent reference.
            if (!type.IsValueType)
            {
                var tgv = typeof(Dictionary<object, object>).GetMethod("Add", new[] { typeof(object), typeof(object) });
                members.Add(Expression.Call(refs, tgv, x, y));
            }
            // copy each member one at a time
            foreach (var field in type.GetFields(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance))
            {
                var access = Expression.Field(x, field);
                var copy = typeof(Copy<>).MakeGenericType(field.FieldType)
                                         .GetMethod(nameof(DeepCopy), BindingFlags.Static | BindingFlags.Public, null,
                                                    new[] { field.FieldType, typeof(Dictionary<object, object>) }, null);
                var ecopy = Expression.Call(copy, access, refs);
                if (field.IsInitOnly || noEmptyCtor)
                    rofields.Add(field.FieldName().ToLower(), ecopy);
                else
                    members.Add(Expression.Assign(Expression.Field(y, field), ecopy));
            }
            members[0] = Expression.Assign(y, ConstructNew(type, rofields));
            members.Add(y);
            dc = Expression.Block(new[] { y }, members);
            return Expression.Lambda<Func<T, Dictionary<object, object>, T>>(dc, x, refs).Compile();
        }

        static NewExpression ConstructNew(Type type, Dictionary<string, Expression> copies)
        {
            if (copies.Count == 0)
                return Expression.New(type);
            var ctors = type.GetConstructors();
            ConstructorInfo ctor = null;
            var bindings = new List<Expression>();  // constructor parameter bindings
            var used = new HashSet<string>();       // tracks used members from 'copies'
            foreach (var x in ctors)
            {
                ctor = x;
                var args = x.GetParameters();
                if (args.Length != copies.Count)
                    continue;
                bindings.Clear();
                used.Clear();
                foreach (var p in args)
                {
                    // find field name matching the parameter name, else find one matching the type
                    Expression e;
                    var name = p.Name.ToLower();
                    if (!copies.TryGetValue(name, out e))
                    {
                        e = copies.First(z => z.Value.Type == p.ParameterType && !used.Contains(z.Key)).Value;
                        used.Add(name); // ensure same member isn't used twice
                    }
                    bindings.Add(e);
                }
                if (copies.Count != bindings.Count)
                    throw new InvalidOperationException("Couldn't find appropriate constructor.");
            }
            return Expression.New(ctor, bindings);
        }

        static bool HasEmptyConstructor(Type type)
        {
            return null != type.GetConstructor(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, Type.EmptyTypes, null);
        }
        #endregion
    }
}
