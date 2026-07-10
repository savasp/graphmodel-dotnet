// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Neo4j.Querying.Linq.Queryables;

using Cvoya.Graph.Neo4j.Core;
using Cvoya.Graph.Neo4j.Querying.Linq.Providers;

/// <summary>
/// Static factory methods for creating full text search queryables
/// </summary>
internal static class FullTextSearchQueryableFactory
{
    public static GraphQueryable<T> CreateSearchQueryable<T>(
        GraphQueryProvider provider,
        GraphTransaction? transaction,
        GraphContext context,
        string searchQuery)
    {
        var searchExpression = new FullTextSearchExpression(searchQuery, typeof(T), GraphQueryableKind.General);
        return new GraphQueryable<T>(provider, context, transaction, searchExpression);
    }

    public static GraphNodeQueryable<T> CreateNodeSearchQueryable<T>(
        GraphQueryProvider provider,
        GraphTransaction? transaction,
        GraphContext context,
        string searchQuery)
        where T : class, INode
    {
        var searchExpression = new FullTextSearchExpression(searchQuery, typeof(T), GraphQueryableKind.Node);
        return new GraphNodeQueryable<T>(provider, context, transaction, searchExpression);
    }

    public static GraphRelationshipQueryable<T> CreateRelationshipSearchQueryable<T>(
        GraphQueryProvider provider,
        GraphTransaction? transaction,
        GraphContext context,
        string searchQuery)
        where T : class, IRelationship
    {
        var searchExpression = new FullTextSearchExpression(searchQuery, typeof(T), GraphQueryableKind.Relationship);
        return new GraphRelationshipQueryable<T>(provider, context, transaction, searchExpression);
    }
}
