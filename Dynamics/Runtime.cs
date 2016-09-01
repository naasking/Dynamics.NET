using System;
using System.Reflection;
using System.Reflection.Emit;
using System.Collections.Generic;
using COMP = System.Runtime.CompilerServices;
using System.Linq;
using System.Text;

namespace Dynamics
{
    /// <summary>
    /// Utilities for runtime reflection.
    /// </summary>
    public static class Runtime
    {
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
            return accessor.IsDefined(typeof(COMP.CompilerGeneratedAttribute), false);
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
            return type.IsGenericParameter      ? Dynamics.Kind.Parameter:
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
        /// </list>
        /// </remarks>
        public static Kind Kind(this Type type, out Type[] context)
        {
            if (type.IsGenericParameter)
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
