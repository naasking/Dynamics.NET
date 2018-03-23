using System;
using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Runtime.Serialization;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;

namespace Dynamics
{
    /// <summary>
    /// A class exposing metadata about type <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The type to analyze.</typeparam>
    [Pure]
    public static class Type<T>
    {
        static Func<T> create;

        //FIXME: add structural equality?
        //FIXME: add structural comparison?

        /// <summary>
        /// The cached delegate for <see cref="EqualityComparer{T}.Default"/>.Equals.
        /// </summary>
        public static readonly Func<T, T, bool> DefaultEquals = typeof(T).Subtypes(typeof(IEquatable<T>)) && !typeof(T).IsValueType
            ? typeof(T).GetMethod("Equals", new[] { typeof(T) }).Create<Func<T, T, bool>>()
            : EqualityComparer<T>.Default.Equals;

        /// <summary>
        /// The cached delegate for <see cref="EqualityComparer{T}.Default"/>.GetHashCode.
        /// </summary>
        public static readonly Func<T, int> DefaultHash = EqualityComparer<T>.Default.GetHashCode;

        static Type()
        {
            var type = typeof(T);
            // initialize the default constructor: if T:new(), then invoke the parameterless constructor
            // else if it's an array or struct, then simply return default(T)
            create = type.Subtypes(typeof(MemberInfo))   ? null:
                     type.IsInterface || type.IsAbstract ? Constructors.Default<T>():
                     type.IsArray || type.IsValueType    ? DefaultCtor:
                     type == typeof(string)              ? (Func<T>)(object)new Func<string>(() => ""):
                     HasEmptyConstructor(type)           ? Constructor<Func<T>>.Invoke:
                                                           () => (T)FormatterServices.GetUninitializedObject(type);
        }

        /// <summary>
        /// Empty constructor for <typeparamref name="T"/>.
        /// </summary>
        /// <remarks>
        /// This may throw a SecurityException if <typeparamref name="T"/> has no empty constructor and isn't a struct.
        /// </remarks>
        public static Func<T> Create
        {
            get { return create; }
        }

        /// <summary>
        /// Override the default empty constructor.
        /// </summary>
        /// <param name="create">The delegate that overrides empty constructor.</param>
        public static void OverrideCreate(Func<T> create)
        {
            Type<T>.create = create;
        }

        #region Constructor helpers
        static bool HasEmptyConstructor(Type type)
        {
            return null != type.GetConstructor(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, Type.EmptyTypes, null);
        }

        static T DefaultCtor()
        {
            return default(T);
        }
        #endregion
    }
}
