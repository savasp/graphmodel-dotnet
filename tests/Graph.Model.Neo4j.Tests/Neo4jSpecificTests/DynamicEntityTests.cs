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

namespace Cvoya.Graph.Model.Neo4j.Tests;

using Microsoft.Extensions.Logging;

public class DynamicEntityTests(TestInfrastructureFixture fixture) :
    Neo4jTest(fixture),
    Model.Tests.IQueryTests
{

    [Fact]
    public async Task CanCreateAndGetDynamicNodeWithPrimitiveProperties()
    {
        var node = new DynamicNode(
            labels: new[] { "Person", "User" },
            properties: new Dictionary<string, object?>
            {
                ["name"] = "John Doe",
                ["age"] = 30,
                ["email"] = "john@example.com",
                ["active"] = true
            }
        );

        await Graph.CreateNodeAsync(node, null, TestContext.Current.CancellationToken);
        var fetched = await Graph.GetDynamicNodeAsync(node.Id, null, TestContext.Current.CancellationToken);

        // Direct assertion for 'active' property
        var activeRaw = fetched.Properties["active"];
        Assert.IsType<bool>(activeRaw);
        Assert.True((bool)activeRaw);

        // Test the extension method in isolation
        var testDict = new Dictionary<string, object?> { ["test"] = true };
        var testNode = new DynamicNode(labels: new[] { "Test" }, properties: testDict);
        var testResult = testNode.GetProperty<bool>("test");
        Assert.True(testResult);

        Assert.Equal("John Doe", fetched.GetProperty<string>("name"));
        Assert.Equal(30, fetched.GetProperty<int>("age"));
        Assert.Equal("john@example.com", fetched.GetProperty<string>("email"));
        Assert.True(fetched.GetProperty<bool>("active"));
        Assert.True(fetched.HasLabel("Person"));
        Assert.True(fetched.HasLabel("User"));
        Assert.Equal(2, fetched.Labels.Count);
    }

    [Fact]
    public async Task CanCreateAndGetDynamicNodeWithComplexProperties()
    {
        var address = new Dictionary<string, object?>
        {
            ["street"] = "123 Main St",
            ["city"] = "Seattle",
            ["zipCode"] = "98101"
        };

        var node = new DynamicNode(
            labels: new[] { "Person" },
            properties: new Dictionary<string, object?>
            {
                ["name"] = "Jane Smith",
                ["address"] = address,
                ["tags"] = new[] { "developer", "manager" }
            }
        );

        await Graph.CreateNodeAsync(node, null, TestContext.Current.CancellationToken);
        var fetched = await Graph.GetDynamicNodeAsync(node.Id, null, TestContext.Current.CancellationToken);

        Assert.Equal("Jane Smith", fetched.GetProperty<string>("name"));
        Assert.True(fetched.HasProperty("address"));
        Assert.True(fetched.HasProperty("tags"));

        var fetchedAddress = fetched.GetProperty<IDictionary<string, object>>("address")!;
        Assert.Equal("123 Main St", fetchedAddress["street"]);
        Assert.Equal("Seattle", fetchedAddress["city"]);
        Assert.Equal("98101", fetchedAddress["zipCode"]);

        var fetchedTags = fetched.GetProperty<IList<string>>("tags")!;
        Assert.Contains("developer", fetchedTags);
        Assert.Contains("manager", fetchedTags);
    }

    [Fact]
    public async Task CanCreateAndGetDynamicRelationship()
    {
        // Create two nodes first
        var person1 = new DynamicNode(
            labels: new[] { "Person" },
            properties: new Dictionary<string, object?> { ["name"] = "Alice" }
        );
        var person2 = new DynamicNode(
            labels: new[] { "Person" },
            properties: new Dictionary<string, object?> { ["name"] = "Bob" }
        );

        await Graph.CreateNodeAsync(person1, null, TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(person2, null, TestContext.Current.CancellationToken);

        var relationship = new DynamicRelationship(
            startNodeId: person1.Id,
            endNodeId: person2.Id,
            type: "KNOWS",
            properties: new Dictionary<string, object?>
            {
                ["since"] = DateTime.UtcNow,
                ["strength"] = 0.8,
                ["active"] = true
            }
        );

        await Graph.CreateRelationshipAsync(relationship, null, TestContext.Current.CancellationToken);
        var fetched = await Graph.GetDynamicRelationshipAsync(relationship.Id, null, TestContext.Current.CancellationToken);

        Assert.Equal(person1.Id, fetched.StartNodeId);
        Assert.Equal(person2.Id, fetched.EndNodeId);
        Assert.Equal("KNOWS", fetched.Type);
        Assert.Equal(0.8, fetched.GetProperty<double>("strength"));
        Assert.True(fetched.GetProperty<bool>("active"));
        Assert.True(fetched.HasType("KNOWS"));
    }

    [Fact]
    public async Task CanUpdateDynamicNode()
    {
        var node = new DynamicNode(
            labels: new[] { "Person" },
            properties: new Dictionary<string, object?> { ["name"] = "Original Name" }
        );

        await Graph.CreateNodeAsync(node, null, TestContext.Current.CancellationToken);

        // Update the node
        var updatedNode = new DynamicNode(
            id: node.Id,
            labels: new[] { "Person", "Employee" },
            properties: new Dictionary<string, object?>
            {
                ["name"] = "Updated Name",
                ["age"] = 25,
                ["department"] = "Engineering"
            }
        );

        await Graph.UpdateNodeAsync(updatedNode, null, TestContext.Current.CancellationToken);
        var fetched = await Graph.GetDynamicNodeAsync(node.Id, null, TestContext.Current.CancellationToken);

        Assert.Equal("Updated Name", fetched.GetProperty<string>("name"));
        Assert.Equal(25, fetched.GetProperty<int>("age"));
        Assert.Equal("Engineering", fetched.GetProperty<string>("department"));
        Assert.True(fetched.HasLabel("Person"));
        Assert.True(fetched.HasLabel("Employee"));
        Assert.Equal(2, fetched.Labels.Count);
    }

    [Fact]
    public async Task CanUpdateDynamicRelationship()
    {
        // Create nodes
        var person1 = new DynamicNode(
            labels: new[] { "Person" },
            properties: new Dictionary<string, object?> { ["name"] = "Alice" }
        );
        var person2 = new DynamicNode(
            labels: new[] { "Person" },
            properties: new Dictionary<string, object?> { ["name"] = "Bob" }
        );

        await Graph.CreateNodeAsync(person1, null, TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(person2, null, TestContext.Current.CancellationToken);

        var relationship = new DynamicRelationship(
            startNodeId: person1.Id,
            endNodeId: person2.Id,
            type: "KNOWS",
            properties: new Dictionary<string, object?> { ["since"] = DateTime.UtcNow }
        );

        await Graph.CreateRelationshipAsync(relationship, null, TestContext.Current.CancellationToken);

        // Update the relationship
        var updatedRelationship = new DynamicRelationship(
            startNodeId: person1.Id,
            endNodeId: person2.Id,
            type: "KNOWS",
            properties: new Dictionary<string, object?>
            {
                ["since"] = DateTime.UtcNow.AddDays(-1),
                ["strength"] = 0.9,
                ["active"] = false
            }
        )
        {
            Id = relationship.Id // Keep the same ID
        };

        await Graph.UpdateRelationshipAsync(updatedRelationship, null, TestContext.Current.CancellationToken);
        var fetched = await Graph.GetDynamicRelationshipAsync(relationship.Id, null, TestContext.Current.CancellationToken);

        Assert.Equal(0.9, fetched.GetProperty<double>("strength"));
        Assert.False(fetched.GetProperty<bool>("active"));
        Assert.True(fetched.HasProperty("since"));
    }

    [Fact]
    public async Task CanDeleteDynamicNode()
    {
        var node = new DynamicNode(
            labels: new[] { "Person" },
            properties: new Dictionary<string, object?> { ["name"] = "To Delete" }
        );

        await Graph.CreateNodeAsync(node, null, TestContext.Current.CancellationToken);
        await Graph.DeleteNodeAsync(node.Id, false, null, TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<GraphException>(() =>
            Graph.GetDynamicNodeAsync(node.Id, null, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CanDeleteDynamicRelationship()
    {
        // Create nodes and relationship
        var person1 = new DynamicNode(
            labels: new[] { "Person" },
            properties: new Dictionary<string, object?> { ["name"] = "Alice" }
        );
        var person2 = new DynamicNode(
            labels: new[] { "Person" },
            properties: new Dictionary<string, object?> { ["name"] = "Bob" }
        );

        await Graph.CreateNodeAsync(person1, null, TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(person2, null, TestContext.Current.CancellationToken);

        var relationship = new DynamicRelationship(
            startNodeId: person1.Id,
            endNodeId: person2.Id,
            type: "KNOWS",
            properties: new Dictionary<string, object?> { ["since"] = DateTime.UtcNow }
        );

        await Graph.CreateRelationshipAsync(relationship, null, TestContext.Current.CancellationToken);
        await Graph.DeleteRelationshipAsync(relationship.Id, null, TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<GraphException>(() =>
            Graph.GetDynamicRelationshipAsync(relationship.Id, null, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CanQueryDynamicNodes()
    {
        // Create multiple nodes
        var nodes = new[]
        {
            new DynamicNode(
                labels: new[] { "Person", "Employee" },
                properties: new Dictionary<string, object?> { ["name"] = "Alice", ["age"] = 30 }
            ),
            new DynamicNode(
                labels: new[] { "Person", "Manager" },
                properties: new Dictionary<string, object?> { ["name"] = "Bob", ["age"] = 35 }
            ),
            new DynamicNode(
                labels: new[] { "Company" },
                properties: new Dictionary<string, object?> { ["name"] = "Tech Corp" }
            )
        };

        foreach (var node in nodes)
        {
            await Graph.CreateNodeAsync(node, null, TestContext.Current.CancellationToken);
        }

        // Query by label
        var people = await Graph.DynamicNodes()
            .Where(n => n.HasLabel("Person"))
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, people.Count);
        Assert.All(people, p => Assert.True(p.HasLabel("Person")));

        // Query by property
        var adults = await Graph.DynamicNodes()
            .Where(n => n.HasLabel("Person") && n.GetProperty<int>("age") > 25)
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, adults.Count);
        Assert.All(adults, p => Assert.True(p.GetProperty<int>("age") > 25));

        // Query by multiple labels
        var employees = await Graph.DynamicNodes()
            .Where(n => n.HasLabel("Employee"))
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Single(employees);
        Assert.True(employees[0].HasLabel("Employee"));
    }

    [Fact]
    public async Task CanQueryDynamicRelationships()
    {
        // Create nodes and relationships
        var person1 = new DynamicNode(
            labels: new[] { "Person" },
            properties: new Dictionary<string, object?> { ["name"] = "Alice" }
        );
        var person2 = new DynamicNode(
            labels: new[] { "Person" },
            properties: new Dictionary<string, object?> { ["name"] = "Bob" }
        );
        var person3 = new DynamicNode(
            labels: new[] { "Person" },
            properties: new Dictionary<string, object?> { ["name"] = "Charlie" }
        );

        await Graph.CreateNodeAsync(person1, null, TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(person2, null, TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(person3, null, TestContext.Current.CancellationToken);

        var relationships = new[]
        {
            new DynamicRelationship(
                startNodeId: person1.Id,
                endNodeId: person2.Id,
                type: "KNOWS",
                properties: new Dictionary<string, object?> { ["since"] = DateTime.UtcNow, ["active"] = true }
            ),
            new DynamicRelationship(
                startNodeId: person2.Id,
                endNodeId: person3.Id,
                type: "KNOWS",
                properties: new Dictionary<string, object?> { ["since"] = DateTime.UtcNow, ["active"] = false }
            ),
            new DynamicRelationship(
                startNodeId: person1.Id,
                endNodeId: person3.Id,
                type: "WORKS_WITH",
                properties: new Dictionary<string, object?> { ["project"] = "Alpha" }
            )
        };

        foreach (var rel in relationships)
        {
            await Graph.CreateRelationshipAsync(rel, null, TestContext.Current.CancellationToken);
        }

        // Query by type
        var knowsRelationships = await Graph.DynamicRelationships()
            .Where(r => r.HasType("KNOWS"))
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, knowsRelationships.Count);
        Assert.All(knowsRelationships, r => Assert.True(r.HasType("KNOWS")));

        // Query by property
        var activeRelationships = await Graph.DynamicRelationships()
            .Where(r => r.HasType("KNOWS") && r.GetProperty<bool>("active") == true)
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Single(activeRelationships);
        Assert.True(activeRelationships[0].GetProperty<bool>("active"));
    }

    [Fact]
    public async Task CanQueryDynamicNodesWithComplexProperties()
    {
        var address1 = new Dictionary<string, object?>
        {
            ["street"] = "123 Main St",
            ["city"] = "Seattle",
            ["zipCode"] = "98101"
        };

        var address2 = new Dictionary<string, object?>
        {
            ["street"] = "456 Oak Ave",
            ["city"] = "Portland",
            ["zipCode"] = "97201"
        };

        var nodes = new[]
        {
            new DynamicNode(
                labels: new[] { "Person" },
                properties: new Dictionary<string, object?>
                {
                    ["name"] = "Alice",
                    ["address"] = address1
                }
            ),
            new DynamicNode(
                labels: new[] { "Person" },
                properties: new Dictionary<string, object?>
                {
                    ["name"] = "Bob",
                    ["address"] = address2
                }
            )
        };

        foreach (var node in nodes)
        {
            await Graph.CreateNodeAsync(node, null, TestContext.Current.CancellationToken);
        }

        // Query by complex property - we'll filter in memory since expression trees don't support 'is' pattern matching
        var allPeople = await Graph.DynamicNodes()
            .Where(n => n.HasLabel("Person"))
            .ToListAsync(TestContext.Current.CancellationToken);

        var seattlePeople = allPeople.Where(n =>
            n.Properties.ContainsKey("address") &&
            n.Properties["address"] is IDictionary<string, object> address &&
            address.ContainsKey("city") &&
            address["city"] is string city &&
            city == "Seattle").ToList();

        Assert.Single(seattlePeople);
        var seattlePerson = seattlePeople[0];
        var seattleAddress = seattlePerson.GetProperty<IDictionary<string, object>>("address")!;
        Assert.Equal("Seattle", seattleAddress["city"]);
    }

    [Fact]
    public async Task CanQueryDynamicNodesWithArrayProperties()
    {
        var nodes = new[]
        {
            new DynamicNode(
                labels: new[] { "Person" },
                properties: new Dictionary<string, object?>
                {
                    ["name"] = "Alice",
                    ["tags"] = new[] { "developer", "manager" }
                }
            ),
            new DynamicNode(
                labels: new[] { "Person" },
                properties: new Dictionary<string, object?>
                {
                    ["name"] = "Bob",
                    ["tags"] = new[] { "developer", "designer" }
                }
            )
        };

        foreach (var node in nodes)
        {
            await Graph.CreateNodeAsync(node, null, TestContext.Current.CancellationToken);
        }

        // Query by array property - this should be translated to Cypher
        var developers = await Graph.DynamicNodes()
            .Where(n => n.HasLabel("Person") && n.GetProperty<IList<string>>("tags")!.Contains("developer"))
            .ToListAsync(TestContext.Current.CancellationToken);

        if (developers.Count > 0)
        {
            Assert.All(developers, p =>
            {
                var tags = p.GetProperty<IList<string>>("tags");
                Assert.NotNull(tags);
                Assert.Contains("developer", tags!);
            });
        }
        Assert.Equal(2, developers.Count);
    }

    [Fact]
    public async Task CanProjectDynamicNodeProperties()
    {
        var node = new DynamicNode(
            labels: new[] { "Person", "Employee" },
            properties: new Dictionary<string, object?>
            {
                ["name"] = "Alice",
                ["age"] = 30,
                ["department"] = "Engineering",
                ["salary"] = 75000
            }
        );

        await Graph.CreateNodeAsync(node, null, TestContext.Current.CancellationToken);

        var projections = await Graph.DynamicNodes()
            .Where(n => n.HasLabel("Person"))
            .Select(n => new
            {
                Id = n.Id,
                Name = n.GetProperty<string>("name"),
                Age = n.GetProperty<int?>("age"),
                Department = n.GetProperty<string>("department"),
                HasSalary = n.HasProperty("salary")
            })
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Single(projections);
        var projection = projections[0];
        Assert.Equal("Alice", projection.Name);
        Assert.Equal(30, projection.Age);
        Assert.Equal("Engineering", projection.Department);
        Assert.True(projection.HasSalary);
    }

    [Fact]
    public async Task CanUseDynamicEntityExtensions()
    {
        var node = new DynamicNode(
            labels: new[] { "Person", "Employee" },
            properties: new Dictionary<string, object?>
            {
                ["name"] = "Alice",
                ["age"] = 30
            }
        );

        await Graph.CreateNodeAsync(node, null, TestContext.Current.CancellationToken);
        var fetched = await Graph.GetDynamicNodeAsync(node.Id, null, TestContext.Current.CancellationToken);

        // Test extension methods
        Assert.True(fetched.HasLabel("Person"));
        Assert.True(fetched.HasLabel("Employee"));
        Assert.True(fetched.HasAnyLabel("Person", "Manager"));
        Assert.True(fetched.HasAllLabels("Person", "Employee"));
        Assert.False(fetched.HasAllLabels("Person", "Manager"));

        Assert.True(fetched.HasProperty("name"));
        Assert.True(fetched.HasProperty("age"));
        Assert.False(fetched.HasProperty("nonexistent"));

        var propertyNames = fetched.GetPropertyNames()?.ToList() ?? new List<string>();
        Assert.Contains("name", propertyNames);
        Assert.Contains("age", propertyNames);
        Assert.Equal(2, propertyNames.Count);

        Assert.Equal("Alice", fetched.GetProperty<string>("name"));
        Assert.Equal(30, fetched.GetProperty<int>("age"));
        Assert.Null(fetched.GetProperty<string>("nonexistent"));
    }

    [Fact]
    public async Task CanHandleNullProperties()
    {
        var node = new DynamicNode(
            labels: new[] { "Person" },
            properties: new Dictionary<string, object?>
            {
                ["name"] = "Alice",
                ["age"] = null,
                ["email"] = "alice@example.com"
            }
        );

        await Graph.CreateNodeAsync(node, null, TestContext.Current.CancellationToken);
        var fetched = await Graph.GetDynamicNodeAsync(node.Id, null, TestContext.Current.CancellationToken);

        Assert.Equal("Alice", fetched.GetProperty<string>("name"));
        Assert.Null(fetched.GetProperty<int?>("age"));
        Assert.Equal("alice@example.com", fetched.GetProperty<string>("email"));
        Assert.False(fetched.HasProperty("age")); // Property does not exist
    }

    [Fact]
    public async Task CanHandleEmptyLabels()
    {
        var node = new DynamicNode(
            labels: new List<string>(),
            properties: new Dictionary<string, object?> { ["name"] = "Alice" }
        );

        await Graph.CreateNodeAsync(node, null, TestContext.Current.CancellationToken);
        var fetched = await Graph.GetDynamicNodeAsync(node.Id, null, TestContext.Current.CancellationToken);

        Assert.Empty(fetched.Labels);
        Assert.Equal("Alice", fetched.GetProperty<string>("name"));
    }

    [Fact]
    public async Task CanHandleEmptyProperties()
    {
        var node = new DynamicNode(
            labels: new[] { "Person" },
            properties: new Dictionary<string, object?>()
        );

        await Graph.CreateNodeAsync(node, null, TestContext.Current.CancellationToken);
        var fetched = await Graph.GetDynamicNodeAsync(node.Id, null, TestContext.Current.CancellationToken);

        Assert.True(fetched.HasLabel("Person"));
        Assert.Empty(fetched.Properties);
        Assert.Empty(fetched.GetPropertyNames());
    }

    [Fact]
    public async Task CanHandleLargeNumberOfProperties()
    {
        var properties = new Dictionary<string, object?>();
        for (int i = 0; i < 100; i++)
        {
            properties[$"property{i}"] = $"value{i}";
        }

        var node = new DynamicNode(
            labels: new[] { "TestNode" },
            properties: properties
        );

        await Graph.CreateNodeAsync(node, null, TestContext.Current.CancellationToken);
        var fetched = await Graph.GetDynamicNodeAsync(node.Id, null, TestContext.Current.CancellationToken);

        Assert.Equal(100, fetched.Properties.Count);
        Assert.Equal("value50", fetched.GetProperty<string>("property50"));
        Assert.Equal("value99", fetched.GetProperty<string>("property99"));
    }

    [Fact]
    public async Task CanHandleSpecialCharactersInPropertyNames()
    {
        var node = new DynamicNode(
            labels: new[] { "Person" },
            properties: new Dictionary<string, object?>
            {
                ["name"] = "Alice",
                ["user_id"] = "12345",
                ["email_address"] = "alice@example.com",
                ["phone_number"] = "+1-555-1234"
            }
        );

        await Graph.CreateNodeAsync(node, null, TestContext.Current.CancellationToken);
        var fetched = await Graph.GetDynamicNodeAsync(node.Id, null, TestContext.Current.CancellationToken);

        Assert.Equal("Alice", fetched.GetProperty<string>("name"));
        Assert.Equal("12345", fetched.GetProperty<string>("user_id"));
        Assert.Equal("alice@example.com", fetched.GetProperty<string>("email_address"));
        Assert.Equal("+1-555-1234", fetched.GetProperty<string>("phone_number"));
    }

    [Fact]
    public async Task CanHandleNumericTypes()
    {
        var node = new DynamicNode(
            labels: new[] { "Person" },
            properties: new Dictionary<string, object?>
            {
                ["intValue"] = 42,
                ["longValue"] = 123456789L,
                ["doubleValue"] = 3.14159,
                ["decimalValue"] = 99.99m,
                ["floatValue"] = 2.5f
            }
        );

        await Graph.CreateNodeAsync(node, null, TestContext.Current.CancellationToken);
        var fetched = await Graph.GetDynamicNodeAsync(node.Id, null, TestContext.Current.CancellationToken);

        Assert.Equal(42, fetched.GetProperty<int>("intValue"));
        Assert.Equal(123456789L, fetched.GetProperty<long>("longValue"));
        Assert.Equal(3.14159, fetched.GetProperty<double>("doubleValue"));
        Assert.Equal(99.99m, fetched.GetProperty<decimal>("decimalValue"));
        Assert.Equal(2.5f, fetched.GetProperty<float>("floatValue"));
    }

    [Fact]
    public async Task CanHandleDateTimeTypes()
    {
        var now = DateTime.UtcNow;
        var dateOnly = DateOnly.FromDateTime(now);
        var timeOnly = TimeOnly.FromDateTime(now);

        var node = new DynamicNode(
            labels: new[] { "Event" },
            properties: new Dictionary<string, object?>
            {
                ["createdAt"] = now,
                ["date"] = dateOnly,
                ["time"] = timeOnly
            }
        );

        await Graph.CreateNodeAsync(node, null, TestContext.Current.CancellationToken);
        var fetched = await Graph.GetDynamicNodeAsync(node.Id, null, TestContext.Current.CancellationToken);

        var fetchedDateTime = fetched.GetProperty<DateTime>("createdAt");
        Assert.Equal(now.Year, fetchedDateTime.Year);
        Assert.Equal(now.Month, fetchedDateTime.Month);
        Assert.Equal(now.Day, fetchedDateTime.Day);

        var fetchedDate = fetched.GetProperty<DateOnly>("date");
        Assert.Equal(dateOnly, fetchedDate);

        var fetchedTime = fetched.GetProperty<TimeOnly>("time");
        Assert.Equal(timeOnly, fetchedTime);
    }
}