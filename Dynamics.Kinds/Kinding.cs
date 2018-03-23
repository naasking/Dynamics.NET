using System;
using System.Collections.Generic;
using System.Text;

namespace Dynamics.Kinds
{
    /// <summary>
    /// Extensions providing .NET with a kind system.
    /// </summary>
    public static class Kinding
    {
        /// <summary>
        /// The <see cref="Dynamics.Kind"/> classifying the <see cref="System.Type"/>.
        /// </summary>
        /// <param name="type">The <see cref="System.Type"/> to classify.</param>
        /// <returns>The <see cref="Dynamics.Kind"/> classifying <paramref name="type"/>.</returns>
        /// <remarks>
        /// Note that arrays are treated like generic types with generic arguments. So an
        /// instantiated array type, like int[], will return <see cref="Dynamics.Kind.Application"/>
        /// with the argument list consisting of the array element type.
        /// <code>
        /// var simpleType = typeof(int).Kind();    // Type
        /// var typeapp = typeof(List&lt;int&gt;).Kind(); // Application
        /// var typedef = typeof(List&lt;&gt;).Kind();    // Definition
        /// var byref = typeof(int).MakeByRefType();// Reference
        /// 
        /// Type definition;
        /// Type[] context;
        /// switch(typeof(Dictionary&lt;int, string&gt;).Kind(out definition, out context))
        /// {
        ///     case Kind.Application:
        ///         // definition == typeof(Dictionary&lt;,&gt;)
        ///         // context    == new[] { typeof(int), typeof(string) }
        ///         // roundtrip  == typeof(Dictionary&lt;int, string&gt;)
        ///         var roundtrip = Kind.Definition.Apply(definition, context);
        ///         break;
        ///     default:
        ///         throw new Excepetion("Impossible!");
        /// }
        /// // construct an array type: intArray == typeof(int[])
        /// var intArray = Kind.Definition.Apply(typeof(Array), typeof(int));
        /// </code>
        /// </remarks>
        public static Kind Kind(this Type type)
        {
            return type.IsArray                 ? Dynamics.Kind.Definition:
                   type.IsPointer               ? Dynamics.Kind.Pointer:
                   type.IsByRef                 ? Dynamics.Kind.Reference:
                   type.IsGenericParameter      ? Dynamics.Kind.Parameter:
                   type.IsGenericTypeDefinition ? Dynamics.Kind.Definition:
                   type.IsGenericType           ? Dynamics.Kind.Application: //this should match ContainsGenericParameters
                                                  Dynamics.Kind.Type;
        }

