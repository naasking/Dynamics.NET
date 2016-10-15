using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Dynamics
{
    /// <summary>
    /// A delegate matching the signature of static T.TryParse(string, out T) methods.
    /// </summary>
    /// <typeparam name="T">The type to parse.</typeparam>
    /// <param name="input">The input string.</param>
    /// <param name="value">The output value.</param>
    /// <returns>True if parsing succeeded, false otherwise.</returns>
    public delegate bool TryParse<T>(string input, out T value);

    /// <summary>
    /// A delegate matching the signature of static T.TryParse(string, T1, out T) methods.
    /// </summary>
    /// <typeparam name="T">The type to parse.</typeparam>
    /// <typeparam name="T1">The first argument after the string.</typeparam>
    /// <param name="input">The input string.</param>
    /// <param name="arg1">The first argument type after the string.</param>
    /// <param name="value">The output value.</param>
    /// <returns>True if parsing succeeded, false otherwise.</returns>
    public delegate bool TryParse<T, T1>(string input, T1 arg1, out T value);

    /// <summary>
    /// A delegate matching the signature of static T.TryParse(string, T1, T2, out T) methods.
    /// </summary>
    /// <typeparam name="T">The type to parse.</typeparam>
    /// <typeparam name="T1">The first argument type after the string.</typeparam>
    /// <typeparam name="T2">The second argument type after the string.</typeparam>
    /// <param name="input">The input string.</param>
    /// <param name="arg1">The first argument type after the string.</param>
    /// <param name="arg2">The second argument type after the string.</param>
    /// <param name="value">The output value.</param>
    /// <returns>True if parsing succeeded, false otherwise.</returns>
    public delegate bool TryParse<T, T1, T2>(string input, T1 arg1, T2 arg2, out T value);
}
