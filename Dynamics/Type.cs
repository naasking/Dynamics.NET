﻿using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Runtime.Serialization;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.CompilerServices;
using System.Diagnostics.Contracts;

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

        /// <summary>
        /// Exposes the conservative mutability of <typeparamref name="T"/>.
        /// </summary>
        public static readonly Mutability Mutability;

        /// <summary>
        /// Exposes whether the type structure allows cycles.
        /// </summary>
        public static readonly Cycles Cycles;

        /// <summary>
        /// Performs a deep copy of any value.
        /// </summary>
        static Func<T, Dictionary<object, object>, T> deepCopy;

        /// <summary>
        /// Dynamically checks value state for mutability.
        /// </summary>
        static readonly Func<T, HashSet<object>, bool> isMutable;

        //FIXME: add structural comparison?

        /// <summary>
        /// The cached delegate for <see cref="EqualityComparer{T}.Default"/>.Equals.
        /// </summary>
        public static readonly Func<T, T, bool> DefaultEquals = typeof(T).Subtypes(typeof(IEquatable<T>)) && !typeof(T).IsValueType
            ? (Func<T, T, bool>)Delegate.CreateDelegate(typeof(Func<T, T, bool>), null, typeof(T).GetMethod("Equals", new[] { typeof(T) }))
            : EqualityComparer<T>.Default.Equals;

        /// <summary>
        /// The cached delegate for <see cref="EqualityComparer{T}.Default"/>.GetHashCode.
        /// </summary>
        public static readonly Func<T, int> DefaultHash = EqualityComparer<T>.Default.GetHashCode;

        /// <summary>
        /// Used to dispatch to the dynamic type to check for mutability.
        /// </summary>
        static readonly ConcurrentDictionary<Type, Func<T, HashSet<object>, bool>> subtypeMutability = new ConcurrentDictionary<Type, Func<T, HashSet<object>, bool>>();

        /// <summary>
        /// Used to dispatch to the dynamic type to copy.
        /// </summary>
        static readonly ConcurrentDictionary<Type, Func<T, Dictionary<object, object>, T>> subtypeCopy =
            new ConcurrentDictionary<Type, Func<T, Dictionary<object, object>, T>>();

        static Type()
        {
            var type = typeof(T);
            // initialize the default constructor: if T:new(), then invoke the parameterless constructor
            // else if it's an array or struct, then simply return default(T)
            create = type.Subtypes(typeof(MemberInfo))   ? null:
                     type.IsInterface || type.IsAbstract ? Runtime.Constructor<T>():
                     type.IsArray || type.IsValueType    ? DefaultCtor:
                     type == typeof(string)              ? Expression.Lambda<Func<T>>(Expression.Constant(string.Empty)).Compile():
                     HasEmptyConstructor(type)           ? Constructor<Func<T>>.Invoke:
                                                           () => (T)FormatterServices.GetUninitializedObject(type);

            // Immutable: any types decorated with [Pure] || T has init-only fields whose types are immutable
            // Maybe: if any fields have types with Mutability.Maybe
            // Mutable: otherwise
            // Need a list of sanitized core immutable types: Int32, DateTime, decimal, Exception, Attribute, Tuple<*>, etc.
            Mutability = ImmutableWhitelist(type) ? Mutability.Immutable:
                         MutableBlacklist(type)   ? Mutability.Mutable:
                                                    TransitiveMutability(type, out isMutable);

            var visited = new Type[6];
            Cycles = DetectCycles(type, ref visited, 0);
            
            deepCopy = Mutability == Mutability.Immutable ? null : GenerateCopy(type);
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
        /// Performs a deep copy of an object, if necessary.
        /// </summary>
        /// <param name="value">The value to copy.</param>
        /// <returns>A deep copy of the value.</returns>
        public static T Copy(T value)
        {
            return Mutability == Mutability.Immutable ? value : Copy(value, new Dictionary<object, object>());
        }

        /// <summary>
        /// The copy method that preserves sharing and circular references.
        /// </summary>
        /// <param name="value"></param>
        /// <param name="refs"></param>
        /// <returns></returns>
        public static T Copy(T value, Dictionary<object, object> refs)
        {
            if (Mutability == Mutability.Immutable || value == null)
                return value;
            var type = value?.GetType();
            object x;
            if (type != typeof(T))
            {
                // type is a subtype of T, so dispatch to the appropriate handler
                Func<T, Dictionary<object, object>, T> f;
                if (!subtypeCopy.TryGetValue(type, out f))
                {
                    // T = Nullable<TElement>, even though TElement is not a subtype
                    MethodInfo dispatch;
                    if (typeof(T).IsConstructedGenericType && typeof(T).GetGenericTypeDefinition() == typeof(Nullable<>))
                        dispatch = new Func<T, Dictionary<object, object>, T>(DispatchAnyCopy<T>).Method
                                   .GetGenericMethodDefinition()
                                   .MakeGenericMethod(type);
                    else
                        dispatch = new Func<T, Dictionary<object, object>, T>(DispatchSubtypeCopy<T>).Method
                                   .GetGenericMethodDefinition()
                                   .MakeGenericMethod(type);
                    f = subtypeCopy[type] = dispatch.Create<Func<T, Dictionary<object, object>, T>>();
                }
                return f(value, refs);
            }
            else if (refs.TryGetValue(value, out x))
            {
                return (T)x;
            }
            else
            {
                return deepCopy(value, refs);
            }
        }

        static Func<T, T, HashSet<(object, object)>, bool> structuralEquals;

        /// <summary>
        /// Structural equality check.
        /// </summary>
        public static bool StructuralEquals(T x0, T x1)
        {
            // this is a lazy initialization to avoid an infinite regress between two
            // mutually recursive class definitions, eg. class A { B b; } and class B { A a; }
            if (structuralEquals == null)
                structuralEquals = StructuralEquality();
            return structuralEquals(x0, x1, new HashSet<(object, object)>());
        }

        /// <summary>
        /// Structural equality check.
        /// </summary>
        internal static bool StructuralEquals(T x0, T x1, HashSet<(object, object)> visited)
        {
            // this is a lazy initialization to avoid an infinite regress between two
            // mutually recursive class definitions, eg. class A { B b; } and class B { A a; }
            if (structuralEquals == null)
                structuralEquals = StructuralEquality();
            return structuralEquals(x0, x1, visited);
        }

        ///// <summary>
        ///// Structural equality check.
        ///// </summary>
        //public static Func<T, T, bool> StructuralEquals(T x0, T x1, HashSet<object> visited) =>
        //    structuralEquals(x0, x1, visited);

        /// <summary>
        /// Override the default auto-generated copy method with a more efficient one.
        /// </summary>
        /// <param name="copy">The delegate that overrides deep copying behaviour.</param>
        public static void OverrideCopy(Func<T, Dictionary<object, object>, T> copy)
        {
            deepCopy = copy;
        }

        /// <summary>
        /// Override the default empty constructor.
        /// </summary>
        /// <param name="create">The delegate that overrides empty constructor.</param>
        public static void OverrideCreate(Func<T> create)
        {
            Type<T>.create = create;
        }

        /// <summary>
        /// Checks a value's mutability.
        /// </summary>
        /// <param name="value">The value to check for mutability.</param>
        /// <returns>True if the current configuration is mutable, false otherwise.</returns>
        public static bool IsMutable(T value)
        {
            return Mutability == Mutability.Mutable
                || Mutability == Mutability.Maybe && IsMutable(value, new HashSet<object>());
        }
        
        #region Copy helpers
        static T DispatchSubtypeCopy<T0>(T value, Dictionary<object, object> refs)
            where T0 : T
        {
            return Type<T0>.Copy((T0)value, refs);
        }

        static T DispatchAnyCopy<T0>(T value, Dictionary<object, object> refs)
        {
            return (T)(object)Type<T0>.Copy((T0)(object)value, refs);
        }

        static Func<T, Dictionary<object, object>, T> GenerateCopy(Type type)
        {
            // if T is an interface type, generate nothing since the base copy code will already
            // dispatch to the concrete subtype for copying purposes
            if (type.IsInterface) return null;
            // if type implements ICopiable, return delegate dispatching to Copy method
            if (!type.IsValueType)
            {
                var icopy = typeof(ICopiable<>).MakeGenericType(type);
                if (type.Subtypes(icopy))
                    return icopy.GetMethod("Copy").Create<Func<T, Dictionary<object, object>, T>>();
            }
            // handle arrays and delegates specially
            if (type.IsArray)
                return new Func<int[], Dictionary<object, object>, int[]>(Copying.Array)
                       .Method
                       .GetGenericMethodDefinition()
                       .MakeGenericMethod(type.GetElementType())
                       .Create<Func<T, Dictionary<object, object>, T>>();
            if (type.Subtypes<Delegate>())
                return new Func<Action, Dictionary<object, object>, Action>(Copying.Delegate)
                       .Method
                       .GetGenericMethodDefinition()
                       .MakeGenericMethod(type)
                       .Create<Func<T, Dictionary<object, object>, T>>();
            // if type has a method in Copying static class, then dispatch to that
            var matchName = type.IsGenericType ? type.Name.Remove(type.Name.Length - 2):
                                                 type.Name;
            var match = Copying.Methods
                        .SingleOrDefault(m => m.Name.Equals(matchName, StringComparison.Ordinal)
                                           && (m.ReturnType == type || m.ReturnType.ContainsGenericParameters && type.IsGenericType));
            if (match != null)
            {
                if (match.IsGenericMethod)
                    match = match.MakeGenericMethod(type.GetGenericArguments());
                return match.Create<Func<T, Dictionary<object, object>, T>>();
            }
            // if we get here, we need to dynamically generate a deepCopy method
            var x = Expression.Parameter(type, "x");
            var refs = Expression.Parameter(typeof(Dictionary<object, object>), "refs");
            var dc = x as Expression;
            var y = Expression.Variable(type, "y");
            var members = new List<Expression>();
            var rofields = new Dictionary<string, Expression>();
            var noEmptyCtor = !type.IsValueType && !HasEmptyConstructor(type);
            members.Add(null); // reserve slot for: y = new T(read-only-fields)
            // need to add new obj to 'refs' before copying children in case of child->parent reference.
            if (!type.IsValueType)
            {
                var tgv = typeof(Dictionary<object, object>).GetMethod("Add", new[] { typeof(object), typeof(object) });
                members.Add(Expression.Call(refs, tgv, x, y));
            }
            // copy each member one at a time
            foreach (var field in type.GetFields(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance))
            {
                var access = Expression.Field(x, field);
                var copy = typeof(Type<>).MakeGenericType(field.FieldType)
                                         .GetMethod("Copy", BindingFlags.Static | BindingFlags.Public, null,
                                                    new[] { field.FieldType, typeof(Dictionary<object, object>) }, null);
                var ecopy = Expression.Call(copy, access, refs);
                if (field.IsInitOnly || noEmptyCtor)
                    rofields.Add(field.FieldName().ToLower(), ecopy);
                else
                    members.Add(Expression.Assign(Expression.Field(y, field), ecopy));
            }
            members[0] = Expression.Assign(y, ConstructNew(type, rofields));
            members.Add(y);
            dc = Expression.Block(new[] { y }, members);
            return Expression.Lambda<Func<T, Dictionary<object, object>, T>>(dc, x, refs).Compile();
        }

        static NewExpression ConstructNew(Type type, Dictionary<string, Expression> copies)
        {
            if (copies.Count == 0)
                return Expression.New(type);
            var ctors = type.GetConstructors();
            ConstructorInfo ctor = null;
            var bindings = new List<Expression>();  // constructor parameter bindings
            var used = new HashSet<string>();       // tracks used members from 'copies'
            // search for best-fit constructor
            foreach (var x in ctors)
            {
                ctor = x;
                var args = x.GetParameters();
                //// a self-referential readonly field can be initialized with 'this',
                ///so the # of params don't have to strictly equal # of bindings
                //if (args.Length != copies.Count)
                //    continue;
                bindings.Clear();
                used.Clear();
                foreach (var p in args)
                {
                    // find field name matching the parameter name, else find one matching the type
                    Expression e;
                    var name = p.Name.ToLower();
                    if (!copies.TryGetValue(name, out e))
                    {
                        e = copies.FirstOrDefault(z => z.Value.Type == p.ParameterType && !used.Contains(z.Key)).Value;
                        if (e == null)
                            throw new Exception($"Couldn't find a field matching parameter {p.Name}:{p.ParameterType} in constructor {ctor.DeclaringType.Name}{ctor.Name}. Used: {string.Join(",", used)}.");
                        used.Add(name); // ensure same member isn't used twice
                    }
                    bindings.Add(e);
                }
                //if (copies.Count != bindings.Count)
                //    throw new InvalidOperationException("Couldn't find appropriate constructor.");
            }
            var cparams = ctor.GetParameters();

            // Assume a self-referential readonly field where the number of parameters of type 'T' don't
            // match the number of readonly fields of T is initializing itself with 'this', so eliminate
            // the binding for that parameter. This is a bit of a hack since I might eliminate the wrong param.
            if (cparams.Length != bindings.Count && bindings.Count(x => x.Type == type) != cparams.Count(x => x.ParameterType == type))
                bindings.Remove(bindings.First(x => x.Type == type));
            if (ctor == null || cparams.Length != bindings.Count)
                throw new InvalidOperationException($"Couldn't find a constructor for type {type.Name} with {copies.Count} parameters.");
            return Expression.New(ctor, bindings);
        }
        #endregion

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

        #region Mutability helpers
        static bool IsMutable(T value, HashSet<object> visited)
        {
            //FIXME: can we exploit cycle detection to avoid adding to 'visited'? This
            //may create duplicate work in DAGs by possibly traversing same node twice.
            switch (Mutability)
            {
                case Mutability.Immutable:
                    return false;
                case Mutability.Mutable:
                    return true;
                case Mutability.Maybe:
                    var type = value?.GetType();
                    if (type == typeof(T))
                        return (value is ValueType || visited.Add(value)) && isMutable(value, visited);
                    Func<T, HashSet<object>, bool> f;
                    if (!subtypeMutability.TryGetValue(type, out f))
                    {
                        var dispatch = new Func<T, HashSet<object>, bool>(DispatchIsMutable<T>).Method
                                       .GetGenericMethodDefinition()
                                       .MakeGenericMethod(type);
                        f = subtypeMutability[type] = (Func<T, HashSet<object>, bool>)Delegate.CreateDelegate(typeof(Func<T, HashSet<object>, bool>), dispatch);
                    }
                    return f(value, visited);
                default:
                    throw new InvalidOperationException("Unknown Mutability value.");
            }
        }

        static bool ImmutableWhitelist(Type type)
        {
            return type.Has<PureAttribute>()
                || type.IsPrimitive
                || type == typeof(DateTime)
                || type == typeof(TimeSpan)
                || type == typeof(DateTimeOffset)
                || type == typeof(decimal)
                || type == typeof(string)
                || type == typeof(System.Linq.Expressions.Expression)
                || type.Subtypes(typeof(Enum))
                || type.Subtypes(typeof(MemberInfo))
                || type.Subtypes<TimeZoneInfo>();
        }

        static bool MutableBlacklist(Type type)
        {
            return type.Subtypes(typeof(Delegate))
                || type.IsArray;
        }

        static Mutability TransitiveMutability(Type type, out Func<T, HashSet<object>, bool> isMutable)
        {
            // Nullable<TElement> inherits the mutability of TElement
            if (type.IsConstructedGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
            {
                // if T is Nullable<TElement>, then dispatch to TElement
                var et = type.GetGenericArguments()[0];
                var f = typeof(Type<>).MakeGenericType(et).GetField("Mutability");
                isMutable = null;
                return (Mutability)f.GetValue(null);
            }

            // sealed types are immutable if they have init-only fields and all fields are immutable
            // non-sealed types may not be immutable, and so generate a residual program to check the dynamic state
            var typeMutable = typeof(Type<>).GetField("Mutability");
            var x = Expression.Parameter(typeof(T), "x");
            var visited = Expression.Parameter(typeof(HashSet<object>), "visited");
            var chkMut = Expression.Constant(false) as Expression;
            var mut = type.IsSealed ? Mutability.Immutable : Mutability.Maybe;
            var pureMethods = AllPureMethods(type);
            foreach (var field in type.GetFields(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy))
            {
                // since this type already has no impure methods, then only public fields should matter
                if (!field.IsInitOnly && (field.IsPublic || !pureMethods))
                {
                    isMutable = null;
                    return Mutability.Mutable;
                }
                else
                {
                    var ftype = typeof(Type<>).MakeGenericType(field.FieldType);
                    switch ((Mutability)ftype.GetField("Mutability").GetValue(null))
                    {
                        case Mutability.Mutable:
                            mut = Mutability.Mutable;
                            break;
                        case Mutability.Maybe:
                            mut = Mutability.Maybe;
                            var fmut = ftype.GetMethod("IsMutable", BindingFlags.Static | BindingFlags.NonPublic,
                                                       null, new[] { field.FieldType, typeof(HashSet<object>) }, null);
                            chkMut = Expression.Or(Expression.Call(fmut, Expression.Field(x, field), visited), chkMut);
                            break;
                    }
                }
            }
            isMutable = mut == Mutability.Maybe
                      ? Expression.Lambda<Func<T, HashSet<object>, bool>>(chkMut, x, visited).Compile()
                      : null;
            return mut;
        }
        
        static bool DispatchIsMutable<T0>(T value, HashSet<object> visited)
            where T0 : T
        {
            return Type<T0>.IsMutable((T0)value, visited);
        }

        static bool AllPureMethods(Type type)
        {
            // T is impure if any method is not decorated with [Pure] or is a static method that does not accept a T
            //FIXME: should also exclude operators?
            var mflags = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.FlattenHierarchy;
            var types = new[]
            {
                typeof(IFormattable), typeof(IConvertible), typeof(ICloneable), typeof(IComparable),
                Kind.Definition.Apply(typeof(IComparable<>), type), Kind.Definition.Apply(typeof(IEquatable<>), type),
                typeof(IFormatProvider), typeof(ICustomFormatter), typeof(ICustomAttributeProvider),
                Kind.Definition.Apply(typeof(IComparer<>), type), 
                //Kind.Definition.Apply(typeof(IGrouping<>), type), Kind.Definition.Apply(typeof(ILookup<>), type),
                Kind.Definition.Apply(typeof(IOrderedQueryable<>), type), typeof(IOrderedQueryable),
                Kind.Definition.Apply(typeof(IOrderedEnumerable<>), type),
                Kind.Definition.Apply(typeof(IQueryable<>), type), typeof(IQueryable),
                typeof(IReflect), typeof(ISafeSerializationData), typeof(IServiceProvider),
                //Type.GetType("System.ITuple, mscorlib"),  //FIXME: not sure why I was loading this dynamically
                typeof(IStructuralEquatable), typeof(IStructuralComparable),
                typeof(ISurrogateSelector), typeof(object), typeof(ValueType)
            }
            .Where(x => type.Subtypes(x));
            var methods = types.SelectMany(x => x.IsInterface ? type.GetInterfaceMap(x).TargetMethods : x.GetMethods(mflags))
                               .Aggregate(new HashSet<int>(), (acc, x) => { acc.Add(x.GetBaseDefinition().MetadataToken); return acc; });
            //FIXME: internal fields can bypass mutability analysis
            return type.GetMethods(mflags)
                       .All(x => methods.Contains(x.GetBaseDefinition().MetadataToken)
                              || x.Has<PureAttribute>() || x.IsPureGetter() || x.IsPureSetter()
                              || x.IsStatic && !Array.Exists(x.GetParameters(), p => p.ParameterType == type));
        }
        #endregion

        #region Circularity helpers
        static Cycles DetectCycles(Type type, ref Type[] visited, int length)
        {
            if (HasParentSubtype(type, visited, length))
                return Cycles.Yes;
            if (length == visited.Length)
                Array.Resize(ref visited, visited.Length * 2);
            visited[length] = type;
            if (type.HasElementType)
            {
                return DetectCycles(type.GetElementType(), ref visited, length + 1);
            }
            else
            {
                foreach (var x in type.GetFields(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy))
                {
                    if (!type.IsPrimitive && Cycles.Yes == DetectCycles(x.FieldType, ref visited, length + 1))
                        return Cycles.Yes;
                }
            }
            return Cycles.No;
        }
        internal static bool HasParentSubtype(Type type, Type[] array, int length)
        {
            for (int i = 0; i < length; ++i)
            {
                if (array[i] == type || array[i].Subtypes(type))
                    return true;
            }
            return false;
        }
        #endregion

        #region Structural equality helpers
        static Func<T, T, HashSet<(object, object)>, bool> StructuralEquality()
        {
            //FIXME: consider whether to use IEquatable<T> if the type is a struct
            var type = typeof(T);

            if (type.IsPrimitive || type.IsEnum || type == typeof(string) || type == typeof(decimal))
            {
                // primitive types are always equal by value, so return a trivial equality check
                return (a0, a1, v) => EqualityComparer<T>.Default.Equals(a0, a1);
            }
            else if (type.IsArray)
            {
                var et = type.GetElementType();
                var atype = typeof(Arrays<>).MakeGenericType(et);
                var eq = atype.GetMethod("StructuralEquals", BindingFlags.Static | BindingFlags.Public);
                return eq.Create<Func<T, T, HashSet<(object, object)>, bool>>();
            }
            // build a field-by-field equality comparison, ensuring we add any reference
            // types to 'visited' to avoid infinite recursion

            //FIXME: if a field is an interface type, then it has no members and we currently skip it,
            //however if it's an IEnumerable type, then we could in principle check it element-wise.
            //The ideal option would be a dynamic dispatch to a type-specific equality check, but the
            //point of this API is to be fast type-specific operations. Actually, for interfaces we
            //could just compare all public properties that have getters.

            var x0 = Expression.Parameter(typeof(T), "x0");
            var x1 = Expression.Parameter(typeof(T), "x1");

            // construct a list of all fields going back to the base type
            var members = new List<FieldInfo>();
            for (var current = type; current != null && current != typeof(object); current = current.BaseType)
            {
                members.AddRange(current.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly));
            }
            if (members.Count == 0)
                return (a0, a1, v) => ReferenceEquals(a0, a1) || (a0 != null && a1 != null);
            var visited = Expression.Parameter(typeof(HashSet<(object, object)>), "visited");
            var addVisited = visited.Type.GetMethod("Add");
            var tobj = typeof(object);
            Expression body = Expression.Constant(true);
            foreach (var f in members)
            {
                var ft = typeof(Type<>).MakeGenericType(f.FieldType);
                var eq = ft.GetMethod("StructuralEquals", BindingFlags.NonPublic | BindingFlags.Static,
                                      Type.DefaultBinder, new[] { f.FieldType, f.FieldType, typeof(HashSet<(object,object)>) }, null);
                // construct a sequence of equality checks connected by &&
                var call = Expression.Call(
                    eq,
                    Expression.Field(x0, f),
                    Expression.Field(x1, f),
                    visited);
                body = Expression.AndAlso(body, call);
            }
            // if a value type then always check all fields, otherwise only eval the
            // whole body if we haven't already compared the two reference types
            if (!type.IsValueType)
            {
                var enull = Expression.Constant(null, tobj);
                // reference equality check ensures either both are null, and hence equal,
                // or the following checks ensure neither are null before proceeding with
                // the usual checks, eg. cycle detection and field-by-field equality
                var x02obj = Expression.Convert(x0, tobj);
                var x12obj = Expression.Convert(x1, tobj);
                var tupleCtor = typeof(ValueTuple<object, object>).GetConstructor(new[] { tobj, tobj });

                // cycle detection is less than ideal as it adds (x0, x1) and (x1, x0) to the visited set,
                // when we could probably do with applying some kind of order to the two objects to
                // canonicalize the pair
                body =
                    Expression.OrElse(
                        Expression.ReferenceEqual(x0, x1), // add a reference equality check for early exit
                        Expression.AndAlso(
                            Expression.NotEqual(x0, enull), // ensure neither parameter is null
                            Expression.AndAlso(
                                Expression.NotEqual(x1, enull),
                                Expression.OrElse(
                                    Expression.Not(
                                        Expression.Call(
                                            visited, addVisited,
                                            Expression.New(tupleCtor, x02obj, x12obj))),
                                    Expression.OrElse(
                                        Expression.Not(
                                            Expression.Call(
                                                visited, addVisited,
                                                Expression.New(tupleCtor, x12obj, x02obj))),
                                            body)))));
            }
            return Expression.Lambda<Func<T, T, HashSet<(object, object)>, bool>>(body, x0, x1, visited)
                             .Compile();
        }
        #endregion
    }
}
