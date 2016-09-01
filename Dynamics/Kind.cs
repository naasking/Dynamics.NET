using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Dynamics
{
    /// <summary>
    /// Kinds classify types into certain categories.
    /// </summary>
    public enum Kind
    {
        /// <summary>
        /// The <see cref="System.Type"/> is a type parameter.
        /// </summary>
        Parameter,

        /// <summary>
        /// The <see cref="System.Type"/> is a basic, non-generic type.
        /// </summary>
        Type,

        /// <summary>
        /// The <see cref="System.Type"/> is a generic type.
        /// </summary>
        Application,

        /// <summary>
        /// The <see cref="System.Type"/> is a generic type definition.
        /// </summary>
        Definition,

        /// <summary>
        /// Represents an unmanaged pointer.
        /// </summary>
        Pointer,

        /// <summary>
        /// Represents a managed reference.
        /// </summary>
        Reference,
    }
}
