# Dynamics.NET

Extensions for efficient runtime reflection and structural induction.
The following features are provided out of the box:

 * generic deep copying: Type&lt;T&gt;.Copy(T value)
 * type mutability heuristics: Type&lt;T&gt;.Mutability and Type&lt;T&gt;.IsMutable(T value)
 * precise type recursion checks: Type&lt;T&gt;.Cycles == Cycles.Yes
 * identifying fields and properties that are compiler-generated
 * generic structural equality checks
 * simple checks for attributes on members, ie. type.Has&lt;SerializableAttribute&gt;()
 * extracting the compiler-generated fields for auto properties
 * analyzing nested generic types
 * simplified .NET types with kinding via Dynamics.Kind
 * identify and invoke constructors via Constructor<TDelegate>.Invoke() ie.
   call "new List&lt;T&gt;(count)" as Constructor&lt;Func&lt;int, List&lt;T&gt;&gt;&gt;.Invoke(count)
 * and more!

These are functions that are useful for serialization, runtime type
and code generation, and similar applications where type structure
analysis is useful.

The functions are provided in as efficient a form as is possible,
typically as statically cached delegates.

## Generic Visitors

Never write double-dispatching logic ever again, and write visitors that
can match on types which you can't modify, like System.Int32!

Here's a sample from the test suite:

    interface IVisitor
    {
        void Int(int x);
        void String(string x);
        void Else(object y);
    }
    ...
    var v = new IVisitorImplementation();
    Visitor<IVisitor>.Invoke(v, 399);
    Visitor<IVisitor>.Invoke(v, "hello world!");
    Visitor<IVisitor>.Invoke(v, default(DateTimeKind));

When the most specific method in the visitor is exactly the type being
passed in, dispatch costs only a single virtual call, so it's even faster
than the usual double-dispatching visitor pattern.

For catch-all cases, like Else(object y), some code is generated that
invokes the most specific method for the runtime type, amounting to
a small set of tests and casts.

The only limitations right now are visitor methods with generic
parameters, which will be integrated into a future update.

## Resolve Most-Specific Method as a Delegate

The Dynamics.Method class lets you easily reify a static or instance
method as a delegate. For instance, the generic visitors are simply
defined as:

    public static class Visitor<TVisitor, T>
        where TVisitor : class
    {
        public static readonly Action<TVisitor, T> Invoke =
			Method.Resolve<Action<TVisitor, T>>();
    }

Or here's a class that caches a delegate for TryParse overloads:

    static class Parse<T>
    {
        public static readonly TryParse<T> TryParse =
			Method.Resolve<TryParse<T>>();
    }
    ...
    int i;
    if (!Parse<int>.TryParse("1234", out i))
        ...

I've found these patterns particularly useful when writing heavily
generic code, where you know the type T you're working with has, say,
a TryParse method but it's absurdly difficult to make use of it.

One example that's come up frequently is re generating a large
string using StringBuilder, which has efficient overloads defined
for a large number of types. You can invoke the most efficient
Append overload as follows:

    static class Append<T>
    {
        public static readonly Func<StringBuilder, T, StringBuilder> Invoke =
            Method.Resolve<Func<StringBuilder, T, StringBuilder>>("Append");
    }
    ...
    StringBuilder buf = ...;
    T somefoo;
    Append<T>.Invoke(buf, somefoo);

Method resolution is currently limited to two parameters, same as
the visitor constraint, and TryParse is handled specially. These
restrictions will eventually go away.

## Mutability Analysis

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

## Deep Copying

Deep copying is as simple as:

    var copy = Type<T>.Copy(value);

No copies are created for immutable types. Your type can participate in
deep copying by implementing ICopiable&lt;T&gt;.

Alternately, you can also manually override the copy function via
Type&lt;T&gt;.OverrideCopy method if you're not able to modify an existing
type.

## Deep Structural Equality

Check for structural equality of any type as simply as:

    var isEq = Type<T>.Equals(obj1, obj2);

This should work for any reference or value type.

## Cycle Checks

Similar to mutability, this checks whether an object graph is cyclic
or acyclic. This is sometimes useful for more efficient object graph
traversal, to avoid the need to mark nodes that have been visited.

There is no runtime-equivalent of this method as there is with
mutability.

## Generic Typed Constructors

The Constructor&lt;TFunc&gt; static class exposes an efficient way to
construct instances of given types. The type TFunc is a delegate
whose signature matches the constructor you wish to invoke, and it
creates instances of the delegate's return type.

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
is also a Type&lt;T&gt;.Create delegate that creates an empty
instance using the most efficient method available. If the type
has an empty constructor, it uses that, otherwise it falls back
on .NET's FormatterServices.

## Kind System

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

Arrays are technically also their own kind in .NET, but they're
handled as any other generic type, like List&lt;T&gt;.

So if you're doing any kind of computation on System.Type,
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
                throw new Exception("Impossible!");
     }
    // construct an array type: intArray == typeof(int[])
    var intArray = Kind.Definition.Apply(typeof(Array), typeof(int));

Type construction and deconstruction is now much more uniform
and sensible. A future enhancement will add simple unification
as an example of how much simpler this organization is, and will
make working with type parameters much simpler.

## Miscellaneous Reflection Extensions

Some extension methods for reflection are also available:

    // true if type x inherits from T
    Type x = ...;
    bool isSubtype = x.Subtypes(typeof(T)) || x.Subtypes<T>();

    // true if 'field' is an auto-generated backing field for a property
    FieldInfo field = ...;
    bool isBackingField = field.IsBackingField();
    PropertyInfo prop = field.GetProperty();
    FieldInfo roundtrip = prop.GetBackingField();
    // roundtrip == field

    // return a human-readable field name (auto-generated backing fields are unreadable)
    string readableName = field.FieldName();

# Status

I'll say alpha quality for now. I've used some of this code in
other projects, so it's not all brand new and untested, but some
of it is.

The test suite isn't complete, but it's sufficient for most purposes
so please experiment.

Suggestions for more useful features for general purpose reflection
are welcome!
