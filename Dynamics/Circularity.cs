using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Dynamics
{
    /// <summary>
    /// Indicates a type structure is cyclic.
    /// </summary>
    public enum Circularity
    {
        /// <summary>
        /// No cycles in a type's structure.
        /// </summary>
        Ayclic,

        /// <summary>
        /// Type structure allows cycles, but depends on runtime parameters.
        /// </summary>
        Cyclic,
    }
}
