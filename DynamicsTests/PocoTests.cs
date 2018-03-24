using System;
using System.Linq;
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

        #region POCO sum tests
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
                ctxtValue = ctxtValue ?? Expression.Field(ctxt, nameof(Ref<int>.value));
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

        #region POCO deserialization tests
        public struct Context
        {
            Stack<IEnumerator<object>> inner;
            public Context(IEnumerator<object> ie)
            {
                inner = new Stack<IEnumerator<object>>();
                inner.Push(ie);
            }
            public void Start()
            {
                if (Current != null)
                    inner.Push(((IEnumerable<object>)Current).GetEnumerator());
            }
            public bool MoveNext()
            {
                if (inner.Peek().MoveNext())
                    return true;
                inner.Pop();
                return false;
            }
            public object Current
            {
                get { return inner.Peek().Current; }
            }
        }
        public static class Serializer<T>
        {
            public static Func<T, Context, T> Deserialize;
        }
        sealed class DelegateDeserializer : IDelegateTraversal<Context>
        {
            public Func<TObject, Context, TObject> Override<TObject>()
            {
                return null;
            }

            public ActionRef<TObject, Context> Init<TObject>()
            {
                return (ref TObject x, Context ie) =>
                {
                    x = x == null ? Constructor<Func<TObject>>.Invoke() : x;
                    ie.Start();
                };
            }

            public Action<TObject, Context> Class<TObject, TMember>(Func<TObject, TMember> getter, Action<TObject, TMember> setter)
                where TObject : class
            {
                return (obj, ie) =>
                {
                    if (ie.MoveNext())
                        setter(obj, Serializer<TMember>.Deserialize(getter(obj), ie));
                };
            }

            public ActionRef<TObject, Context> Struct<TObject, TMember>(FuncRef<TObject, TMember> getter, ActionRef<TObject, TMember> setter)
                where TObject : struct
            {
                return (ref TObject obj, Context ie) =>
                {
                    if (ie.MoveNext())
                        setter(ref obj, Serializer<TMember>.Deserialize(getter(ref obj), ie));
                };
            }
        }

        sealed class ExpressionDeserializer : IExpressionTraversal<Context>
        {
            public Expression<Func<TObject, Context, TObject>> Override<TObject>()
            {
                return null;
            }

            public Expression Init(Expression obj, Expression ctxt)
            {
                var e = Expression.Call(ctxt, ctxt.Type.GetRuntimeMethod(nameof(Context.Start), Type.EmptyTypes)) as Expression;
                return obj.Type.IsValueType
                    ? e
                    : Expression.Block(
                        Expression.IfThen(
                            Expression.Equal(obj, Expression.Constant(null)),
                            Expression.Assign(obj, Expression.New(obj.Type.GetConstructor(Type.EmptyTypes)))),
                            e);
            }

            public Expression Member<TObject, TMember>(Expression obj, Expression ctxt, PropertyInfo property)
            {
                var sumTMember = typeof(Serializer<TMember>);
                return Expression.IfThen(
                        Expression.Call(ctxt, ctxt.Type.GetRuntimeMethod(nameof(Context.MoveNext), Type.EmptyTypes)),
                        Expression.Assign(
                            Expression.Property(obj, property),
                            Expression.Invoke(
                                Expression.Field(null, sumTMember, nameof(Serializer<TMember>.Deserialize)),
                                Expression.Property(obj, property),
                                ctxt)));
            }
        }

        [Fact]
        static void DelegateDeserializationTest()
        {
            var tc = new Dynamics.Poco.Delegates.PushMapper<Context>(new DelegateDeserializer(), ie => ie.MoveNext() ? (string)ie.Current : null, x => x);
            RunDeserializationTests(tc);
        }

        [Fact]
        static void ExpressionDeserializationTest()
        {
            var tc = new Dynamics.Poco.Expressions.PushMapper<Context>(new ExpressionDeserializer(), ie => ie.MoveNext() ? (string)ie.Current : null, x => x);
            RunDeserializationTests(tc);
        }

        static void RunDeserializationTests(IPocoMapper<Context> tc)
        {
            //var foo = new Foo { Index = 3, Bar = new Bar { Baz = 99 } };
            //FIXME: need a stack of streams, Stack<Context>, so that initializing a new object pushes a new enumerator on top,
            //and the failure of MoveNext() pops off the top. Create a custom type that encapsulates Context.
            var stream = new List<object> { nameof(Foo.Bar), new List<object> { nameof(Bar.Baz), 99 }, nameof(Foo.Index), 3 };
            Serializer<int>.Deserialize = (i, ie) => (int)ie.Current;
            Serializer<string>.Deserialize = (x, ie) => (string)ie.Current;
            Serializer<Foo>.Deserialize = tc.Compile<Foo>();
            Serializer<Bar>.Deserialize = tc.Compile<Bar>();
            var nfoo = Serializer<Foo>.Deserialize(null, new Context(stream.GetEnumerator()));
            Assert.Equal(3, nfoo.Index);
            Assert.Null(nfoo.Bar.Empty);
            Assert.Equal(99, nfoo.Bar.Baz);
        }
        #endregion
    }
}
