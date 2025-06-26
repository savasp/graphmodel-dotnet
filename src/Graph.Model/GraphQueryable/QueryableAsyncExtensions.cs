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

namespace Cvoya.Graph.Model;

using System.Linq.Expressions;


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
                    ((Func<IQueryable<T>, List<T>>)QueryableAsyncExtensionsMarkers.ToListAsyncMarker).Method, source.Expression),
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
                    ((Func<IQueryable<T>, T?>)QueryableAsyncExtensionsMarkers.FirstOrDefaultAsyncMarker).Method,
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
                    ((Func<IQueryable<T>, int>)QueryableAsyncExtensionsMarkers.CountAsyncMarker).Method,
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
                    ((Func<IQueryable<T>, bool>)QueryableAsyncExtensionsMarkers.AnyAsyncMarker).Method,
                    source.Expression),
                cancellationToken);
        }

        return await Task.Run(() => source.Any(), cancellationToken);
    }

    /// <summary>
    /// Asynchronously checks if any elements match the specified predicate.
    /// </summary>
    /// <param name="source">The source sequence.</param>
    /// <param name="predicate">The predicate to apply to the elements.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains true if any elements match the predicate; otherwise, false.</returns>
    public static Task<bool> AnyAsync<T>(
        this IQueryable<T> source,
        Expression<Func<T, bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        return source.Where(predicate).AnyAsync(cancellationToken);
    }

    /// <summary>
    /// Asynchronously checks if all elements in the sequence match the specified predicate.
    /// </summary>
    public static async Task<bool> AllAsync<T>(
        this IQueryable<T> source,
        Expression<Func<T, bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (source.Provider is IGraphQueryProvider graphProvider)
        {
            return await graphProvider.ExecuteAsync<bool>(
                Expression.Call(
                    null,
                    ((Func<IQueryable<T>, Expression<Func<T, bool>>, bool>)QueryableAsyncExtensionsMarkers.AllAsyncMarker).Method,
                    source.Expression,
                    predicate),
                cancellationToken);
        }

        return await Task.Run(() => source.All(predicate), cancellationToken);
    }

    /// <summary>
    /// Asynchronously returns a single element of the sequence that satisfies a specified condition.
    /// If no such element exists, an exception is thrown.
    /// If more than one such element exists, an exception is thrown.
    /// </summary>
    /// <returns></returns>
    public static async Task<T> SingleAsync<T>(
        this IQueryable<T> source,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (source.Provider is IGraphQueryProvider graphProvider)
        {
            return await graphProvider.ExecuteAsync<T>(
                Expression.Call(
                    null,
                    ((Func<IQueryable<T>, T>)QueryableAsyncExtensionsMarkers.SingleAsyncMarker).Method,
                    source.Expression),
                cancellationToken);
        }

        return await Task.Run(() => source.Single(), cancellationToken);
    }

    /// <summary>
    /// Asynchronously returns a single element of the sequence that satisfies a specified condition.
    /// If no such element exists, an exception is thrown.
    /// If more than one such element exists, an exception is thrown.
    /// </summary>
    /// <returns></returns>
    public static async Task<T> SingleAsync<T>(
        this IQueryable<T> source,
        Expression<Func<T, bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (source.Provider is IGraphQueryProvider graphProvider)
        {
            return await graphProvider.ExecuteAsync<T>(
                Expression.Call(
                    null,
                    ((Func<IQueryable<T>, Expression<Func<T, bool>>, T>)QueryableAsyncExtensionsMarkers.SingleAsyncMarker).Method,
                    source.Expression,
                    predicate),
                cancellationToken);
        }

        return await Task.Run(() => source.Single(predicate), cancellationToken);
    }

    /// <summary>
    /// Asynchronously returns the last element of the sequence.
    /// If the sequence is empty, an exception is thrown.
    /// If more than one element exists, an exception is thrown.
    /// </summary>
    public static async Task<T> LastAsync<T>(
        this IQueryable<T> source,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (source.Provider is IGraphQueryProvider graphProvider)
        {
            return await graphProvider.ExecuteAsync<T>(
                Expression.Call(
                    null,
                    ((Func<IQueryable<T>, T>)QueryableAsyncExtensionsMarkers.LastAsyncMarker).Method,
                    source.Expression),
                cancellationToken);
        }

        return await Task.Run(() => source.Last(), cancellationToken);
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
                    ((Func<IQueryable<T>, T>)QueryableAsyncExtensionsMarkers.FirstAsyncMarker).Method,
                    source.Expression),
                cancellationToken);
        }

        return await Task.Run(() => source.First(), cancellationToken);
    }

    /// <summary>
    /// Asynchronously executes the query and returns the results as an array.
    /// </summary>
    public static async Task<T[]> ToArrayAsync<T>(
        this IQueryable<T> source,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (source.Provider is IGraphQueryProvider graphProvider)
        {
            return await graphProvider.ExecuteAsync<T[]>(
                Expression.Call(
                    null,
                    ((Func<IQueryable<T>, T[]>)QueryableAsyncExtensionsMarkers.ToArrayAsyncMarker).Method,
                    source.Expression),
                cancellationToken);
        }

        return await Task.Run(() => source.ToArray(), cancellationToken);
    }

    /// <summary>
    /// Asynchronously executes the query and returns the results as a dictionary.
    /// </summary>
    public static async Task<Dictionary<TKey, TElement>> ToDictionaryAsync<TSource, TKey, TElement>(
        this IQueryable<TSource> source,
        Expression<Func<TSource, TKey>> keySelector,
        Expression<Func<TSource, TElement>> elementSelector,
        CancellationToken cancellationToken = default) where TKey : notnull
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(keySelector);
        ArgumentNullException.ThrowIfNull(elementSelector);

        if (source.Provider is IGraphQueryProvider graphProvider)
        {
            return await graphProvider.ExecuteAsync<Dictionary<TKey, TElement>>(
                Expression.Call(
                    null,
                    ((Func<IQueryable<TSource>, Expression<Func<TSource, TKey>>, Expression<Func<TSource, TElement>>, Dictionary<TKey, TElement>>)
                        QueryableAsyncExtensionsMarkers.ToDictionaryAsyncMarker).Method,
                    source.Expression,
                    keySelector,
                    elementSelector),
                cancellationToken);
        }

        return await Task.Run(() => source.ToDictionary(keySelector.Compile(), elementSelector.Compile()), cancellationToken);
    }

    /// <summary>
    /// Asynchronously executes the query and returns the results as a dictionary.
    /// </summary>
    public static async Task<Dictionary<TKey, TSource>> ToDictionaryAsync<TSource, TKey>(
        this IQueryable<TSource> source,
        Expression<Func<TSource, TKey>> keySelector,
        CancellationToken cancellationToken = default) where TKey : notnull
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(keySelector);

        if (source.Provider is IGraphQueryProvider graphProvider)
        {
            return await graphProvider.ExecuteAsync<Dictionary<TKey, TSource>>(
                Expression.Call(
                    null,
                    ((Func<IQueryable<TSource>, Expression<Func<TSource, TKey>>, Dictionary<TKey, TSource>>)
                        QueryableAsyncExtensionsMarkers.ToDictionaryAsyncMarker).Method,
                    source.Expression,
                    keySelector),
                cancellationToken);
        }

        return await Task.Run(() => source.ToDictionary(keySelector.Compile()), cancellationToken);
    }

    /// <summary>
    /// Asynchronously executes the query and returns the results as a lookup.
    /// </summary>
    public static async Task<ILookup<TKey, TElement>> ToLookupAsync<TSource, TKey, TElement>(
        this IQueryable<TSource> source,
        Expression<Func<TSource, TKey>> keySelector,
        Expression<Func<TSource, TElement>> elementSelector,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(keySelector);
        ArgumentNullException.ThrowIfNull(elementSelector);

        if (source.Provider is IGraphQueryProvider graphProvider)
        {
            return await graphProvider.ExecuteAsync<ILookup<TKey, TElement>>(
                Expression.Call(
                    null,
                    ((Func<IQueryable<TSource>, Expression<Func<TSource, TKey>>, Expression<Func<TSource, TElement>>, ILookup<TKey, TElement>>)
                        QueryableAsyncExtensionsMarkers.ToLookupAsyncMarker).Method,
                    source.Expression,
                    keySelector,
                    elementSelector),
                cancellationToken);
        }

        return await Task.Run(() => source.ToLookup(keySelector.Compile(), elementSelector.Compile()), cancellationToken);
    }

    /// <summary>
    /// Asynchronously executes the query and returns the results as a lookup.
    /// </summary>
    public static async Task<ILookup<TKey, TSource>> ToLookupAsync<TSource, TKey>(
        this IQueryable<TSource> source,
        Expression<Func<TSource, TKey>> keySelector,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(keySelector);

        if (source.Provider is IGraphQueryProvider graphProvider)
        {
            return await graphProvider.ExecuteAsync<ILookup<TKey, TSource>>(
                Expression.Call(
                    null,
                    ((Func<IQueryable<TSource>, Expression<Func<TSource, TKey>>, ILookup<TKey, TSource>>)
                        QueryableAsyncExtensionsMarkers.ToLookupAsyncMarker).Method,
                    source.Expression,
                    keySelector),
                cancellationToken);
        }

        return await Task.Run(() => source.ToLookup(keySelector.Compile()), cancellationToken);
    }

    /// <summary>
    /// Asynchronously returns the first element of the sequence that matches the predicate.
    /// </summary>
    public static async Task<T> FirstAsync<T>(
        this IQueryable<T> source,
        Expression<Func<T, bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(predicate);

        if (source.Provider is IGraphQueryProvider graphProvider)
        {
            return await graphProvider.ExecuteAsync<T>(
                Expression.Call(
                    null,
                    ((Func<IQueryable<T>, Expression<Func<T, bool>>, T>)QueryableAsyncExtensionsMarkers.FirstAsyncMarker).Method,
                    source.Expression,
                    predicate),
                cancellationToken);
        }

        return await Task.Run(() => source.First(predicate), cancellationToken);
    }

    /// <summary>
    /// Asynchronously returns the first element or default that matches the predicate.
    /// </summary>
    public static async Task<T?> FirstOrDefaultAsync<T>(
        this IQueryable<T> source,
        Expression<Func<T, bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(predicate);

        if (source.Provider is IGraphQueryProvider graphProvider)
        {
            return await graphProvider.ExecuteAsync<T?>(
                Expression.Call(
                    null,
                    ((Func<IQueryable<T>, Expression<Func<T, bool>>, T?>)QueryableAsyncExtensionsMarkers.FirstOrDefaultAsyncMarker).Method,
                    source.Expression,
                    predicate),
                cancellationToken);
        }

        return await Task.Run(() => source.FirstOrDefault(predicate), cancellationToken);
    }

    /// <summary>
    /// Asynchronously returns the last element of the sequence that matches the predicate.
    /// </summary>
    public static async Task<T> LastAsync<T>(
        this IQueryable<T> source,
        Expression<Func<T, bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(predicate);

        if (source.Provider is IGraphQueryProvider graphProvider)
        {
            return await graphProvider.ExecuteAsync<T>(
                Expression.Call(
                    null,
                    ((Func<IQueryable<T>, Expression<Func<T, bool>>, T>)QueryableAsyncExtensionsMarkers.LastAsyncMarker).Method,
                    source.Expression,
                    predicate),
                cancellationToken);
        }

        return await Task.Run(() => source.Last(predicate), cancellationToken);
    }

    /// <summary>
    /// Asynchronously returns the last element or default of the sequence.
    /// </summary>
    public static async Task<T?> LastOrDefaultAsync<T>(
        this IQueryable<T> source,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (source.Provider is IGraphQueryProvider graphProvider)
        {
            return await graphProvider.ExecuteAsync<T?>(
                Expression.Call(
                    null,
                    ((Func<IQueryable<T>, T?>)QueryableAsyncExtensionsMarkers.LastOrDefaultAsyncMarker).Method,
                    source.Expression),
                cancellationToken);
        }

        return await Task.Run(() => source.LastOrDefault(), cancellationToken);
    }

    /// <summary>
    /// Asynchronously returns the last element or default that matches the predicate.
    /// </summary>
    public static async Task<T?> LastOrDefaultAsync<T>(
        this IQueryable<T> source,
        Expression<Func<T, bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(predicate);

        if (source.Provider is IGraphQueryProvider graphProvider)
        {
            return await graphProvider.ExecuteAsync<T?>(
                Expression.Call(
                    null,
                    ((Func<IQueryable<T>, Expression<Func<T, bool>>, T?>)QueryableAsyncExtensionsMarkers.LastOrDefaultAsyncMarker).Method,
                    source.Expression,
                    predicate),
                cancellationToken);
        }

        return await Task.Run(() => source.LastOrDefault(predicate), cancellationToken);
    }

    /// <summary>
    /// Asynchronously returns the single element or default of the sequence.
    /// </summary>
    public static async Task<T?> SingleOrDefaultAsync<T>(
        this IQueryable<T> source,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (source.Provider is IGraphQueryProvider graphProvider)
        {
            return await graphProvider.ExecuteAsync<T?>(
                Expression.Call(
                    null,
                    ((Func<IQueryable<T>, T?>)QueryableAsyncExtensionsMarkers.SingleOrDefaultAsyncMarker).Method,
                    source.Expression),
                cancellationToken);
        }

        return await Task.Run(() => source.SingleOrDefault(), cancellationToken);
    }

    /// <summary>
    /// Asynchronously returns the single element or default that matches the predicate.
    /// </summary>
    public static async Task<T?> SingleOrDefaultAsync<T>(
        this IQueryable<T> source,
        Expression<Func<T, bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(predicate);

        if (source.Provider is IGraphQueryProvider graphProvider)
        {
            return await graphProvider.ExecuteAsync<T?>(
                Expression.Call(
                    null,
                    ((Func<IQueryable<T>, Expression<Func<T, bool>>, T?>)QueryableAsyncExtensionsMarkers.SingleOrDefaultAsyncMarker).Method,
                    source.Expression,
                    predicate),
                cancellationToken);
        }

        return await Task.Run(() => source.SingleOrDefault(predicate), cancellationToken);
    }

    /// <summary>
    /// Asynchronously counts the elements that match the predicate.
    /// </summary>
    public static async Task<int> CountAsync<T>(
        this IQueryable<T> source,
        Expression<Func<T, bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(predicate);

        if (source.Provider is IGraphQueryProvider graphProvider)
        {
            return await graphProvider.ExecuteAsync<int>(
                Expression.Call(
                    null,
                    ((Func<IQueryable<T>, Expression<Func<T, bool>>, int>)QueryableAsyncExtensionsMarkers.CountAsyncMarker).Method,
                    source.Expression,
                    predicate),
                cancellationToken);
        }

        return await Task.Run(() => source.Count(predicate), cancellationToken);
    }

    /// <summary>
    /// Asynchronously counts the elements in the sequence as a long.
    /// </summary>
    public static async Task<long> LongCountAsync<T>(
        this IQueryable<T> source,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (source.Provider is IGraphQueryProvider graphProvider)
        {
            return await graphProvider.ExecuteAsync<long>(
                Expression.Call(
                    null,
                    ((Func<IQueryable<T>, long>)QueryableAsyncExtensionsMarkers.LongCountAsyncMarker).Method,
                    source.Expression),
                cancellationToken);
        }

        return await Task.Run(() => source.LongCount(), cancellationToken);
    }

    /// <summary>
    /// Asynchronously counts the elements that match the predicate as a long.
    /// </summary>
    public static async Task<long> LongCountAsync<T>(
        this IQueryable<T> source,
        Expression<Func<T, bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(predicate);

        if (source.Provider is IGraphQueryProvider graphProvider)
        {
            return await graphProvider.ExecuteAsync<long>(
                Expression.Call(
                    null,
                    ((Func<IQueryable<T>, Expression<Func<T, bool>>, long>)QueryableAsyncExtensionsMarkers.LongCountAsyncMarker).Method,
                    source.Expression,
                    predicate),
                cancellationToken);
        }

        return await Task.Run(() => source.LongCount(predicate), cancellationToken);
    }

    /// <summary>
    /// Asynchronously computes the sum of the sequence.
    /// </summary>
    public static async Task<int> SumAsync(
        this IQueryable<int> source,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (source.Provider is IGraphQueryProvider graphProvider)
        {
            return await graphProvider.ExecuteAsync<int>(
                Expression.Call(
                    null,
                    ((Func<IQueryable<int>, int>)QueryableAsyncExtensionsMarkers.SumAsyncMarker).Method,
                    source.Expression),
                cancellationToken);
        }

        return await Task.Run(() => source.Sum(), cancellationToken);
    }

    /// <summary>
    /// Asynchronously computes the sum of the projected values.
    /// </summary>
    public static async Task<int> SumAsync<T>(
        this IQueryable<T> source,
        Expression<Func<T, int>> selector,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(selector);

        if (source.Provider is IGraphQueryProvider graphProvider)
        {
            return await graphProvider.ExecuteAsync<int>(
                Expression.Call(
                    null,
                    ((Func<IQueryable<T>, Expression<Func<T, int>>, int>)QueryableAsyncExtensionsMarkers.SumAsyncMarker).Method,
                    source.Expression,
                    selector),
                cancellationToken);
        }

        return await Task.Run(() => source.Sum(selector.Compile()), cancellationToken);
    }

    /// <summary>
    /// Asynchronously computes the average of the sequence.
    /// </summary>
    public static async Task<double> AverageAsync(
        this IQueryable<int> source,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (source.Provider is IGraphQueryProvider graphProvider)
        {
            return await graphProvider.ExecuteAsync<double>(
                Expression.Call(
                    null,
                    ((Func<IQueryable<int>, double>)QueryableAsyncExtensionsMarkers.AverageAsyncMarker).Method,
                    source.Expression),
                cancellationToken);
        }

        return await Task.Run(() => source.Average(), cancellationToken);
    }

    /// <summary>
    /// Asynchronously computes the average of the projected values.
    /// </summary>
    public static async Task<double> AverageAsync<T>(
        this IQueryable<T> source,
        Expression<Func<T, int>> selector,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(selector);

        if (source.Provider is IGraphQueryProvider graphProvider)
        {
            return await graphProvider.ExecuteAsync<double>(
                Expression.Call(
                    null,
                    ((Func<IQueryable<T>, Expression<Func<T, int>>, double>)QueryableAsyncExtensionsMarkers.AverageAsyncMarker).Method,
                    source.Expression,
                    selector),
                cancellationToken);
        }

        return await Task.Run(() => source.Average(selector.Compile()), cancellationToken);
    }

    /// <summary>
    /// Asynchronously finds the minimum value in the sequence.
    /// </summary>
    public static async Task<T?> MinAsync<T>(
        this IQueryable<T> source,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (source.Provider is IGraphQueryProvider graphProvider)
        {
            return await graphProvider.ExecuteAsync<T?>(
                Expression.Call(
                    null,
                    ((Func<IQueryable<T>, T?>)QueryableAsyncExtensionsMarkers.MinAsyncMarker).Method,
                    source.Expression),
                cancellationToken);
        }

        return await Task.Run(() => source.Min(), cancellationToken);
    }

    /// <summary>
    /// Asynchronously finds the minimum projected value.
    /// </summary>
    public static async Task<TResult?> MinAsync<T, TResult>(
        this IQueryable<T> source,
        Expression<Func<T, TResult>> selector,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(selector);

        if (source.Provider is IGraphQueryProvider graphProvider)
        {
            return await graphProvider.ExecuteAsync<TResult?>(
                Expression.Call(
                    null,
                    ((Func<IQueryable<T>, Expression<Func<T, TResult>>, TResult?>)QueryableAsyncExtensionsMarkers.MinAsyncMarker).Method,
                    source.Expression,
                    selector),
                cancellationToken);
        }

        return await Task.Run(() => source.Min(selector.Compile()), cancellationToken);
    }

    /// <summary>
    /// Asynchronously finds the maximum value in the sequence.
    /// </summary>
    public static async Task<T?> MaxAsync<T>(
        this IQueryable<T> source,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (source.Provider is IGraphQueryProvider graphProvider)
        {
            return await graphProvider.ExecuteAsync<T?>(
                Expression.Call(
                    null,
                    ((Func<IQueryable<T>, T?>)QueryableAsyncExtensionsMarkers.MaxAsyncMarker).Method,
                    source.Expression),
                cancellationToken);
        }

        return await Task.Run(() => source.Max(), cancellationToken);
    }

    /// <summary>
    /// Asynchronously finds the maximum projected value.
    /// </summary>
    public static async Task<TResult?> MaxAsync<T, TResult>(
        this IQueryable<T> source,
        Expression<Func<T, TResult>> selector,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(selector);

        if (source.Provider is IGraphQueryProvider graphProvider)
        {
            return await graphProvider.ExecuteAsync<TResult?>(
                Expression.Call(
                    null,
                    ((Func<IQueryable<T>, Expression<Func<T, TResult>>, TResult?>)QueryableAsyncExtensionsMarkers.MaxAsyncMarker).Method,
                    source.Expression,
                    selector),
                cancellationToken);
        }

        return await Task.Run(() => source.Max(selector.Compile()), cancellationToken);
    }

    /// <summary>
    /// Asynchronously checks if the sequence contains the specified value.
    /// </summary>
    public static async Task<bool> ContainsAsync<T>(
        this IQueryable<T> source,
        T item,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (source.Provider is IGraphQueryProvider graphProvider)
        {
            return await graphProvider.ExecuteAsync<bool>(
                Expression.Call(
                    null,
                    ((Func<IQueryable<T>, T, bool>)QueryableAsyncExtensionsMarkers.ContainsAsyncMarker).Method,
                    source.Expression,
                    Expression.Constant(item)),
                cancellationToken);
        }

        return await Task.Run(() => source.Contains(item), cancellationToken);
    }

    /// <summary>
    /// Asynchronously returns the element at the specified index.
    /// </summary>
    public static async Task<T> ElementAtAsync<T>(
        this IQueryable<T> source,
        int index,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (source.Provider is IGraphQueryProvider graphProvider)
        {
            return await graphProvider.ExecuteAsync<T>(
                Expression.Call(
                    null,
                    ((Func<IQueryable<T>, int, T>)QueryableAsyncExtensionsMarkers.ElementAtAsyncMarker).Method,
                    source.Expression,
                    Expression.Constant(index)),
                cancellationToken);
        }

        return await Task.Run(() => source.ElementAt(index), cancellationToken);
    }

    /// <summary>
    /// Asynchronously returns the element at the specified index, or default if out of range.
    /// </summary>
    public static async Task<T?> ElementAtOrDefaultAsync<T>(
        this IQueryable<T> source,
        int index,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (source.Provider is IGraphQueryProvider graphProvider)
        {
            return await graphProvider.ExecuteAsync<T?>(
                Expression.Call(
                    null,
                    ((Func<IQueryable<T>, int, T?>)QueryableAsyncExtensionsMarkers.ElementAtOrDefaultAsyncMarker).Method,
                    source.Expression,
                    Expression.Constant(index)),
                cancellationToken);
        }

        return await Task.Run(() => source.ElementAtOrDefault(index), cancellationToken);
    }

    // Sum overloads for all numeric types

    /// <summary>
    /// Asynchronously computes the sum of a sequence of nullable int values.
    /// </summary>
    public static async Task<int?> SumAsync(
        this IQueryable<int?> source,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (source.Provider is IGraphQueryProvider graphProvider)
        {
            return await graphProvider.ExecuteAsync<int?>(
                Expression.Call(
                    null,
                    ((Func<IQueryable<int?>, int?>)QueryableAsyncExtensionsMarkers.SumAsyncMarker).Method,
                    source.Expression),
                cancellationToken);
        }

        return await Task.Run(() => source.Sum(), cancellationToken);
    }

    /// <summary>
    /// Asynchronously computes the sum of a sequence of long values.
    /// </summary>
    public static async Task<long> SumAsync(
        this IQueryable<long> source,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (source.Provider is IGraphQueryProvider graphProvider)
        {
            return await graphProvider.ExecuteAsync<long>(
                Expression.Call(
                    null,
                    ((Func<IQueryable<long>, long>)QueryableAsyncExtensionsMarkers.SumAsyncMarker).Method,
                    source.Expression),
                cancellationToken);
        }

        return await Task.Run(() => source.Sum(), cancellationToken);
    }

    /// <summary>
    /// Asynchronously computes the sum of a sequence of nullable long values.
    /// </summary>
    public static async Task<long?> SumAsync(
        this IQueryable<long?> source,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (source.Provider is IGraphQueryProvider graphProvider)
        {
            return await graphProvider.ExecuteAsync<long?>(
                Expression.Call(
                    null,
                    ((Func<IQueryable<long?>, long?>)QueryableAsyncExtensionsMarkers.SumAsyncMarker).Method,
                    source.Expression),
                cancellationToken);
        }

        return await Task.Run(() => source.Sum(), cancellationToken);
    }

    /// <summary>
    /// Asynchronously computes the sum of a sequence of float values.
    /// </summary>
    public static async Task<float> SumAsync(
        this IQueryable<float> source,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (source.Provider is IGraphQueryProvider graphProvider)
        {
            return await graphProvider.ExecuteAsync<float>(
                Expression.Call(
                    null,
                    ((Func<IQueryable<float>, float>)QueryableAsyncExtensionsMarkers.SumAsyncMarker).Method,
                    source.Expression),
                cancellationToken);
        }

        return await Task.Run(() => source.Sum(), cancellationToken);
    }

    /// <summary>
    /// Asynchronously computes the sum of a sequence of nullable float values.
    /// </summary>
    public static async Task<float?> SumAsync(
        this IQueryable<float?> source,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (source.Provider is IGraphQueryProvider graphProvider)
        {
            return await graphProvider.ExecuteAsync<float?>(
                Expression.Call(
                    null,
                    ((Func<IQueryable<float?>, float?>)QueryableAsyncExtensionsMarkers.SumAsyncMarker).Method,
                    source.Expression),
                cancellationToken);
        }

        return await Task.Run(() => source.Sum(), cancellationToken);
    }

    /// <summary>
    /// Asynchronously computes the sum of a sequence of double values.
    /// </summary>
    public static async Task<double> SumAsync(
        this IQueryable<double> source,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (source.Provider is IGraphQueryProvider graphProvider)
        {
            return await graphProvider.ExecuteAsync<double>(
                Expression.Call(
                    null,
                    ((Func<IQueryable<double>, double>)QueryableAsyncExtensionsMarkers.SumAsyncMarker).Method,
                    source.Expression),
                cancellationToken);
        }

        return await Task.Run(() => source.Sum(), cancellationToken);
    }

    /// <summary>
    /// Asynchronously computes the sum of a sequence of nullable double values.
    /// </summary>
    public static async Task<double?> SumAsync(
        this IQueryable<double?> source,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (source.Provider is IGraphQueryProvider graphProvider)
        {
            return await graphProvider.ExecuteAsync<double?>(
                Expression.Call(
                    null,
                    ((Func<IQueryable<double?>, double?>)QueryableAsyncExtensionsMarkers.SumAsyncMarker).Method,
                    source.Expression),
                cancellationToken);
        }

        return await Task.Run(() => source.Sum(), cancellationToken);
    }

    /// <summary>
    /// Asynchronously computes the sum of a sequence of decimal values.
    /// </summary>
    public static async Task<decimal> SumAsync(
        this IQueryable<decimal> source,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (source.Provider is IGraphQueryProvider graphProvider)
        {
            return await graphProvider.ExecuteAsync<decimal>(
                Expression.Call(
                    null,
                    ((Func<IQueryable<decimal>, decimal>)QueryableAsyncExtensionsMarkers.SumAsyncMarker).Method,
                    source.Expression),
                cancellationToken);
        }

        return await Task.Run(() => source.Sum(), cancellationToken);
    }

    /// <summary>
    /// Asynchronously computes the sum of a sequence of nullable decimal values.
    /// </summary>
    public static async Task<decimal?> SumAsync(
        this IQueryable<decimal?> source,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (source.Provider is IGraphQueryProvider graphProvider)
        {
            return await graphProvider.ExecuteAsync<decimal?>(
                Expression.Call(
                    null,
                    ((Func<IQueryable<decimal?>, decimal?>)QueryableAsyncExtensionsMarkers.SumAsyncMarker).Method,
                    source.Expression),
                cancellationToken);
        }

        return await Task.Run(() => source.Sum(), cancellationToken);
    }

    // Sum overloads with selectors for all numeric types

    /// <summary>
    /// Asynchronously computes the sum of the projected nullable int values.
    /// </summary>
    public static async Task<int?> SumAsync<T>(
        this IQueryable<T> source,
        Expression<Func<T, int?>> selector,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(selector);

        if (source.Provider is IGraphQueryProvider graphProvider)
        {
            return await graphProvider.ExecuteAsync<int?>(
                Expression.Call(
                    null,
                    ((Func<IQueryable<T>, Expression<Func<T, int?>>, int?>)QueryableAsyncExtensionsMarkers.SumAsyncMarker).Method,
                    source.Expression,
                    selector),
                cancellationToken);
        }

        return await Task.Run(() => source.Sum(selector.Compile()), cancellationToken);
    }

    /// <summary>
    /// Asynchronously computes the sum of the projected long values.
    /// </summary>
    public static async Task<long> SumAsync<T>(
        this IQueryable<T> source,
        Expression<Func<T, long>> selector,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(selector);

        if (source.Provider is IGraphQueryProvider graphProvider)
        {
            return await graphProvider.ExecuteAsync<long>(
                Expression.Call(
                    null,
                    ((Func<IQueryable<T>, Expression<Func<T, long>>, long>)QueryableAsyncExtensionsMarkers.SumAsyncMarker).Method,
                    source.Expression,
                    selector),
                cancellationToken);
        }

        return await Task.Run(() => source.Sum(selector.Compile()), cancellationToken);
    }

    /// <summary>
    /// Asynchronously computes the sum of the projected nullable long values.
    /// </summary>
    public static async Task<long?> SumAsync<T>(
        this IQueryable<T> source,
        Expression<Func<T, long?>> selector,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(selector);

        if (source.Provider is IGraphQueryProvider graphProvider)
        {
            return await graphProvider.ExecuteAsync<long?>(
                Expression.Call(
                    null,
                    ((Func<IQueryable<T>, Expression<Func<T, long?>>, long?>)QueryableAsyncExtensionsMarkers.SumAsyncMarker).Method,
                    source.Expression,
                    selector),
                cancellationToken);
        }

        return await Task.Run(() => source.Sum(selector.Compile()), cancellationToken);
    }

    /// <summary>
    /// Asynchronously computes the sum of the projected float values.
    /// </summary>
    public static async Task<float> SumAsync<T>(
        this IQueryable<T> source,
        Expression<Func<T, float>> selector,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(selector);

        if (source.Provider is IGraphQueryProvider graphProvider)
        {
            return await graphProvider.ExecuteAsync<float>(
                Expression.Call(
                    null,
                    ((Func<IQueryable<T>, Expression<Func<T, float>>, float>)QueryableAsyncExtensionsMarkers.SumAsyncMarker).Method,
                    source.Expression,
                    selector),
                cancellationToken);
        }

        return await Task.Run(() => source.Sum(selector.Compile()), cancellationToken);
    }

    /// <summary>
    /// Asynchronously computes the sum of the projected nullable float values.
    /// </summary>
    public static async Task<float?> SumAsync<T>(
        this IQueryable<T> source,
        Expression<Func<T, float?>> selector,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(selector);

        if (source.Provider is IGraphQueryProvider graphProvider)
        {
            return await graphProvider.ExecuteAsync<float?>(
                Expression.Call(
                    null,
                    ((Func<IQueryable<T>, Expression<Func<T, float?>>, float?>)QueryableAsyncExtensionsMarkers.SumAsyncMarker).Method,
                    source.Expression,
                    selector),
                cancellationToken);
        }

        return await Task.Run(() => source.Sum(selector.Compile()), cancellationToken);
    }

    /// <summary>
    /// Asynchronously computes the sum of the projected double values.
    /// </summary>
    public static async Task<double> SumAsync<T>(
        this IQueryable<T> source,
        Expression<Func<T, double>> selector,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(selector);

        if (source.Provider is IGraphQueryProvider graphProvider)
        {
            return await graphProvider.ExecuteAsync<double>(
                Expression.Call(
                    null,
                    ((Func<IQueryable<T>, Expression<Func<T, double>>, double>)QueryableAsyncExtensionsMarkers.SumAsyncMarker).Method,
                    source.Expression,
                    selector),
                cancellationToken);
        }

        return await Task.Run(() => source.Sum(selector.Compile()), cancellationToken);
    }

    /// <summary>
    /// Asynchronously computes the sum of the projected nullable double values.
    /// </summary>
    public static async Task<double?> SumAsync<T>(
        this IQueryable<T> source,
        Expression<Func<T, double?>> selector,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(selector);

        if (source.Provider is IGraphQueryProvider graphProvider)
        {
            return await graphProvider.ExecuteAsync<double?>(
                Expression.Call(
                    null,
                    ((Func<IQueryable<T>, Expression<Func<T, double?>>, double?>)QueryableAsyncExtensionsMarkers.SumAsyncMarker).Method,
                    source.Expression,
                    selector),
                cancellationToken);
        }

        return await Task.Run(() => source.Sum(selector.Compile()), cancellationToken);
    }

    /// <summary>
    /// Asynchronously computes the sum of the projected decimal values.
    /// </summary>
    public static async Task<decimal> SumAsync<T>(
        this IQueryable<T> source,
        Expression<Func<T, decimal>> selector,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(selector);

        if (source.Provider is IGraphQueryProvider graphProvider)
        {
            return await graphProvider.ExecuteAsync<decimal>(
                Expression.Call(
                    null,
                    ((Func<IQueryable<T>, Expression<Func<T, decimal>>, decimal>)QueryableAsyncExtensionsMarkers.SumAsyncMarker).Method,
                    source.Expression,
                    selector),
                cancellationToken);
        }

        return await Task.Run(() => source.Sum(selector.Compile()), cancellationToken);
    }

    /// <summary>
    /// Asynchronously computes the sum of the projected nullable decimal values.
    /// </summary>
    public static async Task<decimal?> SumAsync<T>(
        this IQueryable<T> source,
        Expression<Func<T, decimal?>> selector,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(selector);

        if (source.Provider is IGraphQueryProvider graphProvider)
        {
            return await graphProvider.ExecuteAsync<decimal?>(
                Expression.Call(
                    null,
                    ((Func<IQueryable<T>, Expression<Func<T, decimal?>>, decimal?>)QueryableAsyncExtensionsMarkers.SumAsyncMarker).Method,
                    source.Expression,
                    selector),
                cancellationToken);
        }

        return await Task.Run(() => source.Sum(selector.Compile()), cancellationToken);
    }

    // Average overloads for all numeric types

    /// <summary>
    /// Asynchronously computes the average of a sequence of nullable int values.
    /// </summary>
    public static async Task<double?> AverageAsync(
        this IQueryable<int?> source,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (source.Provider is IGraphQueryProvider graphProvider)
        {
            return await graphProvider.ExecuteAsync<double?>(
                Expression.Call(
                    null,
                    ((Func<IQueryable<int?>, double?>)QueryableAsyncExtensionsMarkers.AverageAsyncMarker).Method,
                    source.Expression),
                cancellationToken);
        }

        return await Task.Run(() => source.Average(), cancellationToken);
    }

    /// <summary>
    /// Asynchronously computes the average of a sequence of long values.
    /// </summary>
    public static async Task<double> AverageAsync(
        this IQueryable<long> source,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (source.Provider is IGraphQueryProvider graphProvider)
        {
            return await graphProvider.ExecuteAsync<double>(
                Expression.Call(
                    null,
                    ((Func<IQueryable<long>, double>)QueryableAsyncExtensionsMarkers.AverageAsyncMarker).Method,
                    source.Expression),
                cancellationToken);
        }

        return await Task.Run(() => source.Average(), cancellationToken);
    }

    /// <summary>
    /// Asynchronously computes the average of a sequence of nullable long values.
    /// </summary>
    public static async Task<double?> AverageAsync(
        this IQueryable<long?> source,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (source.Provider is IGraphQueryProvider graphProvider)
        {
            return await graphProvider.ExecuteAsync<double?>(
                Expression.Call(
                    null,
                    ((Func<IQueryable<long?>, double?>)QueryableAsyncExtensionsMarkers.AverageAsyncMarker).Method,
                    source.Expression),
                cancellationToken);
        }

        return await Task.Run(() => source.Average(), cancellationToken);
    }

    /// <summary>
    /// Asynchronously computes the average of a sequence of float values.
    /// </summary>
    public static async Task<float> AverageAsync(
        this IQueryable<float> source,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (source.Provider is IGraphQueryProvider graphProvider)
        {
            return await graphProvider.ExecuteAsync<float>(
                Expression.Call(
                    null,
                    ((Func<IQueryable<float>, float>)QueryableAsyncExtensionsMarkers.AverageAsyncMarker).Method,
                    source.Expression),
                cancellationToken);
        }

        return await Task.Run(() => source.Average(), cancellationToken);
    }

    /// <summary>
    /// Asynchronously computes the average of a sequence of nullable float values.
    /// </summary>
    public static async Task<float?> AverageAsync(
        this IQueryable<float?> source,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (source.Provider is IGraphQueryProvider graphProvider)
        {
            return await graphProvider.ExecuteAsync<float?>(
                Expression.Call(
                    null,
                    ((Func<IQueryable<float?>, float?>)QueryableAsyncExtensionsMarkers.AverageAsyncMarker).Method,
                    source.Expression),
                cancellationToken);
        }

        return await Task.Run(() => source.Average(), cancellationToken);
    }

    /// <summary>
    /// Asynchronously computes the average of a sequence of double values.
    /// </summary>
    public static async Task<double> AverageAsync(
        this IQueryable<double> source,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (source.Provider is IGraphQueryProvider graphProvider)
        {
            return await graphProvider.ExecuteAsync<double>(
                Expression.Call(
                    null,
                    ((Func<IQueryable<double>, double>)QueryableAsyncExtensionsMarkers.AverageAsyncMarker).Method,
                    source.Expression),
                cancellationToken);
        }

        return await Task.Run(() => source.Average(), cancellationToken);
    }

    /// <summary>
    /// Asynchronously computes the average of a sequence of nullable double values.
    /// </summary>
    public static async Task<double?> AverageAsync(
        this IQueryable<double?> source,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (source.Provider is IGraphQueryProvider graphProvider)
        {
            return await graphProvider.ExecuteAsync<double?>(
                Expression.Call(
                    null,
                    ((Func<IQueryable<double?>, double?>)QueryableAsyncExtensionsMarkers.AverageAsyncMarker).Method,
                    source.Expression),
                cancellationToken);
        }

        return await Task.Run(() => source.Average(), cancellationToken);
    }

    /// <summary>
    /// Asynchronously computes the average of a sequence of decimal values.
    /// </summary>
    public static async Task<decimal> AverageAsync(
        this IQueryable<decimal> source,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (source.Provider is IGraphQueryProvider graphProvider)
        {
            return await graphProvider.ExecuteAsync<decimal>(
                Expression.Call(
                    null,
                    ((Func<IQueryable<decimal>, decimal>)QueryableAsyncExtensionsMarkers.AverageAsyncMarker).Method,
                    source.Expression),
                cancellationToken);
        }

        return await Task.Run(() => source.Average(), cancellationToken);
    }

    /// <summary>
    /// Asynchronously computes the average of a sequence of nullable decimal values.
    /// </summary>
    public static async Task<decimal?> AverageAsync(
        this IQueryable<decimal?> source,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (source.Provider is IGraphQueryProvider graphProvider)
        {
            return await graphProvider.ExecuteAsync<decimal?>(
                Expression.Call(
                    null,
                    ((Func<IQueryable<decimal?>, decimal?>)QueryableAsyncExtensionsMarkers.AverageAsyncMarker).Method,
                    source.Expression),
                cancellationToken);
        }

        return await Task.Run(() => source.Average(), cancellationToken);
    }

    // Average overloads with selectors for all numeric types

    /// <summary>
    /// Asynchronously computes the average of the projected nullable int values.
    /// </summary>
    public static async Task<double?> AverageAsync<T>(
        this IQueryable<T> source,
        Expression<Func<T, int?>> selector,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(selector);

        if (source.Provider is IGraphQueryProvider graphProvider)
        {
            return await graphProvider.ExecuteAsync<double?>(
                Expression.Call(
                    null,
                    ((Func<IQueryable<T>, Expression<Func<T, int?>>, double?>)QueryableAsyncExtensionsMarkers.AverageAsyncMarker).Method,
                    source.Expression,
                    selector),
                cancellationToken);
        }

        return await Task.Run(() => source.Average(selector.Compile()), cancellationToken);
    }

    /// <summary>
    /// Asynchronously computes the average of the projected long values.
    /// </summary>
    public static async Task<double> AverageAsync<T>(
        this IQueryable<T> source,
        Expression<Func<T, long>> selector,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(selector);

        if (source.Provider is IGraphQueryProvider graphProvider)
        {
            return await graphProvider.ExecuteAsync<double>(
                Expression.Call(
                    null,
                    ((Func<IQueryable<T>, Expression<Func<T, long>>, double>)QueryableAsyncExtensionsMarkers.AverageAsyncMarker).Method,
                    source.Expression,
                    selector),
                cancellationToken);
        }

        return await Task.Run(() => source.Average(selector.Compile()), cancellationToken);
    }

    /// <summary>
    /// Asynchronously computes the average of the projected nullable long values.
    /// </summary>
    public static async Task<double?> AverageAsync<T>(
        this IQueryable<T> source,
        Expression<Func<T, long?>> selector,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(selector);

        if (source.Provider is IGraphQueryProvider graphProvider)
        {
            return await graphProvider.ExecuteAsync<double?>(
                Expression.Call(
                    null,
                    ((Func<IQueryable<T>, Expression<Func<T, long?>>, double?>)QueryableAsyncExtensionsMarkers.AverageAsyncMarker).Method,
                    source.Expression,
                    selector),
                cancellationToken);
        }

        return await Task.Run(() => source.Average(selector.Compile()), cancellationToken);
    }

    /// <summary>
    /// Asynchronously computes the average of the projected float values.
    /// </summary>
    public static async Task<float> AverageAsync<T>(
        this IQueryable<T> source,
        Expression<Func<T, float>> selector,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(selector);

        if (source.Provider is IGraphQueryProvider graphProvider)
        {
            return await graphProvider.ExecuteAsync<float>(
                Expression.Call(
                    null,
                    ((Func<IQueryable<T>, Expression<Func<T, float>>, float>)QueryableAsyncExtensionsMarkers.AverageAsyncMarker).Method,
                    source.Expression,
                    selector),
                cancellationToken);
        }

        return await Task.Run(() => source.Average(selector.Compile()), cancellationToken);
    }

    /// <summary>
    /// Asynchronously computes the average of the projected nullable float values.
    /// </summary>
    public static async Task<float?> AverageAsync<T>(
        this IQueryable<T> source,
        Expression<Func<T, float?>> selector,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(selector);

        if (source.Provider is IGraphQueryProvider graphProvider)
        {
            return await graphProvider.ExecuteAsync<float?>(
                Expression.Call(
                    null,
                    ((Func<IQueryable<T>, Expression<Func<T, float?>>, float?>)QueryableAsyncExtensionsMarkers.AverageAsyncMarker).Method,
                    source.Expression,
                    selector),
                cancellationToken);
        }

        return await Task.Run(() => source.Average(selector.Compile()), cancellationToken);
    }

    /// <summary>
    /// Asynchronously computes the average of the projected double values.
    /// </summary>
    public static async Task<double> AverageAsync<T>(
        this IQueryable<T> source,
        Expression<Func<T, double>> selector,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(selector);

        if (source.Provider is IGraphQueryProvider graphProvider)
        {
            return await graphProvider.ExecuteAsync<double>(
                Expression.Call(
                    null,
                    ((Func<IQueryable<T>, Expression<Func<T, double>>, double>)QueryableAsyncExtensionsMarkers.AverageAsyncMarker).Method,
                    source.Expression,
                    selector),
                cancellationToken);
        }

        return await Task.Run(() => source.Average(selector.Compile()), cancellationToken);
    }

    /// <summary>
    /// Asynchronously computes the average of the projected nullable double values.
    /// </summary>
    public static async Task<double?> AverageAsync<T>(
        this IQueryable<T> source,
        Expression<Func<T, double?>> selector,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(selector);

        if (source.Provider is IGraphQueryProvider graphProvider)
        {
            return await graphProvider.ExecuteAsync<double?>(
                Expression.Call(
                    null,
                    ((Func<IQueryable<T>, Expression<Func<T, double?>>, double?>)QueryableAsyncExtensionsMarkers.AverageAsyncMarker).Method,
                    source.Expression,
                    selector),
                cancellationToken);
        }

        return await Task.Run(() => source.Average(selector.Compile()), cancellationToken);
    }

    /// <summary>
    /// Asynchronously computes the average of the projected decimal values.
    /// </summary>
    public static async Task<decimal> AverageAsync<T>(
        this IQueryable<T> source,
        Expression<Func<T, decimal>> selector,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(selector);

        if (source.Provider is IGraphQueryProvider graphProvider)
        {
            return await graphProvider.ExecuteAsync<decimal>(
                Expression.Call(
                    null,
                    ((Func<IQueryable<T>, Expression<Func<T, decimal>>, decimal>)QueryableAsyncExtensionsMarkers.AverageAsyncMarker).Method,
                    source.Expression,
                    selector),
                cancellationToken);
        }

        return await Task.Run(() => source.Average(selector.Compile()), cancellationToken);
    }

    /// <summary>
    /// Asynchronously computes the average of the projected nullable decimal values.
    /// </summary>
    public static async Task<decimal?> AverageAsync<T>(
        this IQueryable<T> source,
        Expression<Func<T, decimal?>> selector,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(selector);

        if (source.Provider is IGraphQueryProvider graphProvider)
        {
            return await graphProvider.ExecuteAsync<decimal?>(
                Expression.Call(
                    null,
                    ((Func<IQueryable<T>, Expression<Func<T, decimal?>>, decimal?>)QueryableAsyncExtensionsMarkers.AverageAsyncMarker).Method,
                    source.Expression,
                    selector),
                cancellationToken);
        }

        return await Task.Run(() => source.Average(selector.Compile()), cancellationToken);
    }
}