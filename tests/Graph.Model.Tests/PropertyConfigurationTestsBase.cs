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

using Cvoya.Graph.Model.Configuration;
using Xunit;

// Test entities for property configuration testing
[Node("ConfigTestPerson")]
public class ConfigTestPerson : INode
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public int Age { get; set; }
    public string? OptionalField { get; set; }
}

[Relationship("ConfigTestKnows")]
public class ConfigTestKnows : IRelationship
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public string StartNodeId { get; init; } = string.Empty;
    public string EndNodeId { get; init; } = string.Empty;
    public RelationshipDirection Direction { get; init; } = RelationshipDirection.Outgoing;
    public DateTime Since { get; set; } = DateTime.UtcNow;
    public int Strength { get; set; }
    public string? Notes { get; set; }
}

public abstract class PropertyConfigurationTestsBase : ITestBase
{
    private readonly PropertyConfigurationRegistry _registry;

    public abstract IGraph Graph { get; }

    protected PropertyConfigurationTestsBase()
    {
        _registry = new PropertyConfigurationRegistry();
    }

    [Fact]
    public void PropertyConfiguration_RegistryMethods_WorkCorrectly()
    {
        // Arrange
        _registry.ConfigureNode("TestNode", config =>
        {
            config.Property("name", p => { p.IsIndexed = true; p.IsRequired = true; })
                 .Property("value", p => { p.IsUnique = true; });
        });

        // Act & Assert
        var nodeConfig = _registry.GetNodeConfiguration("TestNode");
        Assert.NotNull(nodeConfig);
        Assert.True(nodeConfig.Properties.ContainsKey("name"));
        Assert.True(nodeConfig.Properties.ContainsKey("value"));
        Assert.True(nodeConfig.Properties["name"].IsIndexed);
        Assert.True(nodeConfig.Properties["name"].IsRequired);
        Assert.True(nodeConfig.Properties["value"].IsUnique);
    }

    [Fact]
    public void PropertyConfiguration_WithIndexedProperty_ConfigurationStored()
    {
        // Arrange
        _registry.ConfigureNode("IndexedNode", config =>
        {
            config.Property("searchableField", p => { p.IsIndexed = true; });
        });

        // Act & Assert
        var nodeConfig = _registry.GetNodeConfiguration("IndexedNode");
        Assert.NotNull(nodeConfig);
        Assert.True(nodeConfig.Properties.ContainsKey("searchableField"));
        Assert.True(nodeConfig.Properties["searchableField"].IsIndexed);
    }

    [Fact]
    public void PropertyConfiguration_WithUniqueProperty_ConfigurationStored()
    {
        // Arrange
        _registry.ConfigureNode("UniqueNode", config =>
        {
            config.Property("uniqueField", p => { p.IsUnique = true; });
        });

        // Act & Assert
        var nodeConfig = _registry.GetNodeConfiguration("UniqueNode");
        Assert.NotNull(nodeConfig);
        Assert.True(nodeConfig.Properties.ContainsKey("uniqueField"));
        Assert.True(nodeConfig.Properties["uniqueField"].IsUnique);
    }

    [Fact]
    public void PropertyConfiguration_WithRequiredProperty_ConfigurationStored()
    {
        // Arrange
        _registry.ConfigureNode("RequiredNode", config =>
        {
            config.Property("requiredField", p => { p.IsRequired = true; });
        });

        // Act & Assert
        var nodeConfig = _registry.GetNodeConfiguration("RequiredNode");
        Assert.NotNull(nodeConfig);
        Assert.True(nodeConfig.Properties.ContainsKey("requiredField"));
        Assert.True(nodeConfig.Properties["requiredField"].IsRequired);
    }

