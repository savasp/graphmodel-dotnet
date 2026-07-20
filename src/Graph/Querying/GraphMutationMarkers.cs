// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Querying;

using System.Linq.Expressions;

/// <summary>Method markers carried only inside set-based mutation expression trees.</summary>
internal static class GraphMutationMarkers
{
    internal static int UpdateMarker<TEntity>(
        IQueryable<TEntity> source,
        Expression<Func<GraphPropertySetters<TEntity>, GraphPropertySetters<TEntity>>> setters)
        where TEntity : class, IEntity =>
        throw new InvalidOperationException("Graph mutation markers can only be used in expression trees.");

    internal static int DeleteMarker<TEntity>(IQueryable<TEntity> source, bool cascadeDelete)
        where TEntity : class, IEntity =>
        throw new InvalidOperationException("Graph mutation markers can only be used in expression trees.");
}
