using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Dynamics
{
    /// <summary>
    /// The type's mutability.
    /// </summary>
    public enum Mutability
    {
        /// <summary>
        /// The type is fully immutable.
        /// </summary>
        Immutable,

        /// <summary>
        /// Specific instances must be checked.
        /// </summary>
        Maybe,

        /// <summary>
        /// The type and any subtypes are mutable.
        /// </summary>
        Mutable,
    }
}
