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

using System.Linq.Expressions;

namespace Cvoya.Graph.Model;

/// <summary>
/// Represents a queryable graph data source that supports LINQ operations.
/// This interface extends IQueryable&lt;T&gt; with graph-specific functionality.
/// </summary>
/// <typeparam name="T">The type of elements in the graph queryable</typeparam>
public interface IGraphQueryable<T> : IQueryable<T>
{
    /// <summary>
    /// Gets the graph instance associated with this queryable
    /// </summary>
    IGraph Graph { get; }

    /// <summary>
    /// Gets the graph query provider that handles graph-specific operations
    /// </summary>
    new IGraphQueryProvider Provider { get; }

    /// <summary>
    /// Asynchronously executes the query and returns the results as a list.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token to observe while waiting for the task to complete.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a list of elements of type T.</returns>
    Task<List<T>> ToListAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously executes the query and returns the first element of the sequence, or a default value if no element is found.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token to observe while waiting for the task to complete.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the first element of the sequence, or a default value if no element is found.</returns>
    Task<T?> FirstOrDefaultAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously executes the query and returns the first element of the sequence.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token to observe while waiting for the task to complete.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the first element of the sequence.</returns>
    Task<T> FirstAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously checks if any elements in the sequence satisfy the specified condition.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token to observe while waiting for the task to complete.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains true if any elements satisfy the condition; otherwise, false.</returns>
    Task<bool> AnyAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously counts the number of elements in the sequence.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token to observe while waiting for the task to complete.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the number of elements in the sequence.</returns>
    Task<int> CountAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously checks if the sequence is empty.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token to observe while waiting for the task to complete.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains true if the sequence is empty; otherwise, false.</returns>
    async Task<bool> IsEmptyAsync(CancellationToken cancellationToken = default)
        => !await AnyAsync(cancellationToken);

    /// <summary>
    /// Asynchronously executes the query and returns a single element of the sequence, or a default value if no element is found.
    /// If more than one element is found, an exception is thrown.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token to observe while waiting for the task to complete.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the single element of the sequence, or a default value if no element is found.</returns>
    async Task<T?> SingleOrDefaultAsync(CancellationToken cancellationToken = default)
    {
        var list = await ToListAsync(cancellationToken);
        return list.Count == 1 ? list[0] : default;
    }

    /// <summary>
    /// Asynchronously executes the query and returns the single element of the sequence.
    /// If more than one element or no elements are found, an exception is thrown.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token to observe while waiting for the task to complete.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the single element of the sequence.</returns>
    Task<T> SingleAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously executes the query and returns the last element of the sequence.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token to observe while waiting for the task to complete.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the last element of the sequence.</returns>
    Task<T> LastAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously executes the query and returns the last element of the sequence, or a default value if no element is found.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token to observe while waiting for the task to complete.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the last element of the sequence, or a default value if no element is found.</returns>
    Task<T?> LastOrDefaultAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously determines whether all elements in the sequence satisfy a condition.
    /// </summary>
    /// <param name="predicate">A function to test each element for a condition.</param>
    /// <param name="cancellationToken">A cancellation token to observe while waiting for the task to complete.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains true if all elements satisfy the condition; otherwise, false.</returns>
    Task<bool> AllAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously computes the count of elements in the sequence that satisfy a condition.
    /// </summary>
    /// <param name="predicate">A function to test each element for a condition.</param>
    /// <param name="cancellationToken">A cancellation token to observe while waiting for the task to complete.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the count of elements that satisfy the condition.</returns>
    Task<int> CountAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously determines whether any element in the sequence satisfies a condition.
    /// </summary>
    /// <param name="predicate">A function to test each element for a condition.</param>
    /// <param name="cancellationToken">A cancellation token to observe while waiting for the task to complete.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains true if any element satisfies the condition; otherwise, false.</returns>
    Task<bool> AnyAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously returns the maximum value in the sequence.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token to observe while waiting for the task to complete.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the maximum value in the sequence.</returns>
    Task<T?> MaxAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously returns the maximum value in the sequence based on a selector function.
    /// </summary>
    /// <typeparam name="TResult">The type of the result.</typeparam>
    /// <param name="selector">A function to extract the value from each element.</param>
    /// <param name="cancellationToken">A cancellation token to observe while waiting for the task to complete.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the maximum value in the sequence.</returns>
    Task<TResult?> MaxAsync<TResult>(Expression<Func<T, TResult>> selector, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously returns the minimum value in the sequence.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token to observe while waiting for the task to complete.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the minimum value in the sequence.</returns>
    Task<T?> MinAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously returns the minimum value in the sequence based on a selector function.
    /// </summary>
    /// <typeparam name="TResult">The type of the result.</typeparam>
    /// <param name="selector">A function to extract the value from each element.</param>
    /// <param name="cancellationToken">A cancellation token to observe while waiting for the task to complete.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the minimum value in the sequence.</returns>
    Task<TResult?> MinAsync<TResult>(Expression<Func<T, TResult>> selector, CancellationToken cancellationToken = default);
}