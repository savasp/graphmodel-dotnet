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
}
