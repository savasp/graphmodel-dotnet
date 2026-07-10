// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Serialization;

/// <summary>
/// Represents a collection of entities.
/// </summary>
/// <param name="Entities">A collection of entities (<see cref="EntityInfo"/> ).</param>
/// <param name="Type">The type of entities in the collection.</param>
public record EntityCollection(
    Type Type,
    ICollection<EntityInfo> Entities
) : Serialized;

