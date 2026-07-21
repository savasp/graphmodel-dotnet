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
        GraphCommandEndpoint source,
        IRelationship relationship,
        GraphCommandEndpoint target,
        RelationshipDirection direction,
        GraphRelationshipCreationMode mode,
        CancellationToken cancellationToken) =>
        (source, target, mode) switch
        {
            (SelectedGraphCommandEndpoint selectedSource,
                SelectedGraphCommandEndpoint selectedTarget,
                GraphRelationshipCreationMode.Standard) =>
                context.SubgraphManager.CreateRelationshipAsync(
                    GetNodeElementId(selectedSource.Element, GraphEndpointRole.Source),
                    relationship,
                    GetNodeElementId(selectedTarget.Element, GraphEndpointRole.Target),
                    direction,
                    transaction,
                    cancellationToken),
            (SelectedGraphCommandEndpoint selectedSource,
                NewGraphCommandEndpoint newTarget,
                GraphRelationshipCreationMode.Standard) =>
                context.SubgraphManager.CreateAsync(
                    GetNodeElementId(selectedSource.Element, GraphEndpointRole.Source),
                    relationship,
                    newTarget.Node,
                    direction,
                    transaction,
                    cancellationToken),
            (NewGraphCommandEndpoint newSource,
                SelectedGraphCommandEndpoint selectedTarget,
                GraphRelationshipCreationMode.Standard) =>
                context.SubgraphManager.CreateAsync(
                    newSource.Node,
                    relationship,
                    GetNodeElementId(selectedTarget.Element, GraphEndpointRole.Target),
                    direction,
                    transaction,
                    cancellationToken),
            (NewGraphCommandEndpoint newSource,
                NewGraphCommandEndpoint newTarget,
                GraphRelationshipCreationMode.Standard) =>
                context.SubgraphManager.CreateAsync(
                    newSource.Node,
                    relationship,
                    newTarget.Node,
                    direction,
                    transaction,
                    cancellationToken),
            (NewGraphCommandEndpoint newSource,
                NewGraphCommandEndpoint,
                GraphRelationshipCreationMode.SelfLoop) =>
                context.SubgraphManager.CreateSelfLoopAsync(
                    newSource.Node,
                    relationship,
                    transaction,
                    cancellationToken),
            _ => throw new GraphException("The relationship endpoint command has an invalid operand combination."),
        };

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
