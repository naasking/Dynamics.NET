using System;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.Collections.ObjectModel;
using Dynamics;
using Xunit;
using System.Threading;

namespace DynamicsTests
{
    public static class Tests
    {
        sealed class TransitiveField<T>
        {
            readonly T field;
            public TransitiveField(T value) { field = value; }
        }
        sealed class TransitiveProp<T>
        {
            public T Prop { get; private set; }
            public TransitiveProp(T value) { Prop = value; }
        }

        #region Immutable checks
        struct ROField
        {
            public readonly int X;
            public ROField(int x)
            {
                X = x;
            }
            public double Foo()
            {
                return X * 3;
            }
        }
        struct ROProperty
        {
            public int X { get; private set; }
            public ROProperty(int x) : this()
            {
                X = x;
            }
        }
        sealed class EqualsOverride
        {
            public int X { get; private set; }
            public override bool Equals(object obj)
            {
                return obj is EqualsOverride && X == (obj as EqualsOverride).X;
            }
            public override int GetHashCode()
            {
                return X;
            }
        }
        sealed class Equatable : IEquatable<Equatable>
        {
            public int X { get; private set; }
            public bool Equals(Equatable obj)
            {
                return X == obj.X;
            }
        }

        // trust the purity declaration
        [System.Diagnostics.Contracts.Pure]
        sealed class PureType
        {
            public int X { get; set; }
        }
        sealed class PureProp
        {
            // trust the purity declaration
            [System.Diagnostics.Contracts.Pure]
            public int X { get; set; }
        }
        sealed class Formattable : IFormattable
        {
            public int X { get; private set; }
            public string ToString(string format, IFormatProvider formatProvider)
            {
                return ToString();
            }
        }

        [Fact]
        public static void CheckImmutable()
        {
            IsImmutable<int>();
            IsImmutable<int?>();
            IsImmutable<uint>();
            IsImmutable<float>();
            IsImmutable<short>();
            IsImmutable<ushort>();
            IsImmutable<byte>();
            IsImmutable<sbyte>();
            IsImmutable<double>();
            IsImmutable<char>();
            IsImmutable<long>();
            IsImmutable<ulong>();
            IsImmutable<DateTime>();
            IsImmutable<decimal>();
            IsImmutable<string>();
            IsImmutable<Base64FormattingOptions>();
            IsImmutable<Base64FormattingOptions?>();
            IsImmutable<DateTimeOffset>();
            IsImmutable<DateTimeKind>();
            IsImmutable<TimeSpan>();
            IsImmutable<Kind>();
            IsImmutable<UriKind>();
            IsImmutable<ROField>();
            IsImmutable<ROProperty>();
            IsImmutable<EqualsOverride>();
            IsImmutable<Equatable>();
            //FIXME: the [Pure] attribute doesn't work in .NET 9, need to find a different way
            //IsImmutable<PureType>();
            //IsImmutable<PureProp>();
            IsImmutable<Formattable>();
            IsImmutable<KeyValuePair<int, char>>();
            IsImmutable<TransitiveField<int>>();
            IsImmutable<TransitiveProp<int>>();
            IsImmutable<IntPtr>();
            IsImmutable<MethodInfo>();
            IsImmutable<FieldInfo>();
            IsImmutable<MemberInfo>();
            IsImmutable<MethodBase>();
            IsImmutable<TimeZoneInfo>();
        }
        static void IsImmutable<T>()
        {
            Assert.Equal(Mutability.Immutable, Type<T>.Mutability);
        }
        #endregion

        #region Mutable checks
        struct MutField
        {
            public int X;
        }
        struct MutProperty
        {
            public int X { get; set; }
        }
        struct ImpureMethod
        {
            public int X { get; private set; }
            public void Foo()
            {
            }
        }
        struct PureImpureMethod
        {
            [System.Diagnostics.Contracts.Pure]
            public int X { get; set; }
            public void Foo()
            {
            }
        }
        [Fact]
        public static void CheckMutable()
        {
            IsMutable<int[]>();
            IsMutable<MutField>();
            IsMutable<MutProperty>();
            IsMutable<ImpureMethod>();
            IsMutable<PureImpureMethod>();
            IsMutable<TransitiveField<int[]>>();
            IsMutable<TransitiveProp<int[]>>();
        }
        static void IsMutable<T>()
        {
            Assert.Equal(Mutability.Mutable, Type<T>.Mutability);
        }
        #endregion

