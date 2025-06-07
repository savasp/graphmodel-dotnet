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
/// Extension methods that preserve <see cref="IGraphRelationshipQueryable{T}"/> interface through LINQ operations.
/// </summary>
public static class GraphRelationshipQueryableExtensions
{
    /// <summary>
    /// Filters nodes based on a predicate while preserving the IGraphRelationshipQueryable interface.
    /// </summary>
    public static IGraphRelationshipQueryable<T> Where<T>(
        this IGraphRelationshipQueryable<T> source,
        Expression<Func<T, bool>> predicate)
        where T : IRelationship
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(predicate);

        var filtered = Queryable.Where(source, predicate);
        return new GraphRelationshipQueryableWrapper<T>(filtered, source.Graph, source.Provider);
    }

    /// <summary>
    /// Projects each node to a new form while preserving the graph context.
    /// </summary>
    public static IGraphRelationshipQueryable<TResult> Select<T, TResult>(
        this IGraphRelationshipQueryable<T> source,
        Expression<Func<T, TResult>> selector)
        where T : IRelationship
        where TResult : IRelationship
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(selector);

        var projected = Queryable.Select(source, selector);
        return new GraphRelationshipQueryableWrapper<TResult>(projected, source.Graph, source.Provider);
    }

    /// <summary>
    /// Projects each node to a new form with its index.
    /// </summary>
    public static IGraphRelationshipQueryable<TResult> Select<T, TResult>(
        this IGraphRelationshipQueryable<T> source,
        Expression<Func<T, int, TResult>> selector)
        where T : IRelationship
        where TResult : IRelationship
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(selector);

        var projected = Queryable.Select(source, selector);
        return new GraphRelationshipQueryableWrapper<TResult>(projected, source.Graph, source.Provider);
    }

    /// <summary>
    /// Sorts nodes in ascending order.
    /// </summary>
    public static IOrderedGraphRelationshipQueryable<T> OrderBy<T, TKey>(
        this IGraphRelationshipQueryable<T> source,
        Expression<Func<T, TKey>> keySelector)
        where T : IRelationship
        where TKey : notnull
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(keySelector);

        var ordered = Queryable.OrderBy(source, keySelector);
        return new GraphRelationshipQueryableWrapper<T>(ordered, source.Graph, source.Provider);
    }

    /// <summary>
    /// Sorts nodes in descending order.
    /// </summary>
    public static IOrderedGraphRelationshipQueryable<T> OrderByDescending<T, TKey>(
        this IGraphRelationshipQueryable<T> source,
        Expression<Func<T, TKey>> keySelector)
        where T : IRelationship
        where TKey : notnull
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(keySelector);

        var ordered = Queryable.OrderByDescending(source, keySelector);
        return new GraphRelationshipQueryableWrapper<T>(ordered, source.Graph, source.Provider);
    }

    /// <summary>
    /// Performs a subsequent ordering of nodes in ascending order.
    /// </summary>
    public static IOrderedGraphRelationshipQueryable<T> ThenBy<T, TKey>(
        this IOrderedGraphRelationshipQueryable<T> source,
        Expression<Func<T, TKey>> keySelector)
        where T : IRelationship
        where TKey : notnull
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(keySelector);

        var ordered = Queryable.ThenBy(source, keySelector);
        return new GraphRelationshipQueryableWrapper<T>(ordered, source.Graph, source.Provider);
    }

    /// <summary>
    /// Performs a subsequent ordering of nodes in descending order.
    /// </summary>
    public static IOrderedGraphRelationshipQueryable<T> ThenByDescending<T, TKey>(
        this IOrderedGraphRelationshipQueryable<T> source,
        Expression<Func<T, TKey>> keySelector)
        where T : IRelationship
        where TKey : notnull
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(keySelector);

        var ordered = Queryable.ThenByDescending(source, keySelector);
        return new GraphRelationshipQueryableWrapper<T>(ordered, source.Graph, source.Provider);
    }

    /// <summary>
    /// Skips a specified number of nodes.
    /// </summary>
    public static IGraphRelationshipQueryable<T> Skip<T>(
        this IGraphRelationshipQueryable<T> source,
        int count)
        where T : IRelationship
    {
        ArgumentNullException.ThrowIfNull(source);

        var skipped = Queryable.Skip(source, count);
        return new GraphRelationshipQueryableWrapper<T>(skipped, source.Graph, source.Provider);
    }

    /// <summary>
    /// Takes a specified number of nodes.
    /// </summary>
    public static IGraphRelationshipQueryable<T> Take<T>(
        this IGraphRelationshipQueryable<T> source,
        int count)
        where T : IRelationship
    {
        ArgumentNullException.ThrowIfNull(source);

        var taken = Queryable.Take(source, count);
        return new GraphRelationshipQueryableWrapper<T>(taken, source.Graph, source.Provider);
    }

    /// <summary>
    /// Skips nodes while the condition is true.
    /// </summary>
    public static IGraphRelationshipQueryable<T> SkipWhile<T>(
        this IGraphRelationshipQueryable<T> source,
        Expression<Func<T, bool>> predicate)
        where T : IRelationship
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(predicate);

        var skipped = Queryable.SkipWhile(source, predicate);
        return new GraphRelationshipQueryableWrapper<T>(skipped, source.Graph, source.Provider);
    }

    /// <summary>
    /// Skips nodes while the condition is true, using the index.
    /// </summary>
    public static IGraphRelationshipQueryable<T> SkipWhile<T>(
        this IGraphRelationshipQueryable<T> source,
        Expression<Func<T, int, bool>> predicate)
        where T : IRelationship
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(predicate);

        var skipped = Queryable.SkipWhile(source, predicate);
        return new GraphRelationshipQueryableWrapper<T>(skipped, source.Graph, source.Provider);
    }

    /// <summary>
    /// Takes nodes while the condition is true.
    /// </summary>
    public static IGraphRelationshipQueryable<T> TakeWhile<T>(
        this IGraphRelationshipQueryable<T> source,
        Expression<Func<T, bool>> predicate)
        where T : IRelationship
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(predicate);

        var taken = Queryable.TakeWhile(source, predicate);
        return new GraphRelationshipQueryableWrapper<T>(taken, source.Graph, source.Provider);
    }

    /// <summary>
    /// Takes nodes while the condition is true, using the index.
    /// </summary>
    public static IGraphRelationshipQueryable<T> TakeWhile<T>(
        this IGraphRelationshipQueryable<T> source,
        Expression<Func<T, int, bool>> predicate)
        where T : IRelationship
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(predicate);

        var taken = Queryable.TakeWhile(source, predicate);
        return new GraphRelationshipQueryableWrapper<T>(taken, source.Graph, source.Provider);
    }

    /// <summary>
    /// Returns distinct nodes.
    /// </summary>
    public static IGraphRelationshipQueryable<T> Distinct<T>(
        this IGraphRelationshipQueryable<T> source)
        where T : IRelationship
    {
        ArgumentNullException.ThrowIfNull(source);

        var distinct = Queryable.Distinct(source);
        return new GraphRelationshipQueryableWrapper<T>(distinct, source.Graph, source.Provider);
    }

    /// <summary>
    /// Returns distinct nodes using a custom equality comparer.
    /// </summary>
    public static IGraphRelationshipQueryable<T> Distinct<T>(
        this IGraphRelationshipQueryable<T> source,
        IEqualityComparer<T> comparer)
        where T : IRelationship
    {
        ArgumentNullException.ThrowIfNull(source);

        var distinct = Queryable.Distinct(source, comparer);
        return new GraphRelationshipQueryableWrapper<T>(distinct, source.Graph, source.Provider);
    }

    /// <summary>
    /// Groups nodes by a key.
    /// </summary>
    public static IQueryable<IGrouping<TKey, T>> GroupBy<T, TKey>(
        this IGraphRelationshipQueryable<T> source,
        Expression<Func<T, TKey>> keySelector)
        where T : IRelationship
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
        this IGraphRelationshipQueryable<T> source,
        Expression<Func<T, TKey>> keySelector,
        Expression<Func<T, TElement>> elementSelector)
        where T : IRelationship
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
    public static IGraphRelationshipQueryable<T> Concat<T>(
        this IGraphRelationshipQueryable<T> first,
        IEnumerable<T> second)
        where T : IRelationship
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);

        var concatenated = Queryable.Concat(first, second.AsQueryable());
        return new GraphRelationshipQueryableWrapper<T>(concatenated, first.Graph, first.Provider);
    }

    /// <summary>
    /// Produces the set union of two sequences.
    /// </summary>
    public static IGraphRelationshipQueryable<T> Union<T>(
        this IGraphRelationshipQueryable<T> first,
        IEnumerable<T> second)
        where T : IRelationship
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);

        var union = Queryable.Union(first, second.AsQueryable());
        return new GraphRelationshipQueryableWrapper<T>(union, first.Graph, first.Provider);
    }

    /// <summary>
    /// Produces the set intersection of two sequences.
    /// </summary>
    public static IGraphRelationshipQueryable<T> Intersect<T>(
        this IGraphRelationshipQueryable<T> first,
        IEnumerable<T> second)
        where T : IRelationship
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);

        var intersection = Queryable.Intersect(first, second.AsQueryable());
        return new GraphRelationshipQueryableWrapper<T>(intersection, first.Graph, first.Provider);
    }

    /// <summary>
    /// Produces the set difference of two sequences.
    /// </summary>
    public static IGraphRelationshipQueryable<T> Except<T>(
        this IGraphRelationshipQueryable<T> first,
        IEnumerable<T> second)
        where T : IRelationship
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);

        var except = Queryable.Except(first, second.AsQueryable());
        return new GraphRelationshipQueryableWrapper<T>(except, first.Graph, first.Provider);
    }

    /// <summary>
    /// Reverses the order of nodes.
    /// </summary>
    public static IGraphRelationshipQueryable<T> Reverse<T>(
        this IGraphRelationshipQueryable<T> source)
        where T : IRelationship
    {
        ArgumentNullException.ThrowIfNull(source);

        var reversed = Queryable.Reverse(source);
        return new GraphRelationshipQueryableWrapper<T>(reversed, source.Graph, source.Provider);
    }

    /// <summary>
    /// Determines whether all nodes satisfy a condition.
    /// </summary>
    public static bool All<T>(
        this IGraphRelationshipQueryable<T> source,
        Expression<Func<T, bool>> predicate)
        where T : IRelationship
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(predicate);

        return Queryable.All(source, predicate);
    }

    /// <summary>
    /// Determines whether any node satisfies a condition.
    /// </summary>
    public static bool Any<T>(
        this IGraphRelationshipQueryable<T> source,
        Expression<Func<T, bool>> predicate)
        where T : IRelationship
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(predicate);

        return Queryable.Any(source, predicate);
    }
}