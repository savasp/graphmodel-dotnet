// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph;

using System.Linq.Expressions;
using System.Reflection;

/// <summary>Graph-query filters based on the existence of typed relationships.</summary>
public static class GraphRelationshipPredicateExtensions
{
    /// <summary>
    /// Keeps nodes that participate in at least one relationship of type <typeparamref name="TRel"/>
    /// in <paramref name="direction"/>.
    /// </summary>
    /// <remarks>
    /// This one-type-argument convenience overload returns the common <see cref="INode"/> shape.
    /// Use the two-type-argument overload when the concrete node type must remain in the static
    /// query chain.
    /// </remarks>
    public static IGraphQueryable<INode> WhereHasRelationship<TRel>(
        this IGraphQueryable<INode> source,
        GraphTraversalDirection direction = GraphTraversalDirection.Outgoing)
        where TRel : class, IRelationship =>
        CreateQuery<INode, TRel>(source, direction, predicate: null);

    /// <summary>
    /// Keeps nodes that participate in at least one matching relationship of type
    /// <typeparamref name="TRel"/> in <paramref name="direction"/>.
    /// </summary>
    public static IGraphQueryable<INode> WhereHasRelationship<TRel>(
        this IGraphQueryable<INode> source,
        GraphTraversalDirection direction,
        Expression<Func<TRel, bool>> predicate)
        where TRel : class, IRelationship =>
        CreateQuery<INode, TRel>(source, direction, predicate);

    /// <summary>
    /// Keeps nodes of type <typeparamref name="TNode"/> that participate in at least one matching
    /// relationship of type <typeparamref name="TRel"/>.
    /// </summary>
    public static IGraphQueryable<TNode> WhereHasRelationship<TNode, TRel>(
        this IGraphQueryable<TNode> source,
        GraphTraversalDirection direction,
        Expression<Func<TRel, bool>>? predicate = null)
        where TNode : class, INode
        where TRel : class, IRelationship =>
        CreateQuery<TNode, TRel>(source, direction, predicate);

    private static IGraphQueryable<TNode> CreateQuery<TNode, TRel>(
        IGraphQueryable<TNode> source,
        GraphTraversalDirection direction,
        Expression<Func<TRel, bool>>? predicate)
        where TNode : class, INode
        where TRel : class, IRelationship
    {
        ArgumentNullException.ThrowIfNull(source);
        if (!Enum.IsDefined(direction))
            throw new ArgumentOutOfRangeException(nameof(direction));

        var method = ((MethodInfo)MethodBase.GetCurrentMethod()!)
            .DeclaringType!
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(candidate => candidate.Name == nameof(WhereHasRelationship) &&
                candidate.GetGenericArguments().Length == 2)
            .MakeGenericMethod(typeof(TNode), typeof(TRel));
        var expression = Expression.Call(
            null,
            method,
            source.Expression,
            Expression.Constant(direction),
            Expression.Constant(predicate, typeof(Expression<Func<TRel, bool>>)));
        return source.Provider.CreateQuery<TNode>(expression);
    }
}
