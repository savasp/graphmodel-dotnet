// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.InMemory;

using System.Collections;

/// <summary>
/// Deep-copies simple property values at the store boundary. Most simple values (primitives,
/// strings, temporal types, <see cref="Uri"/>) are immutable and pass through; the mutable ones
/// (<c>byte[]</c>, collections of simple values) are copied so no reference is shared between a
/// caller-held entity and the store, in either direction.
/// </summary>
internal static class ValueSnapshot
{
    /// <summary>Copies one simple value.</summary>
    public static object? Copy(object? value) => value switch
    {
        null => null,
        byte[] bytes => bytes.Clone(),
        string => value,
        IEnumerable enumerable when value is not string => CopyList(enumerable),
        _ => value,
    };

    /// <summary>Copies a collection of simple values into a fresh list.</summary>
    public static List<object?> CopyList(IEnumerable values)
    {
        var copy = new List<object?>();
        foreach (var value in values)
        {
            copy.Add(value is byte[] bytes ? bytes.Clone() : value);
        }

        return copy;
    }
}
