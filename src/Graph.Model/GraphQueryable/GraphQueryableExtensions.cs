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
using System.Reflection;

namespace Cvoya.Graph.Model;

/// <summary>
/// Extension methods that preserve <see cref="IGraphQueryable{T}"/> interface through LINQ operations.
/// </summary>
public static class GraphQueryableExtensions
{
    /// <summary>
    /// Filters nodes based on a predicate while preserving the IGraphQueryable interface.
    /// </summary>
    public static IGraphQueryable<T> Where<T>(
        this IGraphQueryable<T> source,
        Expression<Func<T, bool>> predicate)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(predicate);

        var filtered = Queryable.Where(source, predicate);
        return new GraphQueryableWrapper<T>(filtered, source.Graph, source.Provider);
    }

    /// <summary>
    /// Projects each node to a new form while preserving the graph context.
    /// </summary>
    public static IGraphQueryable<TResult> Select<T, TResult>(
        this IGraphQueryable<T> source,
        Expression<Func<T, TResult>> selector)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(selector);

        var projected = Queryable.Select(source, selector);
        return new GraphQueryableWrapper<TResult>(projected, source.Graph, source.Provider);
    }

    /// <summary>
    /// Projects each node to a new form with its index.
    /// </summary>
    public static IGraphQueryable<TResult> Select<T, TResult>(
        this IGraphQueryable<T> source,
        Expression<Func<T, int, TResult>> selector)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(selector);

        var projected = Queryable.Select(source, selector);
        return new GraphQueryableWrapper<TResult>(projected, source.Graph, source.Provider);
    }

    /// <summary>
    /// Sorts nodes in ascending order.
    /// </summary>
    public static IOrderedGraphQueryable<T> OrderBy<T, TKey>(
        this IGraphQueryable<T> source,
        Expression<Func<T, TKey>> keySelector)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(keySelector);

        var ordered = Queryable.OrderBy(source, keySelector);
        return new GraphQueryableWrapper<T>(ordered, source.Graph, source.Provider);
    }

    /// <summary>
    /// Sorts nodes in descending order.
    /// </summary>
    public static IOrderedGraphQueryable<T> OrderByDescending<T, TKey>(
        this IGraphQueryable<T> source,
        Expression<Func<T, TKey>> keySelector)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(keySelector);

        var ordered = Queryable.OrderByDescending(source, keySelector);
        return new GraphQueryableWrapper<T>(ordered, source.Graph, source.Provider);
    }

    /// <summary>
    /// Performs a subsequent ordering of nodes in ascending order.
    /// </summary>
    public static IOrderedGraphQueryable<T> ThenBy<T, TKey>(
        this IOrderedGraphQueryable<T> source,
        Expression<Func<T, TKey>> keySelector)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(keySelector);

        var ordered = Queryable.ThenBy(source, keySelector);
        return new GraphQueryableWrapper<T>(ordered, source.Graph, source.Provider);
    }

    /// <summary>
    /// Performs a subsequent ordering of nodes in descending order.
    /// </summary>
    public static IOrderedGraphQueryable<T> ThenByDescending<T, TKey>(
        this IOrderedGraphQueryable<T> source,
        Expression<Func<T, TKey>> keySelector)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(keySelector);

        var ordered = Queryable.ThenByDescending(source, keySelector);
        return new GraphQueryableWrapper<T>(ordered, source.Graph, source.Provider);
    }

    /// <summary>
    /// Skips a specified number of nodes.
    /// </summary>
    public static IGraphQueryable<T> Skip<T>(
        this IGraphQueryable<T> source,
        int count)
    {
        ArgumentNullException.ThrowIfNull(source);

        var skipped = Queryable.Skip(source, count);
        return new GraphQueryableWrapper<T>(skipped, source.Graph, source.Provider);
    }

    /// <summary>
    /// Takes a specified number of nodes.
    /// </summary>
    public static IGraphQueryable<T> Take<T>(
        this IGraphQueryable<T> source,
        int count)
    {
        ArgumentNullException.ThrowIfNull(source);

        var taken = Queryable.Take(source, count);
        return new GraphQueryableWrapper<T>(taken, source.Graph, source.Provider);
    }

    /// <summary>
    /// Skips nodes while the condition is true.
    /// </summary>
    public static IGraphQueryable<T> SkipWhile<T>(
        this IGraphQueryable<T> source,
        Expression<Func<T, bool>> predicate)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(predicate);

        var skipped = Queryable.SkipWhile(source, predicate);
        return new GraphQueryableWrapper<T>(skipped, source.Graph, source.Provider);
    }

    /// <summary>
    /// Skips nodes while the condition is true, using the index.
    /// </summary>
    public static IGraphQueryable<T> SkipWhile<T>(
        this IGraphQueryable<T> source,
        Expression<Func<T, int, bool>> predicate)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(predicate);

        var skipped = Queryable.SkipWhile(source, predicate);
        return new GraphQueryableWrapper<T>(skipped, source.Graph, source.Provider);
    }

    /// <summary>
    /// Takes nodes while the condition is true.
    /// </summary>
    public static IGraphQueryable<T> TakeWhile<T>(
        this IGraphQueryable<T> source,
        Expression<Func<T, bool>> predicate)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(predicate);

        var taken = Queryable.TakeWhile(source, predicate);
        return new GraphQueryableWrapper<T>(taken, source.Graph, source.Provider);
    }

    /// <summary>
    /// Takes nodes while the condition is true, using the index.
    /// </summary>
    public static IGraphQueryable<T> TakeWhile<T>(
        this IGraphQueryable<T> source,
        Expression<Func<T, int, bool>> predicate)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(predicate);

        var taken = Queryable.TakeWhile(source, predicate);
        return new GraphQueryableWrapper<T>(taken, source.Graph, source.Provider);
    }

    /// <summary>
    /// Returns distinct nodes.
    /// </summary>
    public static IGraphQueryable<T> Distinct<T>(
        this IGraphQueryable<T> source)
    {
        ArgumentNullException.ThrowIfNull(source);

        var distinct = Queryable.Distinct(source);
        return new GraphQueryableWrapper<T>(distinct, source.Graph, source.Provider);
    }

    /// <summary>
    /// Returns distinct nodes using a custom equality comparer.
    /// </summary>
    public static IGraphQueryable<T> Distinct<T>(
        this IGraphQueryable<T> source,
        IEqualityComparer<T> comparer)
    {
        ArgumentNullException.ThrowIfNull(source);

        var distinct = Queryable.Distinct(source, comparer);
        return new GraphQueryableWrapper<T>(distinct, source.Graph, source.Provider);
    }

    /// <summary>
    /// Groups nodes by a key.
    /// </summary>
    public static IQueryable<IGrouping<TKey, T>> GroupBy<T, TKey>(
        this IGraphQueryable<T> source,
        Expression<Func<T, TKey>> keySelector)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(keySelector);

        return Queryable.GroupBy(source, keySelector);
    }

    /// <summary>
    /// Groups nodes by a key and projects each node.
    /// </summary>
    public static IQueryable<IGrouping<TKey, TElement>> GroupBy<T, TKey, TElement>(
        this IGraphQueryable<T> source,
        Expression<Func<T, TKey>> keySelector,
        Expression<Func<T, TElement>> elementSelector)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(keySelector);
        ArgumentNullException.ThrowIfNull(elementSelector);

        return Queryable.GroupBy(source, keySelector, elementSelector);
    }

    /// <summary>
    /// Concatenates two sequences of nodes.
    /// </summary>
    public static IGraphQueryable<T> Concat<T>(
        this IGraphQueryable<T> first,
        IEnumerable<T> second)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);

        var concatenated = Queryable.Concat(first, second.AsQueryable());
        return new GraphQueryableWrapper<T>(concatenated, first.Graph, first.Provider);
    }

    /// <summary>
    /// Produces the set union of two sequences.
    /// </summary>
    public static IGraphQueryable<T> Union<T>(
        this IGraphQueryable<T> first,
        IEnumerable<T> second)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);

        var union = Queryable.Union(first, second.AsQueryable());
        return new GraphQueryableWrapper<T>(union, first.Graph, first.Provider);
    }

    /// <summary>
    /// Produces the set intersection of two sequences.
    /// </summary>
    public static IGraphQueryable<T> Intersect<T>(
        this IGraphQueryable<T> first,
        IEnumerable<T> second)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);

        var intersection = Queryable.Intersect(first, second.AsQueryable());
        return new GraphQueryableWrapper<T>(intersection, first.Graph, first.Provider);
    }

    /// <summary>
    /// Produces the set difference of two sequences.
    /// </summary>
    public static IGraphQueryable<T> Except<T>(
        this IGraphQueryable<T> first,
        IEnumerable<T> second)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);

        var except = Queryable.Except(first, second.AsQueryable());
        return new GraphQueryableWrapper<T>(except, first.Graph, first.Provider);
    }

    /// <summary>
    /// Reverses the order of nodes.
    /// </summary>
    public static IGraphQueryable<T> Reverse<T>(
        this IGraphQueryable<T> source)
    {
        ArgumentNullException.ThrowIfNull(source);

        var reversed = Queryable.Reverse(source);
        return new GraphQueryableWrapper<T>(reversed, source.Graph, source.Provider);
    }

    /// <summary>
    /// Determines whether all nodes satisfy a condition.
    /// </summary>
    public static bool All<T>(
        this IGraphQueryable<T> source,
        Expression<Func<T, bool>> predicate)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(predicate);

        return Queryable.All(source, predicate);
    }

    /// <summary>
    /// Determines whether any node satisfies a condition.
    /// </summary>
    public static bool Any<T>(
        this IGraphQueryable<T> source,
        Expression<Func<T, bool>> predicate)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(predicate);

        return Queryable.Any(source, predicate);
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

        // Wrap the current expression in a transaction expression
        var transactionExpression = new GraphTransactionExpression(source.Expression, transaction);

        // Use the provider to create a new queryable with the transaction expression
        return source.Provider.CreateQuery<TSource>(transactionExpression);
    }

    /// <summary>
    /// Specifies the maximum traversal depth for graph operations.
    /// </summary>
    /// <typeparam name="T">The type of the queryable items</typeparam>
    /// <param name="source">The source queryable</param>
    /// <param name="maxDepth">The maximum depth to traverse</param>
    /// <returns>A queryable with the specified depth constraint</returns>
    public static IGraphQueryable<T> WithDepth<T>(
        this IGraphQueryable<T> source,
        int maxDepth)
    {
        ArgumentNullException.ThrowIfNull(source);

        return WithDepth(source, 1, maxDepth);
    }

    /// <summary>
    /// Specifies the traversal depth range for graph operations.
    /// </summary>
    /// <typeparam name="T">The type of the queryable items</typeparam>
    /// <param name="source">The source queryable</param>
    /// <param name="minDepth">The minimum depth to traverse</param>
    /// <param name="maxDepth">The maximum depth to traverse</param>
    /// <returns>A queryable with the specified depth constraint</returns>
    public static IGraphQueryable<T> WithDepth<T>(
        this IGraphQueryable<T> source,
        int minDepth,
        int maxDepth)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (minDepth < 0)
            throw new ArgumentOutOfRangeException(nameof(minDepth), "Minimum depth must be non-negative");
        if (maxDepth < minDepth)
            throw new ArgumentOutOfRangeException(nameof(maxDepth), "Maximum depth must be greater than or equal to minimum depth");


        var methodCall = Expression.Call(
            null,
            ((MethodInfo)MethodBase.GetCurrentMethod()!).MakeGenericMethod(typeof(T)),
            source.Expression,
            Expression.Constant(minDepth),
            Expression.Constant(maxDepth));

        return source.Provider.CreateQuery<T>(methodCall);
    }

    /// <summary>
    /// Specifies the direction of traversal for graph operations.
    /// </summary>
    /// <typeparam name="T">The type of the queryable items</typeparam>
    /// <param name="source">The source queryable</param>
    /// <param name="direction">The direction to traverse</param>
    /// <returns>A queryable with the specified direction constraint</returns>
    public static IGraphQueryable<T> InDirection<T>(
        this IGraphQueryable<T> source,
        TraversalDirection direction)
    {
        ArgumentNullException.ThrowIfNull(source);

        var methodCall = Expression.Call(
            null,
            ((MethodInfo)MethodBase.GetCurrentMethod()!).MakeGenericMethod(typeof(T)),
            source.Expression,
            Expression.Constant(direction));

        return source.Provider.CreateQuery<T>(methodCall);
    }
}