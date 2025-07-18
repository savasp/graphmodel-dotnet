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

using Xunit;

// Test entities for property configuration testing
[Node("ConfigTestPerson")]
public class ConfigTestPerson : INode
{
    [Property(IsKey = true)]
    public string Id { get; init; } = Guid.NewGuid().ToString("N");

    [Property(IsRequired = true, IsIndexed = true)]
    public string FirstName { get; set; } = string.Empty;

    [Property(IsIndexed = true)]
    public string LastName { get; set; } = string.Empty;

    [Property(IsRequired = true, IsUnique = true, IsIndexed = true)]
    public string Email { get; set; } = string.Empty;

    [Property(IsIndexed = true)]
    public int Age { get; set; }

    public string? OptionalField { get; set; }
}

[Relationship("ConfigTestKnows")]
public class ConfigTestKnows : IRelationship
{
    [Property(IsKey = true)]
    public string Id { get; init; } = Guid.NewGuid().ToString("N");

    public string StartNodeId { get; init; } = string.Empty;
    public string EndNodeId { get; init; } = string.Empty;
    public RelationshipDirection Direction { get; init; } = RelationshipDirection.Outgoing;

    [Property(IsIndexed = true)]
    public DateTime Since { get; set; } = DateTime.UtcNow;

    [Property(IsIndexed = true)]
    public int Strength { get; set; }

    public string? Notes { get; set; }
}

public interface ISchemaDefinitionTests : IGraphModelTest
{
    SchemaRegistry Registry => Graph.SchemaRegistry;

    [Fact]
    public void SchemaRegistryMethods_WorkCorrectly()
    {
        // Arrange - Initialize the registry to discover types
        Registry.Initialize();

        // Act & Assert - Check that the registry contains our test types
        var nodeLabels = Registry.GetRegisteredNodeLabels().ToList();
        var relationshipTypes = Registry.GetRegisteredRelationshipTypes().ToList();

        Assert.Contains("ConfigTestPerson", nodeLabels);
        Assert.Contains("ConfigTestKnows", relationshipTypes);

        var nodeSchema = Registry.GetNodeSchema("ConfigTestPerson");
        Assert.NotNull(nodeSchema);
        Assert.Equal("ConfigTestPerson", nodeSchema.Label);
        Assert.True(nodeSchema.Properties.ContainsKey("FirstName"));
        Assert.True(nodeSchema.Properties.ContainsKey("LastName"));
        Assert.True(nodeSchema.Properties.ContainsKey("Email"));
    }

    [Fact]
    public void SchemaRegistry_WithIndexedProperty_ConfigurationStored()
    {
        // Arrange
        Registry.Initialize();

        // Act & Assert
        var nodeSchema = Registry.GetNodeSchema("ConfigTestPerson");
        Assert.NotNull(nodeSchema);
        Assert.True(nodeSchema.Properties.ContainsKey("FirstName"));
        Assert.True(nodeSchema.Properties["FirstName"].IsIndexed);
        Assert.True(nodeSchema.Properties["FirstName"].IsRequired);
        Assert.True(nodeSchema.Properties["Email"].IsUnique);
    }

    [Fact]
    public void SchemaRegistry_WithRelationshipConfiguration_WorksCorrectly()
    {
        // Arrange
        Registry.Initialize();

        // Act & Assert
        var relSchema = Registry.GetRelationshipSchema("ConfigTestKnows");
        Assert.NotNull(relSchema);
        Assert.True(relSchema.Properties.ContainsKey("Since"));
        Assert.True(relSchema.Properties["Since"].IsIndexed);
        Assert.True(relSchema.Properties.ContainsKey("Strength"));
        Assert.True(relSchema.Properties["Strength"].IsIndexed);
    }

