// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Age.Querying;

using System.Linq.Expressions;
using Cvoya.Graph.Querying;

/// <summary>
/// AGE's mixed node-and-relationship search root. The provider executes its two physical roots on
/// the same transaction and combines the materialized entities before applying the outer LINQ
/// operators, which preserves global terminal, ordering, and paging semantics.
/// </summary>
internal sealed class AgeMixedSearchRootExpression(
    string searchQuery,
    Expression nodeSource,
    Expression relationshipSource) : Expression, IGraphSearchRootExpression
{
    public string SearchQuery { get; } = searchQuery;

    public Type EntityType => typeof(Graph.IEntity);

    public SearchRootTarget Target => SearchRootTarget.Entities;

    public Expression NodeSource { get; } = nodeSource;

    public Expression RelationshipSource { get; } = relationshipSource;

    public override ExpressionType NodeType => ExpressionType.Extension;

    public override Type Type => typeof(IGraphQueryable<Graph.IEntity>);

    internal static bool TryFind(Expression expression, out AgeMixedSearchRootExpression? root)
    {
        var finder = new Finder();
        finder.Visit(expression);
        root = finder.Root;
        return root is not null;
    }

    private sealed class Finder : ExpressionVisitor
    {
        public AgeMixedSearchRootExpression? Root { get; private set; }

        protected override Expression VisitExtension(Expression node)
        {
            if (node is AgeMixedSearchRootExpression root)
            {
                Root ??= root;
                return node;
            }

            return base.VisitExtension(node);
        }
    }
}
