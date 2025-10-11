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

using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

// Test entities for property configuration testing
[Node("ConfigTestPerson")]
public record ConfigTestPerson : Node
{
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
public record ConfigTestKnows : Relationship
{
    public ConfigTestKnows() : base(string.Empty, string.Empty) { }
    public ConfigTestKnows(string startNodeId, string endNodeId) : base(startNodeId, endNodeId) { }

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
    public async Task SchemaRegistryMethods_WorkCorrectly()
    {
        // Arrange - Initialize the registry to discover types
        await Registry.InitializeAsync(TestContext.Current.CancellationToken);

        // Act & Assert - Check that the registry contains our test types
        var nodeLabels = await Registry.GetRegisteredNodeLabelsAsync(TestContext.Current.CancellationToken);
        var relationshipTypes = await Registry.GetRegisteredRelationshipTypesAsync(TestContext.Current.CancellationToken);

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
    public async Task SchemaRegistry_WithIndexedProperty_ConfigurationStored()
    {
        // Arrange
        await Registry.InitializeAsync(TestContext.Current.CancellationToken);

        // Act & Assert
        var nodeSchema = Registry.GetNodeSchema("ConfigTestPerson");
        Assert.NotNull(nodeSchema);
        Assert.True(nodeSchema.Properties.ContainsKey("FirstName"));
        Assert.True(nodeSchema.Properties["FirstName"].IsIndexed);
        Assert.True(nodeSchema.Properties["FirstName"].IsRequired);
        Assert.True(nodeSchema.Properties["Email"].IsUnique);
    }

    [Fact]
    public async Task SchemaRegistry_WithRelationshipConfiguration_WorksCorrectly()
    {
        // Arrange
        await Registry.InitializeAsync(TestContext.Current.CancellationToken);

        // Act & Assert
        var relSchema = Registry.GetRelationshipSchema("ConfigTestKnows");
        Assert.NotNull(relSchema);
        Assert.True(relSchema.Properties.ContainsKey("Since"));
        Assert.True(relSchema.Properties["Since"].IsIndexed);
        Assert.True(relSchema.Properties.ContainsKey("Strength"));
        Assert.True(relSchema.Properties["Strength"].IsIndexed);
    }

    [Fact]
    public async Task SchemaRegistry_WithValidationRules_ValidatesCorrectly()
    {
        // Arrange
        await Registry.InitializeAsync(TestContext.Current.CancellationToken);

        // Act & Assert - Test that the schema registry contains our test types
        var nodeSchema = await Registry.GetNodeSchemaAsync("ConfigTestPerson", TestContext.Current.CancellationToken);
        Assert.NotNull(nodeSchema);
        Assert.True(nodeSchema.Properties.ContainsKey("FirstName"));
        Assert.True(nodeSchema.Properties["FirstName"].IsRequired);
        Assert.True(nodeSchema.Properties["FirstName"].IsIndexed);
    }

    [Fact]
    public async Task SchemaRegistry_WithCustomPropertyName_WorksCorrectly()
    {
        // Arrange
        await Registry.InitializeAsync(TestContext.Current.CancellationToken);

        // Act & Assert - Test that property names are correctly resolved
        var nodeSchema = await Registry.GetNodeSchemaAsync("ConfigTestPerson", TestContext.Current.CancellationToken);
        Assert.NotNull(nodeSchema);
        Assert.True(nodeSchema.Properties.ContainsKey("FirstName"));
        Assert.Equal("FirstName", nodeSchema.Properties["FirstName"].Name); // Uses property name by default
    }

    [Fact]
    public async Task SchemaRegistry_WithMultipleConfigurations_WorksCorrectly()
    {
        // Arrange
        await Registry.InitializeAsync(TestContext.Current.CancellationToken);

        // Act & Assert
        var nodeSchema = await Registry.GetNodeSchemaAsync("ConfigTestPerson", TestContext.Current.CancellationToken);
        Assert.NotNull(nodeSchema);
        Assert.True(nodeSchema.Properties.ContainsKey("FirstName"));
        Assert.True(nodeSchema.Properties.ContainsKey("LastName"));
        Assert.True(nodeSchema.Properties.ContainsKey("Email"));
        Assert.True(nodeSchema.Properties["FirstName"].IsIndexed);
        Assert.True(nodeSchema.Properties["Email"].IsUnique);
        Assert.True(nodeSchema.Properties["FirstName"].IsRequired);
    }

    [Fact]
    public async Task SchemaRegistryClear_WorksCorrectly()
    {
        // Arrange
        await Registry.InitializeAsync(TestContext.Current.CancellationToken);
        Assert.True(Registry.IsInitialized);

        // Act
        await Registry.ClearAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.False(Registry.IsInitialized);
        var nodeLabels = await Registry.GetRegisteredNodeLabelsAsync(TestContext.Current.CancellationToken);
        Assert.Empty(nodeLabels);
    }

    [Fact]
    public async Task Graph_WithSchemaRegistry_InitializesCorrectly()
    {
        // The registry is initialized on first use of the graph instance.
        // TODO: This behavior needs to change. The registry should be initialized
        // when the graph is ready to use. We can add an initialization method to the graph.

        var person = new Person { FirstName = "Test", LastName = "User" };
        await Graph.CreateNodeAsync(person, null, TestContext.Current.CancellationToken);

        // Assert - Verify that the schema registry is initialized
        Assert.True(Registry.IsInitialized);
        var nodeLabels = await Registry.GetRegisteredNodeLabelsAsync(TestContext.Current.CancellationToken);
        var relationshipTypes = await Registry.GetRegisteredRelationshipTypesAsync(TestContext.Current.CancellationToken);

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
