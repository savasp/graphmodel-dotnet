// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Core.Tests;

/// <summary>
/// Tests for the ToDynamic extension methods.
/// </summary>
public class ToDynamicExtensionsTests
{
    private static readonly DateTime FixedCreatedAt = new(2026, 1, 15, 12, 0, 0, DateTimeKind.Utc);

    /// <summary>
    /// Test node record for testing ToDynamic functionality.
    /// </summary>
    public record DynamicTestNode : Node
    {
        public string Name { get; init; } = string.Empty;
        public int Age { get; init; }
        public string Email { get; init; } = string.Empty;
    }

    /// <summary>
    /// Test relationship record for testing ToDynamic functionality.
    /// </summary>
    public record DynamicTestRelationship(string StartNodeId, string EndNodeId) : Relationship(StartNodeId, EndNodeId)
    {
        public string Description { get; init; } = string.Empty;
        public DateTime CreatedAt { get; init; }
        public bool IsActive { get; init; }
    }

    private sealed record CustomLabelNode : Node
    {
        [Property(Label = "display_name")]
        public string DisplayName { get; init; } = string.Empty;
    }

    [Node("ISSUE_385_ATTRIBUTED_NODE")]
    private sealed record AttributedNode : Node;

    [Relationship("ISSUE_385_ATTRIBUTED_RELATIONSHIP")]
    private sealed record AttributedRelationship(string Start, string End) : Relationship(Start, End);

    private sealed record IndexedNode : Node
    {
        public string Name { get; init; } = string.Empty;

        public string this[int index] => throw new InvalidOperationException($"Indexer {index} should not be read.");
    }

    private sealed record NonPublicGetterNode : Node
    {
        public string Name { get; init; } = string.Empty;

        public string WriteOnly { private get; set; } = "hidden";
    }

    private sealed record ThrowingGetterNode : Node
    {
        public string Unreadable
        {
            get
            {
                _ = Id;
                throw new InvalidOperationException("Modeled property could not be read.");
            }
        }
    }

    private sealed record CrossEntityMetadataNamesNode : Node
    {
        public string Type { get; init; } = string.Empty;
        public string Direction { get; init; } = string.Empty;
        public string StartNodeId { get; init; } = string.Empty;
        public string EndNodeId { get; init; } = string.Empty;
    }

    private sealed record CrossEntityMetadataNamesRelationship(string Start, string End) : Relationship(Start, End)
    {
        public string Labels { get; init; } = string.Empty;
    }

    [Fact]
    public void ToDynamicNode_ConvertsStronglyTypedNodeToDynamicNode()
    {
        // Arrange
        var testNode = new DynamicTestNode
        {
            Name = "John Doe",
            Age = 30,
            Email = "john@example.com"
        };

        // Act
        var dynamicNode = testNode.ToDynamic();

        // Assert
        Assert.NotNull(dynamicNode);
        Assert.Equal(testNode.Id, dynamicNode.Id);
        Assert.Equal(Labels.GetCompatibleLabels(testNode.GetType()), dynamicNode.Labels);
        Assert.Equal("John Doe", dynamicNode.Properties["Name"]);
        Assert.Equal(30, dynamicNode.Properties["Age"]);
        Assert.Equal("john@example.com", dynamicNode.Properties["Email"]);
    }

    [Fact]
    public void ToDynamicNode_UsesPhysicalPropertyLabels()
    {
        var node = new CustomLabelNode { DisplayName = "Ada" };

        var dynamicNode = node.ToDynamicNode();

        Assert.Equal("Ada", dynamicNode.Properties["display_name"]);
        Assert.False(dynamicNode.Properties.ContainsKey(nameof(CustomLabelNode.DisplayName)));
    }

    [Fact]
    public void ToDynamicRelationship_ConvertsStronglyTypedRelationshipToDynamicRelationship()
    {
        // Arrange
        var testRelationship = new DynamicTestRelationship("node1", "node2")
        {
            Description = "Test relationship",
            CreatedAt = FixedCreatedAt,
            IsActive = true
        };

        // Act
        var dynamicRelationship = testRelationship.ToDynamic();

        // Assert
        Assert.NotNull(dynamicRelationship);
        Assert.Equal(testRelationship.Id, dynamicRelationship.Id);
        Assert.Equal(Labels.GetLabelFromType(testRelationship.GetType()), dynamicRelationship.Type);
        Assert.Equal(testRelationship.Direction, dynamicRelationship.Direction);
        Assert.Equal(testRelationship.StartNodeId, dynamicRelationship.StartNodeId);
        Assert.Equal(testRelationship.EndNodeId, dynamicRelationship.EndNodeId);
        Assert.Equal("Test relationship", dynamicRelationship.Properties["Description"]);
        Assert.True((bool)dynamicRelationship.Properties["IsActive"]!);
    }

    [Fact]
    public void ToDynamic_WithInterfaceTypes_ConvertsCorrectly()
    {
        // Arrange
        var testNode = new DynamicTestNode { Name = "Alice", Age = 25, Email = "alice@example.com" };
        var testRelationship = new DynamicTestRelationship("node1", "node2") { Description = "Test", IsActive = true };

        // Act
        INode nodeInterface = testNode;
        IRelationship relInterface = testRelationship;

        var dynamicNodeFromInterface = nodeInterface.ToDynamic();
        var dynamicRelFromInterface = relInterface.ToDynamic();

        // Assert
        Assert.NotNull(dynamicNodeFromInterface);
        Assert.NotNull(dynamicRelFromInterface);
        Assert.Equal(Labels.GetCompatibleLabels(testNode.GetType()), dynamicNodeFromInterface.Labels);
        Assert.Equal(Labels.GetLabelFromType(testRelationship.GetType()), dynamicRelFromInterface.Type);
        Assert.Equal("Alice", dynamicNodeFromInterface.Properties["Name"]);
        Assert.Equal("Test", dynamicRelFromInterface.Properties["Description"]);
    }

