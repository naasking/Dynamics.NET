using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Dynamics
{
    /// <summary>
    /// Cached constructor delegates.
    /// </summary>
    /// <typeparam name="TFunc">The constructor signature.</typeparam>
    public static class Constructor<TFunc>
        where TFunc : class
    {
        /// <summary>
        /// A delegate that constructs a type
        /// </summary>
        public static readonly TFunc Invoke;

        static Constructor()
        {
            // invoke Type<TFunc.ReturnType>.Constructor<TFunc>()
            var type = typeof(TFunc);
            var invoke = type.GetMethod("Invoke");
            Invoke = (TFunc)typeof(Type<>).MakeGenericType(invoke.ReturnType)
                                          .GetMethod("Constructor")
                                          .MakeGenericMethod(type)
                                          .Invoke(null, null);
        }
    }
}
