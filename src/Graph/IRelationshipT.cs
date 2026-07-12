// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph;

/// <summary>
/// Defines a strongly-typed relationship between two specific node types.
/// </summary>
/// <typeparam name="TSource">The type of the source node in the relationship.</typeparam>
/// <typeparam name="TTarget">The type of the target node in the relationship.</typeparam>
/// <remarks>
/// This interface extends <see cref="IRelationship"/> by adding strongly-typed references
/// to the actual source and target node objects, facilitating more type-safe graph traversal.
/// </remarks>
public interface IRelationship<TSource, TTarget> : IRelationship
    where TSource : class, INode, new()
    where TTarget : class, INode, new()
{
    /// <summary>
    /// Gets or sets the source node of the relationship.
    /// </summary>
    /// <remarks>
    /// When set, this also updates the <see cref="IRelationship.StartNodeId"/> property.
    /// May be null if the relationship is not fully loaded.
    /// </remarks>
    TSource Source { get; set; }

    /// <summary>
    /// Gets or sets the target node of the relationship.
    /// </summary>
    /// <remarks>
    /// When set, this also updates the <see cref="IRelationship.EndNodeId"/> property.
    /// May be null if the relationship is not fully loaded.
    /// </remarks>
    TTarget Target { get; set; }
}
