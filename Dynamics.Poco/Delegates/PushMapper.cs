using System;
using System.Linq;
using System.Linq.Expressions;
using System.Collections.Generic;
using System.Reflection;

namespace Dynamics.Poco.Delegates
{
    /// <summary>
    /// A push mapper that performs no code generation.
    /// </summary>
    /// <typeparam name="TContext">The type of the context.</typeparam>
    /// <remarks>
    /// This mapper's traversal is driven by an custom operation on <typeparamref name="TContext"/>.
    /// </remarks>
    public class PushMapper<TContext> : IPocoMapper<TContext>
    {
        IDelegateTraversal<TContext> builder;
        Func<TContext, string> nextMember;
        Func<string, string> normalize;

        public PushMapper(IDelegateTraversal<TContext> builder, Func<TContext, string> nextMember, Func<string, string> normalize)
        {
            this.builder = builder;
            this.nextMember = nextMember;
            this.normalize = normalize;
        }

        public Func<TObject, TContext, TObject> Compile<TObject>()
        {
            var ovr = builder.Override<TObject>();
            if (ovr != null)
                return ovr;
            var props = typeof(TObject).GetRuntimeProperties().ToArray();
            var traversalMethods = typeof(IDelegateTraversal<TContext>).GetRuntimeMethods();
            var otype = typeof(TObject);
            var init = builder.Init<TObject>();
            if (otype.GetTypeInfo().IsValueType)
            {
                var mstruct = traversalMethods.Single(x => x.Name.Equals(nameof(IDelegateTraversal<TContext>.Struct), StringComparison.Ordinal));
                var members = new Dictionary<string, ActionRef<TObject, TContext>>(props.Length);
                for (int i = 0; i < props.Length; ++i)
                {
                    var x = props[i];
                    if (x.SetMethod == null || x.GetMethod == null)
                        continue;
                    var getType = typeof(FuncRef<,>).MakeGenericType(otype, x.PropertyType);
                    var setType = typeof(ActionRef<,>).MakeGenericType(otype, x.PropertyType);
                    members.Add(normalize(x.Name), (ActionRef<TObject, TContext>)
                        mstruct.MakeGenericMethod(otype, x.PropertyType)
                               .Invoke(builder, new[] { x.GetMethod.CreateDelegate(getType), x.SetMethod.CreateDelegate(setType) }));
                }
                return (obj, ctxt) =>
                {
                    init?.Invoke(ref obj, ctxt);
                    for (var x = nextMember(ctxt); x != null; x = nextMember(ctxt))
                        members[x](ref obj, ctxt);
                    return obj;
                };
            }
            else
            {
                var mclass = traversalMethods.Single(x => x.Name.Equals(nameof(IDelegateTraversal<TContext>.Class), StringComparison.Ordinal));
                var members = new Dictionary<string, Action<TObject, TContext>>(props.Length);
                for (int i = 0; i < props.Length; ++i)
                {
                    var x = props[i];
                    if (x.SetMethod == null || x.GetMethod == null)
                        continue;
                    var getType = typeof(Func<,>).MakeGenericType(otype, x.PropertyType);
                    var setType = typeof(Action<,>).MakeGenericType(otype, x.PropertyType);
                    members.Add(normalize(x.Name), (Action<TObject, TContext>)
                        mclass.MakeGenericMethod(otype, x.PropertyType)
                              .Invoke(builder, new[] { x.GetMethod.CreateDelegate(getType), x.SetMethod.CreateDelegate(setType) }));
                }
                return (obj, ctxt) =>
                {
                    init?.Invoke(ref obj, ctxt);
                    for (var x = nextMember(ctxt); x != null; x = nextMember(ctxt))
                        members[x](obj, ctxt);
                    return obj;
                };
            }
        }
    }
}
