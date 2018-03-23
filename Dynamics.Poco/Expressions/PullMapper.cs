using System;
using System.Linq;
using System.Linq.Expressions;
using System.Collections.Generic;
using System.Reflection;

namespace Dynamics.Poco.Expressions
{
    /// <summary>
    /// A fast, code-generating pull mapper.
    /// </summary>
    /// <typeparam name="TContext">The type of the context.</typeparam>
    /// <remarks>
    /// This mapper traverses an object structurally.
    /// </remarks>
    public class PullMapper<TContext> : IPocoMapper<TContext>
    {
        IExpressionTraversal<TContext> builder;

        public PullMapper(IExpressionTraversal<TContext> builder)
        {
            this.builder = builder;
        }

        public Func<TObject, TContext, TObject> Compile<TObject>()
        {
            var ovr = builder.Override<TObject>();
            if (ovr != null)
                return ovr.Compile();
            var props = typeof(TObject).GetRuntimeProperties().ToArray();
            var member = typeof(IExpressionTraversal<TContext>).GetRuntimeMethods().Single();
            var otype = typeof(TObject);
            var obj = Expression.Parameter(otype, "obj");
            var ctxt = Expression.Parameter(typeof(TContext), "ctxt");
            var init = builder.Init(obj, ctxt);
            var body = new List<Expression>(props.Length);
            if (init != null)
                body.Add(init);
            foreach (var x in props)
            {
                if (x.GetMethod == null || x.SetMethod == null)
                    continue;
                body.Add((Expression)
                    member.MakeGenericMethod(otype, x.PropertyType)
                          .Invoke(builder, new object[] { obj, ctxt, x }));
            }
            body.Add(obj);
            return Expression.Lambda<Func<TObject, TContext, TObject>>(
                Expression.Block(body), obj, ctxt).Compile();
        }
    }
}
