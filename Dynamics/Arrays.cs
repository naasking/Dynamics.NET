using System;
using System.Collections.Generic;
using System.Text;

namespace Dynamics
{
    internal static class Arrays<T>
    {
        /// <summary>
        /// Structural equality for arrays of type T.
        /// </summary>
        /// <param name="a0"></param>
        /// <param name="a1"></param>
        /// <param name="visited"></param>
        /// <returns></returns>
        public static bool StructuralEquals(T[] a0, T[] a1, HashSet<(object, object)> visited)
        {
            if (ReferenceEquals(a0, a1))
                return true;
            if (a0 == null || a1 == null)
                return false;
            if (a0.Length != a1.Length)
                return false;
            for (int i = 0; i < a0.Length; i++)
            {
                if (!Type<T>.structuralEquals(a0[i], a1[i], visited))
                    return false;
            }
            return true;
        }
    }
}