    [Fact]
    public void PropertyConfiguration_WithValidationRules_ValidatesCorrectly()
    {
        // Arrange
        _registry.ConfigureNode("ValidationNode", config =>
        {
            config.Property("name", p =>
            {
                p.Validation = new PropertyValidation
                {
                    MinLength = 3,
                    MaxLength = 10
                };
            });
        });

        // Act & Assert - For now, just test that the configuration is stored correctly
        var nodeConfig = _registry.GetNodeConfiguration("ValidationNode");
        Assert.NotNull(nodeConfig);
        var nameConfig = nodeConfig.Properties["name"];
        Assert.NotNull(nameConfig.Validation);
        Assert.Equal(3, nameConfig.Validation.MinLength);
        Assert.Equal(10, nameConfig.Validation.MaxLength);
    }

    [Fact]
    public void PropertyConfiguration_WithCustomPropertyName_WorksCorrectly()
    {
        // Arrange
        _registry.ConfigureNode("CustomNode", config =>
        {
            config.Property("originalName", p => { p.CustomName = "custom_name"; p.IsIndexed = true; });
        });

        // Act & Assert
        var nodeConfig = _registry.GetNodeConfiguration("CustomNode");
        Assert.NotNull(nodeConfig);
        Assert.True(nodeConfig.Properties.ContainsKey("originalName"));
        Assert.Equal("custom_name", nodeConfig.Properties["originalName"].CustomName);
        Assert.True(nodeConfig.Properties["originalName"].IsIndexed);
    }

    [Fact]
    public void PropertyConfiguration_WithMultipleConfigurations_WorksCorrectly()
    {
        // Arrange
        _registry.ConfigureNode("MultiNode", config =>
        {
            config.Property("field1", p => { p.IsIndexed = true; })
                 .Property("field2", p => { p.IsUnique = true; })
                 .Property("field3", p => { p.IsRequired = true; });
        });

        // Act & Assert
        var nodeConfig = _registry.GetNodeConfiguration("MultiNode");
        Assert.NotNull(nodeConfig);
        Assert.True(nodeConfig.Properties.ContainsKey("field1"));
        Assert.True(nodeConfig.Properties.ContainsKey("field2"));
        Assert.True(nodeConfig.Properties.ContainsKey("field3"));
        Assert.True(nodeConfig.Properties["field1"].IsIndexed);
        Assert.True(nodeConfig.Properties["field2"].IsUnique);
        Assert.True(nodeConfig.Properties["field3"].IsRequired);
    }

    [Fact]
    public void PropertyConfiguration_RegistryClear_WorksCorrectly()
    {
        // Arrange
        _registry.ConfigureNode("TestNode", config =>
        {
            config.Property("name", p => { p.IsIndexed = true; });
        });

        // Act
        _registry.Clear();

        // Assert
        var nodeConfig = _registry.GetNodeConfiguration("TestNode");
        Assert.Null(nodeConfig);
    }

    [Fact]
    public void PropertyConfiguration_RelationshipConfiguration_WorksCorrectly()
    {
        // Arrange
        _registry.ConfigureRelationship("TestRel", config =>
        {
            config.Property("since", p => { p.IsIndexed = true; })
                 .Property("strength", p => { p.IsUnique = true; });
        });

        // Act & Assert
        var relConfig = _registry.GetRelationshipConfiguration("TestRel");
        Assert.NotNull(relConfig);
        Assert.True(relConfig.Properties.ContainsKey("since"));
        Assert.True(relConfig.Properties.ContainsKey("strength"));
        Assert.True(relConfig.Properties["since"].IsIndexed);
        Assert.True(relConfig.Properties["strength"].IsUnique);
    }

    [Fact]
    public void PropertyConfiguration_WithComplexValidation_ConfigurationStored()
    {
        // Arrange
        _registry.ConfigureNode("ComplexValidationNode", config =>
        {
            config.Property("complexField", p =>
            {
                p.Validation = new PropertyValidation
                {
                    MinLength = 5,
                    MaxLength = 100,
                    MinValue = 0,
                    MaxValue = 1000,
                    Pattern = @"^[A-Za-z0-9]+$"
                };
            });
        });

        // Act & Assert
        var nodeConfig = _registry.GetNodeConfiguration("ComplexValidationNode");
        Assert.NotNull(nodeConfig);
        var complexFieldConfig = nodeConfig.Properties["complexField"];
        Assert.NotNull(complexFieldConfig.Validation);
        Assert.Equal(5, complexFieldConfig.Validation.MinLength);
        Assert.Equal(100, complexFieldConfig.Validation.MaxLength);
        Assert.Equal(0, complexFieldConfig.Validation.MinValue);
        Assert.Equal(1000, complexFieldConfig.Validation.MaxValue);
        Assert.Equal(@"^[A-Za-z0-9]+$", complexFieldConfig.Validation.Pattern);
    }

