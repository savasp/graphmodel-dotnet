// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Serialization.Results;

internal record GraphPathSegment<TSource, TRel, TTarget>(
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
