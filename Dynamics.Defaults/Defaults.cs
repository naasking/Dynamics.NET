using System;
using System.Collections.Generic;

namespace Dynamics
{
    public static class Defaults<T>
    {
        /// <summary>
        /// The cached delegate for <see cref="EqualityComparer{T}.Default"/>.Equals.
        /// </summary>
        public static readonly Func<T, T, bool> Equality = typeof(T).Subtypes(typeof(IEquatable<T>)) && !typeof(T).IsValueType
            ? typeof(T).GetMethod("Equals", new[] { typeof(T) }).Create<Func<T, T, bool>>()
            : EqualityComparer<T>.Default.Equals;

        /// <summary>
        /// The cached delegate for <see cref="EqualityComparer{T}.Default"/>.GetHashCode.
        /// </summary>
        public static readonly Func<T, int> HashCode = EqualityComparer<T>.Default.GetHashCode;
    }
}
