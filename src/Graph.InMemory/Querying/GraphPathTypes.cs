// Copyright CVOYA. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.InMemory.Querying;

/// <summary>
/// A non-generic single-hop segment used to compose an <see cref="InMemoryGraphPath"/>.
/// Intermediate hops in a variable-length path are not statically typed per-hop, so
/// <see cref="IGraphPath.Segments"/> is expressed in terms of the non-generic base.
/// </summary>
internal sealed record InMemoryPathHopSegment(INode StartNode, IRelationship Relationship, INode EndNode)
    : IGraphPathSegment;

/// <summary>
/// Concrete <see cref="IGraphPath"/> materialized from a <c>TraversePaths</c> query.
/// </summary>
internal sealed record InMemoryGraphPath(INode Start, INode End, IReadOnlyList<IGraphPathSegment> Segments)
    : IGraphPath;

/// <summary>
/// Concrete <see cref="IGraphPathSegment{TSource, TRel, TTarget}"/> materialized for
/// <c>PathSegments</c> projections. For a multi-hop segment (a depth range above one), the
/// relationship is the final hop's relationship, matching the end node it reached.
/// </summary>
internal sealed record InMemoryPathSegment<TSource, TRel, TTarget>(
    TSource StartNode,
    TRel Relationship,
    TTarget EndNode) : IGraphPathSegment<TSource, TRel, TTarget>
    where TSource : class, INode
    where TRel : class, IRelationship
    where TTarget : class, INode
{
    INode IGraphPathSegment.StartNode => StartNode;

    INode IGraphPathSegment.EndNode => EndNode;

    IRelationship IGraphPathSegment.Relationship => Relationship;
}
