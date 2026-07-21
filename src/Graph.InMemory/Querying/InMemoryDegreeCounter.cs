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
    /// Counts relationships assignable to <paramref name="relationshipType"/> incident to
    /// <paramref name="node"/> in the given <paramref name="direction"/>.
    /// </summary>
    internal static int Count(
        StoreState state,
        INode node,
        IReadOnlyDictionary<object, Guid> nodeKeys,
        Type relationshipType,
        GraphTraversalDirection direction)
    {
        if (!nodeKeys.TryGetValue(node, out var key))
        {
            throw new GraphException("The in-memory degree projection lost its private node binding.");
        }

        var count = 0;

        foreach (var relationship in state.Relationships.Values)
        {
            // Internal complex-property edges are keyed by property name, never a user relationship
            // type, and have no CLR type. Match assignability so counting a base relationship also
            // includes its stored derived relationship types, like typed graph queries do.
            if (relationship.IsComplexProperty ||
                relationship.ActualType is null ||
                !relationshipType.IsAssignableFrom(relationship.ActualType))
            {
                continue;
            }

            // Direction follows the physical edge, which may be reversed from logical Start/End by
            // RelationshipDirection.Incoming. An undirected Cypher pattern returns a self-loop once.
            count += direction switch
            {
                GraphTraversalDirection.Outgoing => relationship.PhysicalSourceKey == key ? 1 : 0,
                GraphTraversalDirection.Incoming => relationship.PhysicalTargetKey == key ? 1 : 0,
                GraphTraversalDirection.Both =>
                    relationship.PhysicalSourceKey == key || relationship.PhysicalTargetKey == key ? 1 : 0,
                _ => throw new ArgumentOutOfRangeException(nameof(direction)),
            };
        }

        return count;
    }
}
