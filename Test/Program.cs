using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
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
            NotRecursive<int>();
            NotRecursive<string>();
            NotRecursive<ROField>();
            //MaybeCircular<Self>();
            IsRecursive<Self>();
        }
        static void IsRecursive<T>()
        {
            Assert(Type<T>.RecursiveType == RecursiveType.Yes);
        }
        //static void MaybeCircular<T>()
        //{
        //    Assert(Type<T>.Circularity == Circularity.Maybe);
        //}
        static void NotRecursive<T>()
        {
            Assert(Type<T>.RecursiveType == RecursiveType.No);
        }
        #endregion

        static void Main(string[] args)
        {
            CircularityTests();
            CheckImmutable();
            CheckMutable();
            CheckMaybeMutable();
            RuntimeMutable();
            CopyTests();
        }
    }
}
