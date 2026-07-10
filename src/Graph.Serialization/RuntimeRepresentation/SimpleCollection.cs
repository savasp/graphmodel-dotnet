// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Serialization;

/// <summary>
/// Represents a collection of values. The predicate <see cref="Model.GraphDataModel.IsSimple(System.Type)"/>
/// determines if a value is considered simple.
/// </summary>
/// <param name="Values">An <see cref="ICollection{SimpleValue}"/> of simple values.</param>
/// <param name="ElementType">The type of elements in the collection.</param>
public record SimpleCollection(
    ICollection<SimpleValue> Values,
    Type ElementType
) : Serialized;
