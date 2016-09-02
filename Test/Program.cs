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
        }
        static void IsImmutable<T>()
        {
            Debug.Assert(Type<T>.Mutability == Mutability.Immutable);
        }
        static void Assert(bool cond)
        {
            Debug.Assert(cond);
        }
        static void Main(string[] args)
        {
            CheckImmutable();
        }
    }
}