        #region Maybe mutable checks
        class MaybeMut
        {
            readonly int field;
        }
        [Fact]
        public static void CheckMaybeMutable()
        {
            IsMaybeMutable<object>();
            IsMaybeMutable<MaybeMut>();
            IsMaybeMutable<TransitiveField<object>>();
            IsMaybeMutable<TransitiveProp<object>>();
            IsMaybeMutable<Tuple<int, string>>();
            IsMaybeMutable<IEnumerable<int>>();
        }
        static void IsMaybeMutable<T>()
        {
            Assert.Equal(Mutability.Maybe, Type<T>.Mutability);
        }
        #endregion

        #region Runtime mutability checks
        class DefMut : MaybeMut
        {
            public string Bar { get; set; }
        }
        sealed class Self
        {
            readonly object field;
            public Self()
            {
                field = this;
            }
            public Self(object x)
            {
                field = x;
            }
        }
        [Fact]
        public static void RuntimeMutable()
        {
            IsImmutable(new int?(3));
            IsImmutable(new MaybeMut());
            IsMutable<int[]>(new int[3]);
            IsMutable<MaybeMut>(new DefMut());
            IsImmutable(new TransitiveField<object>("foo"));
            IsMutable(new TransitiveField<object>(new[] { 2, 3 }));
            IsImmutable(new TransitiveProp<object>("foo"));
            IsMutable(new TransitiveProp<object>(new[] { 2, 3 }));
            IsImmutable(new Self());
            IsMutable(new Self(new[] { "", "hello world!" }));
            IsMutable<IEnumerable<int>>(new List<int> { 1, 2, 3 });
            IsMutable<IEnumerable<int>>(new[] { 1, 2, 3 });
            IsMutable<IEnumerable<int>>(new ReadOnlyCollection<int>(new int[] { 1, 2, 3 }));
        }
        static void IsImmutable<T>(T value)
        {
            Assert.False(Type<T>.IsMutable(value));
        }
        static void IsMutable<T>(T value)
        {
            Assert.True(Type<T>.IsMutable(value));
        }
        #endregion

        #region Deep Copy tests
        sealed class Recurse
        {
            public Recurse self;
            public static Recurse Cycle()
            {
                var x = new Recurse();
                x.self = x;
                return x;
            }
        }
        sealed class Copiable : ICopiable<Copiable>
        {
            public bool done;
            public Copiable Copy(Dictionary<object, object> refs)
            {
                var x = new Copiable { done = true };
                refs.Add(this, x);
                return x;
            }
        }
        sealed class EquatableSeq<T> : IEquatable<EquatableSeq<T>>
        {
            public EquatableSeq(params T[] x) { X = x; }
            public T[] X { get; private set; }
            public bool Equals(EquatableSeq<T> obj)
            {
                //return X.SequenceEqual(obj.X);
                return X.Zip(obj.X, (x, y) => Type<T>.DefaultEquals(x, y)).All(x => x);
            }
        }
        [Fact]
        static void CopyTests()
        {
            IsCopied(0);
            IsShared("foo");
            IsCopied(new DefMut { Bar = "Hello World" }, (x, y) => x.Bar == y.Bar);
            IsCopied(new EquatableSeq<int>(2, 3));
            IsCopied(Recurse.Cycle(), (orig, other) => other.self == other);
            IsCopied(new Copiable(), (orig, copy) => copy.done);
            IsCopied(new List<int> { 1, 2, 3 }, Enumerable.SequenceEqual);
            IsCopied(new[] { 1, 2, 3 }, Enumerable.SequenceEqual);
            IsCopied(new Dictionary<int, string>()
            {
                { 1, "one" },
                { 2, "two" },
                { 3, "three" },
            },
            Enumerable.SequenceEqual);
            IsCopied(Base64FormattingOptions.InsertLineBreaks);
            IsCopied(new Action(CopyTests), (x, y) => x.Method == y.Method && x.Target == y.Target);

            // check circular delegates
            //FIXME: this isn't technically correct, since Type<T>.Copy actually duplicates the
            //inner delegate since it can't update the delegate's readonly fields properly at this time
            Func<object> tst = null;
            tst = () => tst;
            IsCopied(tst, (x, y) => x != y && x() != y() && x() == x() && y() == y());
        }
        static void IsShared<T>(T orig)
        {
            var copy = Type<T>.Copy(orig);
            Assert.True(ReferenceEquals(orig, copy));
            Assert.True(Type<T>.DefaultEquals(orig, copy));
        }
        static void IsCopied<T>(T orig, Func<T, T, bool> eq = null)
        {
            var copy = Type<T>.Copy(orig);
            Assert.False(ReferenceEquals(orig, copy));
            Assert.True(eq == null && Type<T>.DefaultEquals(orig, copy) || eq != null && eq(orig, copy));
        }
        #endregion

