// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph;

/// <summary>
/// Represents a variable-length path through the graph: an ordered sequence of one or more
/// single-hop <see cref="IGraphPathSegment"/> instances connecting <see cref="Start"/> to
/// <see cref="End"/>.
/// </summary>
/// <remarks>
/// Use <see cref="IGraphQueryable{T}"/> traversal operators that return
/// <c>IGraphQueryable&lt;IGraphPath&gt;</c> (e.g. <c>TraversePaths</c>) when the number of hops
/// is variable (min/max depth greater than a single hop). For a single, statically-typed hop,
/// use <see cref="IGraphPathSegment{TSource, TRel, TTarget}"/> directly instead.
/// </remarks>
public interface IGraphPath
{
    /// <summary>
    /// Gets the first node in the path.
    /// </summary>
    INode Start { get; }

    /// <summary>
    /// Gets the last node in the path.
    /// </summary>
    INode End { get; }

    /// <summary>
    /// Gets the ordered sequence of single-hop segments that make up this path, from
    /// <see cref="Start"/> to <see cref="End"/>. Always contains at least one segment.
    /// </summary>
    IReadOnlyList<IGraphPathSegment> Segments { get; }
}
