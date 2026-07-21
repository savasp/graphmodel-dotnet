// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Age.Querying.Commands;

using System.Linq.Expressions;
using Cvoya.Graph.Age.Core;
using Cvoya.Graph.Age.Querying.Cypher.Execution;
using Cvoya.Graph.Querying;
using Cvoya.Graph.Querying.Commands;

/// <summary>Executes Apache AGE commands inside one active write transaction.</summary>
internal sealed class AgeGraphCommandExecutionContext(
    AgeGraphTransaction transaction,
    CypherEngine engine,
    AgeGraphContext context) : IGraphRelationshipCommandExecutionContext
{
    public IGraphTransaction Transaction => transaction;

    public Task<IReadOnlyList<SelectedGraphElement>> SelectAsync(
        GraphElementSelectionModel selection,
        Expression sourceExpression,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sourceExpression);
        return engine.SelectNativeAsync(selection, sourceExpression, transaction, cancellationToken);
    }

    public Task<int> ApplyAsync(
        GraphMutationModel mutation,
        Expression mutationExpression,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(mutationExpression);
        return engine.ApplyMutationAsync(mutation, mutationExpression, transaction, cancellationToken);
    }

    public async Task CreateRelationshipAsync(
        GraphEndpointIntent source,
        IRelationship relationship,
        GraphEndpointIntent target,
        RelationshipDirection direction,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(relationship);
        ArgumentNullException.ThrowIfNull(target);

        var sourceGraphId = await ResolveAsync(source, cancellationToken).ConfigureAwait(false);
        var targetGraphId = ReferenceEquals(source, target)
            ? sourceGraphId
            : await ResolveAsync(target, cancellationToken).ConfigureAwait(false);
        await context.RelationshipManager.CreateRelationshipForCommandAsync(
            relationship,
            sourceGraphId,
            targetGraphId,
            direction,
            transaction,
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<long> ResolveAsync(
        GraphEndpointIntent endpoint,
        CancellationToken cancellationToken) => endpoint switch
        {
            SelectedGraphEndpoint { Element.Kind: GraphElementKind.Node, Element.NativeIdentity: long graphId } => graphId,
            SelectedGraphEndpoint => throw new GraphException("A relationship endpoint selection must identify a node."),
            NewGraphEndpoint { Node: { } node } => await context.NodeManager
                .CreateNodeForCommandAsync(node, transaction, cancellationToken)
                .ConfigureAwait(false),
            _ => throw new GraphException("Unsupported relationship endpoint intent."),
        };
}
