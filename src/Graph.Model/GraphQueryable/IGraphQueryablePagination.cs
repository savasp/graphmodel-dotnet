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
/// Represents a page of results from a paginated graph query.
/// </summary>
/// <typeparam name="T">The type of elements in the page</typeparam>
public record GraphPage<T>
{
    /// <summary>
    /// Gets the items in this page.
    /// </summary>
    public IReadOnlyList<T> Items { get; init; } = [];

    /// <summary>
    /// Gets the total count of items across all pages.
    /// </summary>
    public int TotalCount { get; init; }

    /// <summary>
    /// Gets the current page number (1-based).
    /// </summary>
    public int PageNumber { get; init; }

    /// <summary>
    /// Gets the page size.
    /// </summary>
    public int PageSize { get; init; }

    /// <summary>
    /// Gets the total number of pages.
    /// </summary>
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);

    /// <summary>
    /// Gets a value indicating whether there is a next page.
    /// </summary>
    public bool HasNextPage => PageNumber < TotalPages;

    /// <summary>
    /// Gets a value indicating whether there is a previous page.
    /// </summary>
    public bool HasPreviousPage => PageNumber > 1;
}

/// <summary>
/// Defines pagination operations for graph queryables.
/// </summary>
/// <typeparam name="T">The type of elements in the queryable</typeparam>
public interface IGraphQueryablePagination<T>
{
    /// <summary>
    /// Asynchronously retrieves a specific page of results.
    /// </summary>
    /// <param name="pageNumber">The page number to retrieve (1-based)</param>
    /// <param name="pageSize">The number of items per page</param>
    /// <param name="cancellationToken">A cancellation token to observe while waiting for the task to complete</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the page of results</returns>
    Task<GraphPage<T>> PageAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously retrieves the first page of results.
    /// </summary>
    /// <param name="pageSize">The number of items per page</param>
    /// <param name="cancellationToken">A cancellation token to observe while waiting for the task to complete</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the first page of results</returns>
    Task<GraphPage<T>> FirstPageAsync(int pageSize, CancellationToken cancellationToken = default);
}

/// <summary>
/// Provides extension methods for pagination operations on graph queryables.
/// </summary>
public static class GraphQueryablePaginationExtensions
{
    /// <summary>
    /// Asynchronously retrieves a specific page of results.
    /// </summary>
    /// <typeparam name="T">The type of elements in the queryable</typeparam>
    /// <param name="source">The graph queryable to paginate</param>
    /// <param name="pageNumber">The page number to retrieve (1-based)</param>
    /// <param name="pageSize">The number of items per page</param>
    /// <param name="cancellationToken">A cancellation token to observe while waiting for the task to complete</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the page of results</returns>
    public static async Task<GraphPage<T>> PageAsync<T>(
        this IGraphQueryable<T> source,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(pageNumber, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, 1);

        var totalCount = await source.CountAsync(cancellationToken);
        var skip = (pageNumber - 1) * pageSize;

        // Use ToListAsync directly from the source to avoid casting issues
        var items = await source.GraphSkip(skip).GraphTake(pageSize).ToListAsync(cancellationToken);

        return new GraphPage<T>
        {
            Items = items,
            TotalCount = totalCount,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }

    /// <summary>
    /// Asynchronously retrieves the first page of results.
    /// </summary>
    /// <typeparam name="T">The type of elements in the queryable</typeparam>
    /// <param name="source">The graph queryable to paginate</param>
    /// <param name="pageSize">The number of items per page</param>
    /// <param name="cancellationToken">A cancellation token to observe while waiting for the task to complete</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the first page of results</returns>
    public static async Task<GraphPage<T>> FirstPageAsync<T>(
        this IGraphQueryable<T> source,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        return await source.PageAsync(1, pageSize, cancellationToken);
    }

    /// <summary>
    /// Asynchronously skips a specified number of elements and returns the remaining elements.
    /// </summary>
    /// <typeparam name="T">The type of elements in the queryable</typeparam>
    /// <param name="source">The graph queryable to skip elements from</param>
    /// <param name="count">The number of elements to skip</param>
    /// <param name="cancellationToken">A cancellation token to observe while waiting for the task to complete</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the remaining elements</returns>
    public static async Task<List<T>> SkipAsync<T>(
        this IGraphQueryable<T> source,
        int count,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        return await source.GraphSkip(count).ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Asynchronously returns a specified number of contiguous elements from the start of the sequence.
    /// </summary>
    /// <typeparam name="T">The type of elements in the queryable</typeparam>
    /// <param name="source">The graph queryable to take elements from</param>
    /// <param name="count">The number of elements to take</param>
    /// <param name="cancellationToken">A cancellation token to observe while waiting for the task to complete</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the specified number of elements</returns>
    public static async Task<List<T>> TakeAsync<T>(
        this IGraphQueryable<T> source,
        int count,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        return await source.GraphTake(count).ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Asynchronously returns elements from the sequence as long as a specified condition is true.
    /// </summary>
    /// <typeparam name="T">The type of elements in the queryable</typeparam>
    /// <param name="source">The graph queryable to take elements from</param>
    /// <param name="predicate">A function to test each element for a condition</param>
    /// <param name="cancellationToken">A cancellation token to observe while waiting for the task to complete</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the elements while the condition is true</returns>
    public static async Task<List<T>> TakeWhileAsync<T>(
        this IGraphQueryable<T> source,
        Expression<Func<T, bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        var result = source.TakeWhile(predicate);
        return await ((IGraphQueryable<T>)result).ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Asynchronously skips elements from the sequence as long as a specified condition is true and then returns the remaining elements.
    /// </summary>
    /// <typeparam name="T">The type of elements in the queryable</typeparam>
    /// <param name="source">The graph queryable to skip elements from</param>
    /// <param name="predicate">A function to test each element for a condition</param>
    /// <param name="cancellationToken">A cancellation token to observe while waiting for the task to complete</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the remaining elements after skipping</returns>
    public static async Task<List<T>> SkipWhileAsync<T>(
        this IGraphQueryable<T> source,
        Expression<Func<T, bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        var result = source.SkipWhile(predicate);
        return await ((IGraphQueryable<T>)result).ToListAsync(cancellationToken);
    }
}
