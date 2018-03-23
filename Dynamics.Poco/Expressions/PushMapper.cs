using System;
using System.Linq;
using System.Linq.Expressions;
using System.Collections.Generic;
using System.Reflection;

namespace Dynamics.Poco.Expressions
{
    /// <summary>
    /// A fast, code generating mapper.
    /// </summary>
    /// <typeparam name="TContext">The type of the context.</typeparam>
    /// <remarks>
    /// This mapper's traversal is driven by an custom operation on <typeparamref name="TContext"/>.
    /// </remarks>
    public class PushMapper<TContext> : IPocoMapper<TContext>
    {
        IExpressionTraversal<TContext> builder;
        Expression<Func<TContext, string>> nextMember;
        Func<string, string> normalize;

        public PushMapper(IExpressionTraversal<TContext> builder, Expression<Func<TContext, string>> nextMember, Func<string, string> normalize)
        {
            this.builder = builder;
            this.nextMember = nextMember;
            this.normalize = normalize;
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
            var cases = new List<SwitchCase>(props.Length);
            foreach (var x in props)
            {
                if (x.GetMethod == null || x.SetMethod == null)
                    continue;
                cases.Add(Expression.SwitchCase((Expression)
                    member.MakeGenericMethod(otype, x.PropertyType)
                          .Invoke(builder, new object[] { obj, ctxt, x }), Expression.Constant(normalize(x.Name))));
            }
            var m = Expression.Variable(typeof(string), "m");
            var exit = Expression.Label("exit");
            var loop = Expression.Loop(
                Expression.Block(
                    new[] { m },
                    Expression.Assign(m, Expression.Invoke(nextMember, ctxt)),
                    Expression.IfThenElse(
                        Expression.ReferenceEqual(m, Expression.Constant(null)),
                        Expression.Break(exit),
                        Expression.Switch(m, cases.ToArray()))),
                exit);
            var init = builder.Init(obj, ctxt) ?? Expression.Empty();
            return Expression.Lambda<Func<TObject, TContext, TObject>>(
                Expression.Block(init, loop, obj), obj, ctxt).Compile();
        }
    }
}
