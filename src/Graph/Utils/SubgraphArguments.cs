// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph;

/// <summary>
/// Shared argument validation for the node–relationship–node subgraph create operation, so every
/// provider enforces the same contract: non-null arguments, non-empty ids, and a relationship whose
/// endpoints match the supplied source and target nodes.
/// </summary>
internal static class SubgraphArguments
{
    /// <summary>
    /// Validates the arguments of a subgraph create. Throws <see cref="System.ArgumentException"/>
    /// when an argument is null/empty or the relationship's <see cref="IRelationship.StartNodeId"/>
    /// and <see cref="IRelationship.EndNodeId"/> do not match <paramref name="source"/>'s and
    /// <paramref name="target"/>'s ids.
    /// </summary>
    public static void Validate(INode source, IRelationship relationship, INode target)
    {
        if (source is null)
            throw new ArgumentException("Source node cannot be null.", nameof(source));

        if (relationship is null)
            throw new ArgumentException("Relationship cannot be null.", nameof(relationship));

        if (target is null)
            throw new ArgumentException("Target node cannot be null.", nameof(target));

        if (string.IsNullOrEmpty(source.Id))
            throw new ArgumentException("Source node ID cannot be null or empty.", nameof(source));

        if (string.IsNullOrEmpty(relationship.Id))
            throw new ArgumentException("Relationship ID cannot be null or empty.", nameof(relationship));

        if (string.IsNullOrEmpty(target.Id))
            throw new ArgumentException("Target node ID cannot be null or empty.", nameof(target));

        if (!string.Equals(relationship.StartNodeId, source.Id, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"Relationship StartNodeId '{relationship.StartNodeId}' must equal the source node Id '{source.Id}'.",
                nameof(relationship));
        }

        if (!string.Equals(relationship.EndNodeId, target.Id, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"Relationship EndNodeId '{relationship.EndNodeId}' must equal the target node Id '{target.Id}'.",
                nameof(relationship));
        }
    }
}
