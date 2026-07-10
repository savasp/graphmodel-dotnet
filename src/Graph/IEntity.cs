// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph;

/// <summary>
/// Represents the foundation for all entities in the graph model.
/// This is the base interface for both nodes and relationships, providing the core identity functionality.
/// </summary>
public interface IEntity
{
    /// <summary>
    /// Gets the unique identifier of the entity.
    /// This property is used to uniquely identify and reference the entity within the graph system.
    /// </summary>
    /// <remarks>
    /// Identifiers should be immutable once the entity has been persisted to ensure referential integrity.
    /// </remarks>
    [Property(IsKey = true)]
    string Id { get; init; }
}
