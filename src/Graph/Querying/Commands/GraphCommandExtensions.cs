// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Querying.Commands;

using System.Linq.Expressions;
using System.Reflection;

/// <summary>Internal command surface consumed by the public API added in the dependent issue.</summary>
internal static class GraphCommandExtensions
{
    private static readonly MethodInfo UpdateDefinition = typeof(GraphMutationMarkers)
        .GetMethod(nameof(GraphMutationMarkers.UpdateMarker), BindingFlags.NonPublic | BindingFlags.Static)!;

    private static readonly MethodInfo DeleteDefinition = typeof(GraphMutationMarkers)
        .GetMethod(nameof(GraphMutationMarkers.DeleteMarker), BindingFlags.NonPublic | BindingFlags.Static)!;

    public static Task<int> UpdateAsync<TEntity>(
        IGraphQueryable<TEntity> source,
        Expression<Func<GraphPropertySetters<TEntity>, GraphPropertySetters<TEntity>>> setters,
        CancellationToken cancellationToken = default)
        where TEntity : class, IEntity
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(setters);
        return source.Provider.ExecuteAsync<int>(
            Expression.Call(
                UpdateDefinition.MakeGenericMethod(typeof(TEntity)),
                source.Expression,
                Expression.Quote(setters)),
            cancellationToken);
    }

    public static Task<int> DeleteAsync<TEntity>(
        IGraphQueryable<TEntity> source,
        bool cascadeDelete,
        CancellationToken cancellationToken = default)
        where TEntity : class, IEntity
    {
        ArgumentNullException.ThrowIfNull(source);
        return source.Provider.ExecuteAsync<int>(
            Expression.Call(
                DeleteDefinition.MakeGenericMethod(typeof(TEntity)),
                source.Expression,
                Expression.Constant(cascadeDelete)),
            cancellationToken);
    }
}
