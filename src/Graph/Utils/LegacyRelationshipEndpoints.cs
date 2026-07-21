// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph;

/// <summary>
/// Restores the transitional endpoint tuple for legacy direct-ID reads from an endpoint-bearing
/// path segment. Query projections must not use this helper.
/// </summary>
internal static class LegacyRelationshipEndpoints
{
    /// <summary>
    /// Gets the transitional storage direction of the legacy relationship model, defaulting to
    /// <see cref="RelationshipDirection.Outgoing"/> for relationships that do not derive from
    /// <see cref="Relationship"/>.
    /// </summary>
    public static RelationshipDirection LegacyDirection(IRelationship relationship) =>
        relationship is Relationship { Direction: var direction }
            ? direction
            : RelationshipDirection.Outgoing;

    /// <summary>Populates the legacy endpoint tuple and returns the segment relationship.</summary>
    public static TRelationship Populate<TRelationship>(IGraphPathSegment segment)
        where TRelationship : class, IRelationship
    {
        ArgumentNullException.ThrowIfNull(segment);

        var relationship = (TRelationship)segment.Relationship;
        var legacyDirection = LegacyDirection(relationship);
        var physicalStart = segment.Direction == RelationshipDirection.Outgoing
            ? segment.StartNode.Id
            : segment.EndNode.Id;
        var physicalEnd = segment.Direction == RelationshipDirection.Outgoing
            ? segment.EndNode.Id
            : segment.StartNode.Id;
        var logicalStart = legacyDirection == RelationshipDirection.Outgoing ? physicalStart : physicalEnd;
        var logicalEnd = legacyDirection == RelationshipDirection.Outgoing ? physicalEnd : physicalStart;

        SetEndpoint(relationship, nameof(IRelationship.StartNodeId), logicalStart);
        SetEndpoint(relationship, nameof(IRelationship.EndNodeId), logicalEnd);
        return relationship;
    }

    private static void SetEndpoint(IRelationship relationship, string propertyName, string value)
    {
        var property = relationship.GetType().GetProperty(propertyName)
            ?? typeof(IRelationship).GetProperty(propertyName);
        if (property?.SetMethod is null)
        {
            throw new GraphException(
                $"Relationship type '{relationship.GetType().FullName}' does not expose writable legacy endpoint '{propertyName}'.");
        }

        property.SetValue(relationship, value);
    }
}
