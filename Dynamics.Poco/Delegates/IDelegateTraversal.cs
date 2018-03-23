using System;
using System.Collections.Generic;
using System.Text;

namespace Dynamics.Poco.Delegates
{
    /// <summary>
    /// Custom POCO operations.
    /// </summary>
    /// <typeparam name="TContext">The context carried through the traversal.</typeparam>
    /// <remarks>
    /// Clients must implement this interface in order to traverse objects efficiently.
    /// </remarks>
    public interface IDelegateTraversal<TContext>
    {
        //ConstructorInfo Constructor<T>();

        /// <summary>
        /// Lookup any explicit traversal overrides.
        /// </summary>
        /// <typeparam name="TObject"></typeparam>
        /// <returns>Return null if no overrides, else return the override.</returns>
        Func<TObject, TContext, TObject> Override<TObject>();

        /// <summary>
        /// Initialize a new object.
        /// </summary>
        /// <typeparam name="TObject">The object type to initialize.</typeparam>
        /// <returns>A delegate that performs any initialization logic on new objects.</returns>
        ActionRef<TObject, TContext> Init<TObject>();

        /// <summary>
        /// Operate on a member of a reference type.
        /// </summary>
        /// <typeparam name="TObject"></typeparam>
        /// <typeparam name="TMember"></typeparam>
        /// <param name="getter"></param>
        /// <param name="setter"></param>
        /// <returns></returns>
        Action<TObject, TContext> Class<TObject, TMember>(Func<TObject, TMember> getter, Action<TObject, TMember> setter)
            where TObject : class;

        /// <summary>
        /// Operate on a member of a struct.
        /// </summary>
        /// <typeparam name="TObject"></typeparam>
        /// <typeparam name="TMember"></typeparam>
        /// <param name="getter"></param>
        /// <param name="setter"></param>
        /// <returns></returns>
        ActionRef<TObject, TContext> Struct<TObject, TMember>(FuncRef<TObject, TMember> getter, ActionRef<TObject, TMember> setter)
            where TObject : struct;
    }
}
