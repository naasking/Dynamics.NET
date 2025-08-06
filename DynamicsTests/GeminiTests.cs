using System;
using System.Collections.Generic;
using Xunit;
using Dynamics;

namespace DynamicsTests
{

    // ####################
    // ##   XUNIT TESTS  ##
    // ####################

    public class StructuralEqualityTests
    {
        // --- Test Data Structures ---

        enum TestEnum { A, B }

        struct SimpleStruct
        {
            public int IntValue;
            public string StringValue;
        }

        struct NestedStruct
        {
            public double DoubleValue;
            public SimpleStruct Nested;
        }

        class SimpleClass
        {
            public int Id;
            public string Name;
            public decimal Value;
        }

        class EmptyClass { }

        class ClassWithNesting
        {
            public string Description;
            public SimpleClass NestedObject;
        }

        class BaseWithPrivate
        {
            private readonly Guid _privateId = Guid.NewGuid();
            public string BaseName;

            public BaseWithPrivate(string name)
            {
                BaseName = name;
            }
        }

        class DerivedClass : BaseWithPrivate
        {
            public int DerivedValue;
            public DerivedClass(string baseName, int derivedValue) : base(baseName)
            {
                DerivedValue = derivedValue;
            }
        }

        class CyclicNode
        {
            public string Name;
            public CyclicNode Next;
        }

        class MutuallyRecursiveA
        {
            public string Name;
            public MutuallyRecursiveB B;
        }

        class MutuallyRecursiveB
        {
            public int Id;
            public MutuallyRecursiveA A;
        }

        // --- Test Methods ---

        [Theory]
        [InlineData(1, 1, true)]
        [InlineData(1, 2, false)]
        [InlineData("hello", "hello", true)]
        [InlineData("hello", "world", false)]
        [InlineData("hello", null, false)]
        [InlineData(null, null, true)]
        [InlineData(TestEnum.A, TestEnum.A, true)]
        [InlineData(TestEnum.A, TestEnum.B, false)]
        public void SimpleTypes_BehavesCorrectly<T>(T v1, T v2, bool expected)
        {
            Assert.Equal(expected, Type<T>.StructuralEquals(v1, v2));
        }

        [Fact]
        public void SimpleStruct_BehavesCorrectly()
        {
            var s1 = new SimpleStruct { IntValue = 1, StringValue = "test" };
            var s2 = new SimpleStruct { IntValue = 1, StringValue = "test" };
            var s3 = new SimpleStruct { IntValue = 2, StringValue = "test" };

            Assert.True(Type<SimpleStruct>.StructuralEquals(s1, s2));
            Assert.False(Type<SimpleStruct>.StructuralEquals(s1, s3));
        }

        [Fact]
        public void NestedStruct_BehavesCorrectly()
        {
            var ns1 = new NestedStruct { DoubleValue = 1.0, Nested = new SimpleStruct { IntValue = 1, StringValue = "a" } };
            var ns2 = new NestedStruct { DoubleValue = 1.0, Nested = new SimpleStruct { IntValue = 1, StringValue = "a" } };
            var ns3 = new NestedStruct { DoubleValue = 1.0, Nested = new SimpleStruct { IntValue = 2, StringValue = "a" } };

            Assert.True(Type<NestedStruct>.StructuralEquals(ns1, ns2));
            Assert.False(Type<NestedStruct>.StructuralEquals(ns1, ns3));
        }

        [Fact]
        public void EmptyClass_BehavesCorrectly()
        {
            var ec1 = new EmptyClass();
            var ec2 = new EmptyClass();

            Assert.True(Type<EmptyClass>.StructuralEquals(ec1, ec2)); // Two non-null instances are equal
            Assert.True(Type<EmptyClass>.StructuralEquals(ec1, ec1)); // Reference equality
            Assert.False(Type<EmptyClass>.StructuralEquals(ec1, null));
            Assert.True(Type<EmptyClass>.StructuralEquals(null, null));
        }