    [Fact]
    public void ToDynamic_DerivesAttributedMetadataFromRuntimeTypes()
    {
        INode node = new AttributedNode();
        IRelationship relationship = new AttributedRelationship("node1", "node2");

        var dynamicNode = node.ToDynamic();
        var dynamicRelationship = relationship.ToDynamic();

        Assert.Equal(["ISSUE_385_ATTRIBUTED_NODE"], dynamicNode.Labels);
        Assert.Equal("ISSUE_385_ATTRIBUTED_RELATIONSHIP", dynamicRelationship.Type);
    }

    [Fact]
    public void ToDynamic_PreservesPopulatedMetadataExactly()
    {
        IReadOnlyList<string> labels = ["CUSTOM_PRIMARY", "CUSTOM_SECONDARY"];
        var node = new DynamicTestNode { Labels = labels, Name = "Ada" };
        var relationship = new DynamicTestRelationship(node.Id, "node2")
        {
            Type = "CUSTOM_RELATIONSHIP_TYPE",
            Description = "Preserved",
        };

        var dynamicNode = node.ToDynamicNode();
        var dynamicRelationship = relationship.ToDynamicRelationship();

        Assert.Equal(labels, dynamicNode.Labels);
        Assert.Equal("CUSTOM_RELATIONSHIP_TYPE", dynamicRelationship.Type);
    }

    [Fact]
    public void ToDynamicNode_IgnoresIndexersAndPropertiesWithoutPublicGetters()
    {
        var indexed = new IndexedNode { Name = "Ada" }.ToDynamicNode();
        var nonPublicGetter = new NonPublicGetterNode { Name = "Grace", WriteOnly = "ignored" }.ToDynamicNode();

        Assert.Equal("Ada", indexed.Properties[nameof(IndexedNode.Name)]);
        Assert.DoesNotContain("Item", indexed.Properties.Keys);
        Assert.Equal("Grace", nonPublicGetter.Properties[nameof(NonPublicGetterNode.Name)]);
        Assert.DoesNotContain(nameof(NonPublicGetterNode.WriteOnly), nonPublicGetter.Properties.Keys);
    }

    [Fact]
    public void ToDynamicNode_PropagatesOriginalGetterException()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => new ThrowingGetterNode().ToDynamicNode());

        Assert.Equal("Modeled property could not be read.", exception.Message);
        Assert.Contains("get_Unreadable", exception.StackTrace, StringComparison.Ordinal);
    }

    [Fact]
    public void ToDynamicNode_WithSpecificType_ConvertsCorrectly()
    {
        // Arrange
        var testNode = new DynamicTestNode { Name = "Bob", Age = 35, Email = "bob@example.com" };

        // Act
        var dynamicNode = testNode.ToDynamicNode();

        // Assert
        Assert.NotNull(dynamicNode);
        Assert.Equal("Bob", dynamicNode.Properties["Name"]);
        Assert.Equal(35, dynamicNode.Properties["Age"]);
    }

    [Fact]
    public void ToDynamicRelationship_WithSpecificType_ConvertsCorrectly()
    {
        // Arrange
        var testRelationship = new DynamicTestRelationship("node1", "node2")
        {
            Description = "Specific test",
            IsActive = false
        };

        // Act
        var dynamicRelationship = testRelationship.ToDynamicRelationship();

        // Assert
        Assert.NotNull(dynamicRelationship);
        Assert.Equal("Specific test", dynamicRelationship.Properties["Description"]);
        Assert.False((bool)dynamicRelationship.Properties["IsActive"]!);
    }

    [Fact]
    public void ToDynamic_ExcludesBaseProperties()
    {
        // Arrange
        var testNode = new DynamicTestNode { Name = "Test", Age = 42, Email = "test@example.com" };

        // Act
        var dynamicNode = testNode.ToDynamic();

        // Assert
        // Base properties should not be included in the Properties dictionary
        Assert.False(dynamicNode.Properties.ContainsKey(nameof(IEntity.Id)));
        Assert.False(dynamicNode.Properties.ContainsKey(nameof(INode.Labels)));

        // Custom properties should be included
        Assert.True(dynamicNode.Properties.ContainsKey("Name"));
        Assert.True(dynamicNode.Properties.ContainsKey("Age"));
        Assert.True(dynamicNode.Properties.ContainsKey("Email"));
    }

    [Fact]
    public void ToDynamic_PreservesModeledPropertiesNamedForTheOtherEntityKind()
    {
        var node = new CrossEntityMetadataNamesNode
        {
            Type = "modeled type",
            Direction = "modeled direction",
            StartNodeId = "modeled start",
            EndNodeId = "modeled end",
        };
        var relationship = new CrossEntityMetadataNamesRelationship("node1", "node2")
        {
            Labels = "modeled labels",
        };

        var dynamicNode = node.ToDynamicNode();
        var dynamicRelationship = relationship.ToDynamicRelationship();

        Assert.Equal(node.Type, dynamicNode.Properties[nameof(node.Type)]);
        Assert.Equal(node.Direction, dynamicNode.Properties[nameof(node.Direction)]);
        Assert.Equal(node.StartNodeId, dynamicNode.Properties[nameof(node.StartNodeId)]);
        Assert.Equal(node.EndNodeId, dynamicNode.Properties[nameof(node.EndNodeId)]);
        Assert.Equal(relationship.Labels, dynamicRelationship.Properties[nameof(relationship.Labels)]);
    }
}
