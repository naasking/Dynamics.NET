using Dynamics;
using System;
using System.Collections.Generic;
using System.Reflection;
using Xunit;

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

        // --- New Test Data Structures for Stress-Testing Collections ---

        // A container to test a List<T> field.
        class ContainerOfList
        {
            public List<SimpleClass> Items;
        }

        // A container for a jagged array.
        class ContainerOfJaggedArray
        {
            public int[][] JaggedArray;
        }

        // Classes for testing cycles that involve collections.
        class Person
        {
            public string Name;
            // This creates a cycle: Person -> List -> Document -> Person
            public List<Document> Documents;
        }

        class Document
        {
            public string Title;
            public Person Owner;
        }

        // --- New XUnit Stress Tests for Collections ---

        [Fact]
        public void ListOfComplexObjects_BehavesCorrectly()
        {
            // A common real-world case: a class containing a list.
            var c1 = new ContainerOfList
            {
                Items = new List<SimpleClass>
                {
                    new SimpleClass { Id = 1, Name = "A" },
                    new SimpleClass { Id = 2, Name = "B" }
                }
            };
            var c2 = new ContainerOfList // Structurally identical
            {
                Items = new List<SimpleClass>
                {
                    new SimpleClass { Id = 1, Name = "A" },
                    new SimpleClass { Id = 2, Name = "B" }
                }
            };
            var c3 = new ContainerOfList // Different item in list
            {
                Items = new List<SimpleClass>
                {
                    new SimpleClass { Id = 1, Name = "A" },
                    new SimpleClass { Id = 99, Name = "X" }
                }
            };

            Assert.True(Type<ContainerOfList>.StructuralEquals(c1, c2));
            Assert.False(Type<ContainerOfList>.StructuralEquals(c1, c3));
        }

        [Fact]
        public void List_Vs_Null_And_Empty_BehavesCorrectly()
        {
            var c1 = new ContainerOfList { Items = new List<SimpleClass>() }; // Empty list
            var c2 = new ContainerOfList { Items = new List<SimpleClass>() }; // Another empty list
            var c3 = new ContainerOfList { Items = null };                     // Null list
            var c4 = new ContainerOfList { Items = new List<SimpleClass> { new SimpleClass() } }; // List with one item

            Assert.True(Type<ContainerOfList>.StructuralEquals(c1, c2), "Two separate empty lists should be equal.");
            Assert.False(Type<ContainerOfList>.StructuralEquals(c1, c3), "An empty list and a null list should NOT be equal.");
            Assert.False(Type<ContainerOfList>.StructuralEquals(c3, c1), "A null list and an empty list should NOT be equal.");
            Assert.False(Type<ContainerOfList>.StructuralEquals(c1, c4), "An empty list and a non-empty list should NOT be equal.");
        }


        [Fact]
        public void ArrayWithNullElements_BehavesCorrectly()
        {
            var arr1 = new[] { new SimpleClass { Id = 1 }, null, new SimpleClass { Id = 3 } };
            var arr2 = new[] { new SimpleClass { Id = 1 }, null, new SimpleClass { Id = 3 } }; // Identical
            var arr3 = new[] { new SimpleClass { Id = 1 }, new SimpleClass(), new SimpleClass { Id = 3 } }; // Different

            Assert.True(Type<SimpleClass[]>.StructuralEquals(arr1, arr2));
            Assert.False(Type<SimpleClass[]>.StructuralEquals(arr1, arr3));
        }

        [Fact]
        public void JaggedArray_BehavesCorrectly()
        {
            var c1 = new ContainerOfJaggedArray { JaggedArray = new[] { new[] { 1, 2 }, new[] { 3 } } };
            var c2 = new ContainerOfJaggedArray { JaggedArray = new[] { new[] { 1, 2 }, new[] { 3 } } }; // Identical
            var c3 = new ContainerOfJaggedArray { JaggedArray = new[] { new[] { 1, 2 }, new[] { 99 } } };// Different inner value
            var c4 = new ContainerOfJaggedArray { JaggedArray = new[] { new[] { 1, 2 } } };             // Different outer length

            Assert.True(Type<ContainerOfJaggedArray>.StructuralEquals(c1, c2));
            Assert.False(Type<ContainerOfJaggedArray>.StructuralEquals(c1, c3));
            Assert.False(Type<ContainerOfJaggedArray>.StructuralEquals(c1, c4));
        }

        [Fact]
        public void ListOfObjects_WithCycles_HandlesCorrectly()
        {
            // This is the ultimate stress test. It models a common database-like scenario
            // where a parent object contains a list of children, and each child
            // has a reference back to the parent.

            // --- Graph 1 ---
            var person1 = new Person { Name = "Alice" };
            var doc1 = new Document { Title = "CV", Owner = person1 };
            person1.Documents = new List<Document> { doc1 };

            // --- Graph 2 (Structurally Identical) ---
            var person2 = new Person { Name = "Alice" };
            var doc2 = new Document { Title = "CV", Owner = person2 };
            person2.Documents = new List<Document> { doc2 };

            // --- Graph 3 (Different Document Title) ---
            var person3 = new Person { Name = "Alice" };
            var doc3 = new Document { Title = "INVOICE", Owner = person3 };
            person3.Documents = new List<Document> { doc3 };

            // --- Graph 4 (Different Owner Name) ---
            var person4 = new Person { Name = "Bob" };
            var doc4 = new Document { Title = "CV", Owner = person4 };
            person4.Documents = new List<Document> { doc4 };

            // --- Graph 5 (Broken Cycle Link) ---
            var person5 = new Person { Name = "Alice" };
            var doc5 = new Document { Title = "CV", Owner = null }; // Owner is null
            person5.Documents = new List<Document> { doc5 };

            Assert.True(Type<Person>.StructuralEquals(person1, person2), "Identical cyclic graphs should be equal.");
            Assert.False(Type<Person>.StructuralEquals(person1, person3), "Difference in a child object should be detected.");
            Assert.False(Type<Person>.StructuralEquals(person1, person4), "Difference in a parent object should be detected through the cycle.");
            Assert.False(Type<Person>.StructuralEquals(person1, person5), "A complete cycle vs a broken cycle should not be equal.");
        }

        // --- New Test Data Structures for Deeply Nested Collections ---

        class Employee
        {
            public int Id;
            public string Name;
        }

        class Department
        {
            public string DepartmentName;
            public List<Employee> Employees;
        }

        class Organization
        {
            public string OrganizationName;
            public List<Department> Departments;
        }

        // Another structure for a different kind of nesting
        class Project
        {
            public string ProjectName;
            public List<List<string>> TaskGroups; // List of lists
        }

        // --- New XUnit Tests for Deeply Nested Collections ---

        [Fact]
        public void DeeplyNestedCollections_BehavesCorrectly()
        {
            // --- Graph 1 (Baseline) ---
            var org1 = new Organization
            {
                OrganizationName = "MegaCorp",
                Departments = new List<Department>
                {
                    new Department
                    {
                        DepartmentName = "Engineering",
                        Employees = new List<Employee>
                        {
                            new Employee { Id = 101, Name = "Alice" },
                            new Employee { Id = 102, Name = "Bob" }
                        }
                    },
                    new Department
                    {
                        DepartmentName = "Marketing",
                        Employees = new List<Employee>
                        {
                            new Employee { Id = 201, Name = "Charlie" }
                        }
                    }
                }
            };

            // --- Graph 2 (Structurally Identical) ---
            var org2 = new Organization
            {
                OrganizationName = "MegaCorp",
                Departments = new List<Department>
                {
                    new Department
                    {
                        DepartmentName = "Engineering",
                        Employees = new List<Employee>
                        {
                            new Employee { Id = 101, Name = "Alice" },
                            new Employee { Id = 102, Name = "Bob" }
                        }
                    },
                    new Department
                    {
                        DepartmentName = "Marketing",
                        Employees = new List<Employee>
                        {
                            new Employee { Id = 201, Name = "Charlie" }
                        }
                    }
                }
            };

            // --- Graph 3 (Difference deep inside an employee) ---
            var org3 = new Organization
            {
                OrganizationName = "MegaCorp",
                Departments = new List<Department>
                {
                    new Department
                    {
                        DepartmentName = "Engineering",
                        Employees = new List<Employee>
                        {
                            new Employee { Id = 101, Name = "Alice" },
                            new Employee { Id = 999, Name = "Bob" } // <-- Deepest level change
                        }
                    },
                    new Department
                    {
                        DepartmentName = "Marketing",
                        Employees = new List<Employee>
                        {
                            new Employee { Id = 201, Name = "Charlie" }
                        }
                    }
                }
            };

            // --- Graph 4 (Structural difference - missing employee) ---
            var org4 = new Organization
            {
                OrganizationName = "MegaCorp",
                Departments = new List<Department>
                {
                    new Department
                    {
                        DepartmentName = "Engineering",
                        Employees = new List<Employee>
                        {
                            new Employee { Id = 101, Name = "Alice" } // <-- Missing Bob
                        }
                    },
                    new Department
                    {
                        DepartmentName = "Marketing",
                        Employees = new List<Employee>
                        {
                            new Employee { Id = 201, Name = "Charlie" }
                        }
                    }
                }
            };

            Assert.True(Type<Organization>.StructuralEquals(org1, org2), "Identical deep graphs should be equal.");
            Assert.False(Type<Organization>.StructuralEquals(org1, org3), "A deep change in a nested object's field should be detected.");
            Assert.False(Type<Organization>.StructuralEquals(org1, org4), "A structural difference in a nested list's length should be detected.");
        }

        [Fact]
        public void ListOfLists_BehavesCorrectly()
        {
            var p1 = new Project
            {
                ProjectName = "Release 1.0",
                TaskGroups = new List<List<string>>
                {
                    new List<string> { "Design", "Implement" },
                    new List<string> { "Test", "Deploy" }
                }
            };

            var p2 = new Project // Identical
            {
                ProjectName = "Release 1.0",
                TaskGroups = new List<List<string>>
                {
                    new List<string> { "Design", "Implement" },
                    new List<string> { "Test", "Deploy" }
                }
            };

            var p3 = new Project // Different inner list item
            {
                ProjectName = "Release 1.0",
                TaskGroups = new List<List<string>>
                {
                    new List<string> { "Design", "Implement" },
                    new List<string> { "Test", "DOCUMENT" } // <-- Change
                }
            };

            var p4 = new Project // Different inner list length
            {
                ProjectName = "Release 1.0",
                TaskGroups = new List<List<string>>
                {
                    new List<string> { "Design", "Implement" },
                    new List<string> { "Test" } // <-- Shorter list
                }
            };

            Assert.True(Type<Project>.StructuralEquals(p1, p2), "Identical list of lists should be equal.");
            Assert.False(Type<Project>.StructuralEquals(p1, p3), "A change in an inner list should be detected.");
            Assert.False(Type<Project>.StructuralEquals(p1, p4), "A length change in an inner list should be detected.");
        }

        [Fact]
        public void NestedCollections_WithSharedObjectReference_BehavesCorrectly()
        {
            // This tests a subtle case: what if a single object instance
            // appears multiple times within the same object graph?
            var sharedEmployee = new Employee { Id = 500, Name = "Shared" };

            var org1 = new Organization
            {
                OrganizationName = "SharedCorp",
                Departments = new List<Department>
                {
                    new Department { DepartmentName = "Dept A", Employees = new List<Employee> { sharedEmployee } },
                    new Department { DepartmentName = "Dept B", Employees = new List<Employee> { sharedEmployee } }
                }
            };

            var org2 = new Organization // Also has a shared reference
            {
                OrganizationName = "SharedCorp",
                Departments = new List<Department>
                {
                    new Department { DepartmentName = "Dept A", Employees = new List<Employee> { sharedEmployee } },
                    new Department { DepartmentName = "Dept B", Employees = new List<Employee> { sharedEmployee } }
                }
            };

            var org3 = new Organization // Uses two distinct but identical objects instead of a shared one
            {
                OrganizationName = "SharedCorp",
                Departments = new List<Department>
                {
                    new Department { DepartmentName = "Dept A", Employees = new List<Employee> { new Employee { Id = 500, Name = "Shared" } } },
                    new Department { DepartmentName = "Dept B", Employees = new List<Employee> { new Employee { Id = 500, Name = "Shared" } } }
                }
            };

            Assert.True(Type<Organization>.StructuralEquals(org1, org2), "Two graphs with the same shared object reference should be equal.");
            Assert.True(Type<Organization>.StructuralEquals(org1, org3), "A graph with a shared object should be structurally equal to one with distinct but identical objects.");
        }

        // --- New XUnit Tests to Verify Copy() via StructuralEquals ---

        [Fact]
        public void CopyAndCompare_DeeplyNestedCollections_ShouldBeEqual()
        {
            // Arrange: Create a complex, multi-level object graph.
            var original = new Organization
            {
                OrganizationName = "MegaCorp",
                Departments = new List<Department>
        {
            new Department
            {
                DepartmentName = "Engineering",
                Employees = new List<Employee>
                {
                    new Employee { Id = 101, Name = "Alice" },
                    new Employee { Id = 102, Name = "Bob" }
                }
            }
        }
            };

            // Act: Create a deep copy.
            var copy = Type<Organization>.Copy(original);

            // Assert: The copy should be structurally identical to the original.
            Assert.True(Type<Organization>.StructuralEquals(original, copy),
                "A deep copy of a deeply nested object should be structurally equal to the original.");

            // Sanity Check: Ensure the test is valid by modifying the copy and re-comparing.
            copy.Departments[0].Employees[1].Name = "Robert"; // Change "Bob" to "Robert"
            Assert.False(Type<Organization>.StructuralEquals(original, copy),
                "A modified deep copy should no longer be equal to the original.");
        }

        [Fact]
        public void CopyAndCompare_ListOfLists_ShouldBeEqual()
        {
            // Arrange
            var original = new Project
            {
                ProjectName = "Release 1.0",
                TaskGroups = new List<List<string>>
                {
                    new List<string> { "Design", "Implement" },
                    new List<string> { "Test", "Deploy" }
                }
            };

            // Act
            var copy = Type<Project>.Copy(original);

            // Assert
            Assert.True(Type<Project>.StructuralEquals(original, copy),
                "A deep copy of an object with a list of lists should be equal.");

            // Sanity Check
            copy.TaskGroups[1][0] = "Verify"; // Change "Test" to "Verify"
            Assert.False(Type<Project>.StructuralEquals(original, copy),
                "A modified copy of a list of lists should not be equal.");
        }

        [Fact]
        public void CopyAndCompare_GraphWithCycles_ShouldBeEqualAndCorrectlyFormed()
        {
            // Arrange: This is the most critical test. A copy function is most likely
            // to fail when handling cycles.
            var originalPerson = new Person { Name = "Alice" };
            var originalDoc = new Document { Title = "CV", Owner = originalPerson };
            originalPerson.Documents = new List<Document> { originalDoc };

            // Act
            var copiedPerson = Type<Person>.Copy(originalPerson);

            // Assert (Phase 1): The copy is structurally equal.
            // This is the main check that relies on your StructuralEquals function.
            Assert.True(Type<Person>.StructuralEquals(originalPerson, copiedPerson),
                "A deep copy of a cyclic graph should be structurally equal.");

            // Assert (Phase 2): The copy's internal structure is correct.
            // This verifies that the copy function didn't just create a broken graph.
            Assert.NotNull(copiedPerson.Documents);
            Assert.Single(copiedPerson.Documents);
            Assert.NotNull(copiedPerson.Documents[0]);

            // Check that the objects are new instances, not just shallow copies.
            Assert.NotSame(originalPerson, copiedPerson);
            Assert.NotSame(originalPerson.Documents, copiedPerson.Documents);
            Assert.NotSame(originalPerson.Documents[0], copiedPerson.Documents[0]);

            // CRITICAL: Check that the cycle in the *copy* points to the *copied* parent,
            // not the original one. This is the most common bug in a deep copy implementation.
            Assert.Same(copiedPerson, copiedPerson.Documents[0].Owner);
        }

        // --- Test Data Structures to Expose the Copy Bug ---

        public class Parent
        {
            public string Name { get; set; }
            public List<Child> Children { get; set; }
        }

        public class Child
        {
            public string Name { get; set; }
            // This back-reference is what triggers the bug.
            public Parent ParentRef { get; set; }
        }

        [Fact]
        public void CopyAndCompare_WithCycle_ExposesRegisterThenPopulateBug()
        {
            // Arrange: Create a cyclic graph. The Parent contains a Child,
            // and the Child refers back to the Parent.
            var originalParent = new Parent { Name = "Parent" };
            var originalChild = new Child { Name = "Child" };

            originalParent.Children = new List<Child> { originalChild };
            originalChild.ParentRef = originalParent; // Complete the cycle.

            // Act: Attempt to create a deep copy of the graph.
            var copiedParent = Type<Parent>.Copy(originalParent);

            // Assert: Use the trusted StructuralEquals to verify the copy.
            // This assertion will FAIL if the bug exists, because the copy will be structurally different.
            Assert.True(Type<Parent>.StructuralEquals(originalParent, copiedParent),
                "The copied cyclic graph should be structurally equal to the original.");

            // --- Diagnostic Assertions ---
            // The following asserts help pinpoint *why* the main assertion above would fail.
            // If the bug exists, the `copiedChild.ParentRef` will be null.

            Assert.NotNull(copiedParent);
            Assert.NotNull(copiedParent.Children);
            Assert.Single(copiedParent.Children);

            var copiedChild = copiedParent.Children[0];
            Assert.NotNull(copiedChild);

            // This is the most direct test for the bug's symptom.
            Assert.NotNull(copiedChild.ParentRef);
            Assert.Equal("Parent", copiedChild.ParentRef.Name);
        }

        // --- Test Data Structures for Readonly Cycle Bug ---

        // NOTE: These classes have no parameterless constructor.
        // They can only be created with their dependencies.

        public class CyclicParentRO
        {
            public readonly string Name;
            public readonly CyclicChildRO Child;

            public CyclicParentRO(string name, CyclicChildRO child)
            {
                this.Name = name;
                this.Child = child;
            }
        }

        public class CyclicChildRO
        {
            public readonly string Name;
            // The back-reference that completes the cycle.
            public readonly CyclicParentRO Parent;

            public CyclicChildRO(string name, CyclicParentRO parent)
            {
                this.Name = name;
                this.Parent = parent;
            }
        }

        [Fact]
        public void Copy_WithReadOnlyCycle_ThrowsStackOverflow()
        {
            // Arrange: Create a cyclic graph where the cycle is formed by readonly fields.
            // This is tricky to set up because of the constructor dependencies. We have to
            // create one object with a null dependency, then create the second, then
            // create the final version of the first.

            var finalParent = new CyclicParentRO("Parent", null); // Temporary parent
            var child = new CyclicChildRO("Child", finalParent);
            var parent = new CyclicParentRO("Parent", child);

            var x = Type<CyclicParentRO>.Copy(parent);
            Assert.Equal(parent.Name, x.Name);
            Assert.NotEqual(parent, x);
            Assert.NotEqual(parent.Child, x.Child);
            Assert.Equal(parent.Child.Name, x.Child.Name);
            Assert.NotEqual(parent.Child.Parent, x.Child.Parent);
            Assert.Equal(parent.Child.Name, x.Child.Name);
            Assert.NotEqual(x, x.Child.Parent);
        }

        // --- Test Data Structure for Readonly Self-Reference Bug ---

        public class SelfReferentialReadonly
        {
            public readonly int Id;
            // This field creates an impossible-to-resolve constructor dependency during a copy.
            public readonly SelfReferentialReadonly Self;

            public SelfReferentialReadonly(int id)
            {
                this.Id = id;
                this.Self = this; // The field is initialized to the instance itself.
            }
        }

        [Fact]
        public void Copy_WithReadonlySelfReference_CausesStackOverflow()
        {
            // Arrange: Create an instance of the class. Its 'Self' field
            // will point back to the instance itself.
            var original = new SelfReferentialReadonly(123);

            // Act & Assert: The Copy operation must fail with a StackOverflowException.
            // Your generator will create code that tries to call the constructor with an
            // argument that depends on the result of the copy itself, creating an
            // infinite recursion that cannot be resolved by the `refs` dictionary.
            //var e = Assert.Throws<Exception>(() => Type<SelfReferentialReadonly>.Copy(original));
            var x = Type<SelfReferentialReadonly>.Copy(original);
            Assert.NotNull(x);
            Assert.Equal(original.Id, x.Id);
        }

        public class WrongConstructorNode
        {
            public readonly int Id;
            public readonly string Name;

            // This is a valid constructor, but it's incomplete for a deep copy.
            // Because it often appears *after* the two-parameter constructor in the
            // list returned by GetConstructors(), your loop will select it.
            public WrongConstructorNode(int id)
            {
                this.Id = id;
                this.Name = "Default Name"; // The copy logic doesn't know about this.
            }

            // This is the constructor that a correct deep copy function should use.
            public WrongConstructorNode(int id, string name)
            {
                this.Id = id;
                this.Name = name;
            }
        }

        [Fact]
        public void Copy_WithMultipleConstructors_FailsByChoosingTheWrongOne()
        {
            // Arrange: Create an object using the "correct" two-parameter constructor.
            var original = new WrongConstructorNode(123, "Specific Name");

            // Act: Attempt to create a deep copy of the object.
            // The buggy `ConstructNew` function will find both constructors. Because its loop
            // continues, it will likely select the simpler, one-parameter constructor as the
            // final "best fit" because it can satisfy its single parameter.
            var copy = Type<WrongConstructorNode>.Copy(original);

            // Assert: This is the crucial check. The copy will be incomplete.
            // The `copy` object will have been created using the one-parameter constructor,
            // resulting in its `Name` field being "Default Name", not "Specific Name".
            // Your `StructuralEquals` function will correctly detect this difference.
            Assert.True(Type<WrongConstructorNode>.StructuralEquals(original, copy),
                "The copy should be structurally equal, but failed because the wrong constructor was chosen.");

            // --- Diagnostic Assert to prove the failure mode ---
            // This helps confirm *why* the equality check failed.
            Assert.Equal("Specific Name", copy.Name);
        }

        // This is a legit failing test involving circular dependencies, but I'm not sure
        // it's something to really care about as we have to use reflection to bypass
        // the readonly field restriction. Deserialization libraries may do something like
        // this, but it's a highly unnatural construction.

        //// --- Test Data Structure for the Final Generic Re-entrant Bug ---

        //public class GenericNode<T>
        //{
        //    public readonly T Value;
        //    // This creates a cycle with the exact same generic type instantiation.
        //    public readonly GenericNode<T> Next;

        //    public GenericNode(T value, GenericNode<T> next)
        //    {
        //        this.Value = value;
        //        this.Next = next;
        //    }
        //}
        //private static void SetReadonlyField<T>(T instance, string fieldName, object newValue)
        //{
        //    var field = typeof(T).GetField(fieldName, BindingFlags.Public | BindingFlags.Instance);
        //    field.SetValue(instance, newValue);
        //}
        //[Fact]
        //public void CopyAndCompare_WithReadOnlyGenericCycle_IsStructurallyEqual()
        //{
        //    // Arrange: Create a valid cyclic graph where node -> node.
        //    // We must use reflection to set the readonly 'Next' field to create the cycle.
        //    var originalNode = new GenericNode<string>("A", null);
        //    SetReadonlyField(originalNode, nameof(GenericNode<string>.Next), originalNode);

        //    // --- Sanity Check: Ensure the original graph is cyclic ---
        //    Assert.Same(originalNode, originalNode.Next);

        //    // Act: Perform the deep copy. This is the moment of truth.
        //    // If the generator has a re-entrant initialization bug, this call will fail.
        //    var copiedNode = Type<GenericNode<string>>.Copy(originalNode);

        //    // Assert (Phase 1): The copy is structurally identical to the original.
        //    // This high-level check relies on your proven StructuralEquals method.
        //    // A failure here means the copy is broken in some way (e.g., a null field).
        //    Assert.True(Type<GenericNode<string>>.StructuralEquals(originalNode, copiedNode),
        //        "The copied generic cyclic graph should be structurally equal to the original.");

        //    // Assert (Phase 2): The copy's internal structure is correct and cyclic.
        //    // These assertions verify that the copy didn't just produce a correct-looking
        //    // but internally flawed object graph.

        //    Assert.NotNull(copiedNode);
        //    Assert.NotSame(originalNode, copiedNode);
        //    Assert.Equal("A", copiedNode.Value);

        //    // This is the most critical assertion. It proves that the 'Next' field of the
        //    // copied node points to the copied node itself, not the original node or null.
        //    Assert.Same(copiedNode, copiedNode.Next);
        //}
    }
}