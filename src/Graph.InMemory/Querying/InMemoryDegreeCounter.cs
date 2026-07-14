// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.InMemory;

/// <summary>
/// Computes a node's relationship count (degree) restricted to one relationship type and direction,
/// directly against a <see cref="StoreState"/> snapshot. Backs the in-memory rewrite of the
/// <see cref="GraphDegreeExtensions.CountRelationships{TRel}(INode, GraphTraversalDirection)"/>
/// projection marker.
/// </summary>
internal static class InMemoryDegreeCounter
{
    /// <summary>
    /// Counts the relationships of type <paramref name="typeLabel"/> incident to
    /// <paramref name="node"/> in the given <paramref name="direction"/>.
    /// </summary>
    public static int Count(
        StoreState state,
        INode node,
        string typeLabel,
        GraphTraversalDirection direction)
    {
        var id = node.Id;
        var count = 0;

        foreach (var relationship in state.Relationships.Values)
        {
            // Internal complex-property edges are keyed by property name, never a user relationship
            // type, so the type-label match already excludes them; the guard makes that explicit.
            if (relationship.IsComplexProperty ||
                !string.Equals(relationship.Type, typeLabel, StringComparison.Ordinal))
            {
                continue;
            }

            // For Both, count each incident end separately so a self-loop contributes 2 — matching
            // Cypher's undirected `COUNT { (src)-[:R]-() }` (which traverses a self-loop in both
            // directions) and the graph-theory degree where a self-loop counts twice.
            count += direction switch
            {
                GraphTraversalDirection.Outgoing => relationship.StartNodeId == id ? 1 : 0,
                GraphTraversalDirection.Incoming => relationship.EndNodeId == id ? 1 : 0,
                GraphTraversalDirection.Both =>
                    (relationship.StartNodeId == id ? 1 : 0) + (relationship.EndNodeId == id ? 1 : 0),
                _ => 0,
            };
        }

        return count;
    }
}
