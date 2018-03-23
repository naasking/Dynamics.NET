using System;
using System.Linq;
using System.Linq.Expressions;
using System.Collections.Generic;
using System.Reflection;

namespace Dynamics
{
    /// <summary>
    /// An interface that can generate delegates to traverse objects.
    /// </summary>
    /// <typeparam name="TContext">The context used during traversals.</typeparam>
    public interface IPocoMapper<TContext>
    {
        /// <summary>
        /// Compile a delegate to traverse an object of type <typeparamref name="TObject"/>.
        /// </summary>
        /// <typeparam name="TObject"></typeparam>
        /// <returns></returns>
        Func<TObject, TContext, TObject> Compile<TObject>();
    }
}
