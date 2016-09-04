# Dynamics.NET

Extensions for efficient runtime reflection and structural induction.
The following features are provided out of the box:

 * generic deep copying: Type<T>.Copy(T value)
 * type mutability heuristics: Type<T>.Mutability and Type<T>.IsMutable(value)
 * precise type recursion checks: Type<T>.Cycles == Cycles.Yes
 * identifying fields and properties that are compiler-generated
 * simple checks for attributes on members, ie. type.Has<SerializableAttribute>()
 * extracing the compiler-generated fields for auto properties
 * analyzing nested generic types
 * simplified .NET types with kinding via Dynamics.Kind
 * identify and invoke type constructors via Type<T>.Constructor<TDelegate>()
   or cached via Constructor<TDelegate>.Invoke(ctor-args), ie. call
   "new List<T>(count)" as Constructor<Func<int, List<T>>>.Invoke(count)

These are functions that are useful for serialization, runtime type
and code generation, and similar applications where type structure
analysis is useful.

The functions are provided in as efficient a form as is possible,
typically as statically cached delegates.
