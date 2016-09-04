using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Dynamics
{
    internal static class Copying
    {
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

        //FIXME: current copy implementation does not reproduce any internal IComparer or IEqualityComparer instances
        //so the ordering and hashing of new dictionary won't match. Perhaps should let those be auto-copied
        //public static TEnum DictionaryBase<TEnum>(IEnumerable source, TEnum copy, Dictionary<object, object> refs)
        //    where TEnum : IDictionary
        //{
        //    refs.Add(source, copy);
        //    foreach (DictionaryEntry x in source)
        //        copy.Add(Type<object>.Copy(x.Key, refs), Type<object>.Copy(x.Value, refs));
        //    return copy;
        //}

        //public static SortedList SortedList(SortedList source, Dictionary<object, object> refs)
        //{
        //    return DictionaryBase<SortedList>(source, new SortedList(source.Count), refs);
        //}

        //public static Hashtable Hashtable(Hashtable source, Dictionary<object, object> refs)
        //{
        //    return DictionaryBase<Hashtable>(source, new Hashtable(source.Count), refs);
        //}

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

        //public static SortedList<T0, T1> SortedList<T0, T1>(SortedList<T0, T1> source, Dictionary<object, object> refs)
        //{
        //    return DictionaryBase<T0, T1, SortedList<T0, T1>>(source, new SortedList<T0, T1>(), refs);
        //}

        //static TDict DictionaryBase<T0, T1, TDict>(TDict source, TDict copy, Dictionary<object, object> refs)
        //    where TDict : IDictionary<T0, T1>
        //{
        //    refs.Add(source, copy);
        //    foreach (var x in source)
        //        copy.Add(Type<T0>.Copy(x.Key, refs), Type<T1>.Copy(x.Value, refs));
        //    return copy;
        //}

        //public static IDictionary<T0, T1> IDictionary<T0, T1>(IDictionary<T0, T1> source, Dictionary<object, object> refs)
        //{
        //    return DictionaryBase<T0, T1, IDictionary<T0, T1>>(source, new Dictionary<T0, T1>(source.Count), refs);
        //}

        //public static Dictionary<T0, T1> Dictionary<T0, T1>(Dictionary<T0, T1> source, Dictionary<object, object> refs)
        //{
        //    return DictionaryBase<T0, T1, Dictionary<T0, T1>>(source, new Dictionary<T0, T1>(), refs);
        //}

        //public static SortedDictionary<T0, T1> SortedDictionary<T0, T1>(SortedDictionary<T0, T1> source, Dictionary<object, object> refs)
        //{
        //    return DictionaryBase<T0, T1, SortedDictionary<T0, T1>>(source, new SortedDictionary<T0, T1>(), refs);
        //}

        //public static ISet<T> ISet<T>(ISet<T> source, Dictionary<object, object> refs)
        //{
        //    return EnumerableBase<T, ISet<T>>(source, new HashSet<T>(), refs);
        //}

        //public static HashSet<T> HashSet<T>(HashSet<T> source, Dictionary<object, object> refs)
        //{
        //    return EnumerableBase<T, HashSet<T>>(source, new HashSet<T>(), refs);
        //}

        //public static SortedSet<T> HashSet<T>(SortedSet<T> source, Dictionary<object, object> refs)
        //{
        //    return EnumerableBase<T, SortedSet<T>>(source, new SortedSet<T>(), refs);
        //}

        public static KeyValuePair<T0, T1> KeyValuePair<T0, T1>(KeyValuePair<T0, T1> source, Dictionary<object, object> refs)
        {
            return new KeyValuePair<T0, T1>(Type<T0>.Copy(source.Key, refs), Type<T1>.Copy(source.Value, refs));
        }
    }
}
