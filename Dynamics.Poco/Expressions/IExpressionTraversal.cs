using System;
using System.Reflection;
using System.Linq.Expressions;

namespace Dynamics.Poco.Expressions
{
    /// <summary>
    /// Custom POCO operations.
    /// </summary>
    /// <typeparam name="TContext">The context carried through the traversal.</typeparam>
    /// <remarks>
    /// Clients must implement this interface in order to traverse objects efficiently.
    /// </remarks>
    public interface IExpressionTraversal<TContext>
    {
        //Expression Constructor<T>();

        /// <summary>
        /// Lookup any traversal overrides.
        /// </summary>
        /// <typeparam name="TObject"></typeparam>
        /// <returns>Return null if no override, otherwise an expression that overrides the default traversal behaviour.</returns>
        Expression<Func<TObject, TContext, TObject>> Override<TObject>();

        /// <summary>
        /// Initialize any new objects.
        /// </summary>
        /// <param name="obj">The object being initialized.</param>
        /// <param name="ctxt"></param>
        /// <returns></returns>
        Expression Init(Expression obj, Expression ctxt);

        /// <summary>
        /// Operate on an object's member.
        /// </summary>
        /// <typeparam name="TObject"></typeparam>
        /// <typeparam name="TMember"></typeparam>
        /// <param name="obj"></param>
        /// <param name="ctxt"></param>
        /// <param name="property"></param>
        /// <returns></returns>
        Expression Member<TObject, TMember>(Expression obj, Expression ctxt, PropertyInfo property);
    }
}
