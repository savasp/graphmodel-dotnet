// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph;

/// <summary>
/// Represents a sorted node queryable that supports additional ordering operations.
/// </summary>
/// <remarks>
/// Obsolete: use <see cref="IOrderedGraphQueryable{T}"/> instead. Kept for one release to ease migration.
/// </remarks>
/// <typeparam name="TNode">The type of node.</typeparam>
[Obsolete("Use IOrderedGraphQueryable<T> instead. This alias will be removed in a future release.")]
#pragma warning disable CS0618 // Type or member is obsolete
public interface IOrderedGraphNodeQueryable<out TNode> : IGraphNodeQueryable<TNode>, IOrderedQueryable<TNode>
#pragma warning restore CS0618
    where TNode : class, INode
{
}