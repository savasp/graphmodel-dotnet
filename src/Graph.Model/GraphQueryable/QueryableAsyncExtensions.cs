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
/// Extension methods for async execution of IQueryable queries in the graph context.
/// </summary>
public static class QueryableAsyncExtensions
{
    /// <summary>
    /// Asynchronously executes the query and returns the results as a list.
    /// </summary>
    public static async Task<List<T>> ToListAsync<T>(
        this IQueryable<T> source,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        // Check if the provider supports async execution
        if (source.Provider is IGraphQueryProvider graphProvider)
        {
            return await graphProvider.ExecuteAsync<List<T>>(
                Expression.Call(
                    null,
                    new Func<IQueryable<T>, List<T>>(Enumerable.ToList).Method,
                    source.Expression),
                cancellationToken);
        }

        // Fallback to sync execution
        return await Task.Run(() => source.ToList(), cancellationToken);
    }

    /// <summary>
    /// Asynchronously executes the query and returns the first element, or default if empty.
    /// </summary>
    public static async Task<T?> FirstOrDefaultAsync<T>(
        this IQueryable<T> source,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (source.Provider is IGraphQueryProvider graphProvider)
        {
            return await graphProvider.ExecuteAsync<T?>(
                Expression.Call(
                    null,
                    new Func<IQueryable<T>, T?>(Queryable.FirstOrDefault).Method,
                    source.Expression),
                cancellationToken);
        }

        return await Task.Run(() => source.FirstOrDefault(), cancellationToken);
    }

    /// <summary>
    /// Asynchronously counts the elements in the sequence.
    /// </summary>
    public static async Task<int> CountAsync<T>(
        this IQueryable<T> source,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (source.Provider is IGraphQueryProvider graphProvider)
        {
            return await graphProvider.ExecuteAsync<int>(
                Expression.Call(
                    null,
                    new Func<IQueryable<T>, int>(Queryable.Count).Method,
                    source.Expression),
                cancellationToken);
        }

        return await Task.Run(() => source.Count(), cancellationToken);
    }

    /// <summary>
    /// Asynchronously checks if any elements exist in the sequence.
    /// </summary>
    public static async Task<bool> AnyAsync<T>(
        this IQueryable<T> source,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (source.Provider is IGraphQueryProvider graphProvider)
        {
            return await graphProvider.ExecuteAsync<bool>(
                Expression.Call(
                    null,
                    new Func<IQueryable<T>, bool>(Queryable.Any).Method,
                    source.Expression),
                cancellationToken);
        }

        return await Task.Run(() => source.Any(), cancellationToken);
    }

    /// <summary>
    /// Asynchronously returns the first element of the sequence.
    /// </summary>
    public static async Task<T> FirstAsync<T>(
        this IQueryable<T> source,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (source.Provider is IGraphQueryProvider graphProvider)
        {
            return await graphProvider.ExecuteAsync<T>(
                Expression.Call(
                    null,
                    new Func<IQueryable<T>, T>(Queryable.First).Method,
                    source.Expression),
                cancellationToken);
        }

        return await Task.Run(() => source.First(), cancellationToken);
    }
}