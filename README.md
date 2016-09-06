# Dynamics.NET

Extensions for efficient runtime reflection and structural induction.
The following features are provided out of the box:

 * generic deep copying: Type<T>.Copy(T value)
 * type mutability heuristics: Type<T>.Mutability and Type<T>.IsMutable(value)
 * precise type recursion checks: Type<T>.Cycles == Cycles.Yes
 * identifying fields and properties that are compiler-generated
 * simple checks for attributes on members, ie. type.Has<SerializableAttribute>()
 * extracting the compiler-generated fields for auto properties
 * analyzing nested generic types
 * simplified .NET types with kinding via Dynamics.Kind
 * identify and invoke constructors via Constructor<TDelegate>.Invoke() ie.
   call "new List<T>(count)" as Constructor<Func<int, List<T>>>.Invoke(count)
 * and more!

These are functions that are useful for serialization, runtime type
and code generation, and similar applications where type structure
analysis is useful.

The functions are provided in as efficient a form as is possible,
typically as statically cached delegates.

## Mutability analysis

This library can perform a conservative, transitive mutability analysis on
your type. This comes in two forms, an efficient but more conservative one
based only on the type structure, and a more precise one that also checks
instance data at runtime to see if an object is currently mutable.

The efficient form is simply:

    public enum Mutability { Mutable, Maybe, Immutable }

    Mutability isMutable = Type<T>.Mutability;

If a type is transitively immutable, no mutation will ever be observable
via any runtime instance. If a type is mutable, then mutation will be
observable given any instance.

If a type is Mutability.Maybe, then whether mutation is observable depends
on runtime data. You can determine actual mutability for certain via:

    bool isMutable = Type<T>.IsMutable(instance);

This efficiently checks the runtime data of the instance to see if any
of it permits mutation.

## Deep copying

Deep copying is as simple as:

    var copy = Type<T>.Copy(value);

No copies are created for immutable types. Your type can participate in
deep copying by implementing ICopiable<T>.

Alternately, you can also manually override the copy function via
Type<T>.OverrideCopy method if you're not able to modify an existing
type.

## Cycle checks

Similar to mutability, this checks whether an object graph is cyclic
or acyclic. This is sometimes useful for more efficient object graph
traversal, to avoid the need to mark nodes that have been visited.

There is no runtime-equivalent of this method as there is with
mutability.

## Generic typed constructors

The Constructor<TFunc> static class accepts a delegate type TFunc
and exposes a delegate of that same type that can be used to create
an instance of that type.

For instance, arrays have pseudo-constructors that accept a length
and return an array of that length. So you can obtain a delegate
to create arrays like so:

    var createArray = Constructor<Func<int, T[]>>.Invoke;
	var newArray = createArray(100); // 100 item array

Or here's how the constructor to build strings from char[] is
obtained:

    var createString = Constructor<Func<char[], string>>.Invoke;
	var hello = createString(new[] { 'h', 'e', 'l', 'l', 'o'});

Basically, any delegate signature will work as long as the type
implements a constructor with that signature.

As a special case to use in serialization-type scenarios, there
is also have a Type<T>.Create delegate that creates an empty type
using the most efficient method available. If the type has an
empty constructor, it uses that, otherwise it falls back on
.NET's FormatterServices.

## Kind system

.NET has a bit of a weird type system with a mix of first-class
and second-class types that fit awkwardly together. What's worse,
these features aren't organized uniformly, so you have to know all
of the various corner cases and which properties and methods on
System.Type that you need to consult.

Fortunately, there's a simpler organization from type system
literature. Types classify values, in that a value always belongs
to some "equivalence class" of other values which we call a "type".
Analogously, "kinds" classify types. Here are roughly .NET's kinds:

    public enum Kind
	{
		Parameter,	// type parameter
		Type,		// simple non-generic type
		Application,// type application, ie. List<int>
		Definition, // generic type definition, ie. List<T>
		Pointer,	// umanaged pointer type
		Reference,	// managed reference, ie. by-ref parameters
	}

Arrays are technically also their own kind in .NET, but I handle
them as simply another generic type, like List&lt;T&gt;.

So if you're doing any kind of computation on dynamic types,
like program analysis, code generation, etc., then you can use
the set of System.Type.Kind() extension method overloads to
extract the kinds, and the Dynamics.Kind.Apply(System.Type)
overloads to construct types of the needed kinds:

    var simpleType = typeof(int).Kind();	// Type
	var typeapp = typeof(List<int>).Kind();	// Application
	var typedef = typeof(List<>).Kind();	// Definition
	var byref = typeof(int).MakeByRefType();// Reference

	Type definition;
	Type[] context;
	switch(typeof(Dictionary<int, string>).Kind(out definition, out context))
	{
		case Kind.Application:
			// definition == typeof(Dictionary<,>)
			// context    == new[] { typeof(int), typeof(string) }
			// roundtrip  == typeof(Dictionary<int, string>)
			var roundtrip = Kind.Definition.Apply(definition, context);
			break;
		default:
			throw new Excepetion("Impossible!");
	}
	// construct an array type: intArray == typeof(int[])
	var intArray = Kind.Definition.Apply(typeof(Array), typeof(int));

Type construction and deconstruction is now much more uniform
and sensible. A future enhancement will add simple unification
as an example of how much simpler this organization is, and will
make working with type parameters much simpler.

## Miscellaneous reflection extensions

Some utility extension methods are also available:

    // true if type x inherits from T
    Type x = ...;
	bool isSubtype = x.Subtypes(typeof(T)) || x.Subtypes<T>();

	// true if 'field' is an auto-generated backing field for a property
	FieldInfo field = ...;
	bool isBackingField = field.IsBackingField();
	PropertyInfo prop = field.GetProperty();
	FieldInfo roundtrip = prop.GetBackingField();
	// roundtrip == field

	// return a human-readable field name (auto-generated backing fields
	// have unreadable names)
	string readableName = field.FieldName();

# Status

I'll say alpha quality for now. I've used some of this code in
other projects, so it's not all brand new and untested, but some
of it is.

The test suite isn't complete, but it's sufficient for most purposes
so please experiment and let me know if there are any other types
of functions that might be useful for a general purpose reflection
library of this sort.