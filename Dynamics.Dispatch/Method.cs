using System;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Collections.Generic;
using System.Text;

namespace Dynamics
{
    /// <summary>
    /// Resolve a delegate that dispatches to the best method for the given parameter types.
    /// </summary>
    public static class Method
    {
        /// <summary>
        /// Obtain a delegate dispatching to a method that best matches the name and given signature.
        /// </summary>
        /// <typeparam name="TFunc">The delegate type to generate.</typeparam>
        /// <param name="methodName">The optional method name used to filter the candidate methods.</param>
        /// <returns>A delegate</returns>
        /// <remarks>
        /// If a single method best matches the name and signature, then the returned delegate is a zero-overhead open instance delegate.
        /// 
        /// If more than one candidate matches, eg. the dispatch argument is a non-sealed type, then a delegate that performs
        /// a series of runtime tests is dynamically generated.
        /// </remarks>
        public static TFunc Resolve<TFunc>(string methodName = null)
            where TFunc : Delegate
        {
            var tfunc = typeof(TFunc);
            if (!tfunc.Subtypes<Delegate>())
                throw new ArgumentException(tfunc.Name + " must be a delegate type.");
            var invoke = tfunc.GetRuntimeMethods().Single(x => x.Name.Equals("Invoke", StringComparison.Ordinal));
            var iparam = invoke.GetParameters();
            if (iparam.Length != 2)
                throw new NotSupportedException("Only 2-parameter methods supported.");
            var type = iparam[1].ParameterType;
            var tvisit = iparam[0].ParameterType;
            var tmethods = tfunc.IsConstructedGenericType && tfunc.GetGenericTypeDefinition() == typeof(TryParse<>)
                         ? type.GetElementType()
                         : tvisit;
            var typeParam = tfunc.IsConstructedGenericType && tfunc.GetGenericTypeDefinition() == typeof(TryParse<>) ? 1 : 0;
            var methods = tmethods.GetRuntimeMethods();
            if (!string.IsNullOrEmpty(methodName))
                methods = methods.Where(x => methodName.Equals(x.Name, StringComparison.Ordinal)).ToArray();
            // need to order the methods from most to least specific match for T
            // then we perform dynamic type tests and casts to the most specific type
            // if all of those fail, one of the last entries should be the least specific type,
            // matching either a generic T or typeof(T) itself
            //NOTE: instance methods don't expose the 'this' type as a param, but static methods do, so this treats
            //static and instance methods differently as a result
            var matches = methods.Select(x => new { Method = x, Params = x.GetParameters() })
                                 .Where(x => x.Method.ReturnType.Subtypes(invoke.ReturnType)
                                          && (x.Method.IsStatic && x.Params.Length == 2 && (x.Params[typeParam].ParameterType.Subtypes(type) || type.Subtypes(x.Params[typeParam].ParameterType))
                                          || !x.Method.IsStatic && x.Params.Length == 1 && (x.Params[0].ParameterType.Subtypes(type) || type.Subtypes(x.Params[0].ParameterType))))
                                 .OrderBy(x => x.Params[typeParam].ParameterType, new SubtypeComparer())
                                 .ToArray();
            if (matches.Length == 0 || !type.Subtypes(matches.Last().Params[typeParam].ParameterType))
                throw new MissingMethodException(tvisit.Name + " is missing a proper catch-all case for type " + type.Name);
            var ambiguous = matches.GroupBy(x => x.Params[0].ParameterType)
                                   .Where(x => x.Count() > 1)
                                   .SelectMany(x => x)
                                   .ToArray();
            if (ambiguous.Length > 0)
                throw new AmbiguousMatchException(ambiguous.Aggregate(new StringBuilder("The following methods are ambiguous: "), (seed, x) => seed.Append(x.Method).Append(',')).ToString());
            // if most specific match is exactly this type, then dispatch directly to this handler
            if (matches[0].Params[typeParam].ParameterType == type)
                return matches[0].Method.Create<TFunc>();
            // else construct an expression that tests the runtime type
            var v = Expression.Parameter(tvisit, "v");
            var p = Expression.Parameter(type, "p");
            var _r = invoke.ReturnType == typeof(void) ? null : Expression.Variable(invoke.ReturnType, "_r");
            var exit = Expression.Label("exit");
            var localType = Expression.Variable(typeof(Type), "type");
            var tests = new List<Expression>
            {
                Expression.Assign(localType, Expression.Call(p, typeof(object).GetRuntimeMethod("GetType", Type.EmptyTypes)))
            };
            // build a sequence of subtype tests until type <: parameter-type
            //FIXME: if T is sealed, then we need only pick the single closest match. Perhaps
            //should also insert a first test and dispatch in case GetType() == T.
            int i = 0;
            while (!type.Subtypes(matches[i].Params[typeParam].ParameterType))
            {
                var x = matches[i++];
                // if runtime type matches parameter type, then cast and invoke match, then exit
                var typeCheck = Expression.Equal(localType, Expression.Constant(x.Params[typeParam].ParameterType));
                var dispatch = Expression.Call(v, x.Method, Expression.Convert(p, x.Params[typeParam].ParameterType));
                var matched = _r == null ? dispatch as Expression : Expression.Assign(_r, dispatch);
                tests.Add(Expression.IfThen(typeCheck, Expression.Return(exit, matched)));
            }
            // this is the last match, where type <: parameter type, so we just invoke the most specific case
            var final = matches[i];
            //FIXME: cases with generic parameters are correctly last in the list of matches, but
            //final.Method is now a generic method for some subtype of T, so we need to perform a
            //dynamic dispatch, probably via IDynamicType, to extract the actual type to bind the
            //generic parameter.
            tests.Add(Expression.Call(v, final.Method, Expression.Convert(p, final.Params[typeParam].ParameterType)));
            tests.Add(Expression.Label(exit));
            if (_r != null) tests.Add(_r);
            var locals = _r == null ? new[] { localType } : new[] { localType, _r };
            var body = Expression.Block(locals, tests);
            return Expression.Lambda<TFunc>(body, v, p)
                             .Compile();
        }
    }
}
