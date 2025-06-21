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
using System.Reflection;
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

        var orderByMethod = typeof(Queryable)
            .GetMethods(BindingFlags.Static | BindingFlags.Public)
            .Where(m =>
                m.Name == "OrderBy" &&
                m.IsGenericMethodDefinition &&
                m.GetParameters().Length == 2)
            .First(m =>
            {
                var parameters = m.GetParameters();
                return parameters[0].ParameterType.GetGenericTypeDefinition() == typeof(IQueryable<>) &&
                       parameters[1].ParameterType.GetGenericTypeDefinition() == typeof(Expression<>) &&
                       parameters[1].ParameterType.GenericTypeArguments[0].GetGenericTypeDefinition() == typeof(Func<,>);
            })
            .MakeGenericMethod(typeof(T), typeof(TKey));

        var expression = Expression.Call(
            null,
            orderByMethod,
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

        var orderByDescMethod = typeof(Queryable)
            .GetMethods(BindingFlags.Static | BindingFlags.Public)
            .Where(m =>
                m.Name == "OrderByDescending" &&
                m.IsGenericMethodDefinition &&
                m.GetParameters().Length == 2)
            .First(m =>
            {
                var parameters = m.GetParameters();
                return parameters[0].ParameterType.GetGenericTypeDefinition() == typeof(IQueryable<>) &&
                       parameters[1].ParameterType.GetGenericTypeDefinition() == typeof(Expression<>) &&
                       parameters[1].ParameterType.GenericTypeArguments[0].GetGenericTypeDefinition() == typeof(Func<,>);
            })
            .MakeGenericMethod(typeof(T), typeof(TKey));

        var expression = Expression.Call(
            null,
            orderByDescMethod,
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

        var thenByMethod = typeof(Queryable)
            .GetMethods(BindingFlags.Static | BindingFlags.Public)
            .Where(m =>
                m.Name == "ThenBy" &&
                m.IsGenericMethodDefinition &&
                m.GetParameters().Length == 2)
            .First(m =>
            {
                var parameters = m.GetParameters();
                return parameters[0].ParameterType.GetGenericTypeDefinition() == typeof(IOrderedQueryable<>) &&
                       parameters[1].ParameterType.GetGenericTypeDefinition() == typeof(Expression<>) &&
                       parameters[1].ParameterType.GenericTypeArguments[0].GetGenericTypeDefinition() == typeof(Func<,>);
            })
            .MakeGenericMethod(typeof(T), typeof(TKey));

        var expression = Expression.Call(
            null,
            thenByMethod,
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

        var thenByDescMethod = typeof(Queryable)
            .GetMethods(BindingFlags.Static | BindingFlags.Public)
            .Where(m =>
                m.Name == "ThenByDescending" &&
                m.IsGenericMethodDefinition &&
                m.GetParameters().Length == 2)
            .First(m =>
            {
                var parameters = m.GetParameters();
                return parameters[0].ParameterType.GetGenericTypeDefinition() == typeof(IOrderedQueryable<>) &&
                       parameters[1].ParameterType.GetGenericTypeDefinition() == typeof(Expression<>) &&
                       parameters[1].ParameterType.GenericTypeArguments[0].GetGenericTypeDefinition() == typeof(Func<,>);
            })
            .MakeGenericMethod(typeof(T), typeof(TKey));

        var expression = Expression.Call(
            null,
            thenByDescMethod,
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

        var skipMethod = typeof(Queryable)
            .GetMethods(BindingFlags.Static | BindingFlags.Public)
            .Where(m =>
                m.Name == "Skip" &&
                m.IsGenericMethodDefinition &&
                m.GetParameters().Length == 2)
            .First(m =>
            {
                var parameters = m.GetParameters();
                return parameters[0].ParameterType.GetGenericTypeDefinition() == typeof(IQueryable<>) &&
                       parameters[1].ParameterType == typeof(int);
            })
            .MakeGenericMethod(typeof(T));

        var expression = Expression.Call(
            null,
            skipMethod,
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

        var takeMethod = typeof(Queryable)
            .GetMethods(BindingFlags.Static | BindingFlags.Public)
            .Where(m =>
                m.Name == "Take" &&
                m.IsGenericMethodDefinition &&
                m.GetParameters().Length == 2)
            .First(m =>
            {
                var parameters = m.GetParameters();
                return parameters[0].ParameterType.GetGenericTypeDefinition() == typeof(IQueryable<>) &&
                       parameters[1].ParameterType == typeof(int);
            })
            .MakeGenericMethod(typeof(T));

        var expression = Expression.Call(
            null,
            takeMethod,
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

        var methodInfo = typeof(Queryable)
            .GetMethods(BindingFlags.Static | BindingFlags.Public)
            .Single(m =>
                m.Name == "SkipWhile" &&
                m.IsGenericMethodDefinition &&
                m.GetParameters().Length == 2 &&
                m.GetParameters()[1].ParameterType == typeof(Expression<>).MakeGenericType(typeof(Func<,>).MakeGenericType(m.GetGenericArguments()[0], typeof(bool))))
            .MakeGenericMethod(typeof(T));

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

        var methodInfo = typeof(Queryable)
            .GetMethods(BindingFlags.Static | BindingFlags.Public)
            .Single(m =>
                m.Name == "SkipWhile" &&
                m.IsGenericMethodDefinition &&
                m.GetParameters().Length == 2 &&
                m.GetParameters()[1].ParameterType == typeof(Expression<>).MakeGenericType(typeof(Func<,,>).MakeGenericType(m.GetGenericArguments()[0], typeof(int), typeof(bool))))
            .MakeGenericMethod(typeof(T));

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

        var methodInfo = typeof(Queryable)
            .GetMethods(BindingFlags.Static | BindingFlags.Public)
            .Single(m =>
                m.Name == "TakeWhile" &&
                m.IsGenericMethodDefinition &&
                m.GetParameters().Length == 2 &&
                m.GetParameters()[1].ParameterType == typeof(Expression<>).MakeGenericType(typeof(Func<,>).MakeGenericType(m.GetGenericArguments()[0], typeof(bool))))
            .MakeGenericMethod(typeof(T));

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

        var methodInfo = typeof(Queryable)
            .GetMethods(BindingFlags.Static | BindingFlags.Public)
            .Single(m =>
                m.Name == "TakeWhile" &&
                m.IsGenericMethodDefinition &&
                m.GetParameters().Length == 2 &&
                m.GetParameters()[1].ParameterType == typeof(Expression<>).MakeGenericType(typeof(Func<,,>).MakeGenericType(m.GetGenericArguments()[0], typeof(int), typeof(bool))))
            .MakeGenericMethod(typeof(T));

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

        var methodInfo = typeof(Queryable)
            .GetMethods(BindingFlags.Static | BindingFlags.Public)
            .Single(m =>
                m.Name == "Distinct" &&
                m.IsGenericMethodDefinition &&
                m.GetParameters().Length == 1)
            .MakeGenericMethod(typeof(T));

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

        var methodInfo = typeof(Queryable)
            .GetMethods()
            .Where(m => m.Name == nameof(Queryable.Distinct))
            .Single(m => m.GetParameters().Length == 2)
            .MakeGenericMethod(typeof(T));

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

        var methodInfo = typeof(Queryable)
            .GetMethods(BindingFlags.Static | BindingFlags.Public)
            .Single(m =>
                m.Name == "GroupBy" &&
                m.IsGenericMethodDefinition &&
                m.GetParameters().Length == 2 &&
                m.GetGenericArguments().Length == 2)
            .MakeGenericMethod(typeof(T), typeof(TKey));

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

        var methodInfo = typeof(Queryable)
            .GetMethods(BindingFlags.Static | BindingFlags.Public)
            .Single(m =>
                m.Name == "GroupBy" &&
                m.IsGenericMethodDefinition &&
                m.GetParameters().Length == 3 &&
                m.GetGenericArguments().Length == 3)
            .MakeGenericMethod(typeof(T), typeof(TKey), typeof(TResult));

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

        var methodInfo = typeof(Queryable)
            .GetMethods(BindingFlags.Static | BindingFlags.Public)
            .Single(m =>
                m.Name == "Concat" &&
                m.IsGenericMethodDefinition &&
                m.GetParameters().Length == 2)
            .MakeGenericMethod(typeof(T));

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

        var methodInfo = typeof(Queryable)
            .GetMethods(BindingFlags.Static | BindingFlags.Public)
            .Single(m =>
                m.Name == "Union" &&
                m.IsGenericMethodDefinition &&
                m.GetParameters().Length == 2)
            .MakeGenericMethod(typeof(T));

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

        var methodInfo = typeof(Queryable)
            .GetMethods(BindingFlags.Static | BindingFlags.Public)
            .Single(m =>
                m.Name == "Intersect" &&
                m.IsGenericMethodDefinition &&
                m.GetParameters().Length == 2)
            .MakeGenericMethod(typeof(T));

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

        var methodInfo = typeof(Queryable)
            .GetMethods(BindingFlags.Static | BindingFlags.Public)
            .Single(m =>
                m.Name == "Except" &&
                m.IsGenericMethodDefinition &&
                m.GetParameters().Length == 2)
            .MakeGenericMethod(typeof(T));

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

        var methodInfo = new Func<IQueryable<T>, IQueryable<T>>(Queryable.Reverse).Method.MakeGenericMethod(typeof(T));
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

        // Build a method call expression for WithTransaction
        var methodInfo = typeof(GraphNodeQueryableExtensions)
            .GetMethod(nameof(WithTransaction))!
            .MakeGenericMethod(typeof(TSource));

        var expression = Expression.Call(
            null,
            methodInfo,
            source.Expression,
            Expression.Constant(transaction));

        return source.Provider.CreateNodeQuery<TSource>(expression);
    }
}