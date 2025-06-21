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
    /// Sorts relationships in ascending order according to a key.
    /// </summary>
    public static IOrderedGraphRelationshipQueryable<T> OrderBy<T, TKey>(
        this IGraphRelationshipQueryable<T> source,
        Expression<Func<T, TKey>> keySelector)
        where T : IRelationship
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

        var methodInfo = new Func<IQueryable<T>, IQueryable<T>>(Queryable.Distinct).Method.GetGenericMethodDefinition().MakeGenericMethod(typeof(T));
        var expression = Expression.Call(
            null,
            methodInfo,
            source.Expression);

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

        var methodInfo = new Func<IQueryable<T>, Expression<Func<T, TKey>>, IQueryable<IGrouping<TKey, T>>>(Queryable.GroupBy).Method.GetGenericMethodDefinition().MakeGenericMethod(typeof(T), typeof(TKey));
        var expression = Expression.Call(
            null,
            methodInfo,
            source.Expression,
            keySelector);

        return source.Provider.CreateQuery<IGrouping<TKey, T>>(expression);
    }

    /// <summary>
    /// Inverts the order of the relationships in a sequence.
    /// </summary>
    public static IGraphRelationshipQueryable<T> Reverse<T>(
        this IGraphRelationshipQueryable<T> source)
        where T : IRelationship
    {
        ArgumentNullException.ThrowIfNull(source);

        var methodInfo = new Func<IQueryable<T>, IQueryable<T>>(Queryable.Reverse).Method.GetGenericMethodDefinition().MakeGenericMethod(typeof(T));
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

        var methodInfo = new Func<IQueryable<T>, IEnumerable<T>, IQueryable<T>>(Queryable.Concat).Method.GetGenericMethodDefinition().MakeGenericMethod(typeof(T));
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

        var methodInfo = new Func<IQueryable<T>, IEnumerable<T>, IQueryable<T>>(Queryable.Union).Method.GetGenericMethodDefinition().MakeGenericMethod(typeof(T));
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

        var methodInfo = new Func<IQueryable<T>, IEnumerable<T>, IQueryable<T>>(Queryable.Intersect).Method.GetGenericMethodDefinition().MakeGenericMethod(typeof(T));
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

        var methodInfo = new Func<IQueryable<T>, IEnumerable<T>, IQueryable<T>>(Queryable.Except).Method.GetGenericMethodDefinition().MakeGenericMethod(typeof(T));
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
    /// Attaches a transaction to the <see cref="IGraphRelationshipQueryable{TSource}"/>.
    /// </summary>
    public static IGraphRelationshipQueryable<TSource> WithTransaction<TSource>(
        this IGraphRelationshipQueryable<TSource> source,
        IGraphTransaction transaction)
        where TSource : IRelationship
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(transaction);

        // Build a method call expression for WithTransaction
        var methodInfo = typeof(GraphRelationshipQueryableExtensions)
            .GetMethod(nameof(WithTransaction))!
            .MakeGenericMethod(typeof(TSource));

        var expression = Expression.Call(
            null,
            methodInfo,
            source.Expression,
            Expression.Constant(transaction));

        return source.Provider.CreateRelationshipQuery<TSource>(expression);
    }
}