// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph;

/// <summary>
/// Represents a strongly-typed direct connection between two nodes in a graph.
/// </summary>
/// <typeparam name="TSource">The type of the source node.</typeparam>
/// <typeparam name="TRel">The type of the relationship.</typeparam>
/// <typeparam name="TTarget">The type of the target node.</typeparam>
public interface IGraphPathSegment<TSource, TRel, TTarget> : IGraphPathSegment
    where TSource : class, INode
    where TRel : class, IRelationship
    where TTarget : class, INode
{
    /// <summary>
    /// Gets the strongly-typed starting node of the path segment.
    /// </summary>
    new TSource StartNode { get; }

    /// <summary>
    /// Gets the strongly-typed ending node of the path segment.
    /// </summary>
    new TTarget EndNode { get; }

    /// <summary>
    /// Gets the strongly-typed relationship connecting the nodes.
    /// </summary>
    new TRel Relationship { get; }
}