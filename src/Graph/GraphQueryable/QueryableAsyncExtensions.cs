// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph;

using System.Linq.Expressions;
using System.Numerics;

/// <summary>
/// Extension methods for async execution of IQueryable queries in the graph context.
/// </summary>
public static class QueryableAsyncExtensions
{
    /// <summary>
    /// Asynchronously executes the query and returns the results as a list.
    /// </summary>
    public static async Task<List<T>> ToListAsync<T>(
        this IGraphQueryable<T> source,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        // Check if the provider supports async execution
        if (source.Provider is IGraphQueryProvider graphProvider)
        {
            return await graphProvider.ExecuteAsync<List<T>>(
                Expression.Call(
                    null,
                    ((Func<IQueryable<T>, List<T>>)QueryTerminals.ToListAsyncMarker).Method, source.Expression),
                cancellationToken).ConfigureAwait(false);
        }

        // Fallback to sync execution
        return await Task.Run(() => source.ToList(), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Asynchronously executes the query and returns the results as a list.
    /// </summary>
    /// <remarks>
    /// This <see cref="IQueryable{T}"/>-typed overload exists alongside the
    /// <see cref="IGraphQueryable{T}"/>-typed one above for LINQ operators that degrade the
    /// static type away from <see cref="IGraphQueryable{T}"/> (e.g. the standard
    /// <c>Queryable.Join</c>, which has no
    /// graph-typed-chain-preserving override): the compile-time result type of such a call is
    /// <see cref="IQueryable{TResult}"/> even though the runtime instance is still a graph
    /// queryable. Overload resolution prefers the more specific <see cref="IGraphQueryable{T}"/>
    /// overload whenever both apply, so this is purely a fallback and does not conflict with it.
    /// </remarks>
    public static async Task<List<T>> ToListAsync<T>(
        this IQueryable<T> source,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (source is IGraphQueryable<T> graphQueryable)
        {
            return await graphQueryable.ToListAsync(cancellationToken).ConfigureAwait(false);
        }

        if (source.Provider is IGraphQueryProvider graphProvider)
        {
            return await graphProvider.ExecuteAsync<List<T>>(
                Expression.Call(
                    null,
                    ((Func<IQueryable<T>, List<T>>)QueryTerminals.ToListAsyncMarker).Method, source.Expression),
                cancellationToken).ConfigureAwait(false);
        }

        return await Task.Run(() => source.ToList(), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Asynchronously executes the query and returns the first element, or default if empty.
    /// </summary>
    public static async Task<T?> FirstOrDefaultAsync<T>(
        this IGraphQueryable<T> source,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (source.Provider is IGraphQueryProvider graphProvider)
        {
            return await graphProvider.ExecuteAsync<T?>(
                Expression.Call(
                    null,
                    ((Func<IQueryable<T>, T?>)QueryTerminals.FirstOrDefaultAsyncMarker).Method,
                    source.Expression),
                cancellationToken).ConfigureAwait(false);
        }

        return await Task.Run(() => source.FirstOrDefault(), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Asynchronously counts the elements in the sequence.
    /// </summary>
    public static async Task<int> CountAsync<T>(
        this IGraphQueryable<T> source,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (source.Provider is IGraphQueryProvider graphProvider)
        {
            return await graphProvider.ExecuteAsync<int>(
                Expression.Call(
                    null,
                    ((Func<IQueryable<T>, int>)QueryTerminals.CountAsyncMarker).Method,
                    source.Expression),
                cancellationToken).ConfigureAwait(false);
        }

        return await Task.Run(() => source.Count(), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Asynchronously checks if any elements exist in the sequence.
    /// </summary>
    public static async Task<bool> AnyAsync<T>(
        this IGraphQueryable<T> source,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (source.Provider is IGraphQueryProvider graphProvider)
        {
            return await graphProvider.ExecuteAsync<bool>(
                Expression.Call(
                    null,
                    ((Func<IQueryable<T>, bool>)QueryTerminals.AnyAsyncMarker).Method,
                    source.Expression),
                cancellationToken).ConfigureAwait(false);
        }

        return await Task.Run(() => source.Any(), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Asynchronously checks if any elements match the specified predicate.
    /// </summary>
    /// <param name="source">The source sequence.</param>
    /// <param name="predicate">The predicate to apply to the elements.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains true if any elements match the predicate; otherwise, false.</returns>
    public static async Task<bool> AnyAsync<T>(
        this IGraphQueryable<T> source,
        Expression<Func<T, bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(predicate);

        return await source.Where(predicate).AnyAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Asynchronously checks if all elements in the sequence match the specified predicate.
    /// </summary>
    public static async Task<bool> AllAsync<T>(
        this IGraphQueryable<T> source,
        Expression<Func<T, bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(predicate);

        if (source.Provider is IGraphQueryProvider graphProvider)
        {
            return await graphProvider.ExecuteAsync<bool>(
                Expression.Call(
                    null,
                    ((Func<IQueryable<T>, Expression<Func<T, bool>>, bool>)QueryTerminals.AllAsyncMarker).Method,
                    source.Expression,
                    predicate),
                cancellationToken).ConfigureAwait(false);
        }

        return await Task.Run(() => source.All(predicate), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Asynchronously returns a single element of the sequence that satisfies a specified condition.
    /// If no such element exists, an exception is thrown.
    /// If more than one such element exists, an exception is thrown.
    /// </summary>
    /// <returns></returns>
    public static async Task<T> SingleAsync<T>(
        this IGraphQueryable<T> source,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (source.Provider is IGraphQueryProvider graphProvider)
        {
            return await graphProvider.ExecuteAsync<T>(
                Expression.Call(
                    null,
                    ((Func<IQueryable<T>, T>)QueryTerminals.SingleAsyncMarker).Method,
                    source.Expression),
                cancellationToken).ConfigureAwait(false);
        }

        return await Task.Run(() => source.Single(), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Asynchronously returns a single element of the sequence that satisfies a specified condition.
    /// If no such element exists, an exception is thrown.
    /// If more than one such element exists, an exception is thrown.
    /// </summary>
    /// <returns></returns>
    public static async Task<T> SingleAsync<T>(
        this IGraphQueryable<T> source,
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
                    ((Func<IQueryable<T>, Expression<Func<T, bool>>, T>)QueryTerminals.SingleAsyncMarker).Method,
                    source.Expression,
                    predicate),
                cancellationToken).ConfigureAwait(false);
        }

        return await Task.Run(() => source.Single(predicate), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Asynchronously returns the last element of the sequence.
    /// If the sequence is empty, an exception is thrown.
    /// </summary>
    public static async Task<T> LastAsync<T>(
        this IGraphQueryable<T> source,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (source.Provider is IGraphQueryProvider graphProvider)
        {
            return await graphProvider.ExecuteAsync<T>(
                Expression.Call(
                    null,
                    ((Func<IQueryable<T>, T>)QueryTerminals.LastAsyncMarker).Method,
                    source.Expression),
                cancellationToken).ConfigureAwait(false);
        }

        return await Task.Run(() => source.Last(), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Asynchronously returns the first element of the sequence.
    /// </summary>
    public static async Task<T> FirstAsync<T>(
        this IGraphQueryable<T> source,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (source.Provider is IGraphQueryProvider graphProvider)
        {
            return await graphProvider.ExecuteAsync<T>(
                Expression.Call(
                    null,
                    ((Func<IQueryable<T>, T>)QueryTerminals.FirstAsyncMarker).Method,
                    source.Expression),
                cancellationToken).ConfigureAwait(false);
        }

        return await Task.Run(() => source.First(), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Asynchronously executes the query and returns the results as an array.
    /// </summary>
    public static async Task<T[]> ToArrayAsync<T>(
        this IGraphQueryable<T> source,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (source.Provider is IGraphQueryProvider graphProvider)
        {
            return await graphProvider.ExecuteAsync<T[]>(
                Expression.Call(
                    null,
                    ((Func<IQueryable<T>, T[]>)QueryTerminals.ToArrayAsyncMarker).Method,
                    source.Expression),
                cancellationToken).ConfigureAwait(false);
        }

        return await Task.Run(() => source.ToArray(), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Asynchronously executes the query and returns the results as a dictionary.
    /// </summary>
    public static async Task<Dictionary<TKey, TElement>> ToDictionaryAsync<TSource, TKey, TElement>(
        this IGraphQueryable<TSource> source,
        Expression<Func<TSource, TKey>> keySelector,
        Expression<Func<TSource, TElement>> elementSelector,
        CancellationToken cancellationToken = default) where TKey : notnull
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(keySelector);
        ArgumentNullException.ThrowIfNull(elementSelector);

        var items = await source.ToListAsync(cancellationToken).ConfigureAwait(false);
        return items.ToDictionary(keySelector.Compile(), elementSelector.Compile());
    }

    /// <summary>
    /// Asynchronously executes the query and returns the results as a dictionary.
    /// </summary>
    public static async Task<Dictionary<TKey, TSource>> ToDictionaryAsync<TSource, TKey>(
        this IGraphQueryable<TSource> source,
        Expression<Func<TSource, TKey>> keySelector,
        CancellationToken cancellationToken = default) where TKey : notnull
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(keySelector);

        var items = await source.ToListAsync(cancellationToken).ConfigureAwait(false);
        return items.ToDictionary(keySelector.Compile());
    }

    /// <summary>
    /// Asynchronously executes the query and returns the results as a lookup.
    /// </summary>
    public static async Task<ILookup<TKey, TElement>> ToLookupAsync<TSource, TKey, TElement>(
        this IGraphQueryable<TSource> source,
        Expression<Func<TSource, TKey>> keySelector,
        Expression<Func<TSource, TElement>> elementSelector,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(keySelector);
        ArgumentNullException.ThrowIfNull(elementSelector);

        var items = await source.ToListAsync(cancellationToken).ConfigureAwait(false);
        return items.ToLookup(keySelector.Compile(), elementSelector.Compile());
    }

    /// <summary>
    /// Asynchronously executes the query and returns the results as a lookup.
    /// </summary>
    public static async Task<ILookup<TKey, TSource>> ToLookupAsync<TSource, TKey>(
        this IGraphQueryable<TSource> source,
        Expression<Func<TSource, TKey>> keySelector,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(keySelector);

        var items = await source.ToListAsync(cancellationToken).ConfigureAwait(false);
        return items.ToLookup(keySelector.Compile());
    }

    /// <summary>
    /// Asynchronously returns the first element of the sequence that matches the predicate.
    /// </summary>
    public static async Task<T> FirstAsync<T>(
        this IGraphQueryable<T> source,
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
                    ((Func<IQueryable<T>, Expression<Func<T, bool>>, T>)QueryTerminals.FirstAsyncMarker).Method,
                    source.Expression,
                    predicate),
                cancellationToken).ConfigureAwait(false);
        }

        return await Task.Run(() => source.First(predicate), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Asynchronously returns the first element or default that matches the predicate.
    /// </summary>
    public static async Task<T?> FirstOrDefaultAsync<T>(
        this IGraphQueryable<T> source,
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
                    ((Func<IQueryable<T>, Expression<Func<T, bool>>, T?>)QueryTerminals.FirstOrDefaultAsyncMarker).Method,
                    source.Expression,
                    predicate),
                cancellationToken).ConfigureAwait(false);
        }

        return await Task.Run(() => source.FirstOrDefault(predicate), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Asynchronously returns the last element of the sequence that matches the predicate.
    /// </summary>
    public static async Task<T> LastAsync<T>(
        this IGraphQueryable<T> source,
        Expression<Func<T, bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(predicate);

        return await source.Where(predicate).LastAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Asynchronously returns the last element or default of the sequence.
    /// </summary>
    public static async Task<T?> LastOrDefaultAsync<T>(
        this IGraphQueryable<T> source,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (source.Provider is IGraphQueryProvider graphProvider)
        {
            return await graphProvider.ExecuteAsync<T?>(
                Expression.Call(
                    null,
                    ((Func<IQueryable<T>, T?>)QueryTerminals.LastOrDefaultAsyncMarker).Method,
                    source.Expression),
                cancellationToken).ConfigureAwait(false);
        }

        return await Task.Run(() => source.LastOrDefault(), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Asynchronously returns the last element or default that matches the predicate.
    /// </summary>
    public static async Task<T?> LastOrDefaultAsync<T>(
        this IGraphQueryable<T> source,
        Expression<Func<T, bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(predicate);

        return await source.Where(predicate).LastOrDefaultAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Asynchronously returns the single element or default of the sequence.
    /// </summary>
    public static async Task<T?> SingleOrDefaultAsync<T>(
        this IGraphQueryable<T> source,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (source.Provider is IGraphQueryProvider graphProvider)
        {
            return await graphProvider.ExecuteAsync<T?>(
                Expression.Call(
                    null,
                    ((Func<IQueryable<T>, T?>)QueryTerminals.SingleOrDefaultAsyncMarker).Method,
                    source.Expression),
                cancellationToken).ConfigureAwait(false);
        }

        return await Task.Run(() => source.SingleOrDefault(), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Asynchronously returns the single element or default that matches the predicate.
    /// </summary>
    public static async Task<T?> SingleOrDefaultAsync<T>(
        this IGraphQueryable<T> source,
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
                    ((Func<IQueryable<T>, Expression<Func<T, bool>>, T?>)QueryTerminals.SingleOrDefaultAsyncMarker).Method,
                    source.Expression,
                    predicate),
                cancellationToken).ConfigureAwait(false);
        }

        return await Task.Run(() => source.SingleOrDefault(predicate), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Asynchronously counts the elements that match the predicate.
    /// </summary>
    public static async Task<int> CountAsync<T>(
        this IGraphQueryable<T> source,
        Expression<Func<T, bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(predicate);

        return await source.Where(predicate).CountAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Asynchronously counts the elements in the sequence as a long.
    /// </summary>
    public static async Task<long> LongCountAsync<T>(
        this IGraphQueryable<T> source,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (source.Provider is IGraphQueryProvider graphProvider)
        {
            return await graphProvider.ExecuteAsync<long>(
                Expression.Call(
                    null,
                    ((Func<IQueryable<T>, long>)QueryTerminals.LongCountAsyncMarker).Method,
                    source.Expression),
                cancellationToken).ConfigureAwait(false);
        }

        return await Task.Run(() => source.LongCount(), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Asynchronously counts the elements that match the predicate as a long.
    /// </summary>
    public static async Task<long> LongCountAsync<T>(
        this IGraphQueryable<T> source,
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
                    ((Func<IQueryable<T>, Expression<Func<T, bool>>, long>)QueryTerminals.LongCountAsyncMarker).Method,
                    source.Expression,
                    predicate),
                cancellationToken).ConfigureAwait(false);
        }

        return await Task.Run(() => source.LongCount(predicate), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Asynchronously checks if the sequence contains the specified value.
    /// </summary>
    public static async Task<bool> ContainsAsync<T>(
        this IGraphQueryable<T> source,
        T item,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (source.Provider is IGraphQueryProvider graphProvider)
        {
            return await graphProvider.ExecuteAsync<bool>(
                Expression.Call(
                    null,
                    ((Func<IQueryable<T>, T, bool>)QueryTerminals.ContainsAsyncMarker).Method,
                    source.Expression,
                    Expression.Constant(item, typeof(T))),
                cancellationToken).ConfigureAwait(false);
        }

        return await Task.Run(() => source.Contains(item), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Asynchronously returns the element at the specified index.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="index"/> is negative.
    /// </exception>
    public static async Task<T> ElementAtAsync<T>(
        this IGraphQueryable<T> source,
        int index,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentOutOfRangeException.ThrowIfNegative(index);

        if (source.Provider is IGraphQueryProvider graphProvider)
        {
            return await graphProvider.ExecuteAsync<T>(
                Expression.Call(
                    null,
                    ((Func<IQueryable<T>, int, T>)QueryTerminals.ElementAtAsyncMarker).Method,
                    source.Expression,
                    Expression.Constant(index)),
                cancellationToken).ConfigureAwait(false);
        }

        return await Task.Run(() => source.ElementAt(index), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Asynchronously returns the element at the specified index, or default if out of range.
    /// </summary>
    /// <remarks>A negative <paramref name="index"/> returns the default value without executing the query.</remarks>
    public static async Task<T?> ElementAtOrDefaultAsync<T>(
        this IGraphQueryable<T> source,
        int index,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        cancellationToken.ThrowIfCancellationRequested();

        if (index < 0)
        {
            return default;
        }

        if (source.Provider is IGraphQueryProvider graphProvider)
        {
            return await graphProvider.ExecuteAsync<T?>(
                Expression.Call(
                    null,
                    ((Func<IQueryable<T>, int, T?>)QueryTerminals.ElementAtOrDefaultAsyncMarker).Method,
                    source.Expression,
                    Expression.Constant(index)),
                cancellationToken).ConfigureAwait(false);
        }

        return await Task.Run(() => source.ElementAtOrDefault(index), cancellationToken).ConfigureAwait(false);
    }

    #region Aggregation Methods (Sum/Average - collapsed to a single generic definition per shape)

    /// <summary>
    /// Asynchronously computes the sum of the sequence.
    /// </summary>
    public static async Task<TResult> SumAsync<TResult>(
        this IGraphQueryable<TResult> source,
        CancellationToken cancellationToken = default)
        where TResult : INumberBase<TResult>
    {
        ArgumentNullException.ThrowIfNull(source);

        if (source.Provider is IGraphQueryProvider graphProvider)
        {
            var method = ((Func<IQueryable<TResult>, TResult>)QueryTerminals.SumAsyncMarker).Method;
            return await graphProvider.ExecuteAsync<TResult>(
                Expression.Call(null, method, source.Expression),
                cancellationToken).ConfigureAwait(false);
        }

        return await Task.Run(() => Enumerable.Aggregate(source, TResult.Zero, (acc, x) => acc + x), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Asynchronously computes the sum of the projected values.
    /// </summary>
    public static async Task<TResult> SumAsync<TSource, TResult>(
        this IGraphQueryable<TSource> source,
        Expression<Func<TSource, TResult>> selector,
        CancellationToken cancellationToken = default)
        where TResult : INumberBase<TResult>
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(selector);

        if (source.Provider is IGraphQueryProvider graphProvider)
        {
            var method = ((Func<IQueryable<TSource>, Expression<Func<TSource, TResult>>, TResult>)QueryTerminals.SumAsyncMarker).Method;
            return await graphProvider.ExecuteAsync<TResult>(
                Expression.Call(null, method, source.Expression, selector),
                cancellationToken).ConfigureAwait(false);
        }

        var compiled = selector.Compile();
        return await Task.Run(() => Enumerable.Aggregate(source, TResult.Zero, (acc, x) => acc + compiled(x)), cancellationToken).ConfigureAwait(false);
    }

    // Average result types follow standard LINQ (Enumerable/Queryable.Average): int and long
    // average to double, float to float, double to double, decimal to decimal, and every nullable
    // input yields the corresponding nullable result. Explicit overloads (rather than a single
    // INumberBase<T> definition returning the input type) are what make the correct result type
    // visible at compile time and keep providers from truncating an integer average. Non-nullable
    // empty sequences throw InvalidOperationException; nullable empty or all-null sequences return
    // null.

    /// <summary>Asynchronously computes the average of a sequence of <see cref="int"/> values, as a <see cref="double"/>. Throws <see cref="InvalidOperationException"/> if the sequence is empty.</summary>
    public static Task<double> AverageAsync(this IGraphQueryable<int> source, CancellationToken cancellationToken = default) =>
        AverageAsync<int, double>(source, values => values.Average(), cancellationToken);

    /// <summary>Asynchronously computes the average of a sequence of nullable <see cref="int"/> values, as a nullable <see cref="double"/>. Returns <see langword="null"/> if the sequence is empty or contains only nulls.</summary>
    public static Task<double?> AverageAsync(this IGraphQueryable<int?> source, CancellationToken cancellationToken = default) =>
        AverageAsync<int?, double?>(source, values => values.Average(), cancellationToken);

    /// <summary>Asynchronously computes the average of a sequence of <see cref="long"/> values, as a <see cref="double"/>. Throws <see cref="InvalidOperationException"/> if the sequence is empty.</summary>
    public static Task<double> AverageAsync(this IGraphQueryable<long> source, CancellationToken cancellationToken = default) =>
        AverageAsync<long, double>(source, values => values.Average(), cancellationToken);

    /// <summary>Asynchronously computes the average of a sequence of nullable <see cref="long"/> values, as a nullable <see cref="double"/>. Returns <see langword="null"/> if the sequence is empty or contains only nulls.</summary>
    public static Task<double?> AverageAsync(this IGraphQueryable<long?> source, CancellationToken cancellationToken = default) =>
        AverageAsync<long?, double?>(source, values => values.Average(), cancellationToken);

    /// <summary>Asynchronously computes the average of a sequence of <see cref="float"/> values, as a <see cref="float"/>. Throws <see cref="InvalidOperationException"/> if the sequence is empty.</summary>
    public static Task<float> AverageAsync(this IGraphQueryable<float> source, CancellationToken cancellationToken = default) =>
        AverageAsync<float, float>(source, values => values.Average(), cancellationToken);

    /// <summary>Asynchronously computes the average of a sequence of nullable <see cref="float"/> values, as a nullable <see cref="float"/>. Returns <see langword="null"/> if the sequence is empty or contains only nulls.</summary>
    public static Task<float?> AverageAsync(this IGraphQueryable<float?> source, CancellationToken cancellationToken = default) =>
        AverageAsync<float?, float?>(source, values => values.Average(), cancellationToken);

    /// <summary>Asynchronously computes the average of a sequence of <see cref="double"/> values. Throws <see cref="InvalidOperationException"/> if the sequence is empty.</summary>
    public static Task<double> AverageAsync(this IGraphQueryable<double> source, CancellationToken cancellationToken = default) =>
        AverageAsync<double, double>(source, values => values.Average(), cancellationToken);

    /// <summary>Asynchronously computes the average of a sequence of nullable <see cref="double"/> values. Returns <see langword="null"/> if the sequence is empty or contains only nulls.</summary>
    public static Task<double?> AverageAsync(this IGraphQueryable<double?> source, CancellationToken cancellationToken = default) =>
        AverageAsync<double?, double?>(source, values => values.Average(), cancellationToken);

    /// <summary>Asynchronously computes the average of a sequence of <see cref="decimal"/> values. Throws <see cref="InvalidOperationException"/> if the sequence is empty.</summary>
    public static Task<decimal> AverageAsync(this IGraphQueryable<decimal> source, CancellationToken cancellationToken = default) =>
        AverageAsync<decimal, decimal>(source, values => values.Average(), cancellationToken);

    /// <summary>Asynchronously computes the average of a sequence of nullable <see cref="decimal"/> values. Returns <see langword="null"/> if the sequence is empty or contains only nulls.</summary>
    public static Task<decimal?> AverageAsync(this IGraphQueryable<decimal?> source, CancellationToken cancellationToken = default) =>
        AverageAsync<decimal?, decimal?>(source, values => values.Average(), cancellationToken);

    /// <summary>Asynchronously computes the average of the projected <see cref="int"/> values, as a <see cref="double"/>. Throws <see cref="InvalidOperationException"/> if the sequence is empty.</summary>
    public static Task<double> AverageAsync<TSource>(this IGraphQueryable<TSource> source, Expression<Func<TSource, int>> selector, CancellationToken cancellationToken = default) =>
        AverageAsync<TSource, int, double>(source, selector, (values, project) => values.Average(project), cancellationToken);

    /// <summary>Asynchronously computes the average of the projected nullable <see cref="int"/> values, as a nullable <see cref="double"/>. Returns <see langword="null"/> if the sequence is empty or projects only nulls.</summary>
    public static Task<double?> AverageAsync<TSource>(this IGraphQueryable<TSource> source, Expression<Func<TSource, int?>> selector, CancellationToken cancellationToken = default) =>
        AverageAsync<TSource, int?, double?>(source, selector, (values, project) => values.Average(project), cancellationToken);

    /// <summary>Asynchronously computes the average of the projected <see cref="long"/> values, as a <see cref="double"/>. Throws <see cref="InvalidOperationException"/> if the sequence is empty.</summary>
    public static Task<double> AverageAsync<TSource>(this IGraphQueryable<TSource> source, Expression<Func<TSource, long>> selector, CancellationToken cancellationToken = default) =>
        AverageAsync<TSource, long, double>(source, selector, (values, project) => values.Average(project), cancellationToken);

    /// <summary>Asynchronously computes the average of the projected nullable <see cref="long"/> values, as a nullable <see cref="double"/>. Returns <see langword="null"/> if the sequence is empty or projects only nulls.</summary>
    public static Task<double?> AverageAsync<TSource>(this IGraphQueryable<TSource> source, Expression<Func<TSource, long?>> selector, CancellationToken cancellationToken = default) =>
        AverageAsync<TSource, long?, double?>(source, selector, (values, project) => values.Average(project), cancellationToken);

    /// <summary>Asynchronously computes the average of the projected <see cref="float"/> values, as a <see cref="float"/>. Throws <see cref="InvalidOperationException"/> if the sequence is empty.</summary>
    public static Task<float> AverageAsync<TSource>(this IGraphQueryable<TSource> source, Expression<Func<TSource, float>> selector, CancellationToken cancellationToken = default) =>
        AverageAsync<TSource, float, float>(source, selector, (values, project) => values.Average(project), cancellationToken);

    /// <summary>Asynchronously computes the average of the projected nullable <see cref="float"/> values, as a nullable <see cref="float"/>. Returns <see langword="null"/> if the sequence is empty or projects only nulls.</summary>
    public static Task<float?> AverageAsync<TSource>(this IGraphQueryable<TSource> source, Expression<Func<TSource, float?>> selector, CancellationToken cancellationToken = default) =>
        AverageAsync<TSource, float?, float?>(source, selector, (values, project) => values.Average(project), cancellationToken);

    /// <summary>Asynchronously computes the average of the projected <see cref="double"/> values. Throws <see cref="InvalidOperationException"/> if the sequence is empty.</summary>
    public static Task<double> AverageAsync<TSource>(this IGraphQueryable<TSource> source, Expression<Func<TSource, double>> selector, CancellationToken cancellationToken = default) =>
        AverageAsync<TSource, double, double>(source, selector, (values, project) => values.Average(project), cancellationToken);

    /// <summary>Asynchronously computes the average of the projected nullable <see cref="double"/> values. Returns <see langword="null"/> if the sequence is empty or projects only nulls.</summary>
    public static Task<double?> AverageAsync<TSource>(this IGraphQueryable<TSource> source, Expression<Func<TSource, double?>> selector, CancellationToken cancellationToken = default) =>
        AverageAsync<TSource, double?, double?>(source, selector, (values, project) => values.Average(project), cancellationToken);

    /// <summary>Asynchronously computes the average of the projected <see cref="decimal"/> values. Throws <see cref="InvalidOperationException"/> if the sequence is empty.</summary>
    public static Task<decimal> AverageAsync<TSource>(this IGraphQueryable<TSource> source, Expression<Func<TSource, decimal>> selector, CancellationToken cancellationToken = default) =>
        AverageAsync<TSource, decimal, decimal>(source, selector, (values, project) => values.Average(project), cancellationToken);

    /// <summary>Asynchronously computes the average of the projected nullable <see cref="decimal"/> values. Returns <see langword="null"/> if the sequence is empty or projects only nulls.</summary>
    public static Task<decimal?> AverageAsync<TSource>(this IGraphQueryable<TSource> source, Expression<Func<TSource, decimal?>> selector, CancellationToken cancellationToken = default) =>
        AverageAsync<TSource, decimal?, decimal?>(source, selector, (values, project) => values.Average(project), cancellationToken);

    private static async Task<TResult> AverageAsync<TInput, TResult>(
        IGraphQueryable<TInput> source,
        Func<IEnumerable<TInput>, TResult> fallbackAverage,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);

        // Use IQueryable.Provider rather than IGraphQueryable<T>.Provider so custom queryables that
        // expose a non-graph LINQ provider can take the documented LINQ-to-objects fallback.
        if (((IQueryable)source).Provider is IGraphQueryProvider graphProvider)
        {
            var method = ((Func<IQueryable<TInput>, TInput>)QueryTerminals.AverageAsyncMarker<TInput, TInput>).Method;
            return await graphProvider.ExecuteAsync<TResult>(
                Expression.Call(null, method, source.Expression),
                cancellationToken).ConfigureAwait(false);
        }

        return await Task.Run(() => fallbackAverage(source), cancellationToken).ConfigureAwait(false);
    }

    private static async Task<TResult> AverageAsync<TSource, TInput, TResult>(
        IGraphQueryable<TSource> source,
        Expression<Func<TSource, TInput>> selector,
        Func<IEnumerable<TSource>, Func<TSource, TInput>, TResult> fallbackAverage,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(selector);

        // See the source-overload helper above: the base IQueryable provider makes this fallback
        // reachable without changing the strongly typed graph-provider surface.
        if (((IQueryable)source).Provider is IGraphQueryProvider graphProvider)
        {
            var method = ((Func<IQueryable<TSource>, Expression<Func<TSource, TInput>>, TInput>)QueryTerminals.AverageAsyncMarker<TSource, TInput>).Method;
            return await graphProvider.ExecuteAsync<TResult>(
                Expression.Call(null, method, source.Expression, selector),
                cancellationToken).ConfigureAwait(false);
        }

        var compiled = selector.Compile();
        return await Task.Run(() => fallbackAverage(source, compiled), cancellationToken).ConfigureAwait(false);
    }

    #endregion

    /// <summary>
    /// Asynchronously finds the minimum value in the sequence.
    /// </summary>
    public static async Task<T?> MinAsync<T>(
        this IGraphQueryable<T> source,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (source.Provider is IGraphQueryProvider graphProvider)
        {
            var expression = Expression.Call(
                null,
                ((Func<IQueryable<T>, T?>)QueryTerminals.MinAsyncMarker).Method,
                source.Expression);

            return await ExecuteMinMaxAggregateAsync<T>(graphProvider, expression, cancellationToken).ConfigureAwait(false);
        }

        return await Task.Run(() => source.Min(), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Asynchronously finds the minimum projected value.
    /// </summary>
    public static async Task<TResult?> MinAsync<T, TResult>(
        this IGraphQueryable<T> source,
        Expression<Func<T, TResult>> selector,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(selector);

        if (source.Provider is IGraphQueryProvider graphProvider)
        {
            var expression = Expression.Call(
                null,
                ((Func<IQueryable<T>, Expression<Func<T, TResult>>, TResult?>)QueryTerminals.MinAsyncMarker).Method,
                source.Expression,
                selector);

            return await ExecuteMinMaxAggregateAsync<TResult>(graphProvider, expression, cancellationToken).ConfigureAwait(false);
        }

        return await Task.Run(() => source.Min(selector.Compile()), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Asynchronously finds the maximum value in the sequence.
    /// </summary>
    public static async Task<T?> MaxAsync<T>(
        this IGraphQueryable<T> source,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (source.Provider is IGraphQueryProvider graphProvider)
        {
            var expression = Expression.Call(
                null,
                ((Func<IQueryable<T>, T?>)QueryTerminals.MaxAsyncMarker).Method,
                source.Expression);

            return await ExecuteMinMaxAggregateAsync<T>(graphProvider, expression, cancellationToken).ConfigureAwait(false);
        }

        return await Task.Run(() => source.Max(), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Asynchronously finds the maximum projected value.
    /// </summary>
    public static async Task<TResult?> MaxAsync<T, TResult>(
        this IGraphQueryable<T> source,
        Expression<Func<T, TResult>> selector,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(selector);

        if (source.Provider is IGraphQueryProvider graphProvider)
        {
            var expression = Expression.Call(
                null,
                ((Func<IQueryable<T>, Expression<Func<T, TResult>>, TResult?>)QueryTerminals.MaxAsyncMarker).Method,
                source.Expression,
                selector);

            return await ExecuteMinMaxAggregateAsync<TResult>(graphProvider, expression, cancellationToken).ConfigureAwait(false);
        }

        return await Task.Run(() => source.Max(selector.Compile()), cancellationToken).ConfigureAwait(false);
    }

    private static async Task<TResult?> ExecuteMinMaxAggregateAsync<TResult>(
        IGraphQueryProvider graphProvider,
        Expression expression,
        CancellationToken cancellationToken)
    {
        if (RequiresNonEmptySequence(typeof(TResult)))
        {
            return await graphProvider.ExecuteAsync<TResult>(expression, cancellationToken).ConfigureAwait(false);
        }

        return await graphProvider.ExecuteAsync<TResult?>(expression, cancellationToken).ConfigureAwait(false);
    }

    private static bool RequiresNonEmptySequence(Type type)
    {
        return type.IsValueType && Nullable.GetUnderlyingType(type) is null;
    }
}
