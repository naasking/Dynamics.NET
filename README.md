# Dynamics.NET

Extensions for runtime reflection and structural induction. The following
features are provided out of the box:

 * generic deep copying
 * type mutability heuristics
 * precise type circularity checks
 * identifying fields that are compiler-generated
 * finding the compiler-generated fields for auto properties
 * analyzing nested generic types

These are functions that are useful for serialization, runtime type
and code generation, and similar applications where type structure
analysis is useful.

The functions are provided in an efficient form as statically cached
delegates.
