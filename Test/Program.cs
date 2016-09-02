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
        sealed class TransitiveField<T>
        {
            readonly T field;
            public TransitiveField(T value) { field = value; }
        }
        sealed class TransitiveProp<T>
        {
            public T Prop { get; private set; }
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
            Debug.Assert(Type<T>.Mutability == Mutability.Immutable);
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
            Debug.Assert(Type<T>.Mutability == Mutability.Mutable);
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
            Debug.Assert(Type<T>.Mutability == Mutability.Maybe);
        }
        #endregion
        #region Runtime mutability checks
        class DefMut : MaybeMut
        {
            public string Bar { get; set; }
        }
        static void RuntimeMutable()
        {
            IsImmutable<MaybeMut>(new MaybeMut());
            IsMutable<MaybeMut>(new DefMut());
            IsImmutable<TransitiveField<object>>(new TransitiveField<object>("foo"));
            IsMutable<TransitiveField<object>>(new TransitiveField<object>(new[] { 2, 3 }));
        }
        static void IsImmutable<T>(T value)
        {
            Debug.Assert(!Type<T>.IsMutable(value));
        }
        static void IsMutable<T>(T value)
        {
            Debug.Assert(Type<T>.IsMutable(value));
        }
        #endregion
        static void Main(string[] args)
        {
            CheckImmutable();
            CheckMutable();
            CheckMaybeMutable();
            RuntimeMutable();
        }
    }
}
