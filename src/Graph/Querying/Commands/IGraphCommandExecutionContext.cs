// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Querying.Commands;

using System.Linq.Expressions;

/// <summary>Executes target selection and mutation inside one active provider write transaction.</summary>
internal interface IGraphCommandExecutionContext
{
    IGraphTransaction Transaction { get; }

    Task<IReadOnlyList<SelectedGraphElement>> SelectAsync(
        GraphElementSelectionModel selection,
        Expression sourceExpression,
        CancellationToken cancellationToken);

    Task<int> ApplyAsync(
        GraphMutationModel mutation,
        Expression mutationExpression,
        CancellationToken cancellationToken);
}
