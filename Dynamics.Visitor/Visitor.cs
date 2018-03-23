using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Dynamics
{
    /// <summary>
    /// A generic visitor dispatcher.
    /// </summary>
    /// <typeparam name="TVisitor">The visitor to dispatch to.</typeparam>
    public static class Visitor<TVisitor>
        where TVisitor : class
    {
        /// <summary>
        /// Invoke the visitor's best matching method.
        /// </summary>
        /// <typeparam name="T">The type being dispatched on.</typeparam>
        /// <param name="visitor">The visitor being dispatched to.</param>
        /// <param name="value">The value being dispatched.</param>
        public static void Invoke<T>(TVisitor visitor, T value)
        {
            Visitor<TVisitor, T>.Invoke(visitor, value);
        }
    }

    /// <summary>
    /// A generic visitor dispatcher.
    /// </summary>
    /// <typeparam name="TVisitor">The visitor to dispatch to.</typeparam>
    /// <typeparam name="T">The type being dispatched.</typeparam>
    public static class Visitor<TVisitor, T>
        where TVisitor : class
    {
        /// <summary>
        /// A delegate that dispatches to the best handler in <typeparamref name="TVisitor"/>
        /// for a value of type <typeparamref name="T"/>.
        /// </summary>
        public static readonly Action<TVisitor, T> Invoke = Method.Resolve<Action<TVisitor, T>>();
    }

    sealed class SubtypeComparer : IComparer<Type>
    {
        public int Compare(Type x, Type y)
        {
            if (x == y) return 0;
            var xsuby = x.Subtypes(y) && !x.IsGenericParameter;
            var ysubx = y.Subtypes(x) && !y.IsGenericParameter;
            return xsuby && ysubx ?  0:
                   xsuby          ? -1:
                                     1;
        }
    }
}
