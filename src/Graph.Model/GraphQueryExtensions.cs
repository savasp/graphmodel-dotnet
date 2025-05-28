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

using System.Collections;
using System.Linq.Expressions;

namespace Cvoya.Graph.Model;

/// <summary>
/// Extension methods for graph querying and traversal operations.
/// </summary>
public static class GraphQueryExtensions
{
    /// <summary>
    /// Traverses outgoing relationships from the nodes in the query.
    /// </summary>
    /// <typeparam name="TSource">The source node type.</typeparam>
    /// <typeparam name="TRelationship">The relationship type to traverse.</typeparam>
    /// <param name="query">The query to extend.</param>
    /// <param name="depth">The depth of traversal. Default is 1.</param>
    /// <returns>A query with traversal applied.</returns>
    public static IQueryable<TSource> TraverseOutgoing<TSource, TRelationship>(
        this IQueryable<TSource> query, 
        int depth = 1)
        where TSource : INode
        where TRelationship : IRelationship
    {
        // Implementation will be provided by the provider
        return query.Provider.CreateQuery<TSource>(
            Expression.Call(
                null,
                ((Func<IQueryable<TSource>, int, IQueryable<TSource>>)TraverseOutgoing<TSource, TRelationship>).Method,
                query.Expression,
                Expression.Constant(depth)
            )
        );
    }

    /// <summary>
    /// Traverses incoming relationships to the nodes in the query.
    /// </summary>
    /// <typeparam name="TSource">The source node type.</typeparam>
    /// <typeparam name="TRelationship">The relationship type to traverse.</typeparam>
    /// <param name="query">The query to extend.</param>
    /// <param name="depth">The depth of traversal. Default is 1.</param>
    /// <returns>A query with traversal applied.</returns>
    public static IQueryable<TSource> TraverseIncoming<TSource, TRelationship>(
        this IQueryable<TSource> query, 
        int depth = 1)
        where TSource : INode
        where TRelationship : IRelationship
    {
        // Implementation will be provided by the provider
        return query.Provider.CreateQuery<TSource>(
            Expression.Call(
                null,
                ((Func<IQueryable<TSource>, int, IQueryable<TSource>>)TraverseIncoming<TSource, TRelationship>).Method,
                query.Expression,
                Expression.Constant(depth)
            )
        );
    }

    /// <summary>
    /// Traverses both incoming and outgoing relationships of the nodes in the query.
    /// </summary>
    /// <typeparam name="TSource">The source node type.</typeparam>
    /// <typeparam name="TRelationship">The relationship type to traverse.</typeparam>
    /// <param name="query">The query to extend.</param>
    /// <param name="depth">The depth of traversal. Default is 1.</param>
    /// <returns>A query with traversal applied.</returns>
    public static IQueryable<TSource> TraverseBoth<TSource, TRelationship>(
        this IQueryable<TSource> query, 
        int depth = 1)
        where TSource : INode
        where TRelationship : IRelationship
    {
        // Implementation will be provided by the provider
        return query.Provider.CreateQuery<TSource>(
            Expression.Call(
                null,
                ((Func<IQueryable<TSource>, int, IQueryable<TSource>>)TraverseBoth<TSource, TRelationship>).Method,
                query.Expression,
                Expression.Constant(depth)
            )
        );
    }

    /// <summary>
    /// Filters the query to only include relationships of the specified types.
    /// </summary>
    /// <typeparam name="TSource">The source node or relationship type.</typeparam>
    /// <param name="query">The query to extend.</param>
    /// <param name="relationshipTypes">The relationship types to include.</param>
    /// <returns>A query with relationship type filtering applied.</returns>
    public static IQueryable<TSource> WithRelationshipTypes<TSource>(
        this IQueryable<TSource> query,
        params string[] relationshipTypes)
        where TSource : IEntity
    {
        // Implementation will be provided by the provider
        return query.Provider.CreateQuery<TSource>(
            Expression.Call(
                null,
                ((Func<IQueryable<TSource>, string[], IQueryable<TSource>>)WithRelationshipTypes<TSource>).Method,
                query.Expression,
                Expression.Constant(relationshipTypes)
            )
        );
    }

    /// <summary>
    /// Filters the query to exclude relationships of the specified types.
    /// </summary>
    /// <typeparam name="TSource">The source node or relationship type.</typeparam>
    /// <param name="query">The query to extend.</param>
    /// <param name="relationshipTypes">The relationship types to exclude.</param>
    /// <returns>A query with relationship type filtering applied.</returns>
    public static IQueryable<TSource> WithoutRelationshipTypes<TSource>(
        this IQueryable<TSource> query,
        params string[] relationshipTypes)
        where TSource : IEntity
    {
        // Implementation will be provided by the provider
        return query.Provider.CreateQuery<TSource>(
            Expression.Call(
                null,
                ((Func<IQueryable<TSource>, string[], IQueryable<TSource>>)WithoutRelationshipTypes<TSource>).Method,
                query.Expression,
                Expression.Constant(relationshipTypes)
            )
        );
    }

    /// <summary>
    /// Filters the query to exclude property relationships (those starting with "__PROPERTY__").
    /// </summary>
    /// <typeparam name="TSource">The source node or relationship type.</typeparam>
    /// <param name="query">The query to extend.</param>
    /// <returns>A query with property relationships excluded.</returns>
    public static IQueryable<TSource> WithoutPropertyRelationships<TSource>(
        this IQueryable<TSource> query)
        where TSource : IEntity
    {
        // Implementation will be provided by the provider
        return query.Provider.CreateQuery<TSource>(
            Expression.Call(
                null,
                ((Func<IQueryable<TSource>, IQueryable<TSource>>)WithoutPropertyRelationships<TSource>).Method,
                query.Expression
            )
        );
    }
}