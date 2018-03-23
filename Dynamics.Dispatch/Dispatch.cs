using System;
using System.Linq;
using System.Collections.Concurrent;
using System.Reflection;

namespace Dynamics
{
    /// <summary>
    /// Bind a runtime type to a generic type variable.
    /// </summary>
    public static class Dispatch
    {
        #region Dynamic type resolver
        static readonly ConcurrentDictionary<Type, Dispatcher> entries = new ConcurrentDictionary<Type, Dispatcher>();

        /// <summary>
        /// Resolve a dynamic type to a type variable.
        /// </summary>
        /// <typeparam name="T">The dispatcher type.</typeparam>
        /// <param name="dispatch">The dispatcher.</param>
        /// <param name="type">The dynamic type.</param>
        public static void GetType<T>(ref T dispatch, Type type)
            where T : struct, IDynamicType
        {
            Dispatcher x;
            if (!entries.TryGetValue(type, out x))
            {
                x = (Dispatcher)typeof(Case<>)
                    .MakeGenericType(type)
                    .GetTypeInfo()
                    .DeclaredConstructors
                    .Single(z => z.GetParameters().Length == 0)
                    .Invoke(null);
                entries.TryAdd(type, x);
            }
            x.Dispatch(ref dispatch);
        }

        abstract class Dispatcher
        {
            public abstract void Dispatch<TDispatch>(ref TDispatch handler)
                where TDispatch : struct, IDynamicType;
        }
        sealed class Case<T> : Dispatcher
        {
            public override void Dispatch<TDispatch>(ref TDispatch dispatch)
            {
                dispatch.Type<T>();
            }
        }
        #endregion
    }
}
