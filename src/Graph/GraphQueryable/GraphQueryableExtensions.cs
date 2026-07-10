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

namespace Cvoya.Graph;

using System.Linq.Expressions;
using System.Reflection;


using static Cvoya.Graph.ExtensionUtils;

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

        // NOTE: this overload and the with-index Select below both have 2 generic arguments and 2
        // parameters, so GetGenericExtensionMethod(type, name, genericArgCount, paramCount) cannot
        // tell them apart (it previously picked whichever reflection returned first, silently
        // building an Expression.Call against the *other* overload's MethodInfo - caught by an
        // expression-shape test). Resolve via the currently-executing method's own generic
        // definition instead, which is unambiguous by construction.
        var methodInfo = ((MethodInfo)MethodBase.GetCurrentMethod()!)
            .GetGenericMethodDefinition()
            .MakeGenericMethod(typeof(TSource), typeof(TResult));

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

        // See the note in the other Select<TSource, TResult> overload above: resolved via the
        // currently-executing method rather than the ambiguous (genericArgCount, paramCount) match.
        var methodInfo = ((MethodInfo)MethodBase.GetCurrentMethod()!)
            .GetGenericMethodDefinition()
            .MakeGenericMethod(typeof(TSource), typeof(TResult));

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

        var methodInfo = GetGenericExtensionMethod(
            typeof(GraphQueryableExtensions),
            nameof(OrderBy),
            2, // TSource, TKey
            2  // source, keySelector
        ).MakeGenericMethod(typeof(TSource), typeof(TKey));

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

        var methodInfo = GetGenericExtensionMethod(
            typeof(GraphQueryableExtensions),
            nameof(OrderByDescending),
            2, // TSource, TKey
            2  // source, keySelector
        ).MakeGenericMethod(typeof(TSource), typeof(TKey));

        var expression = Expression.Call(
            null,
            methodInfo,
            source.Expression,
            keySelector);

        return (IOrderedGraphQueryable<TSource>)source.Provider.CreateQuery<TSource>(expression);
    }

    /// <summary>
    /// Performs a subsequent ordering of nodes in ascending order.
    /// </summary>
    public static IOrderedGraphQueryable<TSource> ThenBy<TSource, TKey>(
        this IOrderedGraphQueryable<TSource> source,
        Expression<Func<TSource, TKey>> keySelector)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(keySelector);

        var methodInfo = GetGenericExtensionMethod(
            typeof(GraphQueryableExtensions),
            nameof(ThenBy),
            2, // TSource, TKey
            2  // source, keySelector
        ).MakeGenericMethod(typeof(TSource), typeof(TKey));

        var expression = Expression.Call(
            null,
            methodInfo,
            source.Expression,
            keySelector);

        return (IOrderedGraphQueryable<TSource>)source.Provider.CreateQuery<TSource>(expression);
    }

    /// <summary>
    /// Performs a subsequent ordering of nodes in descending order.
    /// </summary>
    public static IOrderedGraphQueryable<TSource> ThenByDescending<TSource, TKey>(
        this IOrderedGraphQueryable<TSource> source,
        Expression<Func<TSource, TKey>> keySelector)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(keySelector);

        var methodInfo = GetGenericExtensionMethod(
            typeof(GraphQueryableExtensions),
            nameof(ThenByDescending),
            2, // TSource, TKey
            2  // source, keySelector
        ).MakeGenericMethod(typeof(TSource), typeof(TKey));

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

        var methodInfo = GetGenericExtensionMethod(
            typeof(GraphQueryableExtensions),
            nameof(Skip),
            1, // TSource
            2  // source, count
        ).MakeGenericMethod(typeof(TSource));

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

        var methodInfo = GetGenericExtensionMethod(
            typeof(GraphQueryableExtensions),
            nameof(Take),
            1, // TSource
            2  // source, count
        ).MakeGenericMethod(typeof(TSource));

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

        var methodInfo = GetGenericExtensionMethod(
            typeof(GraphQueryableExtensions),
            nameof(Distinct),
            1, // TSource
            1  // source
        ).MakeGenericMethod(typeof(TSource));

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

        var methodInfo = GetGenericExtensionMethod(
            typeof(GraphQueryableExtensions),
            nameof(GroupBy),
            2, // TSource, TKey
            2  // source, keySelector
        ).MakeGenericMethod(typeof(TSource), typeof(TKey));

        var expression = Expression.Call(
            null,
            methodInfo,
            source.Expression,
            keySelector);

        return source.Provider.CreateQuery<IGrouping<TKey, TSource>>(expression);
    }

    /// <summary>
    /// Specifies the traversal depth for graph operations.
    /// </summary>
    /// <remarks>
    /// Obsolete: this is a free-floating postfix modifier whose meaning ("mutate the preceding
    /// traversal") is not enforced by the type system — it compiles (and does nothing useful) on
    /// any <see cref="IGraphQueryable{T}"/>, not just the result of a traversal operator. Use the
    /// depth overloads on <c>Traverse</c>/<c>TraversePaths</c>, or their options-lambda overload
    /// (e.g. <c>o =&gt; o.Depth(1, 3)</c>), instead.
    /// </remarks>
    [Obsolete("Use the Traverse/TraversePaths depth overloads or an options lambda (e.g. o => o.Depth(min, max)) instead. This method will be removed in a future release.")]
    public static IGraphQueryable<TSource> WithDepth<TSource>(
        this IGraphQueryable<TSource> source,
        int maxDepth)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (maxDepth < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxDepth), "Depth must be non-negative.");
        }

        var methodInfo = GetGenericExtensionMethod(
            typeof(GraphQueryableExtensions),
            nameof(WithDepth),
            1, // TSource
            2  // source, maxDepth
        ).MakeGenericMethod(typeof(TSource));

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
    /// <remarks>
    /// Obsolete: see <see cref="WithDepth{TSource}(IGraphQueryable{TSource}, int)"/>.
    /// </remarks>
    [Obsolete("Use the Traverse/TraversePaths depth overloads or an options lambda (e.g. o => o.Depth(min, max)) instead. This method will be removed in a future release.")]
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

        var methodInfo = GetGenericExtensionMethod(
            typeof(GraphQueryableExtensions),
            nameof(WithDepth),
            1, // TSource
            3  // source, minDepth, maxDepth
        ).MakeGenericMethod(typeof(TSource));

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
    /// <remarks>
    /// Obsolete: this is a free-floating postfix modifier whose meaning ("mutate the preceding
    /// traversal") is not enforced by the type system. Use the direction overload on
    /// <c>Traverse</c>, or the options-lambda overload (e.g. <c>o =&gt; o.Direction(...)</c>), instead.
    /// </remarks>
    [Obsolete("Use the Traverse direction overload or an options lambda (e.g. o => o.Direction(direction)) instead. This method will be removed in a future release.")]
    public static IGraphQueryable<TSource> Direction<TSource>(
        this IGraphQueryable<TSource> source,
        GraphTraversalDirection direction)
    {
        ArgumentNullException.ThrowIfNull(source);

        var methodInfo = GetGenericExtensionMethod(
            typeof(GraphQueryableExtensions),
            nameof(Direction),
            1, // TSource
            2  // source, direction
        ).MakeGenericMethod(typeof(TSource));

        var expression = Expression.Call(
            null,
            methodInfo,
            source.Expression,
            Expression.Constant(direction));

        return source.Provider.CreateQuery<TSource>(expression);
    }

    /// <summary>
    /// Performs a full text search on the queryable results.
    /// This method can be used in LINQ chains to search for specific text within the results.
    /// </summary>
    /// <typeparam name="T">The type of elements in the queryable</typeparam>
    /// <param name="source">The source queryable</param>
    /// <param name="searchQuery">The search query string</param>
    /// <returns>A queryable that represents the search results</returns>
    public static IGraphQueryable<T> Search<T>(
        this IGraphQueryable<T> source,
        string searchQuery)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(searchQuery, nameof(searchQuery));

        var methodInfo = GetGenericExtensionMethod(
            typeof(GraphQueryableExtensions),
            nameof(Search),
            1, // T
            2  // source, searchQuery
        ).MakeGenericMethod(typeof(T));

        var expression = Expression.Call(
            null,
            methodInfo,
            source.Expression,
            Expression.Constant(searchQuery));

        return source.Provider.CreateQuery<T>(expression);
    }
}