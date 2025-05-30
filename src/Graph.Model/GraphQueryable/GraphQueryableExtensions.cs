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
/// Extension methods that preserve IGraphQueryable&lt;T&gt; interface through LINQ operations.
/// These methods ensure that graph-specific functionality remains available after standard LINQ operations.
/// </summary>
public static class GraphQueryableExtensions
{
    /// <summary>
    /// Filters elements of the graph queryable based on a predicate while preserving graph functionality
    /// </summary>
    /// <typeparam name="T">The type of elements in the query</typeparam>
    /// <param name="source">The source graph queryable</param>
    /// <param name="predicate">A function to test each element for a condition</param>
    /// <returns>An IGraphQueryable&lt;T&gt; that contains elements that satisfy the condition</returns>
    public static IGraphQueryable<T> Where<T>(this IGraphQueryable<T> source, Expression<Func<T, bool>> predicate)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(predicate);

        // Create the expression for the Where call
        var whereMethod = typeof(Queryable).GetMethods()
            .First(m => m.Name == "Where" && m.GetParameters().Length == 2)
            .MakeGenericMethod(typeof(T));

        var callExpression = Expression.Call(null, whereMethod, source.Expression, Expression.Quote(predicate));

        // Use the provider to create a new query
        return (IGraphQueryable<T>)source.Provider.CreateQuery<T>(callExpression);
    }

    /// <summary>
    /// Projects each element of the graph queryable into a new form while preserving graph functionality
    /// </summary>
    /// <typeparam name="TSource">The type of source elements</typeparam>
    /// <typeparam name="TResult">The type of result elements</typeparam>
    /// <param name="source">The source graph queryable</param>
    /// <param name="selector">A transform function to apply to each element</param>
    /// <returns>An IGraphQueryable&lt;TResult&gt; whose elements are the result of invoking the transform function</returns>
    public static IGraphQueryable<TResult> Select<TSource, TResult>(this IGraphQueryable<TSource> source, Expression<Func<TSource, TResult>> selector)
        where TSource : class
        where TResult : class
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(selector);

        var selectMethod = typeof(Queryable).GetMethods()
            .First(m => m.Name == "Select" && m.GetParameters().Length == 2)
            .MakeGenericMethod(typeof(TSource), typeof(TResult));

        var callExpression = Expression.Call(null, selectMethod, source.Expression, Expression.Quote(selector));

        return (IGraphQueryable<TResult>)source.Provider.CreateQuery<TResult>(callExpression);
    }

    /// <summary>
    /// Sorts the elements of the graph queryable in ascending order according to a key while preserving graph functionality
    /// </summary>
    /// <typeparam name="T">The type of elements in the query</typeparam>
    /// <typeparam name="TKey">The type of the key returned by the key selector function</typeparam>
    /// <param name="source">The source graph queryable</param>
    /// <param name="keySelector">A function to extract a key from an element</param>
    /// <returns>An IGraphQueryable&lt;T&gt; whose elements are sorted according to a key</returns>
    public static IGraphQueryable<T> OrderBy<T, TKey>(this IGraphQueryable<T> source, Expression<Func<T, TKey>> keySelector)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(keySelector);

        var orderByMethod = typeof(Queryable).GetMethods()
            .First(m => m.Name == "OrderBy" && m.GetParameters().Length == 2)
            .MakeGenericMethod(typeof(T), typeof(TKey));

        var callExpression = Expression.Call(null, orderByMethod, source.Expression, Expression.Quote(keySelector));

        return (IGraphQueryable<T>)source.Provider.CreateQuery<T>(callExpression);
    }

    /// <summary>
    /// Sorts the elements of the graph queryable in descending order according to a key while preserving graph functionality
    /// </summary>
    /// <typeparam name="T">The type of elements in the query</typeparam>
    /// <typeparam name="TKey">The type of the key returned by the key selector function</typeparam>
    /// <param name="source">The source graph queryable</param>
    /// <param name="keySelector">A function to extract a key from an element</param>
    /// <returns>An IGraphQueryable&lt;T&gt; whose elements are sorted in descending order according to a key</returns>
    public static IGraphQueryable<T> OrderByDescending<T, TKey>(this IGraphQueryable<T> source, Expression<Func<T, TKey>> keySelector)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(keySelector);

        var orderByDescendingMethod = typeof(Queryable).GetMethods()
            .First(m => m.Name == "OrderByDescending" && m.GetParameters().Length == 2)
            .MakeGenericMethod(typeof(T), typeof(TKey));

        var callExpression = Expression.Call(null, orderByDescendingMethod, source.Expression, Expression.Quote(keySelector));

        return (IGraphQueryable<T>)source.Provider.CreateQuery<T>(callExpression);
    }

    /// <summary>
    /// Returns a specified number of contiguous elements from the start of the graph queryable while preserving graph functionality
    /// </summary>
    /// <typeparam name="T">The type of elements in the query</typeparam>
    /// <param name="source">The source graph queryable</param>
    /// <param name="count">The number of elements to return</param>
    /// <returns>An IGraphQueryable&lt;T&gt; that contains the specified number of elements from the start</returns>
    public static IGraphQueryable<T> Take<T>(this IGraphQueryable<T> source, int count)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(source);

        var takeMethod = typeof(Queryable).GetMethods()
            .First(m => m.Name == "Take" && m.GetParameters().Length == 2)
            .MakeGenericMethod(typeof(T));

        var callExpression = Expression.Call(null, takeMethod, source.Expression, Expression.Constant(count));

        return (IGraphQueryable<T>)source.Provider.CreateQuery<T>(callExpression);
    }

    /// <summary>
    /// Bypasses a specified number of elements in the graph queryable and returns the remaining elements while preserving graph functionality
    /// </summary>
    /// <typeparam name="T">The type of elements in the query</typeparam>
    /// <param name="source">The source graph queryable</param>
    /// <param name="count">The number of elements to skip before returning the remaining elements</param>
    /// <returns>An IGraphQueryable&lt;T&gt; that contains the elements that occur after the specified index</returns>
    public static IGraphQueryable<T> Skip<T>(this IGraphQueryable<T> source, int count)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(source);

        var skipMethod = typeof(Queryable).GetMethods()
            .First(m => m.Name == "Skip" && m.GetParameters().Length == 2)
            .MakeGenericMethod(typeof(T));

        var callExpression = Expression.Call(null, skipMethod, source.Expression, Expression.Constant(count));

        return (IGraphQueryable<T>)source.Provider.CreateQuery<T>(callExpression);
    }

    /// <summary>
    /// Returns distinct elements from the graph queryable while preserving graph functionality
    /// </summary>
    /// <typeparam name="T">The type of elements in the query</typeparam>
    /// <param name="source">The source graph queryable</param>
    /// <returns>An IGraphQueryable&lt;T&gt; that contains distinct elements</returns>
    public static IGraphQueryable<T> Distinct<T>(this IGraphQueryable<T> source)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(source);

        var distinctMethod = typeof(Queryable).GetMethods()
            .First(m => m.Name == "Distinct" && m.GetParameters().Length == 1)
            .MakeGenericMethod(typeof(T));

        var callExpression = Expression.Call(null, distinctMethod, source.Expression);

        return (IGraphQueryable<T>)source.Provider.CreateQuery<T>(callExpression);
    }

    /// <summary>
    /// Groups the elements of the graph queryable according to a specified key selector function while preserving graph functionality
    /// </summary>
    /// <typeparam name="T">The type of elements in the query</typeparam>
    /// <typeparam name="TKey">The type of the key returned by the key selector function</typeparam>
    /// <param name="source">The source graph queryable</param>
    /// <param name="keySelector">A function to extract the key for each element</param>
    /// <returns>An IGraphQueryable&lt;IGrouping&lt;TKey, T&gt;&gt; where each IGrouping&lt;TKey, T&gt; object contains a sequence of objects and a key</returns>
    public static IGraphQueryable<IGrouping<TKey, T>> GroupBy<T, TKey>(this IGraphQueryable<T> source, Expression<Func<T, TKey>> keySelector)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(keySelector);

        var groupByMethod = typeof(Queryable).GetMethods()
            .First(m => m.Name == "GroupBy" && m.GetParameters().Length == 2)
            .MakeGenericMethod(typeof(T), typeof(TKey));

        var callExpression = Expression.Call(null, groupByMethod, source.Expression, Expression.Quote(keySelector));

        return (IGraphQueryable<IGrouping<TKey, T>>)source.Provider.CreateQuery<IGrouping<TKey, T>>(callExpression);
    }
}
