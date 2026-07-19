using System;
using System.Collections.Generic;
using System.Linq;

namespace GenerationArk.Simulation.Tests;

internal static class ArrayEnumerableExtensions
{
    public static IEnumerable<T> Reverse<T>(this T[] source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return Enumerable.Reverse(source);
    }
}