        #region Check circularity
        [Fact]
        public static void CircularityTests()
        {
            IsAcyclic<int>();
            IsAcyclic<string>();
            IsAcyclic<ROField>();
            IsAcyclic<int[]>();
            IsCyclic<Self>();
            IsCyclic<EquatableSeq<object>>();
            IsCyclic<EquatableSeq<object[]>>();
            IsCyclic<object[]>();
        }
        static void IsCyclic<T>()
        {
            Assert.Equal(Cycles.Yes, Type<T>.Cycles);
        }
        static void IsAcyclic<T>()
        {
            Assert.Equal(Cycles.No, Type<T>.Cycles);
        }
        #endregion

        #region Constructor tests
        [Fact]
        public static void CheckConstructors()
        {
            var x = Constructor<Func<int, int[]>>.Invoke(89);
            Assert.Equal(89, x.Length);
            var s = Constructor<Func<char[], string>>.Invoke(new[] { 'h', 'e', 'l', 'l', 'o' });
            Assert.Equal("hello", s);
            Assert.NotNull(Constructor<Func<char[], string>>.Info);
            var a = Constructor<Func<int, char[]>>.Invoke(3);
            Assert.Equal(3, a.Length);
            Assert.Null(Constructor<Func<int, char[]>>.Info);
            // the following correct throws an error, but breaks the debugger on the error thrown
            //try
            //{
            //    var impossible = Constructor<Func<int[]>>.Invoke();
            //    Assert(false);
            //}
            //catch (TypeInitializationException)
            //{
            //}
        }
        #endregion

        #region Runtime tests
        [Fact]
        static void TestDelegateCreate()
        {
            var x = new Action(TestDelegateCreate);
            var y = x.Method.Create<Action>();
            Assert.NotNull(y);
            try
            {
                x.Method.Create<Func<int>>();
            }
            catch (ArgumentException)
            {
            }
        }
        class NoAutoField
        {
            int x;
            public int X { get { return x; } }
        }
        [Fact]
        public static void TestBackingFields()
        {
            var field = typeof(ROProperty).GetFields(BindingFlags.NonPublic | BindingFlags.Instance)[0];
            var prop = typeof(ROProperty).GetProperty(nameof(ROProperty.X));
            var inferred = prop.GetBackingField();
            Assert.NotNull(field);
            Assert.NotNull(prop);
            Assert.Equal(field, inferred);
            Assert.True(field.IsBackingField());
            Assert.True(prop.HasAutoField());
            Assert.Equal("X", field.FieldName());

            var noauto = typeof(NoAutoField).GetProperty(nameof(NoAutoField.X));
            Assert.False(noauto.HasAutoField());
            Assert.Null(noauto.GetBackingField());
        }
        static void TestHasAttribute()
        {
            Assert.True(typeof(PureType).Has<System.Diagnostics.Contracts.PureAttribute>());
            var prop = typeof(PureImpureMethod).GetProperty(nameof(PureImpureMethod.X));
            Assert.True(prop.Has<System.Diagnostics.Contracts.PureAttribute>());
        }
        static void TestGetProperty()
        {
            var prop = typeof(PureImpureMethod).GetProperty(nameof(PureImpureMethod.X));
            var getter = prop.GetGetMethod();
            var indirect = getter.GetProperty();
            Assert.NotNull(prop);
            Assert.Equal(prop, indirect);
        }
        #endregion