        [Fact]
        public void ClassWithNesting_BehavesCorrectly()
        {
            var c1 = new ClassWithNesting { Description = "d", NestedObject = new SimpleClass { Id = 1, Name = "n" } };
            var c2 = new ClassWithNesting { Description = "d", NestedObject = new SimpleClass { Id = 1, Name = "n" } };
            var c3 = new ClassWithNesting { Description = "d", NestedObject = new SimpleClass { Id = 2, Name = "n" } };
            var c4 = new ClassWithNesting { Description = "d", NestedObject = null };

            Assert.True(Type<ClassWithNesting>.StructuralEquals(c1, c2));
            Assert.False(Type<ClassWithNesting>.StructuralEquals(c1, c3));
            Assert.False(Type<ClassWithNesting>.StructuralEquals(c1, c4));
            Assert.False(Type<ClassWithNesting>.StructuralEquals(c4, c1));
        }

        [Fact]
        public void Inheritance_ComparesPrivateBaseFields()
        {
            // This test will fail if private fields in base classes are not being compared.
            // Because _privateId is different for each instance, two objects should never be equal.
            var d1 = new DerivedClass("name", 1);
            var d2 = new DerivedClass("name", 1);

            Assert.False(Type<DerivedClass>.StructuralEquals(d1, d2));
            Assert.True(Type<DerivedClass>.StructuralEquals(d1, d1));
        }

        [Fact]
        public void DirectCycle_HandlesCorrectly()
        {
            var node1 = new CyclicNode { Name = "A" };
            node1.Next = node1; // Self-reference

            var node2 = new CyclicNode { Name = "A" };
            node2.Next = node2; // Self-reference

            var node3 = new CyclicNode { Name = "B" };
            node3.Next = node3; // Different name

            var node4 = new CyclicNode { Name = "A" };
            // node4.Next is null (acyclic)

            Assert.True(Type<CyclicNode>.StructuralEquals(node1, node1)); // Two identical cycles are equal
            Assert.True(Type<CyclicNode>.StructuralEquals(node1, node2)); // Two identical cycles are equal
            Assert.False(Type<CyclicNode>.StructuralEquals(node1, node3)); // Different data in cycle
            Assert.False(Type<CyclicNode>.StructuralEquals(node1, node4)); // Cyclic vs Acyclic
        }

        [Fact]
        public void MutuallyRecursiveTypes_HandlesCycleCorrectly()
        {
            // Graph 1
            var a1 = new MutuallyRecursiveA { Name = "A" };
            var b1 = new MutuallyRecursiveB { Id = 1 };
            a1.B = b1;
            b1.A = a1;

            // Graph 2: Structurally identical to Graph 1
            var a2 = new MutuallyRecursiveA { Name = "A" };
            var b2 = new MutuallyRecursiveB { Id = 1 };
            a2.B = b2;
            b2.A = a2;

            // Graph 3: Structurally different from Graph 1
            var a3 = new MutuallyRecursiveA { Name = "DIFFERENT" };
            var b3 = new MutuallyRecursiveB { Id = 1 };
            a3.B = b3;
            b3.A = a3;

            Assert.True(Type<MutuallyRecursiveA>.StructuralEquals(a1, a2));
            Assert.False(Type<MutuallyRecursiveA>.StructuralEquals(a1, a3));
        }

        [Fact]
        public void Arrays_BehavesCorrectly()
        {
            var a1 = new[] { 1, 2, 3 };
            var a2 = new[] { 1, 2, 3 };
            var a3 = new[] { 1, 2, 4 };
            var a4 = new[] { 1, 2 };

            Assert.True(Type<int[]>.StructuralEquals(a1, a2));
            Assert.False(Type<int[]>.StructuralEquals(a1, a3));
            Assert.False(Type<int[]>.StructuralEquals(a1, a4));
            Assert.False(Type<int[]>.StructuralEquals(a1, null));
        }

        [Fact]
        public void ArrayOfComplexTypes_BehavesCorrectly()
        {
            var arr1 = new[] { new SimpleClass { Id = 1, Name = "A" }, new SimpleClass { Id = 2, Name = "B" } };
            var arr2 = new[] { new SimpleClass { Id = 1, Name = "A" }, new SimpleClass { Id = 2, Name = "B" } };
            var arr3 = new[] { new SimpleClass { Id = 1, Name = "A" }, new SimpleClass { Id = 99, Name = "B" } };

            Assert.True(Type<SimpleClass[]>.StructuralEquals(arr1, arr2));
            Assert.False(Type<SimpleClass[]>.StructuralEquals(arr1, arr3));
        }
    }
}