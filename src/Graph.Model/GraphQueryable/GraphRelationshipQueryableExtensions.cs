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
/// Extension methods that preserve <see cref="IGraphRelationshipQueryable{T}"/> interface through LINQ operations.
/// </summary>
public static class GraphRelationshipQueryableExtensions
{
    /// <summary>
    /// Filters relationships based on a predicate while preserving the IGraphRelationshipQueryable interface.
    /// </summary>
    public static IGraphRelationshipQueryable<T> Where<T>(
        this IGraphRelationshipQueryable<T> source,
        Expression<Func<T, bool>> predicate)
        where T : IRelationship
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(predicate);

        var methodInfo = GetGenericExtensionMethod(
            typeof(GraphRelationshipQueryableExtensions),
            nameof(Where),
            1, // T
            2  // source, predicate
        ).MakeGenericMethod(typeof(T));

        var expression = Expression.Call(
            null,
            methodInfo,
            source.Expression,
            predicate);

        return source.Provider.CreateRelationshipQuery<T>(expression);
    }

    /// <summary>
    /// Projects each relationship into a new form while preserving the IGraphRelationshipQueryable interface.
    /// </summary>
    public static IGraphRelationshipQueryable<TResult> Select<T, TResult>(
        this IGraphRelationshipQueryable<T> source,
        Expression<Func<T, TResult>> selector)
        where T : IRelationship
        where TResult : IRelationship
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(selector);

        var methodInfo = GetGenericExtensionMethod(
            typeof(GraphRelationshipQueryableExtensions),
            nameof(Select),
            2, // T, TResult
            2  // source, selector
        ).MakeGenericMethod(typeof(T), typeof(TResult));

        var expression = Expression.Call(
            null,
            methodInfo,
            source.Expression,
            selector);

        return source.Provider.CreateRelationshipQuery<TResult>(expression);
    }

    /// <summary>
    /// Projects each relationship into a new form with index while preserving the IGraphRelationshipQueryable interface.
    /// </summary>
    public static IGraphRelationshipQueryable<TResult> Select<T, TResult>(
        this IGraphRelationshipQueryable<T> source,
        Expression<Func<T, int, TResult>> selector)
        where T : IRelationship
        where TResult : IRelationship
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(selector);

        var methodInfo = GetGenericExtensionMethod(
            typeof(GraphRelationshipQueryableExtensions),
            nameof(Select),
            2, // T, TResult
            2  // source, selector
        ).MakeGenericMethod(typeof(T), typeof(TResult));

        var expression = Expression.Call(
            null,
            methodInfo,
            source.Expression,
            selector);

        return source.Provider.CreateRelationshipQuery<TResult>(expression);
    }

    /// <summary>
    /// Sorts relationships in ascending order according to a key.
    /// </summary>
    public static IOrderedGraphRelationshipQueryable<T> OrderBy<T, TKey>(
        this IGraphRelationshipQueryable<T> source,
        Expression<Func<T, TKey>> keySelector)
        where T : IRelationship
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(keySelector);

        var methodInfo = GetGenericExtensionMethod(
            typeof(GraphRelationshipQueryableExtensions),
            nameof(OrderBy),
            2, // T, TKey
            2  // source, keySelector
        ).MakeGenericMethod(typeof(T), typeof(TKey));

        var expression = Expression.Call(
            null,
            methodInfo,
            source.Expression,
            keySelector);

        return (IOrderedGraphRelationshipQueryable<T>)source.Provider.CreateRelationshipQuery<T>(expression);
    }

    /// <summary>
    /// Sorts relationships in descending order according to a key.
    /// </summary>
    public static IOrderedGraphRelationshipQueryable<T> OrderByDescending<T, TKey>(
        this IGraphRelationshipQueryable<T> source,
        Expression<Func<T, TKey>> keySelector)
        where T : IRelationship
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(keySelector);

        var methodInfo = GetGenericExtensionMethod(
            typeof(GraphRelationshipQueryableExtensions),
            nameof(OrderByDescending),
            2, // T, TKey
            2  // source, keySelector
        ).MakeGenericMethod(typeof(T), typeof(TKey));

        var expression = Expression.Call(
            null,
            methodInfo,
            source.Expression,
            keySelector);

        return (IOrderedGraphRelationshipQueryable<T>)source.Provider.CreateRelationshipQuery<T>(expression);
    }

    /// <summary>
    /// Performs a subsequent ordering of relationships in ascending order.
    /// </summary>
    public static IOrderedGraphRelationshipQueryable<T> ThenBy<T, TKey>(
        this IOrderedGraphRelationshipQueryable<T> source,
        Expression<Func<T, TKey>> keySelector)
        where T : IRelationship
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(keySelector);

        var methodInfo = GetGenericExtensionMethod(
            typeof(GraphRelationshipQueryableExtensions),
            nameof(ThenBy),
            2, // T, TKey
            2  // source, keySelector
        ).MakeGenericMethod(typeof(T), typeof(TKey));

        var expression = Expression.Call(
            null,
            methodInfo,
            source.Expression,
            keySelector);

        return (IOrderedGraphRelationshipQueryable<T>)source.Provider.CreateRelationshipQuery<T>(expression);
    }

    /// <summary>
    /// Performs a subsequent ordering of relationships in descending order.
    /// </summary>
    public static IOrderedGraphRelationshipQueryable<T> ThenByDescending<T, TKey>(
        this IOrderedGraphRelationshipQueryable<T> source,
        Expression<Func<T, TKey>> keySelector)
        where T : IRelationship
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(keySelector);

        var methodInfo = GetGenericExtensionMethod(
            typeof(GraphRelationshipQueryableExtensions),
            nameof(ThenByDescending),
            2, // T, TKey
            2  // source, keySelector
        ).MakeGenericMethod(typeof(T), typeof(TKey));

        var expression = Expression.Call(
            null,
            methodInfo,
            source.Expression,
            keySelector);

        return (IOrderedGraphRelationshipQueryable<T>)source.Provider.CreateRelationshipQuery<T>(expression);
    }

    /// <summary>
    /// Bypasses a specified number of relationships and returns the remaining relationships.
    /// </summary>
    public static IGraphRelationshipQueryable<T> Skip<T>(
        this IGraphRelationshipQueryable<T> source,
        int count)
        where T : IRelationship
    {
        ArgumentNullException.ThrowIfNull(source);

        var methodInfo = GetGenericExtensionMethod(
            typeof(GraphRelationshipQueryableExtensions),
            nameof(Skip),
            1, // T
            2  // source, count
        ).MakeGenericMethod(typeof(T));

        var expression = Expression.Call(
            null,
            methodInfo,
            source.Expression,
            Expression.Constant(count));

        return source.Provider.CreateRelationshipQuery<T>(expression);
    }

    /// <summary>
    /// Returns a specified number of contiguous relationships from the start.
    /// </summary>
    public static IGraphRelationshipQueryable<T> Take<T>(
        this IGraphRelationshipQueryable<T> source,
        int count)
        where T : IRelationship
    {
        ArgumentNullException.ThrowIfNull(source);

        var methodInfo = GetGenericExtensionMethod(
            typeof(GraphRelationshipQueryableExtensions),
            nameof(Take),
            1, // T
            2  // source, count
        ).MakeGenericMethod(typeof(T));

        var expression = Expression.Call(
            null,
            methodInfo,
            source.Expression,
            Expression.Constant(count));

        return source.Provider.CreateRelationshipQuery<T>(expression);
    }

    /// <summary>
    /// Bypasses relationships while the specified condition is true and returns the remaining relationships.
    /// </summary>
    public static IGraphRelationshipQueryable<T> SkipWhile<T>(
        this IGraphRelationshipQueryable<T> source,
        Expression<Func<T, bool>> predicate)
        where T : IRelationship
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(predicate);

        var methodInfo = GetGenericExtensionMethod(
            typeof(GraphRelationshipQueryableExtensions),
            nameof(SkipWhile),
            1, // T
            2  // source, predicate
        ).MakeGenericMethod(typeof(T));

        var expression = Expression.Call(
            null,
            methodInfo,
            source.Expression,
            predicate);

        return source.Provider.CreateRelationshipQuery<T>(expression);
    }

    /// <summary>
    /// Bypasses relationships while the specified condition with index is true and returns the remaining relationships.
    /// </summary>
    public static IGraphRelationshipQueryable<T> SkipWhile<T>(
        this IGraphRelationshipQueryable<T> source,
        Expression<Func<T, int, bool>> predicate)
        where T : IRelationship
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(predicate);

        var methodInfo = GetGenericExtensionMethod(
            typeof(GraphRelationshipQueryableExtensions),
            nameof(SkipWhile),
            1, // T
            2  // source, predicate
        ).MakeGenericMethod(typeof(T));

        var expression = Expression.Call(
            null,
            methodInfo,
            source.Expression,
            predicate);

        return source.Provider.CreateRelationshipQuery<T>(expression);
    }

    /// <summary>
    /// Returns relationships while the specified condition is true.
    /// </summary>
    public static IGraphRelationshipQueryable<T> TakeWhile<T>(
        this IGraphRelationshipQueryable<T> source,
        Expression<Func<T, bool>> predicate)
        where T : IRelationship
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(predicate);

        var methodInfo = GetGenericExtensionMethod(
            typeof(GraphRelationshipQueryableExtensions),
            nameof(TakeWhile),
            1, // T
            2  // source, predicate
        ).MakeGenericMethod(typeof(T));

        var expression = Expression.Call(
            null,
            methodInfo,
            source.Expression,
            predicate);

        return source.Provider.CreateRelationshipQuery<T>(expression);
    }

    /// <summary>
    /// Returns relationships while the specified condition with index is true.
    /// </summary>
    public static IGraphRelationshipQueryable<T> TakeWhile<T>(
        this IGraphRelationshipQueryable<T> source,
        Expression<Func<T, int, bool>> predicate)
        where T : IRelationship
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(predicate);

        var methodInfo = GetGenericExtensionMethod(
            typeof(GraphRelationshipQueryableExtensions),
            nameof(TakeWhile),
            1, // T
            2  // source, predicate
        ).MakeGenericMethod(typeof(T));

        var expression = Expression.Call(
            null,
            methodInfo,
            source.Expression,
            predicate);

        return source.Provider.CreateRelationshipQuery<T>(expression);
    }

    /// <summary>
    /// Returns distinct relationships from a sequence.
    /// </summary>
    public static IGraphRelationshipQueryable<T> Distinct<T>(
        this IGraphRelationshipQueryable<T> source)
        where T : IRelationship
    {
        ArgumentNullException.ThrowIfNull(source);

        var methodInfo = GetGenericExtensionMethod(
            typeof(GraphRelationshipQueryableExtensions),
            nameof(Distinct),
            1, // T
            1  // source
        ).MakeGenericMethod(typeof(T));

        var expression = Expression.Call(
            null,
            methodInfo,
            source.Expression);

        return source.Provider.CreateRelationshipQuery<T>(expression);
    }

    /// <summary>
    /// Returns distinct relationships from a sequence using a specified comparer.
    /// </summary>
    public static IGraphRelationshipQueryable<T> Distinct<T>(
        this IGraphRelationshipQueryable<T> source,
        IEqualityComparer<T> comparer)
        where T : IRelationship
    {
        ArgumentNullException.ThrowIfNull(source);

        var methodInfo = GetGenericExtensionMethod(
            typeof(GraphRelationshipQueryableExtensions),
            nameof(Distinct),
            1, // T
            2  // source, comparer
        ).MakeGenericMethod(typeof(T));

        var expression = Expression.Call(
            null,
            methodInfo,
            source.Expression,
            Expression.Constant(comparer));

        return source.Provider.CreateRelationshipQuery<T>(expression);
    }

    /// <summary>
    /// Groups relationships according to a specified key selector function.
    /// </summary>
    public static IGraphQueryable<IGrouping<TKey, T>> GroupBy<T, TKey>(
        this IGraphRelationshipQueryable<T> source,
        Expression<Func<T, TKey>> keySelector)
        where T : IRelationship
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(keySelector);

        var methodInfo = GetGenericExtensionMethod(
            typeof(GraphRelationshipQueryableExtensions),
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
    /// Groups relationships and projects each group using the specified functions.
    /// </summary>
    public static IGraphQueryable<TResult> GroupBy<T, TKey, TResult>(
        this IGraphRelationshipQueryable<T> source,
        Expression<Func<T, TKey>> keySelector,
        Expression<Func<TKey, IEnumerable<T>, TResult>> resultSelector)
        where T : IRelationship
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(keySelector);
        ArgumentNullException.ThrowIfNull(resultSelector);

        var methodInfo = GetGenericExtensionMethod(
            typeof(GraphRelationshipQueryableExtensions),
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
    /// Inverts the order of the relationships in a sequence.
    /// </summary>
    public static IGraphRelationshipQueryable<T> Reverse<T>(
        this IGraphRelationshipQueryable<T> source)
        where T : IRelationship
    {
        ArgumentNullException.ThrowIfNull(source);

        var methodInfo = GetGenericExtensionMethod(
            typeof(GraphRelationshipQueryableExtensions),
            nameof(Reverse),
            1, // T
            1  // source
        ).MakeGenericMethod(typeof(T));

        var expression = Expression.Call(
            null,
            methodInfo,
            source.Expression);

        return source.Provider.CreateRelationshipQuery<T>(expression);
    }

    /// <summary>
    /// Concatenates two relationship sequences.
    /// </summary>
    public static IGraphRelationshipQueryable<T> Concat<T>(
        this IGraphRelationshipQueryable<T> first,
        IEnumerable<T> second)
        where T : IRelationship
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);

        var methodInfo = GetGenericExtensionMethod(
            typeof(GraphRelationshipQueryableExtensions),
            nameof(Concat),
            1, // T
            2  // first, second
        ).MakeGenericMethod(typeof(T));

        var expression = Expression.Call(
            null,
            methodInfo,
            first.Expression,
            Expression.Constant(second));

        return first.Provider.CreateRelationshipQuery<T>(expression);
    }

    /// <summary>
    /// Produces the set union of two relationship sequences.
    /// </summary>
    public static IGraphRelationshipQueryable<T> Union<T>(
        this IGraphRelationshipQueryable<T> first,
        IEnumerable<T> second)
        where T : IRelationship
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);

        var methodInfo = GetGenericExtensionMethod(
            typeof(GraphRelationshipQueryableExtensions),
            nameof(Union),
            1, // T
            2  // first, second
        ).MakeGenericMethod(typeof(T));

        var expression = Expression.Call(
            null,
            methodInfo,
            first.Expression,
            Expression.Constant(second));

        return first.Provider.CreateRelationshipQuery<T>(expression);
    }

    /// <summary>
    /// Produces the set intersection of two relationship sequences.
    /// </summary>
    public static IGraphRelationshipQueryable<T> Intersect<T>(
        this IGraphRelationshipQueryable<T> first,
        IEnumerable<T> second)
        where T : IRelationship
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);

        var methodInfo = GetGenericExtensionMethod(
            typeof(GraphRelationshipQueryableExtensions),
            nameof(Intersect),
            1, // T
            2  // first, second
        ).MakeGenericMethod(typeof(T));

        var expression = Expression.Call(
            null,
            methodInfo,
            first.Expression,
            Expression.Constant(second));

        return first.Provider.CreateRelationshipQuery<T>(expression);
    }

    /// <summary>
    /// Produces the set difference of two relationship sequences.
    /// </summary>
    public static IGraphRelationshipQueryable<T> Except<T>(
        this IGraphRelationshipQueryable<T> first,
        IEnumerable<T> second)
        where T : IRelationship
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);

        var methodInfo = GetGenericExtensionMethod(
            typeof(GraphRelationshipQueryableExtensions),
            nameof(Except),
            1, // T
            2  // first, second
        ).MakeGenericMethod(typeof(T));

        var expression = Expression.Call(
            null,
            methodInfo,
            first.Expression,
            Expression.Constant(second));

        return first.Provider.CreateRelationshipQuery<T>(expression);
    }

    /// <summary>
    /// Projects each relationship to an IEnumerable and flattens the resulting sequences.
    /// </summary>
    public static IGraphQueryable<TResult> SelectMany<T, TResult>(
        this IGraphRelationshipQueryable<T> source,
        Expression<Func<T, IEnumerable<TResult>>> selector)
        where T : IRelationship
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(selector);

        var methodInfo = GetGenericExtensionMethod(
            typeof(GraphRelationshipQueryableExtensions),
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
    /// Projects each relationship to an IEnumerable, flattens the sequences, and invokes a result selector.
    /// </summary>
    public static IGraphQueryable<TResult> SelectMany<T, TCollection, TResult>(
        this IGraphRelationshipQueryable<T> source,
        Expression<Func<T, IEnumerable<TCollection>>> collectionSelector,
        Expression<Func<T, TCollection, TResult>> resultSelector)
        where T : IRelationship
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(collectionSelector);
        ArgumentNullException.ThrowIfNull(resultSelector);

        var methodInfo = GetGenericExtensionMethod(
            typeof(GraphRelationshipQueryableExtensions),
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
    /// Attaches a transaction to the <see cref="IGraphRelationshipQueryable{TSource}"/>.
    /// </summary>
    public static IGraphRelationshipQueryable<TSource> WithTransaction<TSource>(
        this IGraphRelationshipQueryable<TSource> source,
        IGraphTransaction transaction)
        where TSource : IRelationship
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(transaction);

        var methodInfo = GetGenericExtensionMethod(
            typeof(GraphRelationshipQueryableExtensions),
            nameof(WithTransaction),
            1, // TSource
            2  // source, transaction
        ).MakeGenericMethod(typeof(TSource));

        var expression = Expression.Call(
            null,
            methodInfo,
            source.Expression,
            Expression.Constant(transaction));

        return source.Provider.CreateRelationshipQuery<TSource>(expression);
    }
}