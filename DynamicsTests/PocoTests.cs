using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using Xunit;
using Dynamics;
using Dynamics.Poco;
using Dynamics.Poco.Delegates;
using Dynamics.Poco.Expressions;

namespace DynamicsTests
{
    public static class PocoTests
    {
        public struct Bar
        {
            public int Baz { get; set; }
            public string Empty { get; set; }
        }
        public class Foo
        {
            public int Index { get; set; }
            public Bar Bar { get; set; }
        }

        #region Delegate sum tests
        public sealed class Ref<T>
        {
            public T value;
        }
        public static class Sum<T>
        {
            public static Func<T, Ref<int>, T> Compute;
        }
        sealed class DelegateSum : IDelegateTraversal<Ref<int>>
        {
            public Func<TObject, Ref<int>, TObject> Override<TObject>()
            {
                return null;
            }

            public ActionRef<TObject, Ref<int>> Init<TObject>()
            {
                return (ref TObject x, Ref<int> sum) => { };
            }

            public Action<TObject, Ref<int>> Class<TObject, TMember>(Func<TObject, TMember> getter, Action<TObject, TMember> setter)
                where TObject : class
            {
                return (obj, sum) => Sum<TMember>.Compute(getter(obj), sum);
            }

            public ActionRef<TObject, Ref<int>> Struct<TObject, TMember>(FuncRef<TObject, TMember> getter, ActionRef<TObject, TMember> setter)
                where TObject : struct
            {
                return (ref TObject obj, Ref<int> sum) => Sum<TMember>.Compute(getter(ref obj), sum);
            }
        }

        sealed class ExpressionSum : IExpressionTraversal<Ref<int>>
        {
            Expression ctxtValue;

            public Expression<Func<TObject, Ref<int>, TObject>> Override<TObject>()
            {
                return null;
            }

            public Expression Init(Expression obj, Expression ctxt)
            {
                ctxtValue = Expression.Field(ctxt, nameof(Ref<int>.value));
                return null;
            }

            public Expression Member<TObject, TMember>(Expression obj, Expression ctxt, PropertyInfo property)
            {
                var sumTMember = typeof(Sum<TMember>);
                return Expression.Invoke(
                        Expression.Field(null, sumTMember, nameof(Sum<TMember>.Compute)),
                        Expression.Property(obj, property),
                        ctxt);
            }
        }

        [Fact]
        public static void DelegateTests()
        {
            RunSimpleTests(new Dynamics.Poco.Delegates.PullMapper<Ref<int>>(new DelegateSum()));
        }

        [Fact]
        public static void ExpressionTests()
        {
            RunSimpleTests(new Dynamics.Poco.Expressions.PullMapper<Ref<int>>(new ExpressionSum()));
        }

        static void RunSimpleTests(IPocoMapper<Ref<int>> tc)
        {
            var foo = new Foo { Index = 3, Bar = new Bar { Baz = 99 } };
            Sum<int>.Compute = (i, r) => r.value += i;
            Sum<string>.Compute = (x, r) => x;
            Sum<Foo>.Compute = tc.Compile<Foo>();
            Sum<Bar>.Compute = tc.Compile<Bar>();
            var sum = new Ref<int>();
            Sum<Foo>.Compute(foo, sum);
            Assert.Equal(102, sum.value);
        }
        #endregion
    }
}
