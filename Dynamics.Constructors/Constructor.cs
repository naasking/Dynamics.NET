﻿using System;
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
        /// <remarks>
        /// Here's the constructor to build strings from char[]:
        /// <code>
        /// var createString = Constructor&lt;Func&lt;char[], string&gt;&gt;.Invoke;
        /// var hello = createString(new[] { 'h', 'e', 'l', 'l', 'o' })
        /// </code>
        /// Arrays have pseudo-constructors which are also exposed
        /// as if they were ordinary constructors:
        /// <code>
        /// var createArray = Constructor&lt;Func&lt;int, T[]&gt;&gt;.Invoke;
        /// var newArray = createArray(100); // 100 item array
        /// </code>
        /// </remarks>
        public static readonly TFunc Invoke;

        /// <summary>
        /// The constructor info used to create <see cref="Invoke"/>.
        /// </summary>
        public static readonly ConstructorInfo Info;

        static Constructor()
        {
            var tfunc = typeof(TFunc);
            if (!typeof(Delegate).IsAssignableFrom(tfunc))
                throw new ArgumentException(tfunc.Name + " must be a delegate type.");
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
