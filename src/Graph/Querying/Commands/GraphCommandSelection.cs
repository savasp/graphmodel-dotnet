// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Querying.Commands;

using System.Linq.Expressions;

/// <summary>Shared exact-one cardinality handling over provider-native selection references.</summary>
internal static class GraphCommandSelection
{
    public static async Task<SelectedGraphElement> SelectExactOneAsync(
        IGraphCommandExecutionContext context,
        GraphElementSelectionModel selection,
        Expression sourceExpression,
        GraphEndpointRole role,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(selection);
        ArgumentNullException.ThrowIfNull(sourceExpression);

        var exactSelection = selection.Mode == GraphElementSelectionMode.ExactOne
            ? selection
            : new GraphElementSelectionModel(selection.Query, GraphElementSelectionMode.ExactOne);
        GraphElementSelectionModelValidator.Validate(exactSelection);
        var selected = await context.SelectAsync(exactSelection, sourceExpression, cancellationToken)
            .ConfigureAwait(false);
        return selected.Count switch
        {
            0 => throw new GraphCardinalityException(role, GraphCardinalityFailure.Empty),
            1 => selected[0],
            _ => throw new GraphCardinalityException(role, GraphCardinalityFailure.Multiple),
        };
    }
}
