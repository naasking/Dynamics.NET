using System;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;

namespace Dynamics
{
    /// <summary>
    /// Optimized copy implementations for some basic BCL types. This saves
    /// some initialization time and uses less memory than dynamically generated
    /// methods that do the same job.
    /// 
    /// We don't provide implementations for dictionary or set types because there's
    /// no way to know what IComparer or IEqualityComparer instances were used, so
    /// the ordinary internal reflection logic handles that.
    /// </summary>
    internal static class Copying
    {
        public static readonly MethodInfo[] Methods = typeof(Copying).GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.DeclaredOnly);

        public static T[] Array<T>(T[] source, Dictionary<object, object> refs)
        {
            var x = new T[source.Length];
            refs.Add(source, x);
            for (int i = 0; i < source.Length; ++i)
            {
                x[i] = Type<T>.Copy(source[i], refs);
            }
            return x;
        }

        public static TEnum EnumerableBase<TEnum>(IEnumerable source, TEnum copy, Dictionary<object, object> refs)
            where TEnum : IList
        {
            refs.Add(source, copy);
            foreach (var x in source)
                copy.Add(Type<object>.Copy(x, refs));
            return copy;
        }

        public static IEnumerable IEnumerable(IEnumerable source, Dictionary<object, object> refs)
        {
            return EnumerableBase<ArrayList>(source, new ArrayList(), refs);
        }

        public static ICollection ICollection(ICollection source, Dictionary<object, object> refs)
        {
            return EnumerableBase<ArrayList>(source, new ArrayList(source.Count), refs);
        }
        
        public static TEnum EnumerableBase<T, TEnum>(IEnumerable<T> source, TEnum copy, Dictionary<object, object> refs)
            where TEnum : ICollection<T>
        {
            refs.Add(source, copy);
            foreach (var x in source)
                copy.Add(Type<T>.Copy(x, refs));
            return copy;
        }

        public static IEnumerable<T> IEnumerable<T>(IEnumerable<T> source, Dictionary<object, object> refs)
        {
            return EnumerableBase<T, List<T>>(source, new List<T>(), refs);
        }

        public static ICollection<T> ICollection<T>(ICollection<T> source, Dictionary<object, object> refs)
        {
            return EnumerableBase<T, List<T>>(source, new List<T>(source.Count), refs);
        }

        static TList ListBase<T, TList>(TList source, TList copy, Dictionary<object, object> refs)
            where TList : IList<T>
        {
            refs.Add(source, copy);
            foreach (var x in source)
                copy.Add(Type<T>.Copy(x, refs));
            return copy;
        }

        public static IList<T> IList<T>(IList<T> source, Dictionary<object, object> refs)
        {
            return ListBase<T, IList<T>>(source, new List<T>(), refs);
        }

        public static List<T> List<T>(List<T> source, Dictionary<object, object> refs)
        {
            return ListBase<T, List<T>>(source, new List<T>(), refs);
        }
        
        public static KeyValuePair<T0, T1> KeyValuePair<T0, T1>(KeyValuePair<T0, T1> source, Dictionary<object, object> refs)
        {
            return new KeyValuePair<T0, T1>(Type<T0>.Copy(source.Key, refs), Type<T1>.Copy(source.Value, refs));
        }

        public static ReadOnlyCollection<T> ReadOnlyCollection<T>(ReadOnlyCollection<T> source, Dictionary<object, object> refs)
        {
            //FIXME: this doesn't quite work because T could have a back ref to this collection, which isn't yet created
            return Type<T>.Mutability == Mutability.Immutable
                ? source
                : new ReadOnlyCollection<T>(source.Select(x => Type<T>.Copy(x, refs)).ToList());
        }

        //FIXME: add other collection types?

        public static T Delegate<T>(T source, Dictionary<object, object> refs)
            where T : class
        {
            //FIXME: I think this works only for non-circular delegates, ie. target could have back ref to this delegate
            var del = (Delegate)(object)source;
            var copy = (T)(object)System.Delegate.CreateDelegate(typeof(T), Type<object>.Copy(del.Target, refs), del.Method);
            refs[source] = copy;
            return copy;
        }

        public static T? Nullable<T>(T value, Dictionary<object, object> refs)
            where T : struct =>
            new T?(Type<T>.Copy(value, refs));
    }
}
