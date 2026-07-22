// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Querying.Commands;

/// <summary>Validates graph and bound-transaction scope before selected-endpoint I/O.</summary>
internal static class GraphCommandProviderScope
{
    public static void ValidateGraph(IGraphCommandProvider graph, IGraphCommandProvider query)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(query);

        if (!ReferenceEquals(graph.GraphOwnershipToken, query.GraphOwnershipToken))
        {
            throw new GraphException("A selected relationship endpoint must belong to the receiver graph instance.");
        }
    }

    public static void Validate(IGraphCommandProvider first, IGraphCommandProvider second)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);

        if (!ReferenceEquals(first.GraphOwnershipToken, second.GraphOwnershipToken))
        {
            throw new GraphException("Selected graph endpoints must belong to the same graph instance.");
        }

        if (!ReferenceEquals(first.BoundTransaction, second.BoundTransaction))
        {
            throw new GraphException(
                "Selected graph endpoints must both be unbound or be bound to the same transaction object.");
        }
    }
}
