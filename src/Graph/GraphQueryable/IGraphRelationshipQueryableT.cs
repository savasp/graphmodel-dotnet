// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph;

/// <summary>
/// Represents a queryable graph data source that supports LINQ operations over relationships.
/// </summary>
/// <remarks>
/// Obsolete: use <see cref="IGraphQueryable{T}"/> directly (with <c>T : IRelationship</c>).
/// Kept for one release to ease migration.
/// </remarks>
/// <typeparam name="TRel">An <see cref="IRelationship"/>-derived type.</typeparam>
[Obsolete("Use IGraphQueryable<T> instead; relationship-only operators are gated by generic constraints. This alias will be removed in a future release.")]
#pragma warning disable CS0618 // Type or member is obsolete
public interface IGraphRelationshipQueryable<out TRel> : IGraphQueryable<TRel>, IGraphRelationshipQueryable
#pragma warning restore CS0618
    where TRel : class, IRelationship
{
}