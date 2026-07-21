// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Age.Tests;

using System.Linq.Expressions;
using System.Reflection;

internal static class AgeEntitySelectors
{
    public static IGraphQueryable<TNode> SelectNode<TNode>(
        this IGraph graph,
        TNode node,
        IGraphTransaction? transaction = null)
        where TNode : class, INode =>
        graph.Nodes<TNode>(transaction).Where(BuildTestKeyPredicate(node));

    public static IGraphQueryable<TRelationship> SelectRelationship<TRelationship>(
        this IGraph graph,
        TRelationship relationship,
        IGraphTransaction? transaction = null)
        where TRelationship : class, IRelationship =>
        graph.Relationships<TRelationship>(transaction).Where(BuildTestKeyPredicate(relationship));

    public static Task<TNode> FindNodeAsync<TNode>(
        this IGraph graph,
        TNode node,
        IGraphTransaction? transaction = null,
        CancellationToken cancellationToken = default)
        where TNode : class, INode =>
        graph.SelectNode(node, transaction).SingleAsync(cancellationToken);

    public static Task<TRelationship> FindRelationshipAsync<TRelationship>(
        this IGraph graph,
        TRelationship relationship,
        IGraphTransaction? transaction = null,
        CancellationToken cancellationToken = default)
        where TRelationship : class, IRelationship =>
        graph.SelectRelationship(relationship, transaction).SingleAsync(cancellationToken);

    public static Task ConnectAsync<TSource, TRelationship, TTarget>(
        this IGraph graph,
        TSource source,
        TRelationship relationship,
        TTarget target,
        RelationshipDirection direction = RelationshipDirection.Outgoing,
        IGraphTransaction? transaction = null,
        CancellationToken cancellationToken = default)
        where TSource : class, INode
        where TRelationship : class, IRelationship
        where TTarget : class, INode =>
        graph.CreateRelationshipAsync(
            graph.SelectNode(source, transaction),
            relationship,
            graph.SelectNode(target, transaction),
            direction,
            cancellationToken);

    private static Expression<Func<TEntity, bool>> BuildTestKeyPredicate<TEntity>(TEntity entity)
        where TEntity : class, IEntity
    {
        var property = typeof(TEntity).GetProperty("TestKey", BindingFlags.Instance | BindingFlags.Public)
            ?? throw new InvalidOperationException($"{typeof(TEntity).Name} does not declare a TestKey selector.");
        if (property.PropertyType != typeof(string))
        {
            throw new InvalidOperationException($"{typeof(TEntity).Name}.TestKey must be a string.");
        }

        var key = property.GetValue(entity) as string
            ?? throw new InvalidOperationException($"{typeof(TEntity).Name}.TestKey cannot be null.");
        var parameter = Expression.Parameter(typeof(TEntity), "candidate");
        var comparison = Expression.Equal(Expression.Property(parameter, property), Expression.Constant(key));
        return Expression.Lambda<Func<TEntity, bool>>(comparison, parameter);
    }
}
