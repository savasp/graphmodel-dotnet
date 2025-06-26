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
/// Extension methods that preserve <see cref="IGraphRelationshipQueryable{T}"/> interface for type-specific operations.
/// </summary>
public static class GraphRelationshipQueryableExtensions
{
    /// <summary>
    /// Filters relationships based on a predicate while preserving the IGraphRelationshipQueryable interface.
    /// This allows chaining with type-specific operations if any are added in the future.
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

        return (IGraphRelationshipQueryable<T>)source.Provider.CreateQuery<T>(expression);
    }

    /// <summary>
    /// Projects each relationship into a new relationship type while preserving the IGraphRelationshipQueryable interface.
    /// Use this when projecting from one relationship type to another relationship type.
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

        return (IGraphRelationshipQueryable<TResult>)source.Provider.CreateQuery<TResult>(expression);
    }

    /// <summary>
    /// Projects each relationship into a new relationship type with index while preserving the IGraphRelationshipQueryable interface.
    /// Use this when projecting from one relationship type to another relationship type with index information.
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

        return (IGraphRelationshipQueryable<TResult>)source.Provider.CreateQuery<TResult>(expression);
    }
}