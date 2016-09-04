using System;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.Collections.ObjectModel;
using Dynamics;

namespace Test
{
    class Program
    {
        static void Assert(bool cond)
        {
            if (!cond) Debugger.Break();
        }
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

        static void CheckImmutable()
        {
            IsImmutable<int>();
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
            IsImmutable<DateTimeOffset>();
            IsImmutable<DateTimeKind>();
            IsImmutable<TimeSpan>();
            IsImmutable<Kind>();
            IsImmutable<UriKind>();
            IsImmutable<ROField>();
            IsImmutable<ROProperty>();
            IsImmutable<EqualsOverride>();
            IsImmutable<Equatable>();
            IsImmutable<PureType>();
            IsImmutable<PureProp>();
            IsImmutable<Formattable>();
            IsImmutable<KeyValuePair<int, char>>();
            IsImmutable<TransitiveField<int>>();
            IsImmutable<TransitiveProp<int>>();
            IsImmutable<IntPtr>();
            IsImmutable<MethodInfo>();
            IsImmutable<FieldInfo>();
            IsImmutable<MemberInfo>();
            IsImmutable<MethodBase>();
        }
        static void IsImmutable<T>()
        {
            Assert(Type<T>.Mutability == Mutability.Immutable);
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
        static void CheckMutable()
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
            Assert(Type<T>.Mutability == Mutability.Mutable);
        }
        #endregion
        #region Maybe mutable checks
        class MaybeMut
        {
            readonly int field;
        }
        static void CheckMaybeMutable()
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
            Assert(Type<T>.Mutability == Mutability.Maybe);
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
        static void RuntimeMutable()
        {
            IsImmutable(new MaybeMut());
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
            Assert(!Type<T>.IsMutable(value));
        }
        static void IsMutable<T>(T value)
        {
            Assert(Type<T>.IsMutable(value));
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
            IsCopied(new Action(CopyTests), (x, y) => x.Method == y.Method && x.Target == y.Target);
        }
        static void IsShared<T>(T orig)
        {
            var copy = Type<T>.Copy(orig);
            Assert(ReferenceEquals(orig, copy));
            Assert(Type<T>.DefaultEquals(orig, copy));
        }
        static void IsCopied<T>(T orig, Func<T, T, bool> eq = null)
        {
            var copy = Type<T>.Copy(orig);
            Assert(!ReferenceEquals(orig, copy));
            Assert(eq == null && Type<T>.DefaultEquals(orig, copy) || eq != null && eq(orig, copy));
        }
        #endregion

        #region Check circularity
        static void CircularityTests()
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
            Assert(Type<T>.Cycles == Cycles.Yes);
        }
        static void IsAcyclic<T>()
        {
            Assert(Type<T>.Cycles == Cycles.No);
        }
        #endregion

        #region Constructor tests
        static void CheckConstructors()
        {
            var x = Constructor<Func<int, int[]>>.Invoke(89);
            Assert(x.Length == 89);
            var s = Constructor<Func<char[], string>>.Invoke(new[] { 'h', 'e', 'l', 'l', 'o' });
            Assert(s == "hello");
            // following works, but breaks the debugger on the error thrown
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

        static void Main(string[] args)
        {
            CheckConstructors();
            CircularityTests();
            CheckImmutable();
            CheckMutable();
            CheckMaybeMutable();
            RuntimeMutable();
            CopyTests();
        }
    }
}
