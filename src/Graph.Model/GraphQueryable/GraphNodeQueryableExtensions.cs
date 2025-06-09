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

        var filtered = Queryable.Where(source, predicate);
        return new GraphNodeQueryableWrapper<T>(filtered, source.Graph, source.Provider);
    }

    /// <summary>
    /// Projects each node to a new form while preserving the graph context.
    /// </summary>
    public static IGraphNodeQueryable<TResult> Select<T, TResult>(
        this IGraphNodeQueryable<T> source,
        Expression<Func<T, TResult>> selector)
        where T : INode
        where TResult : INode
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(selector);

        var projected = Queryable.Select(source, selector);
        return new GraphNodeQueryableWrapper<TResult>(projected, source.Graph, source.Provider);
    }

    /// <summary>
    /// Projects each node to a new form with its index.
    /// </summary>
    public static IGraphNodeQueryable<TResult> Select<T, TResult>(
        this IGraphNodeQueryable<T> source,
        Expression<Func<T, int, TResult>> selector)
        where T : INode
        where TResult : INode
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(selector);

        var projected = Queryable.Select(source, selector);
        return new GraphNodeQueryableWrapper<TResult>(projected, source.Graph, source.Provider);
    }

    /// <summary>
    /// Sorts nodes in ascending order.
    /// </summary>
    public static IOrderedGraphNodeQueryable<T> OrderBy<T, TKey>(
        this IGraphNodeQueryable<T> source,
        Expression<Func<T, TKey>> keySelector)
        where T : INode
        where TKey : notnull
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(keySelector);

        var ordered = Queryable.OrderBy(source, keySelector);
        return new GraphNodeQueryableWrapper<T>(ordered, source.Graph, source.Provider);
    }

    /// <summary>
    /// Sorts nodes in descending order.
    /// </summary>
    public static IOrderedGraphNodeQueryable<T> OrderByDescending<T, TKey>(
        this IGraphNodeQueryable<T> source,
        Expression<Func<T, TKey>> keySelector)
        where T : INode
        where TKey : notnull
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(keySelector);

        var ordered = Queryable.OrderByDescending(source, keySelector);
        return new GraphNodeQueryableWrapper<T>(ordered, source.Graph, source.Provider);
    }

    /// <summary>
    /// Performs a subsequent ordering of nodes in ascending order.
    /// </summary>
    public static IOrderedGraphNodeQueryable<T> ThenBy<T, TKey>(
        this IOrderedGraphNodeQueryable<T> source,
        Expression<Func<T, TKey>> keySelector)
        where T : INode
        where TKey : notnull
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(keySelector);

        var ordered = Queryable.ThenBy(source, keySelector);
        return new GraphNodeQueryableWrapper<T>(ordered, source.Graph, source.Provider);
    }

    /// <summary>
    /// Performs a subsequent ordering of nodes in descending order.
    /// </summary>
    public static IOrderedGraphNodeQueryable<T> ThenByDescending<T, TKey>(
        this IOrderedGraphNodeQueryable<T> source,
        Expression<Func<T, TKey>> keySelector)
        where T : INode
        where TKey : notnull
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(keySelector);

        var ordered = Queryable.ThenByDescending(source, keySelector);
        return new GraphNodeQueryableWrapper<T>(ordered, source.Graph, source.Provider);
    }

    /// <summary>
    /// Skips a specified number of nodes.
    /// </summary>
    public static IGraphNodeQueryable<T> Skip<T>(
        this IGraphNodeQueryable<T> source,
        int count)
        where T : INode
    {
        ArgumentNullException.ThrowIfNull(source);

        var skipped = Queryable.Skip(source, count);
        return new GraphNodeQueryableWrapper<T>(skipped, source.Graph, source.Provider);
    }

    /// <summary>
    /// Takes a specified number of nodes.
    /// </summary>
    public static IGraphNodeQueryable<T> Take<T>(
        this IGraphNodeQueryable<T> source,
        int count)
        where T : INode
    {
        ArgumentNullException.ThrowIfNull(source);

        var taken = Queryable.Take(source, count);
        return new GraphNodeQueryableWrapper<T>(taken, source.Graph, source.Provider);
    }

    /// <summary>
    /// Skips nodes while the condition is true.
    /// </summary>
    public static IGraphNodeQueryable<T> SkipWhile<T>(
        this IGraphNodeQueryable<T> source,
        Expression<Func<T, bool>> predicate)
        where T : INode
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(predicate);

        var skipped = Queryable.SkipWhile(source, predicate);
        return new GraphNodeQueryableWrapper<T>(skipped, source.Graph, source.Provider);
    }

    /// <summary>
    /// Skips nodes while the condition is true, using the index.
    /// </summary>
    public static IGraphNodeQueryable<T> SkipWhile<T>(
        this IGraphNodeQueryable<T> source,
        Expression<Func<T, int, bool>> predicate)
        where T : INode
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(predicate);

        var skipped = Queryable.SkipWhile(source, predicate);
        return new GraphNodeQueryableWrapper<T>(skipped, source.Graph, source.Provider);
    }

    /// <summary>
    /// Takes nodes while the condition is true.
    /// </summary>
    public static IGraphNodeQueryable<T> TakeWhile<T>(
        this IGraphNodeQueryable<T> source,
        Expression<Func<T, bool>> predicate)
        where T : INode
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(predicate);

        var taken = Queryable.TakeWhile(source, predicate);
        return new GraphNodeQueryableWrapper<T>(taken, source.Graph, source.Provider);
    }

    /// <summary>
    /// Takes nodes while the condition is true, using the index.
    /// </summary>
    public static IGraphNodeQueryable<T> TakeWhile<T>(
        this IGraphNodeQueryable<T> source,
        Expression<Func<T, int, bool>> predicate)
        where T : INode
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(predicate);

        var taken = Queryable.TakeWhile(source, predicate);
        return new GraphNodeQueryableWrapper<T>(taken, source.Graph, source.Provider);
    }

    /// <summary>
    /// Returns distinct nodes.
    /// </summary>
    public static IGraphNodeQueryable<T> Distinct<T>(
        this IGraphNodeQueryable<T> source)
        where T : INode
    {
        ArgumentNullException.ThrowIfNull(source);

        var distinct = Queryable.Distinct(source);
        return new GraphNodeQueryableWrapper<T>(distinct, source.Graph, source.Provider);
    }

    /// <summary>
    /// Returns distinct nodes using a custom equality comparer.
    /// </summary>
    public static IGraphNodeQueryable<T> Distinct<T>(
        this IGraphNodeQueryable<T> source,
        IEqualityComparer<T> comparer)
        where T : INode
    {
        ArgumentNullException.ThrowIfNull(source);

        var distinct = Queryable.Distinct(source, comparer);
        return new GraphNodeQueryableWrapper<T>(distinct, source.Graph, source.Provider);
    }

    /// <summary>
    /// Groups nodes by a key.
    /// </summary>
    public static IQueryable<IGrouping<TKey, T>> GroupBy<T, TKey>(
        this IGraphNodeQueryable<T> source,
        Expression<Func<T, TKey>> keySelector)
        where T : INode
        where TKey : notnull
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(keySelector);

        return Queryable.GroupBy(source, keySelector);
    }

    /// <summary>
    /// Groups nodes by a key and projects each node.
    /// </summary>
    public static IQueryable<IGrouping<TKey, TElement>> GroupBy<T, TKey, TElement>(
        this IGraphNodeQueryable<T> source,
        Expression<Func<T, TKey>> keySelector,
        Expression<Func<T, TElement>> elementSelector)
        where T : INode
        where TKey : notnull
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(keySelector);
        ArgumentNullException.ThrowIfNull(elementSelector);

        return Queryable.GroupBy(source, keySelector, elementSelector);
    }

    /// <summary>
    /// Concatenates two sequences of nodes.
    /// </summary>
    public static IGraphNodeQueryable<T> Concat<T>(
        this IGraphNodeQueryable<T> first,
        IEnumerable<T> second)
        where T : INode
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);

        var concatenated = Queryable.Concat(first, second.AsQueryable());
        return new GraphNodeQueryableWrapper<T>(concatenated, first.Graph, first.Provider);
    }

    /// <summary>
    /// Produces the set union of two sequences.
    /// </summary>
    public static IGraphNodeQueryable<T> Union<T>(
        this IGraphNodeQueryable<T> first,
        IEnumerable<T> second)
        where T : INode
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);

        var union = Queryable.Union(first, second.AsQueryable());
        return new GraphNodeQueryableWrapper<T>(union, first.Graph, first.Provider);
    }

    /// <summary>
    /// Produces the set intersection of two sequences.
    /// </summary>
    public static IGraphNodeQueryable<T> Intersect<T>(
        this IGraphNodeQueryable<T> first,
        IEnumerable<T> second)
        where T : INode
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);

        var intersection = Queryable.Intersect(first, second.AsQueryable());
        return new GraphNodeQueryableWrapper<T>(intersection, first.Graph, first.Provider);
    }

    /// <summary>
    /// Produces the set difference of two sequences.
    /// </summary>
    public static IGraphNodeQueryable<T> Except<T>(
        this IGraphNodeQueryable<T> first,
        IEnumerable<T> second)
        where T : INode
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);

        var except = Queryable.Except(first, second.AsQueryable());
        return new GraphNodeQueryableWrapper<T>(except, first.Graph, first.Provider);
    }

    /// <summary>
    /// Reverses the order of nodes.
    /// </summary>
    public static IGraphNodeQueryable<T> Reverse<T>(
        this IGraphNodeQueryable<T> source)
        where T : INode
    {
        ArgumentNullException.ThrowIfNull(source);

        var reversed = Queryable.Reverse(source);
        return new GraphNodeQueryableWrapper<T>(reversed, source.Graph, source.Provider);
    }

    /// <summary>
    /// Determines whether all nodes satisfy a condition.
    /// </summary>
    public static bool All<T>(
        this IGraphNodeQueryable<T> source,
        Expression<Func<T, bool>> predicate)
        where T : INode
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(predicate);

        return Queryable.All(source, predicate);
    }

    /// <summary>
    /// Determines whether any node satisfies a condition.
    /// </summary>
    public static bool Any<T>(
        this IGraphNodeQueryable<T> source,
        Expression<Func<T, bool>> predicate)
        where T : INode
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(predicate);

        return Queryable.Any(source, predicate);
    }

    /// <summary>
    /// Attaches a transaction to the <see cref="IGraphNodeQueryable{TSource}"/>.
    /// </summary>
    public static IGraphNodeQueryable<TSource> WithTransaction<TSource>(
        this IGraphNodeQueryable<TSource> source,
        IGraphTransaction transaction) where TSource : INode
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(transaction);

        // Wrap the current expression in a transaction expression
        var transactionExpression = new GraphTransactionExpression(source.Expression, transaction);

        // Use the provider to create a new queryable with the transaction expression
        return source.Provider.CreateNodeQuery<TSource>(transactionExpression);
    }
}