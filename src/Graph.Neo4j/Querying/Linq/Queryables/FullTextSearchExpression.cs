// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Neo4j.Querying.Linq.Queryables;

using System.Linq.Expressions;
using Cvoya.Graph;
using Cvoya.Graph.Querying;

/// <summary>
/// Represents a full text search query expression
/// </summary>
internal class FullTextSearchExpression : Expression, IGraphSearchRootExpression
{
    public string SearchQuery { get; }
    public Type EntityType { get; }
    public GraphQueryableKind QueryableKind { get; }

    public FullTextSearchExpression(string searchQuery, Type entityType, GraphQueryableKind queryableKind)
    {
        SearchQuery = searchQuery;
        EntityType = entityType;
        QueryableKind = queryableKind;
    }

    public override ExpressionType NodeType => ExpressionType.Extension;

    public override Type Type => typeof(IGraphQueryable<>).MakeGenericType(EntityType);

    SearchRootTarget IGraphSearchRootExpression.Target => QueryableKind switch
    {
        GraphQueryableKind.Node => SearchRootTarget.Nodes,
        GraphQueryableKind.Relationship => SearchRootTarget.Relationships,
        _ => SearchRootTarget.Entities,
    };
}

/// <summary>
/// Represents a full text search operation on a LINQ queryable
/// </summary>
internal class SearchExpression : Expression
{
    public string SearchQuery { get; }
    public Expression Source { get; }

    public SearchExpression(Expression source, string searchQuery)
    {
        Source = source ?? throw new ArgumentNullException(nameof(source));
        SearchQuery = searchQuery ?? throw new ArgumentNullException(nameof(searchQuery));
    }

    public override ExpressionType NodeType => ExpressionType.Extension;
    public override Type Type => Source.Type;
}
