// Copyright 2025 Savas Parastatidis

namespace Cvoya.Graph.Model.Age.Querying.Linq.Queryables;

using System.Linq.Expressions;
using Cvoya.Graph.Model;

/// <summary>
/// Represents a full text search query expression for AGE.
/// When embedded in an expression tree, it signals the Cypher query visitor
/// to add a WHERE clause with text matching (CONTAINS / ILIKE).
/// </summary>
internal sealed class AgeFullTextSearchExpression : Expression
{
    public string SearchQuery { get; }
    public Type EntityType { get; }

    public AgeFullTextSearchExpression(string searchQuery, Type entityType)
    {
        SearchQuery = searchQuery;
        EntityType = entityType;
    }

    public override ExpressionType NodeType => ExpressionType.Extension;
    public override Type Type
    {
        get
        {
            if (typeof(INode).IsAssignableFrom(EntityType))
                return typeof(IGraphNodeQueryable<>).MakeGenericType(EntityType);
            if (typeof(IRelationship).IsAssignableFrom(EntityType))
                return typeof(IGraphRelationshipQueryable<>).MakeGenericType(EntityType);
            return typeof(IGraphQueryable<>).MakeGenericType(EntityType);
        }
    }
}
