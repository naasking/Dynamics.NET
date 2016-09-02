using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Dynamics
{
    /// <summary>
    /// Indicates whether a type's structure permits cycles.
    /// </summary>
    public enum Cycles
    {
        /// <summary>
        /// No cycles in a type's structure.
        /// </summary>
        No,

        /// <summary>
        /// Type structure allows cycles, but depends on runtime parameters.
        /// </summary>
        Yes,
    }
}
