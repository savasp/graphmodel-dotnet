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
    CypherEngine engine) : IGraphCommandExecutionContext
{
    public IGraphTransaction Transaction => transaction;

    public Task<IReadOnlyList<SelectedGraphElement>> SelectAsync(
        GraphElementSelectionModel selection,
        Expression sourceExpression,
        CancellationToken cancellationToken) =>
        engine.SelectNativeAsync(selection, sourceExpression, transaction, cancellationToken);

    public Task<int> ApplyAsync(
        GraphMutationModel mutation,
        Expression mutationExpression,
        CancellationToken cancellationToken) =>
        engine.ApplyMutationAsync(mutation, mutationExpression, transaction, cancellationToken);
}
