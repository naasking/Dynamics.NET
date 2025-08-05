using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;

namespace Dynamics.Poco.Delegates
{
    /// <summary>
    /// A pull mapper that performs no code generation.
    /// </summary>
    /// <typeparam name="TContext">The type of the context.</typeparam>
    /// <remarks>
    /// This mapper traverses an object structurally.
    /// </remarks>
    public readonly struct PullMapper<TContext> : IPocoMapper<TContext>
    {
        readonly IDelegateTraversal<TContext> builder;

        public PullMapper(IDelegateTraversal<TContext> builder)
        {
            this.builder = builder;
        }

        public Func<TObject, TContext, TObject> Compile<TObject>()
        {
            var ovr = builder.Override<TObject>();
            if (ovr != null)
                return ovr;
            var props = typeof(TObject).GetRuntimeProperties();
            var traversalMethods = typeof(IDelegateTraversal<TContext>).GetRuntimeMethods();
            var otype = typeof(TObject);
            var init = builder.Init<TObject>();
            if (otype.GetTypeInfo().IsValueType)
            {
                var mstruct = traversalMethods.Single(x => x.Name.Equals(nameof(IDelegateTraversal<TContext>.Struct), StringComparison.Ordinal));
                var members = new List<ActionRef<TObject, TContext>>();
                foreach (var x in props)
                {
                    if (x.SetMethod == null || x.GetMethod == null)
                        continue;
                    var getType = typeof(FuncRef<,>).MakeGenericType(otype, x.PropertyType);
                    var setType = typeof(ActionRef<,>).MakeGenericType(otype, x.PropertyType);
                    members.Add((ActionRef<TObject, TContext>)
                        mstruct.MakeGenericMethod(otype, x.PropertyType)
                               .Invoke(builder, new[] { x.GetMethod.CreateDelegate(getType), x.SetMethod.CreateDelegate(setType) }));
                }
                return (obj, ctxt) =>
                {
                    init?.Invoke(ref obj, ctxt);
                    foreach (var x in members)
                        x(ref obj, ctxt);
                    return obj;
                };
            }
            else
            {
                var mclass = traversalMethods.Single(x => x.Name.Equals(nameof(IDelegateTraversal<TContext>.Class), StringComparison.Ordinal));
                var members = new List<Action<TObject, TContext>>();
                foreach (var x in props)
                {
                    if (x.SetMethod == null || x.GetMethod == null)
                        continue;
                    var getType = typeof(Func<,>).MakeGenericType(otype, x.PropertyType);
                    var setType = typeof(Action<,>).MakeGenericType(otype, x.PropertyType);
                    members.Add((Action<TObject, TContext>)
                        mclass.MakeGenericMethod(otype, x.PropertyType)
                              .Invoke(builder, new[] { x.GetMethod.CreateDelegate(getType), x.SetMethod.CreateDelegate(setType) }));
                }
                return (obj, ctxt) =>
                {
                    init?.Invoke(ref obj, ctxt);
                    foreach (var x in members)
                        x(obj, ctxt);
                    return obj;
                };
            }
        }
    }
}
