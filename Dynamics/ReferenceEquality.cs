using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Dynamics
{
    /// <summary>
    /// An equality comparer that compares by reference identity, ignoring any
    /// <see cref="object.Equals(object)"/> and <see cref="object.GetHashCode"/> overrides.
    /// </summary>
    /// <remarks>
    /// This is the comparer used for the 'refs' map that <see cref="Type{T}.Copy(T)"/> threads
    /// through a deep copy to preserve sharing and terminate on cycles. That map is keyed on
    /// object identity, so it must not consult a type's equality overrides: a type with value
    /// equality would alias two distinct-but-equal objects onto one entry, and the second object
    /// copied would be handed the first one's copy. That silently merges two objects into one in
    /// the result, and throws <see cref="InvalidCastException"/> outright when the two are of
    /// different types in the same hierarchy, eg. a base class B with value equality and a
    /// subclass D, where a B is copied before an equal D.
    /// <para>
    /// Callers that supply their own map to <see cref="Type{T}.Copy(T, Dictionary{object, object})"/>
    /// should construct it with <see cref="Instance"/> for the same reason.
    /// </para>
    /// </remarks>
    public sealed class ReferenceEquality : IEqualityComparer<object>
    {
        /// <summary>
        /// The single instance of <see cref="ReferenceEquality"/>.
        /// </summary>
        public static readonly ReferenceEquality Instance = new ReferenceEquality();

        ReferenceEquality() { }

        /// <summary>
        /// Compare two objects by reference identity.
        /// </summary>
        public new bool Equals(object x, object y) => ReferenceEquals(x, y);

        /// <summary>
        /// The identity hash code of <paramref name="obj"/>.
        /// </summary>
        public int GetHashCode(object obj) => RuntimeHelpers.GetHashCode(obj);
    }
}
