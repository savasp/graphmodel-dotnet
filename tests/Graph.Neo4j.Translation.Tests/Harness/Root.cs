// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Neo4j.Translation.Tests.Harness;

using System.Linq.Expressions;

/// <summary>
/// Entry points for building root <see cref="IGraphQueryable{T}"/> instances backed by
/// <see cref="FakeGraphQueryProvider"/>. This mirrors how the real
/// <c>GraphNodeQueryable&lt;TNode&gt;</c>/<c>GraphRelationshipQueryable&lt;TRel&gt;</c> build a
/// root expression: a constant carrying a placeholder queryable whose only purpose is to give
/// <c>CypherQueryVisitor.VisitConstant</c> the element type and node-vs-relationship kind.
/// </summary>
internal static class Root
{
    public static IGraphQueryable<T> Nodes<T>() where T : class, INode
    {
        var provider = new FakeGraphQueryProvider();
        var placeholder = new FakeGraphNodeQueryable<T>(provider, Expression.Constant(null, typeof(Expression)));
        var expression = Expression.Constant(placeholder, typeof(IGraphQueryable<T>));
        return new FakeGraphNodeQueryable<T>(provider, expression);
    }

    public static IGraphQueryable<T> ConcreteNodeQueryableConstant<T>() where T : class, INode
    {
        var provider = new FakeGraphQueryProvider();
        var placeholder = new FakeGraphNodeQueryable<T>(provider, Expression.Constant(null, typeof(Expression)));
        var expression = Expression.Constant(placeholder);
        return new FakeGraphNodeQueryable<T>(provider, expression);
    }

    public static IGraphQueryable<T> Relationships<T>() where T : class, IRelationship
    {
        var provider = new FakeGraphQueryProvider();
        var placeholder = new FakeGraphRelationshipQueryable<T>(provider, Expression.Constant(null, typeof(Expression)));
        var expression = Expression.Constant(placeholder, typeof(IGraphQueryable<T>));
        return new FakeGraphRelationshipQueryable<T>(provider, expression);
    }
}