        #region Dynamic dispatch tests
        struct Dispatcher : IDynamicType
        {
            public Type Extracted;
            public void Type<T>()
            {
                Extracted = typeof(T);
            }
        }
        [Fact]
        public static void TestDynamicDispatch()
        {
            DispatchMatch<int>();
            DispatchMatch<object>();
            DispatchMatch<Action>();
        }
        static void DispatchMatch<T>()
        {
            var dispatcher = new Dispatcher();
            Runtime.GetType(ref dispatcher, typeof(T));
            Assert.Equal(typeof(T), dispatcher.Extracted);
        }
        #endregion

        #region Generic visitor
        interface IVisitor
        {
            void Int(int x);
            void String(string x);
            void Else(object y);
        }
        sealed class Visitor : IVisitor
        {
            public void Int(int x)
            {
                Assert.Equal(399, x);
            }
            public void String(string x)
            {
                Assert.Equal("hello world!", x);
            }
            public void Else(object y)
            {
                Assert.Equal(typeof(DateTimeKind), y.GetType());
            }
        }
        [Fact]
        public static void TestVisitor()
        {
            Assert.NotNull(Visitor<IVisitor, int>.Invoke);
            Assert.NotNull(Visitor<IVisitor, string>.Invoke);
            Assert.NotNull(Visitor<IVisitor, object>.Invoke);
            Assert.NotNull(Visitor<IVisitor, Enum>.Invoke);

            var v = new Visitor();
            Visitor<IVisitor>.Invoke(v, 399);
            Visitor<IVisitor>.Invoke(v, "hello world!");
            Visitor<IVisitor>.Invoke(v, default(DateTimeKind));
        }
        static class AppendOverload<T>
        {
            public static readonly Func<StringBuilder, T, StringBuilder> Invoke = Method.Resolve<Func<StringBuilder, T, StringBuilder>>("Append");
        }
        [Fact]
        public static void TestStringBuilderVisitor()
        {
            var buf = new StringBuilder();
            AppendOverload<int>.Invoke(buf, 3);
            Assert.Equal("3", buf.ToString());
            buf.Clear();
            AppendOverload<string>.Invoke(buf, "foo");
            Assert.Equal("foo", buf.ToString());
            buf.Clear();
            AppendOverload<object>.Invoke(buf, 99);
            Assert.Equal("99", buf.ToString());
            buf.Clear();
        }
        static class Parse<T>
        {
            public static readonly TryParse<T> TryParse = Method.Resolve<TryParse<T>>();
        }
        [Fact]
        public static void TestMethodResolution()
        {
            int i;
            Assert.True(Parse<int>.TryParse("345", out i));
            Assert.Equal(345, i);
            DateTime d;
            Assert.True(Parse<DateTime>.TryParse("2016-02-01", out d));
            Assert.Equal(new DateTime(2016, 2, 1), d);
        }
        #endregion

        #region Generic type associations
        [Fact]
        public static void TestDynamicGenerics()
        {
            var list = Type<IList<int>>.Create();
            Assert.NotNull(list);
            Assert.True(list is List<int>);

            var dict = Type<IDictionary<int, string>>.Create();
            Assert.NotNull(dict);
            Assert.True(dict is Dictionary<int, string>);
        }
        #endregion

