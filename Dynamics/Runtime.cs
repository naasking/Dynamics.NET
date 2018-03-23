using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;

namespace Dynamics
{
    /// <summary>
    /// Runtime type operations.
    /// </summary>
    public static class Runtime
    {
        /// <summary>
        /// Checks subtyping relationships.
        /// </summary>
        /// <param name="subtype">The subtype.</param>
        /// <param name="supertype">The potential supertype.</param>
        /// <param name="unifyVariables">
        /// Controls whether a deeper subtyping check occurs. Defaults to false, which is the ordinary
        /// <see cref="Type.IsAssignableFrom"/> method, but if true and the <paramref name="supertype"/>
        /// is a generic parameter, then <paramref name="subtype"/> is recursively compared for subtyping
        /// matches to all of <paramref name="supertype"/>'s generic parameter constraints. This means
        /// that the type parameter can be successfully bound via MakeGenericMethod or MakeGenericType.
        /// </param>
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
        public static bool Subtypes(this Type subtype, Type supertype, bool unifyVariables = false)
        {
            if (supertype == null) throw new ArgumentNullException("supertype");
            if (subtype == null) throw new ArgumentNullException("subtype");
            //FIXME: this still may not be general enough a subtyping relation, ie. supertype may contain generic parameters that
            //need to unify with types inside 'subtype' -- need full unification to ascertain proper subtyping?
            if (supertype.GetTypeInfo().IsAssignableFrom(subtype.GetTypeInfo()))
                return true;
            if (!unifyVariables || !supertype.IsGenericParameter)
                return false;
            foreach (var x in supertype.GetTypeInfo().GetGenericParameterConstraints())
                if (!subtype.Subtypes(x))
                    return false;
            return true;
        }

        /// <summary>
        /// Checks subtyping relationships.
        /// </summary>
        /// <typeparam name="T">The supertype to check.</typeparam>
        /// <param name="subtype">The subtype.</param>
        /// <returns>True if <paramref name="subtype"/> is a subtype of <typeparamref name="T"/>.</returns>
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
        public static bool Subtypes<T>(this Type subtype)
        {
            return subtype.Subtypes(typeof(T));
        }

        /// <summary>
        /// Shorthand for creating open instance delegates from method handles.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="method"></param>
        /// <returns>A open instance delegate of type <typeparamref name="T"/> for the designated method.</returns>
        public static T Create<T>(this MethodInfo method)
            where T : class
        {
            var type = typeof(T);
            if (!type.Subtypes(typeof(Delegate)))
                throw new ArgumentException("Type " + type.Name + " is not a delegate type.");
            return (T)(object)method.CreateDelegate(type, null);
        }
    }
}
