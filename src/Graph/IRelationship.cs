// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph;

/// <summary>
/// Defines the contract for relationship entities in the graph model.
/// Relationships connect two nodes and can have their own properties.
/// </summary>
/// <remarks>
/// Relationships form the connections between nodes, creating the graph structure.
/// </remarks>
public interface IRelationship : IEntity
{
    /// <summary>
    /// Gets the type of this relationship as it is stored in the graph database.
    /// This is a runtime property that reflects the actual relationship type.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This property is automatically populated by the graph provider when the relationship is
    /// retrieved from or saved to the database. The type is derived from the <see cref="RelationshipAttribute"/>
    /// on the implementing type, or the type name if no attribute is present.
    /// </para>
    /// <para>
    /// This property enables polymorphic queries and filtering by type at runtime,
    /// complementing the compile-time type system.
    /// </para>
    /// <para>
    /// Do not set this property manually - it is managed by the graph provider.
    /// </para>
    /// </remarks>
    string Type { get; }

    /// <summary>
    /// Gets the ID of the start node in this relationship.
    /// </summary>
    /// <remarks>
    /// This transitional endpoint member is not populated by bare relationship projections.
    /// Select an <see cref="IGraphPathSegment"/> when endpoint and orientation information is required.
    /// </remarks>
    string StartNodeId { get; init; }

    /// <summary>
    /// Gets the ID of the end node in this relationship.
    /// </summary>
    /// <remarks>
    /// This transitional endpoint member is not populated by bare relationship projections.
    /// Select an <see cref="IGraphPathSegment"/> when endpoint and orientation information is required.
    /// </remarks>
    string EndNodeId { get; init; }
}
