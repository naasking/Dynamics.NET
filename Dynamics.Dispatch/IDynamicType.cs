using System;
using System.Reflection;
using System.Collections.Concurrent;
using System.Linq;
using System.Text;

namespace Dynamics
{
    /// <summary>
    /// Accepts the type handler of the runtime type.
    /// </summary>
    public interface IDynamicType
    {
        /// <summary>
        /// The exact type.
        /// </summary>
        /// <typeparam name="T0">The exact runtime type.</typeparam>
        void Type<T0>();
    }
}
