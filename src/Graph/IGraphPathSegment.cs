// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph;

/// <summary>
/// Represents a direct connection between two nodes in a graph, encapsulating the relationship and its endpoints.
/// This interface is used to model segments of paths in a graph, where each segment consists of a start node, an end node, and the relationship connecting them.
/// It is typically used in graph traversal and pathfinding algorithms to represent the individual steps in a path.
/// </summary>
public interface IGraphPathSegment
{
    /// <summary>
    /// Gets the starting node of the path segment.
    /// </summary>
    INode StartNode { get; }

    /// <summary>
    /// Gets the ending node of the path segment.
    /// </summary>
    INode EndNode { get; }

    /// <summary>
    /// Gets the relationship connecting the start and end nodes.
    /// </summary>
    IRelationship Relationship { get; }

    /// <summary>
    /// Gets the physical orientation of the relationship relative to this segment's endpoints.
    /// </summary>
    /// <remarks>
    /// <see cref="RelationshipDirection.Outgoing"/> means the physical edge runs from
    /// <see cref="StartNode"/> to <see cref="EndNode"/>; <see cref="RelationshipDirection.Incoming"/>
    /// means it runs from <see cref="EndNode"/> to <see cref="StartNode"/>. A self-loop is reported
    /// as <see cref="RelationshipDirection.Outgoing"/> deterministically.
    /// </remarks>
    RelationshipDirection Direction { get; }
}
