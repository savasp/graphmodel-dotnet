// Copyright CVOYA. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.InMemory;

/// <summary>
/// A snapshot of one simple property value as held by the store. Values are deep-copied on the
/// way in and on the way out (see <see cref="ValueSnapshot"/>), so a stored snapshot can never
/// be reached through a reference the caller still holds.
/// </summary>
/// <param name="Name">The storage name of the property.</param>
/// <param name="Value">The copied value: a simple value, or a <c>List&lt;object?&gt;</c> of simple
/// values when <paramref name="IsCollection"/> is true.</param>
/// <param name="Type">The declared .NET type of the property value.</param>
/// <param name="IsNullable">Whether the property was serialized as nullable.</param>
/// <param name="IsCollection">Whether the value is a collection of simple values.</param>
/// <param name="ElementType">The element type when <paramref name="IsCollection"/> is true.</param>
internal sealed record StoredProperty(
    string Name,
    object? Value,
    Type Type,
    bool IsNullable,
    bool IsCollection,
    Type? ElementType);
