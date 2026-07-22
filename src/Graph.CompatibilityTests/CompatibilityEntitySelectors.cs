// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.CompatibilityTests;

using System.Linq.Expressions;
using System.Reflection;

internal static class CompatibilityEntitySelectors
{
    public static Task<TNode> FindNodeByTestKeyAsync<TNode>(
        this IGraph graph,
        string testKey,
        IGraphTransaction? transaction = null,
        CancellationToken cancellationToken = default)
        where TNode : class, INode =>
        graph.NodesByTestKey<TNode>(testKey, transaction).SingleAsync(cancellationToken);

    public static Task<TRelationship> FindRelationshipByTestKeyAsync<TRelationship>(
        this IGraph graph,
        string testKey,
        IGraphTransaction? transaction = null,
        CancellationToken cancellationToken = default)
        where TRelationship : class, IRelationship =>
        graph.RelationshipsByTestKey<TRelationship>(testKey, transaction).SingleAsync(cancellationToken);

    public static Task<DynamicNode> FindDynamicNodeByTestKeyAsync(
        this IGraph graph,
        string testKey,
        IGraphTransaction? transaction = null,
        CancellationToken cancellationToken = default) =>
        graph.DynamicNodesByTestKey(testKey, transaction).SingleAsync(cancellationToken);

    public static Task<DynamicRelationship> FindDynamicRelationshipByTestKeyAsync(
        this IGraph graph,
        string testKey,
        IGraphTransaction? transaction = null,
        CancellationToken cancellationToken = default) =>
        graph.DynamicRelationshipsByTestKey(testKey, transaction).SingleAsync(cancellationToken);

    public static IGraphQueryable<TNode> NodesByTestKey<TNode>(
        this IGraph graph,
        string testKey,
        IGraphTransaction? transaction = null)
        where TNode : class, INode =>
        graph.Nodes<TNode>(transaction).Where(BuildTestKeyPredicate<TNode>(testKey));

    public static IGraphQueryable<TRelationship> RelationshipsByTestKey<TRelationship>(
        this IGraph graph,
        string testKey,
        IGraphTransaction? transaction = null)
        where TRelationship : class, IRelationship =>
        graph.Relationships<TRelationship>(transaction).Where(BuildTestKeyPredicate<TRelationship>(testKey));

    public static IGraphQueryable<DynamicNode> DynamicNodesByTestKey(
        this IGraph graph,
        string testKey,
        IGraphTransaction? transaction = null) =>
        graph.DynamicNodes(transaction).OfLabel(testKey);

    public static IGraphQueryable<DynamicRelationship> DynamicRelationshipsByTestKey(
        this IGraph graph,
        string testKey,
        IGraphTransaction? transaction = null) =>
        graph.DynamicRelationships(transaction).Where(candidate => candidate.Type == testKey);

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
        var property = GetTestKeyProperty<TEntity>();
        if (property.PropertyType != typeof(string))
        {
            throw new InvalidOperationException($"{typeof(TEntity).Name}.TestKey must be a string.");
        }

        var key = property.GetValue(entity) as string
            ?? throw new InvalidOperationException($"{typeof(TEntity).Name}.TestKey cannot be null.");
        return BuildTestKeyPredicate<TEntity>(key);
    }

    private static Expression<Func<TEntity, bool>> BuildTestKeyPredicate<TEntity>(string key)
        where TEntity : class, IEntity
    {
        var property = GetTestKeyProperty<TEntity>();
        var parameter = Expression.Parameter(typeof(TEntity), "candidate");
        var comparison = Expression.Equal(Expression.Property(parameter, property), Expression.Constant(key));
        return Expression.Lambda<Func<TEntity, bool>>(comparison, parameter);
    }

    private static PropertyInfo GetTestKeyProperty<TEntity>()
        where TEntity : class, IEntity
    {
        var property = typeof(TEntity).GetProperty("TestKey", BindingFlags.Instance | BindingFlags.Public)
            ?? throw new InvalidOperationException($"{typeof(TEntity).Name} does not declare a TestKey selector.");
        if (property.PropertyType != typeof(string))
        {
            throw new InvalidOperationException($"{typeof(TEntity).Name}.TestKey must be a string.");
        }

        return property;
    }
}