        #region Structural equality checks
        [Fact]
        public static void StructuralEqualityList()
        {
            var x = new List<int> { 1, 2, 3 };
            var copy = Type<List<int>>.Copy(x);
            Assert.True(Type<List<int>>.StructuralEquals(x, copy));

            copy[1] = 0;
            Assert.False(Type<List<int>>.StructuralEquals(x, copy));
        }

        [Fact]
        public static void StructuralEqualityPrimitives()
        {
            var x = 1;
            Assert.True(Type<int>.StructuralEquals(x, x));
            Assert.True(Type<int>.StructuralEquals(x, 1));
            Assert.False(Type<int>.StructuralEquals(x, 2));

            var array = new[] { 1, 2, 3 };
            var array2 = new[] { 1, 2, 3 };
            Assert.True(Type<int[]>.StructuralEquals(array, array2));
            Assert.True(Type<int[]>.StructuralEquals(array, new[] { 1, 2, 3 }));
            Assert.False(Type<int[]>.StructuralEquals(array, new[] { 2, 2, 3 }));

            var d = 23M;
            Assert.True(Type<decimal>.StructuralEquals(2, 2));
            Assert.True(Type<decimal>.StructuralEquals(d, 23));
            Assert.False(Type<decimal>.StructuralEquals(d, 22M));
        }

        [Fact]
        public static void HashSetTupleEquality()
        {
            var x = new List<int> { 1, 2, 3 };
            var copy = Type<List<int>>.Copy(x);
            var tuple = (x as object, copy as object);
            var set = new HashSet<(object, object)>();

            Assert.True(set.Add(tuple));
            Assert.False(set.Add((x as object, copy as object)));
        }

        [Fact]
        public static void StructuralEqualityComplex()
        {
            var foo1 = new FooEq { Prop = 1, Bar = new BarEq { Prop = 2, Name = "foo" } };
            var foo2 = new FooEq { Prop = 1, Bar = new BarEq { Prop = 2, Name = "foo" } };
            Assert.True(Type<FooEq>.StructuralEquals(foo1, foo2));
            foo2.Bar.Name = "bar";
            Assert.False(Type<FooEq>.StructuralEquals(foo1, foo2));
            foo2.Bar = foo1.Bar;
            Assert.True(Type<FooEq>.StructuralEquals(foo1, foo2));
        }

        [Fact]
        public static void StructuralEqualityCycles()
        {
            var bar1 = new BarEq { Prop = 2, Name = "foo" };
            var bar2 = new BarEq { Prop = 2, Name = "foo" };
            Assert.True(Type<BarEq>.StructuralEquals(bar1, bar2));

            var foo1 = new FooEq { Prop = 1, Bar = new BarEq { Prop = 2, Name = "foo" } };
            var foo2 = new FooEq { Prop = 1, Bar = new BarEq { Prop = 2, Name = foo1.Bar.Name } };
            Assert.True(Type<FooEq>.StructuralEquals(foo1, foo2));
            foo2.Bar.Name = "bar";
            Assert.False(Type<FooEq>.StructuralEquals(foo1, foo2));
            foo2.Bar = foo1.Bar;
            Assert.True(Type<FooEq>.StructuralEquals(foo1, foo2));
        }

        [Fact]
        public static void StructuralEqualityCyclesWithNull()
        {
            var x = new CylesEq { Name = "foo", Recursive = null };
            Assert.True(Type<CylesEq>.StructuralEquals(x, x));
            var y = new CylesEq { Name = "foo", Recursive = x };
            Assert.False(Type<CylesEq>.StructuralEquals(x, y));
            Assert.True(Type<CylesEq>.StructuralEquals(y, y));
        }

        class FooEq
        {
            public int Prop { get; set; }
            public BarEq Bar { get; set; }
        }

        public class BarEq
        {
            public int Prop { get; set; }
            public string Name { get; set; }
        }

        class CylesEq
        {
            public string Name { get; set; }
            public object Recursive { get; set; }
        }

        #endregion
    }
}
