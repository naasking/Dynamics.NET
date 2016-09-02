using System;
using System.Reflection;
using System.Reflection.Emit;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Linq;
using System.Text;
using System.Diagnostics.Contracts;

namespace Dynamics
{
    /// <summary>
    /// Utilities for runtime reflection.
    /// </summary>
    public static class Runtime
    {
        /// <summary>
        /// Checks subtyping relationships.
        /// </summary>
        /// <param name="subtype">The subtype.</param>
        /// <param name="supertype">The potential supertype.</param>
        /// <returns>True if <paramref name="subtype"/> is a subtype of <paramref name="supertype"/>.</returns>
        /// <exception cref="ArgumentNullException">Thrown if argument is null.</exception>
        /// <remarks>
        /// This is an extension method on <see cref="System.Type"/> that checks subtyping relationships
        /// on runtime types and type arguments:
        /// <code>
        /// Console.WriteLine(typeof(int).Subtypes(typeof(object)));
        /// Console.WriteLine(typeof(int).Subtypes&lt;object&gt;());
        /// Console.WriteLine(typeof(int).Subtypes&lt;string&gt;());
        /// </code>
        /// However, this check has an important limitation when dealing with type parameters. See
        /// <see cref="System.Type.IsAssignableFrom"/>.
        /// </remarks>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId = "supertype")]
        public static bool Subtypes(this Type subtype, Type supertype)
        {
            if (supertype == null) throw new ArgumentNullException("supertype");
            if (subtype == null) throw new ArgumentNullException("subtype");
            //FUTURE: this only returns true for generic parameter if subtype is exactly a type constraint
            //that appears on supertype. A real subtyping relation would return true if it subtypes all
            //of the constraints.
            return supertype.IsAssignableFrom(subtype);
        }

