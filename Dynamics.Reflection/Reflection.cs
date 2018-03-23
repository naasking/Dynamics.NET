using System;
using System.Linq;
using System.Collections;
using System.Collections.Specialized;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace Dynamics
{
    /// <summary>
    /// Utilities for runtime reflection.
    /// </summary>
    public static class Reflection
    {
        static Reflection()
        {
        }

        /// <summary>
        /// Normalizes a field name.
        /// </summary>
        /// <param name="field">The field whose name to normalize.</param>
        /// <returns>
        /// If <paramref name="field"/> is an auto-generated field from an auto property, the property name is returned.
        /// Otherwise the field name is returned.
        /// </returns>
        /// <remarks>
        /// This extension method on <see cref="FieldInfo"/> extracts a "normalized" field name. By
        /// "normalized", I mean that if the field was a compiler-generated backing field for an auto
        /// property, then it will extract the property name. Otherwise, it will just return the
        /// field name itself:
        /// <code>
        /// public class Foo
        /// {
        ///     public int normalField;
        ///     public int AutoProperty { get; set; }
        /// }
        /// var backingField = Types.Property&lt;Foo, int&gt;(x =&gt; x.AutoProperty)
        ///                         .GetBackingField();
        /// var normalField = Types.Field&lt;Foo, int&gt;(x =&gt; x.normalField);
        /// 
        /// Console.WriteLine(backingField.FieldName());
        /// Console.WriteLine(normalField.FieldName());
        /// // output:
        /// // AutoProperty
        /// // normalField
        /// </code>
        /// </remarks>
        public static string FieldName(this FieldInfo field)
        {
            if (field == null) throw new ArgumentNullException("field");
            return field.IsBackingField() ? field.Name.Substring(1, field.Name.Length - ">k__BackingField".Length - 1) :
                                            field.Name;
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
            var name = '<' + property.Name + ">k__BackingField";
            return property.DeclaringType.GetRuntimeFields().FirstOrDefault(x => name.Equals(x.Name, StringComparison.Ordinal));
            //return property.DeclaringType.GetRuntimeField('<' + property.Name + ">k__BackingField");
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
            var accessor = property.GetMethod
                        ?? property.SetMethod;
                        //?? property.DeclaringType.GetMethod("get_" + property.Name, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
                        //?? property.DeclaringType.GetMethod("set_" + property.Name, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
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
                && null != method.GetCustomAttribute<CompilerGeneratedAttribute>();
        }

        /// <summary>
        /// Identifies auto-generated getters.
        /// </summary>
        /// <param name="method"></param>
        /// <returns></returns>
        public static bool IsAutoSetter(this MethodInfo method)
        {
            return method.Name.StartsWith("set_")
                && null != method.GetCustomAttribute<CompilerGeneratedAttribute>();
        }

        /// <summary>
        /// Identifies pure setters.
        /// </summary>
        /// <param name="method"></param>
        /// <returns></returns>
        public static bool IsPureSetter(this MethodInfo method)
        {
            return method.IsAutoSetter()
                && (method.IsPrivate || null != method.GetProperty().GetCustomAttribute<PureAttribute>());
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
                || method.Name.StartsWith("get_") && (null != method.GetProperty().GetCustomAttribute<PureAttribute>() || method.GetProperty().SetMethod == null);
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
            return method.DeclaringType.GetRuntimeProperty(method.Name.Substring(4));
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

        ///// <summary>
        ///// Generate a dynamic type.
        ///// </summary>
        ///// <param name="name">The type name.</param>
        ///// <param name="saveAssembly">Flag indicating whether the dynamic assembly should be saved.</param>
        ///// <param name="generate">The callback used to generate the type.</param>
        ///// <returns>The created type.</returns>
        ///// <exception cref="ArgumentNullException">Thrown if argument is null.</exception>
        ///// <remarks>
        ///// This is a static method used to create a dynamic type in a dynamic assembly, often for
        ///// code generation purposes. It automates various steps and provides a boolean parameter
        ///// indicating whether to save the assembly to a file, so you can run verification passes on it:
        ///// <code>
        ///// var newType = Runtime.CreateType(name: "TypeFoo",
        /////                                  saveAssembly: true,
        /////                                  generate: typeBuilder =&gt;
        ///// {
        /////     // see docs on TypeBuilder
        /////     ...
        ///// });
        ///// </code>
        ///// </remarks>
        //public static Type CreateType(string name, bool saveAssembly, Action<TypeBuilder> generate)
        //{
        //    return CreateType(name, TypeAttributes.Class | TypeAttributes.Public, saveAssembly, generate);
        //}

        ///// <summary>
        ///// Generate a dynamic type.
        ///// </summary>
        ///// <param name="name">The type name.</param>
        ///// <param name="attributes">The type's attributes.</param>
        ///// <param name="saveAssembly">Flag indicating whether the dynamic assembly should be saved.</param>
        ///// <param name="generate">The callback used to generate the type.</param>
        ///// <returns>The created type.</returns>
        ///// <exception cref="ArgumentNullException">Thrown if argument is null.</exception>
        ///// <remarks>
        ///// This is a static method used to create a dynamic type in a dynamic assembly, often for
        ///// code generation purposes. It automates various steps and provides a boolean parameter
        ///// indicating whether to save the assembly to a file, so you can run verification passes on it:
        ///// <code>
        ///// var newType = Runtime.CreateType(name: "TypeFoo",
        /////                                  saveAssembly: true,
        /////                                  generate: typeBuilder =&gt;
        ///// {
        /////     // see docs on TypeBuilder
        /////     ...
        ///// });
        ///// </code>
        ///// </remarks>
        //public static Type CreateType(string name, TypeAttributes attributes, bool saveAssembly, Action<TypeBuilder> generate)
        //{
        //    if (name == null) throw new ArgumentNullException("name");
        //    if (generate == null) throw new ArgumentNullException("generate");
        //    var asmName = new AssemblyName(name);
        //    var asm = AppDomain.CurrentDomain
        //                       .DefineDynamicAssembly(asmName, saveAssembly ? AssemblyBuilderAccess.RunAndSave : AssemblyBuilderAccess.Run);
        //    var mod = asm.DefineDynamicModule(name, name + ".dll");
        //    var typ = mod.DefineType(name, TypeAttributes.Class | TypeAttributes.Public);
        //    generate(typ);
        //    var final = typ.CreateType();
        //    if (saveAssembly) asm.Save(name + ".dll");
        //    return final;
        //}
    }
}
