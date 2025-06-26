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
/// Extension methods that preserve <see cref="IGraphNodeQueryable{T}"/> interface for type-specific operations.
/// </summary>
public static class GraphNodeQueryableExtensions
{
    /// <summary>
    /// Filters nodes based on a predicate while preserving the IGraphNodeQueryable interface.
    /// This allows chaining with type-specific operations like Traverse.
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

        return (IGraphNodeQueryable<T>)source.Provider.CreateQuery<T>(expression);
    }

    /// <summary>
    /// Projects each node into a new node type while preserving the IGraphNodeQueryable interface.
    /// Use this when projecting from one node type to another node type.
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

        return (IGraphNodeQueryable<TResult>)source.Provider.CreateQuery<TResult>(expression);
    }

    /// <summary>
    /// Projects each node into a new node type with index while preserving the IGraphNodeQueryable interface.
    /// Use this when projecting from one node type to another node type with index information.
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

        return (IGraphNodeQueryable<TResult>)source.Provider.CreateQuery<TResult>(expression);
    }
}