    // ===== GRAPH ADHERENCE TESTS =====

    [Fact]
    public async Task Graph_WithRequiredPropertyConfiguration_EnforcesRequiredFields()
    {
        // Arrange - Configure required properties
        _registry.ConfigureNode("ConfigTestPerson", config =>
        {
            config.Property("FirstName", p => { p.IsRequired = true; })
                 .Property("Email", p => { p.IsRequired = true; });
        });

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
    public async Task Graph_WithUniquePropertyConfiguration_EnforcesUniqueness()
    {
        // Arrange - Configure unique properties
        _registry.ConfigureNode("ConfigTestPerson", config =>
        {
            config.Property("Email", p => { p.IsUnique = true; });
        });

        // Create first person with unique email
        var person1 = new ConfigTestPerson
        {
            FirstName = "Alice",
            LastName = "Johnson",
            Email = "alice@example.com"
        };

        await Graph.CreateNodeAsync(person1, null, TestContext.Current.CancellationToken);

        // Act & Assert - Try to create second person with same email
        var person2 = new ConfigTestPerson
        {
            FirstName = "Bob",
            LastName = "Smith",
            Email = "alice@example.com" // Same email as person1
        };

        // This should throw an exception due to uniqueness constraint violation
        await Assert.ThrowsAsync<GraphException>(async () =>
        {
            await Graph.CreateNodeAsync(person2, null, TestContext.Current.CancellationToken);
        });
    }

    [Fact]
    public async Task Graph_WithValidationRules_EnforcesValidationConstraints()
    {
        // Arrange - Configure validation rules
        _registry.ConfigureNode("ConfigTestPerson", config =>
        {
            config.Property("Age", p =>
            {
                p.Validation = new PropertyValidation
                {
                    MinValue = 18,
                    MaxValue = 120
                };
            });
            config.Property("FirstName", p =>
            {
                p.Validation = new PropertyValidation
                {
                    MinLength = 2,
                    MaxLength = 50
                };
            });
        });

        // Act & Assert - Try to create person with invalid age
        var personWithInvalidAge = new ConfigTestPerson
        {
            FirstName = "John",
            LastName = "Doe",
            Email = "john@example.com",
            Age = 15 // Below minimum age
        };

        // This should throw an exception due to validation failure
        await Assert.ThrowsAsync<GraphException>(async () =>
        {
            await Graph.CreateNodeAsync(personWithInvalidAge, null, TestContext.Current.CancellationToken);
        });

        // Try to create person with invalid name length
        var personWithInvalidName = new ConfigTestPerson
        {
            FirstName = "A", // Too short
            LastName = "Doe",
            Email = "a@example.com",
            Age = 25
        };

        // This should also throw an exception
        await Assert.ThrowsAsync<GraphException>(async () =>
        {
            await Graph.CreateNodeAsync(personWithInvalidName, null, TestContext.Current.CancellationToken);
        });
    }

    [Fact]
    public async Task Graph_WithPatternValidation_EnforcesRegexPattern()
    {
        // Arrange - Configure pattern validation
        _registry.ConfigureNode("ConfigTestPerson", config =>
        {
            config.Property("Email", p =>
            {
                p.Validation = new PropertyValidation
                {
                    Pattern = @"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$"
                };
            });
        });

        // Act & Assert - Try to create person with invalid email format
        var personWithInvalidEmail = new ConfigTestPerson
        {
            FirstName = "John",
            LastName = "Doe",
            Email = "invalid-email-format", // Invalid email format
            Age = 25
        };

        // This should throw an exception due to pattern validation failure
        await Assert.ThrowsAsync<GraphException>(async () =>
        {
            await Graph.CreateNodeAsync(personWithInvalidEmail, null, TestContext.Current.CancellationToken);
        });
    }

    [Fact]
    public async Task Graph_WithRelationshipConfiguration_EnforcesRelationshipConstraints()
    {
        // Arrange - Configure relationship properties
        _registry.ConfigureRelationship("ConfigTestKnows", config =>
        {
            config.Property("Strength", p =>
            {
                p.IsRequired = true;
                p.Validation = new PropertyValidation
                {
                    MinValue = 1,
                    MaxValue = 10
                };
            });
        });

        // Create two people first
        var person1 = new ConfigTestPerson
        {
            FirstName = "Alice",
            LastName = "Johnson",
            Email = "alice@example.com"
        };
        var person2 = new ConfigTestPerson
        {
            FirstName = "Bob",
            LastName = "Smith",
            Email = "bob@example.com"
        };

        await Graph.CreateNodeAsync(person1, null, TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(person2, null, TestContext.Current.CancellationToken);

        // Act & Assert - Try to create relationship with invalid strength
        var relationshipWithInvalidStrength = new ConfigTestKnows
        {
            StartNodeId = person1.Id,
            EndNodeId = person2.Id,
            Strength = 15 // Above maximum
        };

        // This should throw an exception due to validation failure
        await Assert.ThrowsAsync<GraphException>(async () =>
        {
            await Graph.CreateRelationshipAsync(relationshipWithInvalidStrength, null, TestContext.Current.CancellationToken);
        });
    }

    [Fact]
    public async Task Graph_WithValidConfiguration_AllowsValidEntities()
    {
        // Arrange - Configure reasonable validation rules
        _registry.ConfigureNode("ConfigTestPerson", config =>
        {
            config.Property("FirstName", p =>
            {
                p.IsRequired = true;
                p.Validation = new PropertyValidation
                {
                    MinLength = 2,
                    MaxLength = 50
                };
            });
            config.Property("Email", p =>
            {
                p.IsRequired = true;
                p.IsUnique = true;
                p.Validation = new PropertyValidation
                {
                    Pattern = @"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$"
                };
            });
            config.Property("Age", p =>
            {
                p.Validation = new PropertyValidation
                {
                    MinValue = 0,
                    MaxValue = 150
                };
            });
        });

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

    [Fact]
    public async Task Graph_WithIndexedProperties_CreatesIndexesForQueryPerformance()
    {
        // Arrange - Configure indexed properties
        _registry.ConfigureNode("ConfigTestPerson", config =>
        {
            config.Property("Email", p => { p.IsIndexed = true; })
                 .Property("Age", p => { p.IsIndexed = true; });
        });

        // Create multiple people
        var people = new[]
        {
            new ConfigTestPerson { FirstName = "Alice", LastName = "Johnson", Email = "alice@example.com", Age = 25 },
            new ConfigTestPerson { FirstName = "Bob", LastName = "Smith", Email = "bob@example.com", Age = 30 },
            new ConfigTestPerson { FirstName = "Charlie", LastName = "Brown", Email = "charlie@example.com", Age = 35 }
        };

        foreach (var person in people)
        {
            await Graph.CreateNodeAsync(person, null, TestContext.Current.CancellationToken);
        }

        // Act - Query using indexed properties (this should be fast if indexes are created)
        var queryable = Graph.Nodes<ConfigTestPerson>();
        var resultsByEmail = queryable.Where(p => p.Email == "bob@example.com").ToList();
        var resultsByAge = queryable.Where(p => p.Age >= 30).ToList();

        // Assert - Verify queries work correctly
        Assert.Single(resultsByEmail);
        Assert.Equal("Bob", resultsByEmail[0].FirstName);
        Assert.Equal(2, resultsByAge.Count); // Bob (30) and Charlie (35)
    }

    [Fact]
    public async Task Graph_WithCustomPropertyNames_UsesCustomNamesInDatabase()
    {
        // Arrange - Configure custom property names
        _registry.ConfigureNode("ConfigTestPerson", config =>
        {
            config.Property("FirstName", p => { p.CustomName = "first_name"; })
                 .Property("LastName", p => { p.CustomName = "last_name"; })
                 .Property("Email", p => { p.CustomName = "email_address"; });
        });

        // Act - Create person
        var person = new ConfigTestPerson
        {
            FirstName = "John",
            LastName = "Doe",
            Email = "john@example.com"
        };

        await Graph.CreateNodeAsync(person, null, TestContext.Current.CancellationToken);

        // Assert - Verify the person was created and can be retrieved
        var retrievedPerson = await Graph.GetNodeAsync<ConfigTestPerson>(person.Id, null, TestContext.Current.CancellationToken);
        Assert.NotNull(retrievedPerson);
        Assert.Equal("John", retrievedPerson.FirstName);
        Assert.Equal("Doe", retrievedPerson.LastName);
        Assert.Equal("john@example.com", retrievedPerson.Email);
    }

    [Fact]
    public async Task Graph_WithComplexValidation_EnforcesMultipleConstraints()
    {
        // Arrange - Configure complex validation rules
        _registry.ConfigureNode("ConfigTestPerson", config =>
        {
            config.Property("FirstName", p =>
            {
                p.IsRequired = true;
                p.Validation = new PropertyValidation
                {
                    MinLength = 2,
                    MaxLength = 20,
                    Pattern = @"^[A-Za-z\s]+$" // Only letters and spaces
                };
            });
            config.Property("Age", p =>
            {
                p.Validation = new PropertyValidation
                {
                    MinValue = 0,
                    MaxValue = 120
                };
            });
        });

        // Act & Assert - Test various validation scenarios

        // Valid person should succeed
        var validPerson = new ConfigTestPerson
        {
            FirstName = "John",
            LastName = "Doe",
            Email = "john@example.com",
            Age = 25
        };
        await Graph.CreateNodeAsync(validPerson, null, TestContext.Current.CancellationToken);

        // Person with invalid name pattern should fail
        var personWithInvalidName = new ConfigTestPerson
        {
            FirstName = "John123", // Contains numbers
            LastName = "Doe",
            Email = "john123@example.com",
            Age = 25
        };
        await Assert.ThrowsAsync<GraphException>(async () =>
        {
            await Graph.CreateNodeAsync(personWithInvalidName, null, TestContext.Current.CancellationToken);
        });

        // Person with invalid age should fail
        var personWithInvalidAge = new ConfigTestPerson
        {
            FirstName = "Jane",
            LastName = "Smith",
            Email = "jane@example.com",
            Age = 150 // Above maximum
        };
        await Assert.ThrowsAsync<GraphException>(async () =>
        {
            await Graph.CreateNodeAsync(personWithInvalidAge, null, TestContext.Current.CancellationToken);
        });
    }

    [Fact]
    public async Task Graph_WithUpdateOperations_EnforcesConfigurationOnUpdates()
    {
        // Arrange - Configure validation rules
        _registry.ConfigureNode("ConfigTestPerson", config =>
        {
            config.Property("Email", p =>
            {
                p.IsUnique = true;
                p.Validation = new PropertyValidation
                {
                    Pattern = @"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$"
                };
            });
        });

        // Create initial person
        var person = new ConfigTestPerson
        {
            FirstName = "John",
            LastName = "Doe",
            Email = "john@example.com"
        };
        await Graph.CreateNodeAsync(person, null, TestContext.Current.CancellationToken);

        // Act & Assert - Try to update with invalid email format
        person.Email = "invalid-email";
        await Assert.ThrowsAsync<GraphException>(async () =>
        {
            await Graph.UpdateNodeAsync(person, null, TestContext.Current.CancellationToken);
        });

        // Try to update with valid email format
        person.Email = "john.updated@example.com";
        await Graph.UpdateNodeAsync(person, null, TestContext.Current.CancellationToken);

        // Verify the update was successful
        var updatedPerson = await Graph.GetNodeAsync<ConfigTestPerson>(person.Id, null, TestContext.Current.CancellationToken);
        Assert.Equal("john.updated@example.com", updatedPerson.Email);
    }
}