        /// <summary>
        /// The <see cref="Dynamics.Kind"/> classifying the <see cref="System.Type"/>.
        /// </summary>
        /// <param name="type">The <see cref="System.Type"/> to classify.</param>
        /// <param name="context">The typing context.</param>
        /// <returns>The <see cref="Dynamics.Kind"/> classifying <paramref name="type"/>.</returns>
        /// <remarks>
        /// The meaning of the <paramref name="context"/> parameter changes based on the <see cref="Dynamics.Kind"/>
        /// of the type:
        /// <list type="table">
        /// <listheader><term>Kind</term><term>Value of <paramref name="context"/></term></listheader>
        /// <item><term><see cref="Dynamics.Kind.Parameter"/></term><term>Generic parameter constraints.</term></item>
        /// <item><term><see cref="Dynamics.Kind.Type"/></term><term><see cref="Type.EmptyTypes"/>.</term></item>
        /// <item><term><see cref="Dynamics.Kind.Definition"/></term><term>Generic arguments.</term></item>
        /// <item><term><see cref="Dynamics.Kind.Application"/></term><term>Generic arguments.</term></item>
        /// <item><term><see cref="Dynamics.Kind.Pointer"/></term><term>Base/element type.</term></item>
        /// <item><term><see cref="Dynamics.Kind.Reference"/></term><term>Base/element type.</term></item>
        /// </list>
        /// Note that arrays are treated like generic types with generic arguments. So an
        /// instantiated array type, like int[], will return <see cref="Dynamics.Kind.Application"/>
        /// with the argument list consisting of the array element type.
        /// <code>
        /// var simpleType = typeof(int).Kind();    // Type
        /// var typeapp = typeof(List&lt;int&gt;).Kind(); // Application
        /// var typedef = typeof(List&lt;&gt;).Kind();    // Definition
        /// var byref = typeof(int).MakeByRefType();// Reference
        /// 
        /// Type definition;
        /// Type[] context;
        /// switch(typeof(Dictionary&lt;int, string&gt;).Kind(out definition, out context))
        /// {
        ///     case Kind.Application:
        ///         // definition == typeof(Dictionary&lt;,&gt;)
        ///         // context    == new[] { typeof(int), typeof(string) }
        ///         // roundtrip  == typeof(Dictionary&lt;int, string&gt;)
        ///         var roundtrip = Kind.Definition.Apply(definition, context);
        ///         break;
        ///     default:
        ///         throw new Excepetion("Impossible!");
        /// }
        /// // construct an array type: intArray == typeof(int[])
        /// var intArray = Kind.Definition.Apply(typeof(Array), typeof(int));
        /// </code>
        /// </remarks>
        public static Kind Kind(this Type type, out Type[] context)
        {
            if (type.IsArray)
            {
                context = new[] { type.GetElementType() };
                return Dynamics.Kind.Definition;
            }
            else if (type.IsPointer || type.IsByRef)
            {
                context = Type.EmptyTypes;
                return type.IsPointer ? Dynamics.Kind.Pointer : Dynamics.Kind.Reference;
            }
            else if (type.IsGenericParameter)
            {
                context = type.GetGenericParameterConstraints();
                return Dynamics.Kind.Parameter;
            }
            else if (type.IsGenericTypeDefinition)
            {
                context = type.GetGenericArguments();
                return Dynamics.Kind.Definition;
            }
            else if (type.IsGenericType)
            {
                context = type.GetGenericArguments();
                return Dynamics.Kind.Application;
            }
            else
            {
                context = Type.EmptyTypes;
                return Dynamics.Kind.Type;
            }
        }

        /// <summary>
        /// The <see cref="Dynamics.Kind"/> classifying the <see cref="System.Type"/>.
        /// </summary>
        /// <param name="type">The <see cref="System.Type"/> to classify.</param>
        /// <param name="definition">The underlying type definition.</param>
        /// <param name="context">The typing context.</param>
        /// <returns>The <see cref="Dynamics.Kind"/> classifying <paramref name="type"/>.</returns>
        /// <remarks>
        /// The meaning of the <paramref name="context"/> parameter changes based on the <see cref="Dynamics.Kind"/>
        /// of the type:
        /// <list type="table">
        /// <listheader><term>Kind</term><term>Value of <paramref name="context"/></term></listheader>
        /// <item><term><see cref="Dynamics.Kind.Parameter"/></term><term>Generic parameter constraints.</term></item>
        /// <item><term><see cref="Dynamics.Kind.Type"/></term><term><see cref="Type.EmptyTypes"/>.</term></item>
        /// <item><term><see cref="Dynamics.Kind.Definition"/></term><term>Generic arguments.</term></item>
        /// <item><term><see cref="Dynamics.Kind.Application"/></term><term>Generic arguments.</term></item>
        /// <item><term><see cref="Dynamics.Kind.Pointer"/></term><term>Base/element type.</term></item>
        /// <item><term><see cref="Dynamics.Kind.Reference"/></term><term>Base/element type.</term></item>
        /// </list>
        /// Note that arrays are treated like generic types with generic arguments. So an
        /// instantiated array type, like int[], will return <see cref="Dynamics.Kind.Application"/>
        /// with the argument list consisting of the array element type.
        /// <code>
        /// var simpleType = typeof(int).Kind();    // Type
        /// var typeapp = typeof(List&lt;int&gt;).Kind(); // Application
        /// var typedef = typeof(List&lt;&gt;).Kind();    // Definition
        /// var byref = typeof(int).MakeByRefType();// Reference
        /// 
        /// Type definition;
        /// Type[] context;
        /// switch(typeof(Dictionary&lt;int, string&gt;).Kind(out definition, out context))
        /// {
        ///     case Kind.Application:
        ///         // definition == typeof(Dictionary&lt;,&gt;)
        ///         // context    == new[] { typeof(int), typeof(string) }
        ///         // roundtrip  == typeof(Dictionary&lt;int, string&gt;)
        ///         var roundtrip = Kind.Definition.Apply(definition, context);
        ///         break;
        ///     default:
        ///         throw new Excepetion("Impossible!");
        /// }
        /// // construct an array type: intArray == typeof(int[])
        /// var intArray = Kind.Definition.Apply(typeof(Array), typeof(int));
        /// </code>
        /// </remarks>
        public static Kind Kind(this Type type, out Type definition, out Type[] context)
        {
            if (type.IsArray)
            {
                context = new[] { type.GetElementType() };
                definition = typeof(Array);
                return Dynamics.Kind.Definition;
            }
            else if (type.IsPointer || type.IsByRef)
            {
                context = Type.EmptyTypes;
                definition = type.GetElementType();
                return type.IsPointer ? Dynamics.Kind.Pointer : Dynamics.Kind.Reference;
            }
            else if (type.IsGenericParameter)
            {
                context = type.GetGenericParameterConstraints();
                definition = type;
                return Dynamics.Kind.Parameter;
            }
            else if (type.IsGenericTypeDefinition)
            {
                context = type.GetGenericArguments();
                definition = type;
                return Dynamics.Kind.Definition;
            }
            else if (type.IsGenericType)
            {
                context = type.GetGenericArguments();
                definition = type.GetGenericTypeDefinition();
                return Dynamics.Kind.Application;
            }
            else
            {
                context = Type.EmptyTypes;
                definition = type;
                return Dynamics.Kind.Type;
            }
        }

