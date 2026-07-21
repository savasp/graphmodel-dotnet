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
    AgeGraphContext context) : IGraphCommandExecutionContext
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
        GraphCommandEndpoint source,
        IRelationship relationship,
        GraphCommandEndpoint target,
        RelationshipDirection direction,
        GraphRelationshipCreationMode mode,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(relationship);
        ArgumentNullException.ThrowIfNull(target);

        if (!Enum.IsDefined(direction))
        {
            throw new ArgumentOutOfRangeException(nameof(direction));
        }

        if (!Enum.IsDefined(mode))
        {
            throw new ArgumentOutOfRangeException(nameof(mode));
        }

        if (mode == GraphRelationshipCreationMode.Standard &&
            source is SelectedGraphCommandEndpoint selectedSource &&
            target is SelectedGraphCommandEndpoint selectedTarget)
        {
            await context.RelationshipManager.CreateRelationshipForCommandAsync(
                relationship,
                GetGraphId(selectedSource, GraphEndpointRole.Source),
                GetGraphId(selectedTarget, GraphEndpointRole.Target),
                direction,
                transaction,
                cancellationToken).ConfigureAwait(false);
            return;
        }

        await context.SubgraphManager.CreateSubgraphAsync(
            source,
            relationship,
            target,
            direction,
            mode,
            transaction,
            cancellationToken).ConfigureAwait(false);
    }

    private static long GetGraphId(
        SelectedGraphCommandEndpoint endpoint,
        GraphEndpointRole role) => endpoint switch
        {
            { Element.Kind: GraphElementKind.Node, Element.NativeIdentity: long graphId } => graphId,
            _ => throw new GraphException(
                $"The selected {role.ToString().ToLowerInvariant()} endpoint is not an AGE node graphid."),
        };
}
