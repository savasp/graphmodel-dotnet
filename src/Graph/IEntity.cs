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
    /// <para>
    /// Identifiers should be immutable once the entity has been persisted to ensure referential integrity.
    /// </para>
    /// <para>
    /// A node id is unique <em>graph-wide</em>: within one configured graph or store, an id identifies
    /// at most one node regardless of its labels or CLR type. Creating a second node with an id that an
    /// existing node already uses fails, even when the two carry different labels. Because every id-only
    /// API - relationship endpoints, lookup, update, delete, traversal - resolves through this id alone,
    /// an operation that cannot resolve exactly one node fails before any mutation rather than choosing
    /// a match.
    /// </para>
    /// <para>
    /// Node ids and relationship ids occupy separate namespaces: a relationship may reuse an id that a
    /// node already uses. The same id is also valid in a different configured graph or store.
    /// </para>
    /// </remarks>
    [Property(IsKey = true)]
    string Id { get; init; }
}
