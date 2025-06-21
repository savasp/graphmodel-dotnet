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
using static Cvoya.Graph.Model.ExtensionUtils;

/// <summary>
/// Extension methods for <see cref="IGraphQueryable{T}"/> that provide LINQ functionality.
/// </summary>
public static class GraphQueryableExtensions
{
    /// <summary>
    /// Filters a sequence of values based on a predicate.
    /// </summary>
    public static IGraphQueryable<T> Where<T>(
        this IGraphQueryable<T> source,
        Expression<Func<T, bool>> predicate)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(predicate);

        var methodInfo = GetGenericExtensionMethod(
            typeof(GraphQueryableExtensions),
            nameof(Where),
            1, // T
            2  // source, predicate
        ).MakeGenericMethod(typeof(T));
        var expression = Expression.Call(
            null,
            methodInfo,
            source.Expression,
            predicate);

        return source.Provider.CreateQuery<T>(expression);
    }

    /// <summary>
    /// Projects each element of a sequence into a new form.
    /// </summary>
    public static IGraphQueryable<TResult> Select<TSource, TResult>(
        this IGraphQueryable<TSource> source,
        Expression<Func<TSource, TResult>> selector)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(selector);

        var methodInfo = GetGenericExtensionMethod(
            typeof(GraphQueryableExtensions),
            nameof(Select),
            2, // TSource, TResult
            2  // source, selector
        ).MakeGenericMethod(typeof(TSource), typeof(TResult));
        var expression = Expression.Call(
            null,
            methodInfo,
            source.Expression,
            selector);

        return source.Provider.CreateQuery<TResult>(expression);
    }

    /// <summary>
    /// Projects each element of a sequence into a new form with the element's index.
    /// </summary>
    public static IGraphQueryable<TResult> Select<TSource, TResult>(
        this IGraphQueryable<TSource> source,
        Expression<Func<TSource, int, TResult>> selector)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(selector);

        var methodInfo = ExtensionUtils.GetGenericExtensionMethod(
            typeof(GraphQueryableExtensions),
            nameof(Select),
            2,
            2).MakeGenericMethod(typeof(TSource), typeof(TResult));
        var expression = Expression.Call(
            null,
            methodInfo,
            source.Expression,
            selector);

        return source.Provider.CreateQuery<TResult>(expression);
    }

    /// <summary>
    /// Projects each element to an IEnumerable and flattens the resulting sequences into one sequence.
    /// </summary>
    public static IGraphQueryable<TResult> SelectMany<TSource, TResult>(
        this IGraphQueryable<TSource> source,
        Expression<Func<TSource, IEnumerable<TResult>>> selector)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(selector);

        var methodInfo = GetGenericExtensionMethod(
            typeof(GraphQueryableExtensions),
            nameof(SelectMany),
            2, // TSource, TResult
            2  // source, selector
        ).MakeGenericMethod(typeof(TSource), typeof(TResult));
        var expression = Expression.Call(
            null,
            methodInfo,
            source.Expression,
            selector);

        return source.Provider.CreateQuery<TResult>(expression);
    }

    /// <summary>
    /// Projects each element to an IEnumerable, flattens the resulting sequences into one sequence, and invokes a result selector function on each element.
    /// </summary>
    public static IGraphQueryable<TResult> SelectMany<TSource, TCollection, TResult>(
        this IGraphQueryable<TSource> source,
        Expression<Func<TSource, IEnumerable<TCollection>>> collectionSelector,
        Expression<Func<TSource, TCollection, TResult>> resultSelector)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(collectionSelector);
        ArgumentNullException.ThrowIfNull(resultSelector);

        var methodInfo = GetGenericExtensionMethod(
            typeof(GraphQueryableExtensions),
            nameof(SelectMany),
            3, // TSource, TCollection, TResult
            3  // source, collectionSelector, resultSelector
        ).MakeGenericMethod(typeof(TSource), typeof(TCollection), typeof(TResult));
        var expression = Expression.Call(
            null,
            methodInfo,
            source.Expression,
            collectionSelector,
            resultSelector);

        return source.Provider.CreateQuery<TResult>(expression);
    }

    /// <summary>
    /// Sorts the elements of a sequence in ascending order according to a key.
    /// </summary>
    public static IOrderedGraphQueryable<TSource> OrderBy<TSource, TKey>(
        this IGraphQueryable<TSource> source,
        Expression<Func<TSource, TKey>> keySelector)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(keySelector);

        var methodInfo = new Func<IQueryable<TSource>, Expression<Func<TSource, TKey>>, IOrderedQueryable<TSource>>(Queryable.OrderBy).Method.MakeGenericMethod(typeof(TSource), typeof(TKey));
        var expression = Expression.Call(
            null,
            methodInfo,
            source.Expression,
            keySelector);

        return (IOrderedGraphQueryable<TSource>)source.Provider.CreateQuery<TSource>(expression);
    }

    /// <summary>
    /// Sorts the elements of a sequence in descending order according to a key.
    /// </summary>
    public static IOrderedGraphQueryable<TSource> OrderByDescending<TSource, TKey>(
        this IGraphQueryable<TSource> source,
        Expression<Func<TSource, TKey>> keySelector)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(keySelector);

        var methodInfo = new Func<IQueryable<TSource>, Expression<Func<TSource, TKey>>, IOrderedQueryable<TSource>>(Queryable.OrderByDescending).Method.MakeGenericMethod(typeof(TSource), typeof(TKey));
        var expression = Expression.Call(
            null,
            methodInfo,
            source.Expression,
            keySelector);

        return (IOrderedGraphQueryable<TSource>)source.Provider.CreateQuery<TSource>(expression);
    }

    /// <summary>
    /// Bypasses a specified number of elements in a sequence and then returns the remaining elements.
    /// </summary>
    public static IGraphQueryable<TSource> Skip<TSource>(
        this IGraphQueryable<TSource> source,
        int count)
    {
        ArgumentNullException.ThrowIfNull(source);

        var methodInfo = new Func<IQueryable<TSource>, int, IQueryable<TSource>>(Queryable.Skip).Method.MakeGenericMethod(typeof(TSource));
        var expression = Expression.Call(
            null,
            methodInfo,
            source.Expression,
            Expression.Constant(count));

        return source.Provider.CreateQuery<TSource>(expression);
    }

    /// <summary>
    /// Returns a specified number of contiguous elements from the start of a sequence.
    /// </summary>
    public static IGraphQueryable<TSource> Take<TSource>(
        this IGraphQueryable<TSource> source,
        int count)
    {
        ArgumentNullException.ThrowIfNull(source);

        var methodInfo = new Func<IQueryable<TSource>, int, IQueryable<TSource>>(Queryable.Take).Method.MakeGenericMethod(typeof(TSource));
        var expression = Expression.Call(
            null,
            methodInfo,
            source.Expression,
            Expression.Constant(count));

        return source.Provider.CreateQuery<TSource>(expression);
    }

    /// <summary>
    /// Returns distinct elements from a sequence.
    /// </summary>
    public static IGraphQueryable<TSource> Distinct<TSource>(
        this IGraphQueryable<TSource> source)
    {
        ArgumentNullException.ThrowIfNull(source);

        var methodInfo = new Func<IQueryable<TSource>, IQueryable<TSource>>(Queryable.Distinct).Method.MakeGenericMethod(typeof(TSource));
        var expression = Expression.Call(
            null,
            methodInfo,
            source.Expression);

        return source.Provider.CreateQuery<TSource>(expression);
    }

    /// <summary>
    /// Groups the elements of a sequence according to a specified key selector function.
    /// </summary>
    public static IGraphQueryable<IGrouping<TKey, TSource>> GroupBy<TSource, TKey>(
        this IGraphQueryable<TSource> source,
        Expression<Func<TSource, TKey>> keySelector)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(keySelector);

        var methodInfo = ExtensionUtils.GetGenericExtensionMethod(
            typeof(GraphQueryableExtensions),
            nameof(GroupBy),
            2,
            2).MakeGenericMethod(typeof(TSource), typeof(TKey));

        var expression = Expression.Call(
            null,
            methodInfo,
            source.Expression,
            keySelector);

        return source.Provider.CreateQuery<IGrouping<TKey, TSource>>(expression);
    }

    /// <summary>
    /// Attaches a transaction to the <see cref="IGraphQueryable{TSource}"/>.
    /// </summary>
    public static IGraphQueryable<TSource> WithTransaction<TSource>(
        this IGraphQueryable<TSource> source,
        IGraphTransaction transaction)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(transaction);

        // Build a method call expression for WithTransaction
        var methodInfo = ExtensionUtils.GetGenericExtensionMethod(
            typeof(GraphQueryableExtensions),
            nameof(WithTransaction),
            1,
            2).MakeGenericMethod(typeof(TSource));

        var expression = Expression.Call(
            null,
            methodInfo,
            source.Expression,
            Expression.Constant(transaction));

        return source.Provider.CreateQuery<TSource>(expression);
    }

    /// <summary>
    /// Specifies the traversal depth for graph operations.
    /// </summary>
    public static IGraphQueryable<TSource> WithDepth<TSource>(
        this IGraphQueryable<TSource> source,
        int maxDepth)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (maxDepth < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxDepth), "Depth must be non-negative.");
        }

        var methodInfo = ExtensionUtils.GetGenericExtensionMethod(
            typeof(GraphQueryableExtensions),
            nameof(WithDepth),
            1,
            2).MakeGenericMethod(typeof(TSource));

        var expression = Expression.Call(
            null,
            methodInfo,
            source.Expression,
            Expression.Constant(maxDepth));

        return source.Provider.CreateQuery<TSource>(expression);
    }

    /// <summary>
    /// Specifies the traversal depth range for graph operations.
    /// </summary>
    public static IGraphQueryable<TSource> WithDepth<TSource>(
        this IGraphQueryable<TSource> source,
        int minDepth,
        int maxDepth)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (minDepth < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(minDepth), "Minimum depth must be non-negative.");
        }

        if (maxDepth < minDepth)
        {
            throw new ArgumentOutOfRangeException(nameof(maxDepth), "Maximum depth must be greater than or equal to minimum depth.");
        }

        var methodInfo = ExtensionUtils.GetGenericExtensionMethod(
            typeof(GraphQueryableExtensions),
            nameof(WithDepth),
            1,
            3).MakeGenericMethod(typeof(TSource));

        var expression = Expression.Call(
            null,
            methodInfo,
            source.Expression,
            Expression.Constant(minDepth),
            Expression.Constant(maxDepth));

        return source.Provider.CreateQuery<TSource>(expression);
    }

    /// <summary>
    /// Specifies the direction of traversal for graph operations.
    /// </summary>
    public static IGraphQueryable<TSource> Direction<TSource>(
        this IGraphQueryable<TSource> source,
        GraphTraversalDirection direction)
    {
        ArgumentNullException.ThrowIfNull(source);

        var methodInfo = ExtensionUtils.GetGenericExtensionMethod(
            typeof(GraphQueryableExtensions),
            nameof(Direction),
            1,
            2).MakeGenericMethod(typeof(TSource));

        var expression = Expression.Call(
            null,
            methodInfo,
            source.Expression,
            Expression.Constant(direction));

        return source.Provider.CreateQuery<TSource>(expression);
    }
}