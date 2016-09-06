using System;
using System.Reflection;
using System.Linq.Expressions;
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
        public static readonly Action<TVisitor, T> Invoke = BestMatch();

        static Action<TVisitor, T> BestMatch()
        {
            var type = typeof(T);
            var tvisit = typeof(TVisitor);
            var methods = tvisit.GetMethods(BindingFlags.Public | BindingFlags.Instance);
            // need to order the methods from most to least specific match for T
            // then we perform dynamic type tests and casts to the most specific type
            // if all of those fail, one of the last entries should be the least specific type,
            // matching either a generic T or typeof(T) itself
            var matches = methods.Select(x => new { Method = x, Params = x.GetParameters() })
                                 .Where(x => x.Params.Length == 1 && (x.Params[0].ParameterType.Subtypes(type) || type.Subtypes(x.Params[0].ParameterType)))
                                 .OrderBy(x => x.Params[0].ParameterType, new SubtypeComparer())
                                 .ToArray();
            if (matches.Length == 0 || !type.Subtypes(matches.Last().Params[0].ParameterType))
                throw new MissingMethodException(tvisit.Name + " is missing a proper catch-all case for type " + type.Name);
            // if most specific match is exactly this type, then dispatch directly to this handler
            if (matches[0].Params[0].ParameterType == type)
                return matches[0].Method.Create<Action<TVisitor, T>>();
            // else construct an expression that tests the runtime type
            var v = Expression.Parameter(tvisit, "v");
            var p = Expression.Parameter(type, "p");
            var exit = Expression.Label("exit");
            var localType = Expression.Variable(typeof(Type), "type");
            var tests = new List<Expression>
            {
                Expression.Assign(localType, Expression.Call(p, typeof(object).GetMethod("GetType")))
            };
            // build a sequence of subtype tests until type <: parameter-type
            //FIXME: if T is sealed, then we need only pick the single closest match. Perhaps
            //should also insert a first test and dispatch in case GetType() == T.
            int i = 0;
            while (!type.Subtypes(matches[i].Params[0].ParameterType))
            {
                var x = matches[i++];
                // if runtime type matches parameter type, then cast and invoke match, then exit
                var typeCheck = Expression.Equal(localType, Expression.Constant(x.Params[0].ParameterType));
                var dispatch = Expression.Call(v, x.Method, Expression.Convert(p, x.Params[0].ParameterType));
                tests.Add(Expression.IfThen(typeCheck, Expression.Return(exit, dispatch)));
            }
            // this is the last match, where type <: parameter type, so we just invoke the most specific case
            var final = matches[i];
            //FIXME: cases with generic parameters are correctly last in the list of matches, but
            //final.Method is now a generic method for some subtype of T, so we need to perform a
            //dynamic dispatch, probably via IDynamicType, to extract the actual type to bind the
            //generic parameter.
            tests.Add(Expression.Call(v, final.Method, Expression.Convert(p, final.Params[0].ParameterType)));
            tests.Add(Expression.Label(exit));
            var body = Expression.Block(new[] { localType }, tests);
            return Expression.Lambda<Action<TVisitor, T>>(body, v, p)
                             .Compile();
        }
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
