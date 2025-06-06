// Copyright 2025 Savas Parastatidis
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//      https://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System.Collections;
using System.Linq.Expressions;

namespace Cvoya.Graph.Model;

/// <summary>
/// Extension methods that preserve IGraphQueryable&lt;T&gt; interface through LINQ operations.
/// These methods ensure that graph-specific functionality remains available after standard LINQ operations.
/// </summary>
public static class GraphQueryableExtensions
{
    /// <summary>
    /// Skips a specified number of elements in a sequence and returns the remaining elements while preserving the IGraphQueryable interface.
    /// </summary>
    /// <typeparam name="T">The type of the elements of source.</typeparam>
    /// <param name="source">An IGraphQueryable&lt;T&gt; to return elements from.</param>
    /// <param name="count">The number of elements to skip before returning the remaining elements.</param>
    /// <returns>An IGraphQueryable&lt;T&gt; that contains the elements that occur after the specified index in the input sequence.</returns>
    public static IGraphQueryable<T> GraphSkip<T>(this IGraphQueryable<T> source, int count)
    {
        var skipped = source.Skip(count);
        return new GraphQueryableWrapper<T>(skipped, source.Graph, source.Provider);
    }

    /// <summary>
    /// Returns a specified number of contiguous elements from the start of a sequence while preserving the IGraphQueryable interface.
    /// </summary>
    /// <typeparam name="T">The type of the elements of source.</typeparam>
    /// <param name="source">The sequence to return elements from.</param>
    /// <param name="count">The number of elements to return.</param>
    /// <returns>An IGraphQueryable&lt;T&gt; that contains the specified number of elements from the start of the input sequence.</returns>
    public static IGraphQueryable<T> GraphTake<T>(this IGraphQueryable<T> source, int count)
    {
        var taken = source.Take(count);
        return new GraphQueryableWrapper<T>(taken, source.Graph, source.Provider);
    }

    /// <summary>
    /// Simple wrapper implementation that preserves IGraphQueryable interface for LINQ operations.
    /// This is a basic implementation - providers should implement their own versions for optimal performance.
    /// </summary>
    private sealed class GraphQueryableWrapper<T> : IGraphQueryable<T>
    {
        private readonly IQueryable<T> _queryable;

        public GraphQueryableWrapper(IQueryable<T> queryable, IGraph graph, IGraphQueryProvider provider)
        {
            _queryable = queryable;
            Graph = graph;
            Provider = provider;
        }

        public IGraph Graph { get; }
        public IGraphQueryProvider Provider { get; }
        public Type ElementType => _queryable.ElementType;
        public Expression Expression => _queryable.Expression;
        IQueryProvider IQueryable.Provider => Provider;

        public IEnumerator<T> GetEnumerator() => _queryable.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable)_queryable).GetEnumerator();

        // Delegate async operations to the provider
        public Task<List<T>> ToListAsync(CancellationToken cancellationToken = default) =>
            Provider.ExecuteAsync<List<T>>(Expression, cancellationToken);

        public Task<T?> FirstOrDefaultAsync(CancellationToken cancellationToken = default) =>
            Provider.ExecuteAsync<T?>(Expression, cancellationToken);

        public Task<T> FirstAsync(CancellationToken cancellationToken = default) =>
            Provider.ExecuteAsync<T>(Expression, cancellationToken);

        public Task<bool> AnyAsync(CancellationToken cancellationToken = default) =>
            Provider.ExecuteAsync<bool>(Expression, cancellationToken);

        public Task<int> CountAsync(CancellationToken cancellationToken = default) =>
            Provider.ExecuteAsync<int>(Expression, cancellationToken);

        public Task<T> SingleAsync(CancellationToken cancellationToken = default) =>
            Provider.ExecuteAsync<T>(Expression, cancellationToken);

        public Task<T> LastAsync(CancellationToken cancellationToken = default) =>
            Provider.ExecuteAsync<T>(Expression, cancellationToken);

        public Task<T?> LastOrDefaultAsync(CancellationToken cancellationToken = default) =>
            Provider.ExecuteAsync<T?>(Expression, cancellationToken);

        public Task<bool> AllAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default) =>
            Provider.ExecuteAsync<bool>(Expression, cancellationToken);

        public Task<int> CountAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default) =>
            Provider.ExecuteAsync<int>(Expression, cancellationToken);

        public Task<bool> AnyAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default) =>
            Provider.ExecuteAsync<bool>(Expression, cancellationToken);

        public Task<T?> MaxAsync(CancellationToken cancellationToken = default) =>
            Provider.ExecuteAsync<T?>(Expression, cancellationToken);

        public Task<TResult?> MaxAsync<TResult>(Expression<Func<T, TResult>> selector, CancellationToken cancellationToken = default) =>
            Provider.ExecuteAsync<TResult?>(Expression, cancellationToken);

        public Task<T?> MinAsync(CancellationToken cancellationToken = default) =>
            Provider.ExecuteAsync<T?>(Expression, cancellationToken);

        public Task<TResult?> MinAsync<TResult>(Expression<Func<T, TResult>> selector, CancellationToken cancellationToken = default) =>
            Provider.ExecuteAsync<TResult?>(Expression, cancellationToken);
    }
}