    [Fact]
    public void SchemaRegistry_WithValidationRules_ValidatesCorrectly()
    {
        // Arrange
        Registry.Initialize();

        // Act & Assert - Test that the schema registry contains our test types
        var nodeSchema = Registry.GetNodeSchema("ConfigTestPerson");
        Assert.NotNull(nodeSchema);
        Assert.True(nodeSchema.Properties.ContainsKey("FirstName"));
        Assert.True(nodeSchema.Properties["FirstName"].IsRequired);
        Assert.True(nodeSchema.Properties["FirstName"].IsIndexed);
    }

    [Fact]
    public void SchemaRegistry_WithCustomPropertyName_WorksCorrectly()
    {
        // Arrange
        Registry.Initialize();

        // Act & Assert - Test that property names are correctly resolved
        var nodeSchema = Registry.GetNodeSchema("ConfigTestPerson");
        Assert.NotNull(nodeSchema);
        Assert.True(nodeSchema.Properties.ContainsKey("FirstName"));
        Assert.Equal("FirstName", nodeSchema.Properties["FirstName"].Name); // Uses property name by default
    }

    [Fact]
    public void SchemaRegistry_WithMultipleConfigurations_WorksCorrectly()
    {
        // Arrange
        Registry.Initialize();

        // Act & Assert
        var nodeSchema = Registry.GetNodeSchema("ConfigTestPerson");
        Assert.NotNull(nodeSchema);
        Assert.True(nodeSchema.Properties.ContainsKey("FirstName"));
        Assert.True(nodeSchema.Properties.ContainsKey("LastName"));
        Assert.True(nodeSchema.Properties.ContainsKey("Email"));
        Assert.True(nodeSchema.Properties["FirstName"].IsIndexed);
        Assert.True(nodeSchema.Properties["Email"].IsUnique);
        Assert.True(nodeSchema.Properties["FirstName"].IsRequired);
    }

    [Fact]
    public void SchemaRegistryClear_WorksCorrectly()
    {
        // Arrange
        Registry.Initialize();
        Assert.True(Registry.IsInitialized);

        // Act
        Registry.Clear();

        // Assert
        Assert.False(Registry.IsInitialized);
        var nodeLabels = Registry.GetRegisteredNodeLabels();
        Assert.Empty(nodeLabels);
    }

    [Fact]
    public void Graph_WithSchemaRegistry_InitializesCorrectly()
    {
        // Assert - Verify that the schema registry is initialized
        Assert.True(Registry.IsInitialized);
        var nodeLabels = Registry.GetRegisteredNodeLabels().ToList();
        var relationshipTypes = Registry.GetRegisteredRelationshipTypes().ToList();

        Assert.Contains("ConfigTestPerson", nodeLabels);
        Assert.Contains("ConfigTestKnows", relationshipTypes);
    }

    [Fact]
    public async Task Graph_WithRequiredPropertyConfiguration_EnforcesRequiredFields()
    {
        // Act & Assert - Try to create node with missing required fields
        var person = new ConfigTestPerson
        {
            FirstName = "", // Empty string should be treated as missing for required fields
            Email = null! // Null should definitely fail
        };

        // This should throw an exception due to required field validation
        await Assert.ThrowsAsync<GraphException>(async () =>
        {
            await Graph.CreateNodeAsync(person, null, TestContext.Current.CancellationToken);
        });
    }

    [Fact]
    public async Task Graph_WithValidConfiguration_AllowsValidOperations()
    {
        // Act - Create valid person
        var validPerson = new ConfigTestPerson
        {
            FirstName = "John",
            LastName = "Doe",
            Email = "john.doe@example.com",
            Age = 30
        };

        await Graph.CreateNodeAsync(validPerson, null, TestContext.Current.CancellationToken);

        // Assert - Verify the person was created successfully
        var retrievedPerson = await Graph.GetNodeAsync<ConfigTestPerson>(validPerson.Id, null, TestContext.Current.CancellationToken);
        Assert.NotNull(retrievedPerson);
        Assert.Equal("John", retrievedPerson.FirstName);
        Assert.Equal("john.doe@example.com", retrievedPerson.Email);
        Assert.Equal(30, retrievedPerson.Age);
    }
}
