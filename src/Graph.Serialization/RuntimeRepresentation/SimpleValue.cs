// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Serialization;

/// <summary>
/// Represents a serialized simple value. The predicate <see cref="GraphDataModel.IsSimple(System.Type)"/>
/// determines if a value is considered simple.
/// </summary>
/// <param name="Object">The object value.</param>
/// <param name="Type">The type of the value.</param>
public record SimpleValue(
    object Object,
    Type Type
) : Serialized;

