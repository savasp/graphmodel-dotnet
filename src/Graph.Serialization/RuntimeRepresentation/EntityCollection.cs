// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Serialization;

/// <summary>
/// Represents a collection of entities.
/// </summary>
/// <param name="Type">The declared type of elements in the collection.</param>
/// <param name="Entities">
/// The ordered collection slots. A <see langword="null"/> entry is an explicit null element, not
/// an omitted entity; count and position are part of the serialized contract.
/// </param>
public record EntityCollection(
    Type Type,
    IReadOnlyList<EntityInfo?> Entities
) : Serialized;
