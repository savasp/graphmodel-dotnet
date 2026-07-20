// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Core.Tests;

public class LegacyRelationshipEndpointsTests
{
    [Theory]
    [InlineData(RelationshipDirection.Outgoing, RelationshipDirection.Outgoing, "start", "end")]
    [InlineData(RelationshipDirection.Outgoing, RelationshipDirection.Incoming, "end", "start")]
    [InlineData(RelationshipDirection.Incoming, RelationshipDirection.Outgoing, "end", "start")]
    [InlineData(RelationshipDirection.Incoming, RelationshipDirection.Incoming, "start", "end")]
    public void Populate_CombinesSegmentAndLegacyDirections(
        RelationshipDirection segmentDirection,
        RelationshipDirection legacyDirection,
        string expectedStart,
        string expectedEnd)
    {
        var segment = new TestSegment(
            new LegacyEndpointNode { Id = "start" },
            new LegacyEndpointRelationship(string.Empty, string.Empty, legacyDirection),
            new LegacyEndpointNode { Id = "end" },
            segmentDirection);

        var relationship = LegacyRelationshipEndpoints.Populate<LegacyEndpointRelationship>(segment);

        Assert.Equal(expectedStart, relationship.StartNodeId);
        Assert.Equal(expectedEnd, relationship.EndNodeId);
    }

    private sealed record LegacyEndpointNode : Node;

    private sealed record LegacyEndpointRelationship(
        string StartNodeId,
        string EndNodeId,
        RelationshipDirection Direction) : Relationship(StartNodeId, EndNodeId, Direction);

    private sealed record TestSegment(
        INode StartNode,
        IRelationship Relationship,
        INode EndNode,
        RelationshipDirection Direction) : IGraphPathSegment;
}
