// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph;

/// <summary>
/// Represents a queryable graph data source that supports LINQ operations.
/// This interface extends IQueryable&lt;T&gt; with graph-specific functionality.
/// </summary>
/// <remarks>
/// A single <see cref="IGraphQueryable{T}"/> represents both node and relationship queries;
/// graph-specific operators (e.g. traversal) are gated by generic constraints
/// (<c>where T : INode</c>) on the operator, not by a separate receiver interface.
/// </remarks>
/// <typeparam name="T">The type of elements in the graph queryable</typeparam>
public interface IGraphQueryable<out T> : IQueryable<T>, IGraphQueryable, IAsyncEnumerable<T>
{
    /// <summary>
    /// Gets the graph instance associated with this queryable
    /// </summary>
    IGraph Graph { get; }

    /// <summary>
    /// Gets the graph query provider that handles graph-specific operations
    /// </summary>
    new IGraphQueryProvider Provider { get; }
}