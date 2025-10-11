// Copyright 2025 Savas Parastatidis
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//      https://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

namespace Cvoya.Graph.Model.Tests;

/// <summary>
/// Tests for the ToDynamic extension methods.
/// </summary>
public class ToDynamicExtensionsTests
{
    /// <summary>
    /// Test node record for testing ToDynamic functionality.
    /// </summary>
    public record TestNode : Node
    {
        public string Name { get; init; } = string.Empty;
        public int Age { get; init; }
        public string Email { get; init; } = string.Empty;
    }

    /// <summary>
    /// Test relationship record for testing ToDynamic functionality.
    /// </summary>
    public record TestRelationship(string StartNodeId, string EndNodeId) : Relationship(StartNodeId, EndNodeId)
    {
        public string Description { get; init; } = string.Empty;
        public DateTime CreatedAt { get; init; }
        public bool IsActive { get; init; }
    }

    [Fact]
    public void ToDynamicNode_ConvertsStronglyTypedNodeToDynamicNode()
    {
        // Arrange
        var testNode = new TestNode
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
        Assert.Equal(testNode.Labels, dynamicNode.Labels);
        Assert.Equal("John Doe", dynamicNode.Properties["Name"]);
        Assert.Equal(30, dynamicNode.Properties["Age"]);
        Assert.Equal("john@example.com", dynamicNode.Properties["Email"]);
    }

    [Fact]
    public void ToDynamicRelationship_ConvertsStronglyTypedRelationshipToDynamicRelationship()
    {
        // Arrange
        var testRelationship = new TestRelationship("node1", "node2")
        {
            Description = "Test relationship",
            CreatedAt = DateTime.Now,
            IsActive = true
        };

        // Act
        var dynamicRelationship = testRelationship.ToDynamic();

        // Assert
        Assert.NotNull(dynamicRelationship);
        Assert.Equal(testRelationship.Id, dynamicRelationship.Id);
        Assert.Equal(testRelationship.Type, dynamicRelationship.Type);
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
        var testNode = new TestNode { Name = "Alice", Age = 25, Email = "alice@example.com" };
        var testRelationship = new TestRelationship("node1", "node2") { Description = "Test", IsActive = true };

        // Act
        INode nodeInterface = testNode;
        IRelationship relInterface = testRelationship;

        var dynamicNodeFromInterface = nodeInterface.ToDynamic();
        var dynamicRelFromInterface = relInterface.ToDynamic();

        // Assert
        Assert.NotNull(dynamicNodeFromInterface);
        Assert.NotNull(dynamicRelFromInterface);
        Assert.Equal("Alice", dynamicNodeFromInterface.Properties["Name"]);
        Assert.Equal("Test", dynamicRelFromInterface.Properties["Description"]);
    }

    [Fact]
    public void ToDynamicNode_WithSpecificType_ConvertsCorrectly()
    {
        // Arrange
        var testNode = new TestNode { Name = "Bob", Age = 35, Email = "bob@example.com" };

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
        var testRelationship = new TestRelationship("node1", "node2")
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
        var testNode = new TestNode { Name = "Test", Age = 42, Email = "test@example.com" };

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
}