        /// <summary>
        /// Obtains the backing field for <paramref name="property"/>, if any.
        /// </summary>
        /// <param name="property">The property whose backing field being obtained.</param>
        /// <returns>The backing field if <paramref name="property"/> is an auto-property, else null.</returns>
        /// <remarks>
        /// This extension method on <see cref="PropertyInfo"/> attempts to extract the
        /// compiler-generated field metadata:
        /// <code>
        /// public class Foo
        /// {
        ///     public int AutoProperty { get; set; }
        /// }
        /// var backingField = typeof(Foo).GetProperty("AutoProperty")
        ///                               .GetBackingField();
        /// Console.WriteLine(backingField.Name.FieldName());
        /// Console.WriteLine(backingField.Name);
        /// // output:
        /// // AutoProperty
        /// // &lt;AutoProperty&gt;k__BackingField
        /// </code>
        /// Note that this method currently depends on the naming convention used by the
        /// compiler, so it may not be 100% future-proof. If the convention ever does
        /// change, I anticipate updating this implementation to reflect that.
        /// </remarks>
        public static FieldInfo GetBackingField(this PropertyInfo property)
        {
            if (property == null) throw new ArgumentNullException("property");
            return property.DeclaringType
                           .GetField('<' + property.Name + ">k__BackingField",
                                     BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
        }

        /// <summary>
        /// Checks whether property is an auto-property with compiler-generated backing fields.
        /// </summary>
        /// <param name="property">The property to check.</param>
        /// <returns>True if auto-property, false otherwise.</returns>
        /// <remarks>
        /// This is an extension method on <see cref="PropertyInfo"/> that checks whether a
        /// property is an auto-property with a compiler-generated backing field:
        /// <code>
        /// class Foo
        /// {
        ///     public int Bar
        ///     {
        ///        get { return 0; }
        ///     }
        ///     public int AutoProp { get; set; }
        /// }
        /// var autop = typeof(Foo).GetProperty("Bar");
        /// var normp = typeof(Foo).GetProperty("AutoProp");
        /// 
        /// Console.WriteLine(autop.HasAutoField());
        /// Console.WriteLine(normp.HasAutoField());
        /// // output:
        /// // true
        /// // false
        /// </code>
        /// </remarks>
        public static bool HasAutoField(this PropertyInfo property)
        {
            if (property == null) throw new ArgumentNullException("property");
            var accessor = property.GetGetMethod()
                        ?? property.GetSetMethod()
                        ?? property.DeclaringType.GetMethod("get_" + property.Name, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
                        ?? property.DeclaringType.GetMethod("set_" + property.Name, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
            return accessor.IsDefined(typeof(CompilerGeneratedAttribute), false);
        }
        
        /// <summary>
        /// Identifies auto-generated getters.
        /// </summary>
        /// <param name="method"></param>
        /// <returns></returns>
        public static bool IsAutoGetter(this MethodInfo method)
        {
            return method.Name.StartsWith("get_")
                && method.Has<CompilerGeneratedAttribute>();
        }

        /// <summary>
        /// Identifies auto-generated getters.
        /// </summary>
        /// <param name="method"></param>
        /// <returns></returns>
        public static bool IsAutoSetter(this MethodInfo method)
        {
            return method.Name.StartsWith("set_")
                && method.Has<CompilerGeneratedAttribute>();
        }

        /// <summary>
        /// Identifies pure setters.
        /// </summary>
        /// <param name="method"></param>
        /// <returns></returns>
        public static bool IsPureSetter(this MethodInfo method)
        {
            return method.IsAutoSetter()
                && (method.IsPrivate || method.GetProperty().Has<PureAttribute>());
        }

        /// <summary>
        /// Identifies pure setters.
        /// </summary>
        /// <param name="method"></param>
        /// <returns></returns>
        public static bool IsPureGetter(this MethodInfo method)
        {
            // assume getter is pure if it's auto-generated or it has [Pure] or setter is private/does not exist
            return method.IsAutoGetter()
                || method.Name.StartsWith("get_") && (method.GetProperty().Has<PureAttribute>() || method.GetProperty().GetSetMethod() == null);
        }

        /// <summary>
        /// Extract the property for a get/set method.
        /// </summary>
        /// <param name="method"></param>
        /// <returns></returns>
        public static PropertyInfo GetProperty(this MethodInfo method)
        {
            if (!method.Name.StartsWith("get_") && !method.Name.StartsWith("set_"))
                throw new ArgumentException("Not a getter or setter.", "method");
            return method.ReflectedType.GetProperty(method.Name.Substring(4));
        }

        /// <summary>
        /// Checks whether the given member has a particular attribute.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="member"></param>
        /// <returns></returns>
        public static bool Has<T>(this ICustomAttributeProvider x)
            where T : Attribute
        {
            return x.GetCustomAttributes(typeof(T), false).Length != 0;
        }

        /// <summary>
        /// Checks whether a field was auto-generated from a property declaration.
        /// </summary>
        /// <param name="field">The field to check.</param>
        /// <returns>True if field was auto-generated, false otherwise.</returns>
        /// <remarks>
        /// This is an extension method on <see cref="FieldInfo"/> that checks whether a
        /// field is a compiler-generated backing field for a property:
        /// <code>
        /// public class Foo
        /// {
        ///     public int normalField;
        ///     public int AutoProperty { get; set; }
        /// }
        /// var backingField = typeof(Foo).GetProperty("AutoProperty")
        ///                               .GetBackingField();
        /// var normalField = typeof(Foo).GetField("normalField");
        /// 
        /// Console.WriteLine(backingField.IsBackingField());
        /// Console.WriteLine(normalField.IsBackingField());
        /// // output:
        /// // true
        /// // false
        /// </code>
        /// </remarks>
        public static bool IsBackingField(this FieldInfo field)
        {
            if (field == null) throw new ArgumentNullException("field");
            return field.Name[0] == '<' && field.Name.EndsWith(">k__BackingField");
            // this is more future-proof, just slower:
            // || field.GetCustomAttributes(typeof(COMP.CompilerGeneratedattribute), false)
        }
        
        /// <summary>
        /// The <see cref="Dynamics.Kind"/> classifying the <see cref="System.Type"/>.
        /// </summary>
        /// <param name="type">The <see cref="System.Type"/> to classify.</param>
        /// <returns>The <see cref="Dynamics.Kind"/> classifying <paramref name="type"/>.</returns>
        public static Kind Kind(this Type type)
        {
            return type.IsArray                 ? Dynamics.Kind.Definition:
                   type.IsPointer               ? Dynamics.Kind.Pointer:
                   type.IsByRef                 ? Dynamics.Kind.Reference:
                   type.IsGenericParameter      ? Dynamics.Kind.Parameter:
                   type.IsGenericTypeDefinition ? Dynamics.Kind.Definition:
                   type.IsGenericType           ? Dynamics.Kind.Application:
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
        /// Note that arrays, pointers and by-ref types are treated like generic types with generic arguments. So
        /// an instantiated array type, like int[], will return <see cref="Dynamics.Kind.Application"/> with the
        /// argument list consisting of the array element type.
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
        /// Note that arrays, pointers and by-ref types are treated like generic types with generic arguments. So
        /// an instantiated array type, like int[], will return <see cref="Dynamics.Kind.Application"/> with the
        /// argument list consisting of the array element type.
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
        
        /// <summary>
        /// Generate a dynamic type.
        /// </summary>
        /// <param name="name">The type name.</param>
        /// <param name="saveAssembly">Flag indicating whether the dynamic assembly should be saved.</param>
        /// <param name="generate">The callback used to generate the type.</param>
        /// <returns>The created type.</returns>
        /// <exception cref="ArgumentNullException">Thrown if argument is null.</exception>
        /// <remarks>
        /// This is a static method used to create a dynamic type in a dynamic assembly, often for
        /// code generation purposes. It automates various steps and provides a boolean parameter
        /// indicating whether to save the assembly to a file, so you can run verification passes on it:
        /// <code>
        /// var newType = Runtime.CreateType(name: "TypeFoo",
        ///                                  saveAssembly: true,
        ///                                  generate: typeBuilder =&gt;
        /// {
        ///     // see docs on TypeBuilder
        ///     ...
        /// });
        /// </code>
        /// </remarks>
        public static Type CreateType(string name, bool saveAssembly, Action<TypeBuilder> generate)
        {
            return CreateType(name, TypeAttributes.Class | TypeAttributes.Public, saveAssembly, generate);
        }

        /// <summary>
        /// Generate a dynamic type.
        /// </summary>
        /// <param name="name">The type name.</param>
        /// <param name="attributes">The type's attributes.</param>
        /// <param name="saveAssembly">Flag indicating whether the dynamic assembly should be saved.</param>
        /// <param name="generate">The callback used to generate the type.</param>
        /// <returns>The created type.</returns>
        /// <exception cref="ArgumentNullException">Thrown if argument is null.</exception>
        /// <remarks>
        /// This is a static method used to create a dynamic type in a dynamic assembly, often for
        /// code generation purposes. It automates various steps and provides a boolean parameter
        /// indicating whether to save the assembly to a file, so you can run verification passes on it:
        /// <code>
        /// var newType = Runtime.CreateType(name: "TypeFoo",
        ///                                  saveAssembly: true,
        ///                                  generate: typeBuilder =&gt;
        /// {
        ///     // see docs on TypeBuilder
        ///     ...
        /// });
        /// </code>
        /// </remarks>
        public static Type CreateType(string name, TypeAttributes attributes, bool saveAssembly, Action<TypeBuilder> generate)
        {
            if (name == null) throw new ArgumentNullException("name");
            if (generate == null) throw new ArgumentNullException("generate");
            var asmName = new AssemblyName(name);
            var asm = AppDomain.CurrentDomain
                               .DefineDynamicAssembly(asmName, saveAssembly ? AssemblyBuilderAccess.RunAndSave : AssemblyBuilderAccess.Run);
            var mod = asm.DefineDynamicModule(name, name + ".dll");
            var typ = mod.DefineType(name, TypeAttributes.Class | TypeAttributes.Public);
            generate(typ);
            var final = typ.CreateType();
            if (saveAssembly) asm.Save(name + ".dll");
            return final;
        }
    }
}
