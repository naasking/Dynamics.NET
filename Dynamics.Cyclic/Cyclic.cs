using System;
using System.Reflection;

namespace Dynamics
{
    /// <summary>
    /// Check <typeparamref name="T"/> for ability to hold cyclic references.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    [Pure]
    public class Cyclic<T>
    {
        /// <summary>
        /// Exposes whether the type structure allows cycles.
        /// </summary>
        public static readonly Cycles Cycles;

        static Cyclic()
        {
            var type = typeof(T);
            var visited = new Type[6];
            Cycles = DetectCycles(type, ref visited, 0);
        }

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
    }
}
