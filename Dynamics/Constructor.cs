using System;
using System.Linq.Expressions;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Dynamics
{
    /// <summary>
    /// Cached constructor delegates.
    /// </summary>
    /// <typeparam name="TFunc">The constructor signature.</typeparam>
    public static class Constructor<TFunc>
        where TFunc : class
    {
        /// <summary>
        /// A delegate that constructs a type
        /// </summary>
        public static readonly TFunc Invoke;

        /// <summary>
        /// The constructor info used to create <see cref="Invoke"/>.
        /// </summary>
        public static readonly ConstructorInfo Info;

        static Constructor()
        {
            var tfunc = typeof(TFunc);
            if (!tfunc.Subtypes(typeof(Delegate)))
                throw new ArgumentException("Type " + tfunc.Name + " is not a delegate type.");
            var invoke = tfunc.GetMethod("Invoke", BindingFlags.Public | BindingFlags.Instance);
            var type = invoke.ReturnType;
            if (type.IsAbstract || type.IsInterface)
                throw new ArgumentException("No constructors for abstract or interface type " + type.Name + ".");
            var ptypes = invoke.GetParameters().Select(x => x.ParameterType).ToArray();
            // treat arrays specially as having a constructor with a single Int32 parameter
            Expression body;
            var param = ptypes.Select(Expression.Parameter).ToArray();
            if (type.IsArray)
            {
                if (ptypes.Length > 1 && ptypes[0] != typeof(int))
                    throw new ArgumentException("Array constructor requires a single parameter of type Int32.");
                body = Expression.NewArrayBounds(type.GetElementType(), param);
                Info = null;
            }
            else
            {
                Info = type.GetConstructor(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance, null, ptypes, null);
                if (Info == null)
                    throw new ArgumentException("Type " + tfunc.Name + " has no constructor with signature " + ptypes.Aggregate("(", (a, x) => a + x + ',') + ")->" + type.Name);
                body = Expression.New(Info, param);
            }
            Invoke = Expression.Lambda<TFunc>(body, param).Compile();
        }
    }
}
