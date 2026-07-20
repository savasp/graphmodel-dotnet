// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph;

using System.Linq.Expressions;

/// <summary>Builds the property assignments for a set-based graph update expression.</summary>
/// <typeparam name="TEntity">The graph entity type being updated.</typeparam>
/// <remarks>
/// This type is an expression-tree marker. Its methods are parsed by a graph query provider and
/// must never execute directly.
/// </remarks>
public sealed class GraphPropertySetters<TEntity>
    where TEntity : class, IEntity
{
    private GraphPropertySetters()
    {
    }

    /// <summary>Sets a mapped property to a constant or captured value.</summary>
    /// <typeparam name="TProperty">The property value type.</typeparam>
    /// <param name="property">A direct mapped-property selector.</param>
    /// <param name="value">The value to assign.</param>
    /// <returns>The marker used to chain another assignment.</returns>
    public GraphPropertySetters<TEntity> SetProperty<TProperty>(
        Expression<Func<TEntity, TProperty>> property,
        TProperty value) =>
        throw new InvalidOperationException("Graph property setter markers can only be used in expression trees.");

    /// <summary>Sets a mapped property to a value computed from the current entity.</summary>
    /// <typeparam name="TProperty">The property value type.</typeparam>
    /// <param name="property">A direct mapped-property selector.</param>
    /// <param name="value">An expression that computes the assigned value from the current entity.</param>
    /// <returns>The marker used to chain another assignment.</returns>
    public GraphPropertySetters<TEntity> SetProperty<TProperty>(
        Expression<Func<TEntity, TProperty>> property,
        Expression<Func<TEntity, TProperty>> value) =>
        throw new InvalidOperationException("Graph property setter markers can only be used in expression trees.");
}
