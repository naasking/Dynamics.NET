using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections.Concurrent;

namespace Dynamics
{
    public static class Constructors
    {
        #region Interface instance resolver
        static readonly ConcurrentDictionary<Type, Tuple<Type, int[]>> typeMap = new ConcurrentDictionary<Type, Tuple<Type, int[]>>();

        /// <summary>
        /// Registers an association
        /// </summary>
        /// <param name="type">The abstract type.</param>
        /// <param name="instance">The instance type that inherits from the abstract type.</param>
        public static void Associate(Type type, Type instance)
        {
            if (!type.IsAbstract || !type.IsInterface)
                throw new ArgumentException("Argument must be an abstract or interface type.", "type");
            if (instance.IsAbstract || instance.IsInterface)
                throw new ArgumentException("Argument must not be an abstract or interface type.", "instance");
            if (!type.IsGenericTypeDefinition || !instance.IsGenericTypeDefinition)
                throw new ArgumentException("Arguments must both be generic type definitions.");
            var args = new List<int>();
            var impl = instance.GetInterfaces().SingleOrDefault(x => x.ContainsGenericParameters && x.GetGenericTypeDefinition() == type);
            if (impl == null) throw new ArgumentException(instance.Name + " does not inherit from or implement " + type.Name, "instance");
            var targs = impl.GetGenericArguments();
            var iargs = instance.GetGenericArguments();
            foreach (var x in targs)
                if (x.IsGenericParameter)
                    args.Add(Array.IndexOf(iargs, x));
            typeMap[type] = Tuple.Create(instance, args.ToArray());
        }

        /// <summary>
        /// Resolve a constructor for the given interface or abstract type.
        /// </summary>
        /// <typeparam name="T">The abstract or interface being instantiated.</typeparam>
        /// <returns>A delegate to creat an instance of an abstract or interface type.</returns>
        public static Func<T> Default<T>()
        {
            var type = typeof(T);
            Tuple<Type, int[]> idef;
            if (!type.IsGenericType || !typeMap.TryGetValue(type.GetGenericTypeDefinition(), out idef))
                return null;
            var targs = type.GetGenericArguments();
            var instance = idef.Item1.MakeGenericType(idef.Item2.Select(x => targs[x]).ToArray());
            var ctor = new Func<object>(Constructor<string, object>).Method.GetGenericMethodDefinition();
            return (Func<T>)ctor.MakeGenericMethod(instance, type).CreateDelegate(typeof(Func<T>));
        }
        static TAbstract Constructor<TInstance, TAbstract>()
            where TInstance : TAbstract
        {
            return Dynamics.Constructor<Func<TInstance>>.Invoke();
        }
        #endregion
    }
}
