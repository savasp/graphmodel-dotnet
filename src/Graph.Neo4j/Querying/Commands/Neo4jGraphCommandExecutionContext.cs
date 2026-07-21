// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Neo4j.Querying.Commands;

using System.Linq.Expressions;
using Cvoya.Graph.Neo4j.Core;
using Cvoya.Graph.Neo4j.Querying.Cypher.Execution;
using Cvoya.Graph.Querying;
using Cvoya.Graph.Querying.Commands;

/// <summary>Executes Neo4j commands inside one active write transaction.</summary>
internal sealed class Neo4jGraphCommandExecutionContext(
    GraphContext context,
    GraphTransaction transaction,
    CypherEngine engine) : IGraphCommandExecutionContext
{
    public IGraphTransaction Transaction => transaction;

    public Task<IReadOnlyList<SelectedGraphElement>> SelectAsync(
        GraphElementSelectionModel selection,
        Expression sourceExpression,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sourceExpression);
        return engine.SelectNativeAsync(selection, transaction, cancellationToken);
    }

    public Task<int> ApplyAsync(
        GraphMutationModel mutation,
        Expression mutationExpression,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(mutationExpression);
        return engine.ApplyMutationAsync(mutation, transaction, cancellationToken);
    }

    public Task CreateRelationshipAsync(
        SelectedGraphElement source,
        IRelationship relationship,
        SelectedGraphElement target,
        RelationshipDirection direction,
        CancellationToken cancellationToken) =>
        context.SubgraphManager.CreateRelationshipAsync(
            GetNodeElementId(source, GraphEndpointRole.Source),
            relationship,
            GetNodeElementId(target, GraphEndpointRole.Target),
            direction,
            transaction,
            cancellationToken);

    public Task CreateAsync(
        SelectedGraphElement source,
        IRelationship relationship,
        INode newTarget,
        RelationshipDirection direction,
        CancellationToken cancellationToken) =>
        context.SubgraphManager.CreateAsync(
            GetNodeElementId(source, GraphEndpointRole.Source),
            relationship,
            newTarget,
            direction,
            transaction,
            cancellationToken);

    public Task CreateAsync(
        INode newSource,
        IRelationship relationship,
        SelectedGraphElement target,
        RelationshipDirection direction,
        CancellationToken cancellationToken) =>
        context.SubgraphManager.CreateAsync(
            newSource,
            relationship,
            GetNodeElementId(target, GraphEndpointRole.Target),
            direction,
            transaction,
            cancellationToken);

    public Task CreateAsync(
        INode newSource,
        IRelationship relationship,
        INode newTarget,
        RelationshipDirection direction,
        CancellationToken cancellationToken) =>
        context.SubgraphManager.CreateAsync(
            newSource,
            relationship,
            newTarget,
            direction,
            transaction,
            cancellationToken);

    public Task CreateSelfLoopAsync(
        INode node,
        IRelationship relationship,
        CancellationToken cancellationToken) =>
        context.SubgraphManager.CreateSelfLoopAsync(
            node,
            relationship,
            transaction,
            cancellationToken);

    private static string GetNodeElementId(SelectedGraphElement selected, GraphEndpointRole role)
    {
        ArgumentNullException.ThrowIfNull(selected);
        if (selected.Kind != GraphElementKind.Node || selected.NativeIdentity is not string elementId)
        {
            throw new GraphException(
                $"The selected {role.ToString().ToLowerInvariant()} endpoint is not a Neo4j node element.");
        }

        return elementId;
    }
}
