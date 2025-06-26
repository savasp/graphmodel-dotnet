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
/// Extension methods that preserve <see cref="IGraphNodeQueryable{T}"/> interface through LINQ operations.
/// </summary>
public static class GraphNodeQueryableExtensions
{
    /// <summary>
    /// Filters nodes based on a predicate while preserving the IGraphNodeQueryable interface.
    /// </summary>
    public static IGraphNodeQueryable<T> Where<T>(
        this IGraphNodeQueryable<T> source,
        Expression<Func<T, bool>> predicate)
        where T : INode
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(predicate);

        var methodInfo = GetGenericExtensionMethod(
            typeof(GraphNodeQueryableExtensions),
            nameof(Where),
            1, // T
            2  // source, predicate
        ).MakeGenericMethod(typeof(T));

        var expression = Expression.Call(
            null,
            methodInfo,
            source.Expression,
            predicate);

        return source.Provider.CreateNodeQuery<T>(expression);
    }

    /// <summary>
    /// Projects each node into a new form while preserving the IGraphNodeQueryable interface.
    /// </summary>
    public static IGraphNodeQueryable<TResult> Select<T, TResult>(
        this IGraphNodeQueryable<T> source,
        Expression<Func<T, TResult>> selector)
        where T : INode
        where TResult : INode
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(selector);

        var methodInfo = GetGenericExtensionMethod(
            typeof(GraphNodeQueryableExtensions),
            nameof(Select),
            2, // T, TResult
            2  // source, selector
        ).MakeGenericMethod(typeof(T), typeof(TResult));

        var expression = Expression.Call(
            null,
            methodInfo,
            source.Expression,
            selector);

        return source.Provider.CreateNodeQuery<TResult>(expression);
    }

    /// <summary>
    /// Projects each node into a new form with index while preserving the IGraphNodeQueryable interface.
    /// </summary>
    public static IGraphNodeQueryable<TResult> Select<T, TResult>(
        this IGraphNodeQueryable<T> source,
        Expression<Func<T, int, TResult>> selector)
        where T : INode
        where TResult : INode
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(selector);

        var methodInfo = GetGenericExtensionMethod(
            typeof(GraphNodeQueryableExtensions),
            nameof(Select),
            2, // T, TResult
            2  // source, selector
        ).MakeGenericMethod(typeof(T), typeof(TResult));

        var expression = Expression.Call(
            null,
            methodInfo,
            source.Expression,
            selector);

        return source.Provider.CreateNodeQuery<TResult>(expression);
    }

    /// <summary>
    /// Sorts nodes in ascending order according to a key.
    /// </summary>
    public static IOrderedGraphNodeQueryable<T> OrderBy<T, TKey>(
        this IGraphNodeQueryable<T> source,
        Expression<Func<T, TKey>> keySelector)
        where T : INode
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(keySelector);

        var methodInfo = GetGenericExtensionMethod(
            typeof(GraphNodeQueryableExtensions),
            nameof(OrderBy),
            2, // T, TKey
            2  // source, keySelector
        ).MakeGenericMethod(typeof(T), typeof(TKey));

        var expression = Expression.Call(
            null,
            methodInfo,
            source.Expression,
            keySelector);

        return (IOrderedGraphNodeQueryable<T>)source.Provider.CreateNodeQuery<T>(expression);
    }

    /// <summary>
    /// Sorts nodes in descending order according to a key.
    /// </summary>
    public static IOrderedGraphNodeQueryable<T> OrderByDescending<T, TKey>(
        this IGraphNodeQueryable<T> source,
        Expression<Func<T, TKey>> keySelector)
        where T : INode
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(keySelector);

        var methodInfo = GetGenericExtensionMethod(
            typeof(GraphNodeQueryableExtensions),
            nameof(OrderByDescending),
            2, // T, TKey
            2  // source, keySelector
        ).MakeGenericMethod(typeof(T), typeof(TKey));

        var expression = Expression.Call(
            null,
            methodInfo,
            source.Expression,
            keySelector);

        return (IOrderedGraphNodeQueryable<T>)source.Provider.CreateNodeQuery<T>(expression);
    }

    /// <summary>
    /// Performs a subsequent ordering of nodes in ascending order.
    /// </summary>
    public static IOrderedGraphNodeQueryable<T> ThenBy<T, TKey>(
        this IOrderedGraphNodeQueryable<T> source,
        Expression<Func<T, TKey>> keySelector)
        where T : INode
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(keySelector);

        var methodInfo = GetGenericExtensionMethod(
            typeof(GraphNodeQueryableExtensions),
            nameof(ThenBy),
            2, // T, TKey
            2  // source, keySelector
        ).MakeGenericMethod(typeof(T), typeof(TKey));

        var expression = Expression.Call(
            null,
            methodInfo,
            source.Expression,
            keySelector);

        return (IOrderedGraphNodeQueryable<T>)source.Provider.CreateNodeQuery<T>(expression);
    }

    /// <summary>
    /// Performs a subsequent ordering of nodes in descending order.
    /// </summary>
    public static IOrderedGraphNodeQueryable<T> ThenByDescending<T, TKey>(
        this IOrderedGraphNodeQueryable<T> source,
        Expression<Func<T, TKey>> keySelector)
        where T : INode
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(keySelector);

        var methodInfo = GetGenericExtensionMethod(
            typeof(GraphNodeQueryableExtensions),
            nameof(ThenByDescending),
            2, // T, TKey
            2  // source, keySelector
        ).MakeGenericMethod(typeof(T), typeof(TKey));

        var expression = Expression.Call(
            null,
            methodInfo,
            source.Expression,
            keySelector);

        return (IOrderedGraphNodeQueryable<T>)source.Provider.CreateNodeQuery<T>(expression);
    }

    /// <summary>
    /// Bypasses a specified number of nodes and returns the remaining nodes.
    /// </summary>
    public static IGraphNodeQueryable<T> Skip<T>(
        this IGraphNodeQueryable<T> source,
        int count)
        where T : INode
    {
        ArgumentNullException.ThrowIfNull(source);

        var methodInfo = GetGenericExtensionMethod(
            typeof(GraphNodeQueryableExtensions),
            nameof(Skip),
            1, // T
            2  // source, count
        ).MakeGenericMethod(typeof(T));

        var expression = Expression.Call(
            null,
            methodInfo,
            source.Expression,
            Expression.Constant(count));

        return source.Provider.CreateNodeQuery<T>(expression);
    }

    /// <summary>
    /// Returns a specified number of contiguous nodes from the start.
    /// </summary>
    public static IGraphNodeQueryable<T> Take<T>(
        this IGraphNodeQueryable<T> source,
        int count)
        where T : INode
    {
        ArgumentNullException.ThrowIfNull(source);

        var methodInfo = GetGenericExtensionMethod(
            typeof(GraphNodeQueryableExtensions),
            nameof(Take),
            1, // T
            2  // source, count
        ).MakeGenericMethod(typeof(T));

        var expression = Expression.Call(
            null,
            methodInfo,
            source.Expression,
            Expression.Constant(count));

        return source.Provider.CreateNodeQuery<T>(expression);
    }

    /// <summary>
    /// Bypasses nodes while the specified condition is true and returns the remaining nodes.
    /// </summary>
    public static IGraphNodeQueryable<T> SkipWhile<T>(
        this IGraphNodeQueryable<T> source,
        Expression<Func<T, bool>> predicate)
        where T : INode
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(predicate);

        var methodInfo = GetGenericExtensionMethod(
            typeof(GraphNodeQueryableExtensions),
            nameof(SkipWhile),
            1, // T
            2  // source, predicate
        ).MakeGenericMethod(typeof(T));

        var expression = Expression.Call(
            null,
            methodInfo,
            source.Expression,
            predicate);

        return source.Provider.CreateNodeQuery<T>(expression);
    }

    /// <summary>
    /// Bypasses nodes while the specified condition with index is true and returns the remaining nodes.
    /// </summary>
    public static IGraphNodeQueryable<T> SkipWhile<T>(
        this IGraphNodeQueryable<T> source,
        Expression<Func<T, int, bool>> predicate)
        where T : INode
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(predicate);

        var methodInfo = GetGenericExtensionMethod(
            typeof(GraphNodeQueryableExtensions),
            nameof(SkipWhile),
            1, // T
            2  // source, predicate
        ).MakeGenericMethod(typeof(T));

        var expression = Expression.Call(
            null,
            methodInfo,
            source.Expression,
            predicate);

        return source.Provider.CreateNodeQuery<T>(expression);
    }

    /// <summary>
    /// Returns nodes while the specified condition is true.
    /// </summary>
    public static IGraphNodeQueryable<T> TakeWhile<T>(
        this IGraphNodeQueryable<T> source,
        Expression<Func<T, bool>> predicate)
        where T : INode
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(predicate);

        var methodInfo = GetGenericExtensionMethod(
            typeof(GraphNodeQueryableExtensions),
            nameof(TakeWhile),
            1, // T
            2  // source, predicate
        ).MakeGenericMethod(typeof(T));

        var expression = Expression.Call(
            null,
            methodInfo,
            source.Expression,
            predicate);

        return source.Provider.CreateNodeQuery<T>(expression);
    }

    /// <summary>
    /// Returns nodes while the specified condition with index is true.
    /// </summary>
    public static IGraphNodeQueryable<T> TakeWhile<T>(
        this IGraphNodeQueryable<T> source,
        Expression<Func<T, int, bool>> predicate)
        where T : INode
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(predicate);

        var methodInfo = GetGenericExtensionMethod(
            typeof(GraphNodeQueryableExtensions),
            nameof(TakeWhile),
            1, // T
            2  // source, predicate
        ).MakeGenericMethod(typeof(T));

        var expression = Expression.Call(
            null,
            methodInfo,
            source.Expression,
            predicate);

        return source.Provider.CreateNodeQuery<T>(expression);
    }

    /// <summary>
    /// Returns distinct nodes from a sequence.
    /// </summary>
    public static IGraphNodeQueryable<T> Distinct<T>(
        this IGraphNodeQueryable<T> source)
        where T : INode
    {
        ArgumentNullException.ThrowIfNull(source);

        var methodInfo = GetGenericExtensionMethod(
            typeof(GraphNodeQueryableExtensions),
            nameof(Distinct),
            1, // T
            1  // source
        ).MakeGenericMethod(typeof(T));

        var expression = Expression.Call(
            null,
            methodInfo,
            source.Expression);

        return source.Provider.CreateNodeQuery<T>(expression);
    }

    /// <summary>
    /// Returns distinct nodes from a sequence using a specified comparer.
    /// </summary>
    public static IGraphNodeQueryable<T> Distinct<T>(
        this IGraphNodeQueryable<T> source,
        IEqualityComparer<T> comparer)
        where T : INode
    {
        ArgumentNullException.ThrowIfNull(source);

        var methodInfo = GetGenericExtensionMethod(
            typeof(GraphNodeQueryableExtensions),
            nameof(Distinct),
            1, // T
            2  // source, comparer
        ).MakeGenericMethod(typeof(T));

        var expression = Expression.Call(
            null,
            methodInfo,
            source.Expression,
            Expression.Constant(comparer));

        return source.Provider.CreateNodeQuery<T>(expression);
    }

    /// <summary>
    /// Groups nodes according to a specified key selector function.
    /// </summary>
    public static IGraphQueryable<IGrouping<TKey, T>> GroupBy<T, TKey>(
        this IGraphNodeQueryable<T> source,
        Expression<Func<T, TKey>> keySelector)
        where T : INode
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(keySelector);

        var methodInfo = GetGenericExtensionMethod(
            typeof(GraphNodeQueryableExtensions),
            nameof(GroupBy),
            2, // T, TKey
            2  // source, keySelector
        ).MakeGenericMethod(typeof(T), typeof(TKey));

        var expression = Expression.Call(
            null,
            methodInfo,
            source.Expression,
            keySelector);

        return source.Provider.CreateQuery<IGrouping<TKey, T>>(expression);
    }

    /// <summary>
    /// Groups nodes and projects each group using the specified functions.
    /// </summary>
    public static IGraphQueryable<TResult> GroupBy<T, TKey, TResult>(
        this IGraphNodeQueryable<T> source,
        Expression<Func<T, TKey>> keySelector,
        Expression<Func<TKey, IEnumerable<T>, TResult>> resultSelector)
        where T : INode
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(keySelector);
        ArgumentNullException.ThrowIfNull(resultSelector);

        var methodInfo = GetGenericExtensionMethod(
            typeof(GraphNodeQueryableExtensions),
            nameof(GroupBy),
            3, // T, TKey, TResult
            3  // source, keySelector, resultSelector
        ).MakeGenericMethod(typeof(T), typeof(TKey), typeof(TResult));

        var expression = Expression.Call(
            null,
            methodInfo,
            source.Expression,
            keySelector,
            resultSelector);

        return source.Provider.CreateQuery<TResult>(expression);
    }

    /// <summary>
    /// Concatenates two node sequences.
    /// </summary>
    public static IGraphNodeQueryable<T> Concat<T>(
        this IGraphNodeQueryable<T> first,
        IEnumerable<T> second)
        where T : INode
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);

        var methodInfo = GetGenericExtensionMethod(
            typeof(GraphNodeQueryableExtensions),
            nameof(Concat),
            1, // T
            2  // first, second
        ).MakeGenericMethod(typeof(T));

        var expression = Expression.Call(
            null,
            methodInfo,
            first.Expression,
            Expression.Constant(second));

        return first.Provider.CreateNodeQuery<T>(expression);
    }

    /// <summary>
    /// Produces the set union of two node sequences.
    /// </summary>
    public static IGraphNodeQueryable<T> Union<T>(
        this IGraphNodeQueryable<T> first,
        IEnumerable<T> second)
        where T : INode
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);

        var methodInfo = GetGenericExtensionMethod(
            typeof(GraphNodeQueryableExtensions),
            nameof(Union),
            1, // T
            2  // first, second
        ).MakeGenericMethod(typeof(T));

        var expression = Expression.Call(
            null,
            methodInfo,
            first.Expression,
            Expression.Constant(second));

        return first.Provider.CreateNodeQuery<T>(expression);
    }

    /// <summary>
    /// Produces the set intersection of two node sequences.
    /// </summary>
    public static IGraphNodeQueryable<T> Intersect<T>(
        this IGraphNodeQueryable<T> first,
        IEnumerable<T> second)
        where T : INode
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);

        var methodInfo = GetGenericExtensionMethod(
            typeof(GraphNodeQueryableExtensions),
            nameof(Intersect),
            1, // T
            2  // first, second
        ).MakeGenericMethod(typeof(T));

        var expression = Expression.Call(
            null,
            methodInfo,
            first.Expression,
            Expression.Constant(second));

        return first.Provider.CreateNodeQuery<T>(expression);
    }

    /// <summary>
    /// Produces the set difference of two node sequences.
    /// </summary>
    public static IGraphNodeQueryable<T> Except<T>(
        this IGraphNodeQueryable<T> first,
        IEnumerable<T> second)
        where T : INode
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);

        var methodInfo = GetGenericExtensionMethod(
            typeof(GraphNodeQueryableExtensions),
            nameof(Except),
            1, // T
            2  // first, second
        ).MakeGenericMethod(typeof(T));

        var expression = Expression.Call(
            null,
            methodInfo,
            first.Expression,
            Expression.Constant(second));

        return first.Provider.CreateNodeQuery<T>(expression);
    }

    /// <summary>
    /// Inverts the order of the nodes in a sequence.
    /// </summary>
    public static IGraphNodeQueryable<T> Reverse<T>(
        this IGraphNodeQueryable<T> source)
        where T : INode
    {
        ArgumentNullException.ThrowIfNull(source);

        var methodInfo = GetGenericExtensionMethod(
            typeof(GraphNodeQueryableExtensions),
            nameof(Reverse),
            1, // T
            1  // source
        ).MakeGenericMethod(typeof(T));

        var expression = Expression.Call(
            null,
            methodInfo,
            source.Expression);

        return source.Provider.CreateNodeQuery<T>(expression);
    }

    /// <summary>
    /// Projects each node to an IEnumerable and flattens the resulting sequences.
    /// </summary>
    public static IGraphQueryable<TResult> SelectMany<T, TResult>(
        this IGraphNodeQueryable<T> source,
        Expression<Func<T, IEnumerable<TResult>>> selector)
        where T : INode
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(selector);

        var methodInfo = GetGenericExtensionMethod(
            typeof(GraphNodeQueryableExtensions),
            nameof(SelectMany),
            2, // T, TResult
            2  // source, selector
        ).MakeGenericMethod(typeof(T), typeof(TResult));

        var expression = Expression.Call(
            null,
            methodInfo,
            source.Expression,
            selector);

        return source.Provider.CreateQuery<TResult>(expression);
    }

    /// <summary>
    /// Projects each node to an IEnumerable, flattens the sequences, and invokes a result selector.
    /// </summary>
    public static IGraphQueryable<TResult> SelectMany<T, TCollection, TResult>(
        this IGraphNodeQueryable<T> source,
        Expression<Func<T, IEnumerable<TCollection>>> collectionSelector,
        Expression<Func<T, TCollection, TResult>> resultSelector)
        where T : INode
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(collectionSelector);
        ArgumentNullException.ThrowIfNull(resultSelector);

        var methodInfo = GetGenericExtensionMethod(
            typeof(GraphNodeQueryableExtensions),
            nameof(SelectMany),
            3, // T, TCollection, TResult
            3  // source, collectionSelector, resultSelector
        ).MakeGenericMethod(typeof(T), typeof(TCollection), typeof(TResult));

        var expression = Expression.Call(
            null,
            methodInfo,
            source.Expression,
            collectionSelector,
            resultSelector);

        return source.Provider.CreateQuery<TResult>(expression);
    }

    /// <summary>
    /// Attaches a transaction to the <see cref="IGraphNodeQueryable{TSource}"/>.
    /// </summary>
    public static IGraphNodeQueryable<TSource> WithTransaction<TSource>(
        this IGraphNodeQueryable<TSource> source,
        IGraphTransaction transaction)
        where TSource : INode
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(transaction);

        var methodInfo = GetGenericExtensionMethod(
            typeof(GraphNodeQueryableExtensions),
            nameof(WithTransaction),
            1, // TSource
            2  // source, transaction
        ).MakeGenericMethod(typeof(TSource));

        var expression = Expression.Call(
            null,
            methodInfo,
            source.Expression,
            Expression.Constant(transaction));

        return source.Provider.CreateNodeQuery<TSource>(expression);
    }
}