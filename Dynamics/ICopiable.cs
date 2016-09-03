using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Dynamics
{
    /// <summary>
    /// Identifies types that can deep copy themselves.
    /// </summary>
    /// <typeparam name="TSelf">The type being copied.</typeparam>
    /// <remarks>
    /// The type need not perform an actual deep copy, but it must at least
    /// preserve thread safety and other desirable properties of copies.
    /// 
    /// The object must also add itself to <paramref name="references"/>
    /// before building any child objects in order to preserve sharing.
    /// </remarks>
    public interface ICopiable<TSelf>
        where TSelf : class
    {
        /// <summary>
        /// Copy the current object.
        /// </summary>
        /// <param name="references">A dictionary mapping old references to new copies.</param>
        /// <returns>A copy of this object.</returns>
        TSelf Copy(Dictionary<object, object> references);
    }
}
