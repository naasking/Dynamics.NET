using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Linq.Expressions;

namespace Dynamics.Poco.Delegates
{
    /// <summary>
    /// <see cref="Func{T, TResult}"/> equivalent for structs.
    /// </summary>
    /// <typeparam name="T0"></typeparam>
    /// <typeparam name="T1"></typeparam>
    /// <param name="obj"></param>
    /// <returns></returns>
    public delegate T1 FuncRef<T0, T1>(ref T0 obj);

    /// <summary>
    /// <see cref="Action{T0, TResult}"/> equivalent for structs.
    /// </summary>
    /// <typeparam name="T0"></typeparam>
    /// <typeparam name="T1"></typeparam>
    /// <param name="obj"></param>
    /// <param name="value"></param>
    public delegate void ActionRef<T0, T1>(ref T0 obj, T1 value);
}
