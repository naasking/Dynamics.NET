using System;
using System.Collections.Generic;
using System.Text;

namespace Dynamics
{
    /// <summary>
    /// Indicates that a class or member is pure.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Struct | AttributeTargets.Constructor)]
    public class PureAttribute : Attribute
    {
    }
}