        /// <summary>
        /// Construct a type given the kind and the relevant types.
        /// </summary>
        /// <param name="kind">The kind being constructed.</param>
        /// <param name="definition">The type definition.</param>
        /// <param name="args">The applicable type arguments.</param>
        /// <returns>A constructed type.</returns>
        /// <remarks>
        /// Note that arrays are treated like generic types with generic arguments. So an
        /// instantiated array type, like int[], will return <see cref="Dynamics.Kind.Application"/>
        /// with the argument list consisting of the array element type.
        /// <code>
        /// var simpleType = typeof(int).Kind();    // Type
        /// var typeapp = typeof(List&lt;int&gt;).Kind(); // Application
        /// var typedef = typeof(List&lt;&gt;).Kind();    // Definition
        /// var byref = typeof(int).MakeByRefType();// Reference
        /// 
        /// Type definition;
        /// Type[] context;
        /// switch(typeof(Dictionary&lt;int, string&gt;).Kind(out definition, out context))
        /// {
        ///     case Kind.Application:
        ///         // definition == typeof(Dictionary&lt;,&gt;)
        ///         // context    == new[] { typeof(int), typeof(string) }
        ///         // roundtrip  == typeof(Dictionary&lt;int, string&gt;)
        ///         var roundtrip = Kind.Definition.Apply(definition, context);
        ///         break;
        ///     default:
        ///         throw new Excepetion("Impossible!");
        /// }
        /// // construct an array type: intArray == typeof(int[])
        /// var intArray = Kind.Definition.Apply(typeof(Array), typeof(int));
        /// </code>
        /// </remarks>
        public static Type Apply(this Kind kind, Type definition, params Type[] args)
        {
            switch (kind)
            {
                case Dynamics.Kind.Definition:
                    if (definition != typeof(Array))
                        return definition.MakeGenericType(args);
                    else if (args.Length > 1)
                        throw new ArgumentException(definition + " only requires a single type parameter, but given " + args.Length, "args");
                    else
                        return args[0].MakeArrayType();
                case Dynamics.Kind.Pointer:
                    if (args.Length != 0) throw new ArgumentException("Too many arguments.", "args");
                    return definition.MakePointerType();
                case Dynamics.Kind.Reference:
                    if (args.Length != 0) throw new ArgumentException("Too many arguments.", "args");
                    return definition.MakeByRefType();
                case Dynamics.Kind.Type:
                    return definition;
                default:
                    throw new ArgumentException("Cannot apply a type of kind " + kind);
            }
        }
        
    }
}
