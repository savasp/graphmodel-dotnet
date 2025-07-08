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

public interface IAttributeValidationTests : IGraphModelTest
{
    [Node("CustomPersonLabel")]
    public class PersonWithCustomLabel : INode
    {
        public string Id { get; init; } = Guid.NewGuid().ToString("N");
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
    }

    [Node("Employee", "Person", "User")]
    public class PersonWithMultipleLabels : INode
    {
        public string Id { get; init; } = Guid.NewGuid().ToString("N");
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Department { get; set; } = string.Empty;
    }

    public class PersonWithoutLabel : INode
    {
        public string Id { get; init; } = Guid.NewGuid().ToString("N");
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
    }

    [Node("IndexedPerson")]
    public class PersonWithCustomPropertyLabels : INode
    {
        public string Id { get; init; } = Guid.NewGuid().ToString("N");

        [Property(Label = "first_name")]
        public string FirstName { get; set; } = string.Empty;

        [Property(Label = "last_name")]
        public string LastName { get; set; } = string.Empty;

        [Property(Label = "email")]
        public string Email { get; set; } = string.Empty;

        [Property]
        public int Age { get; set; }
    }

    [Node("PersonWithIgnoredProps")]
    public class PersonWithIgnoredProperties : INode
    {
        public string Id { get; init; } = Guid.NewGuid().ToString("N");

        [Property]
        public string FirstName { get; set; } = string.Empty;

        [Property(Ignore = true)]
        public string InternalNote { get; set; } = string.Empty;

        [Property(Ignore = true)]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public string LastName { get; set; } = string.Empty; // No attribute, should still be persisted
    }

    [Relationship("CUSTOM_WORKS_WITH")]
    public class CustomWorksWith : IRelationship
    {
        public string Id { get; init; } = Guid.NewGuid().ToString("N");
        public string StartNodeId { get; init; } = string.Empty;
        public string EndNodeId { get; init; } = string.Empty;
        public RelationshipDirection Direction { get; init; } = RelationshipDirection.Outgoing;

        [Property(Label = "start_date")]
        public DateTime StartDate { get; set; } = DateTime.UtcNow;

        [Property(Label = "project_name")]
        public string ProjectName { get; set; } = string.Empty;
    }

    public class WorksWithoutLabel : IRelationship
    {
        public string Id { get; init; } = Guid.NewGuid().ToString("N");
        public string StartNodeId { get; init; } = string.Empty;
        public string EndNodeId { get; init; } = string.Empty;
        public RelationshipDirection Direction { get; init; } = RelationshipDirection.Outgoing;
        public DateTime Since { get; set; } = DateTime.UtcNow;
    }

    [Fact]
    public async Task NodeWithCustomLabel_CreatedSuccessfully()
    {
        var person = new PersonWithCustomLabel
        {
            FirstName = "John",
            LastName = "Doe"
        };

        await Graph.CreateNodeAsync(person, null, TestContext.Current.CancellationToken);

        var retrieved = await Graph.GetNodeAsync<PersonWithCustomLabel>(person.Id, null, TestContext.Current.CancellationToken);
        Assert.Equal("John", retrieved.FirstName);
        Assert.Equal("Doe", retrieved.LastName);
    }

    [Fact]
    public async Task NodeWithMultipleLabels_CreatedSuccessfully()
    {
        var person = new PersonWithMultipleLabels
        {
            FirstName = "Jane",
            LastName = "Smith",
            Department = "Engineering"
        };

        await Graph.CreateNodeAsync(person, null, TestContext.Current.CancellationToken);

        var retrieved = await Graph.GetNodeAsync<PersonWithMultipleLabels>(person.Id, null, TestContext.Current.CancellationToken);
        Assert.Equal("Jane", retrieved.FirstName);
        Assert.Equal("Smith", retrieved.LastName);
        Assert.Equal("Engineering", retrieved.Department);
    }

    [Fact]
    public async Task NodeWithoutLabel_CreatedSuccessfully()
    {
        var person = new PersonWithoutLabel
        {
            FirstName = "Bob",
            LastName = "Johnson"
        };

        await Graph.CreateNodeAsync(person, null, TestContext.Current.CancellationToken);

        var retrieved = await Graph.GetNodeAsync<PersonWithoutLabel>(person.Id, null, TestContext.Current.CancellationToken);
        Assert.Equal("Bob", retrieved.FirstName);
        Assert.Equal("Johnson", retrieved.LastName);
    }

    [Fact]
    public async Task NodeWithCustomPropertyLabels_CreatedAndQueryable()
    {
        var people = new[]
        {
            new PersonWithCustomPropertyLabels
            {
                FirstName = "Alice",
                LastName = "Brown",
                Email = "alice@example.com",
                Age = 30
            },
            new PersonWithCustomPropertyLabels
            {
                FirstName = "Bob",
                LastName = "Brown",
                Email = "bob@example.com",
                Age = 25
            }
        };

        foreach (var person in people)
        {
            await Graph.CreateNodeAsync(person, null, TestContext.Current.CancellationToken);
        }

        // Query by property with custom label
        var brownPeople = await Graph.Nodes<PersonWithCustomPropertyLabels>()
            .Where(p => p.LastName == "Brown")
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, brownPeople.Count);
        Assert.Contains(brownPeople, p => p.FirstName == "Alice");
        Assert.Contains(brownPeople, p => p.FirstName == "Bob");

        // Query by email property
        var alice = await Graph.Nodes<PersonWithCustomPropertyLabels>()
            .Where(p => p.Email == "alice@example.com")
            .FirstOrDefaultAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(alice);
        Assert.Equal("Alice", alice.FirstName);
    }

    [Fact]
    public async Task NodeWithIgnoredProperties_IgnoredPropertiesNotPersisted()
    {
        var person = new PersonWithIgnoredProperties
        {
            FirstName = "Charlie",
            LastName = "Davis",
            InternalNote = "This should not be saved",
            CreatedAt = DateTime.UtcNow.AddDays(-1)
        };

        await Graph.CreateNodeAsync(person, null, TestContext.Current.CancellationToken);

        var retrieved = await Graph.GetNodeAsync<PersonWithIgnoredProperties>(person.Id, null, TestContext.Current.CancellationToken);

        Assert.Equal("Charlie", retrieved.FirstName);
        Assert.Equal("Davis", retrieved.LastName);

        // Ignored properties should have default values
        Assert.Equal(string.Empty, retrieved.InternalNote);
        Assert.NotEqual(person.CreatedAt, retrieved.CreatedAt);
    }

    [Fact]
    public async Task RelationshipWithCustomLabel_CreatedSuccessfully()
    {
        var person1 = new PersonWithCustomLabel { FirstName = "Alice", LastName = "Wonder" };
        var person2 = new PersonWithCustomLabel { FirstName = "Bob", LastName = "Builder" };

        await Graph.CreateNodeAsync(person1, null, TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(person2, null, TestContext.Current.CancellationToken);

        var relationship = new CustomWorksWith
        {
            StartNodeId = person1.Id,
            EndNodeId = person2.Id,
            StartDate = DateTime.UtcNow.AddMonths(-6),
            ProjectName = "Project Alpha"
        };

        await Graph.CreateRelationshipAsync(relationship, null, TestContext.Current.CancellationToken);

        var retrieved = await Graph.GetRelationshipAsync<CustomWorksWith>(relationship.Id, null, TestContext.Current.CancellationToken);
        Assert.Equal(person1.Id, retrieved.StartNodeId);
        Assert.Equal(person2.Id, retrieved.EndNodeId);
        Assert.Equal("Project Alpha", retrieved.ProjectName);
    }

    [Fact]
    public async Task RelationshipWithoutLabel_CreatedSuccessfully()
    {
        var person1 = new PersonWithoutLabel { FirstName = "Eve", LastName = "Adams" };
        var person2 = new PersonWithoutLabel { FirstName = "Frank", LastName = "White" };

        await Graph.CreateNodeAsync(person1, null, TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(person2, null, TestContext.Current.CancellationToken);

        var relationship = new WorksWithoutLabel
        {
            StartNodeId = person1.Id,
            EndNodeId = person2.Id,
            Since = DateTime.UtcNow.AddYears(-1)
        };

        await Graph.CreateRelationshipAsync(relationship, null, TestContext.Current.CancellationToken);

        var retrieved = await Graph.GetRelationshipAsync<WorksWithoutLabel>(relationship.Id, null, TestContext.Current.CancellationToken);
        Assert.Equal(person1.Id, retrieved.StartNodeId);
        Assert.Equal(person2.Id, retrieved.EndNodeId);
    }

    [Fact]
    public async Task PropertyWithCustomName_MappedCorrectly()
    {
        var person = new PersonWithCustomPropertyLabels
        {
            FirstName = "Test",
            LastName = "User",
            Email = "test@example.com",
            Age = 30
        };

        await Graph.CreateNodeAsync(person, null, TestContext.Current.CancellationToken);

        var retrieved = await Graph.GetNodeAsync<PersonWithCustomPropertyLabels>(person.Id, null, TestContext.Current.CancellationToken);

        // Properties with custom names should still be retrievable
        Assert.Equal("Test", retrieved.FirstName);
        Assert.Equal("User", retrieved.LastName);
        Assert.Equal("test@example.com", retrieved.Email);
        Assert.Equal(30, retrieved.Age);
    }

    [Fact]
    public async Task QueryByCustomLabelProperty_WorksCorrectly()
    {
        var people = Enumerable.Range(1, 100)
            .Select(i => new PersonWithCustomPropertyLabels
            {
                FirstName = $"Person{i}",
                LastName = i % 10 == 0 ? "Smith" : $"LastName{i}",
                Email = $"person{i}@example.com",
                Age = 20 + (i % 40)
            })
            .ToArray();

        foreach (var person in people)
        {
            await Graph.CreateNodeAsync(person, null, TestContext.Current.CancellationToken);
        }

        // Query by property with custom label
        var smiths = await Graph.Nodes<PersonWithCustomPropertyLabels>()
            .Where(p => p.LastName == "Smith")
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Equal(10, smiths.Count);

        // Query by property with custom label
        var person50 = await Graph.Nodes<PersonWithCustomPropertyLabels>()
            .Where(p => p.FirstName == "Person50")
            .SingleOrDefaultAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(person50);
        Assert.Equal("person50@example.com", person50.Email);
    }

    [Fact]
    public async Task ComplexQueryWithMultipleCustomLabelProperties_WorksCorrectly()
    {
        var people = new[]
        {
            new PersonWithCustomPropertyLabels
            {
                FirstName = "Alice",
                LastName = "Smith",
                Email = "alice.smith@company.com",
                Age = 30
            },
            new PersonWithCustomPropertyLabels
            {
                FirstName = "Bob",
                LastName = "Smith",
                Email = "bob.smith@company.com",
                Age = 25
            },
            new PersonWithCustomPropertyLabels
            {
                FirstName = "Alice",
                LastName = "Johnson",
                Email = "alice.johnson@company.com",
                Age = 35
            }
        };

        foreach (var person in people)
        {
            await Graph.CreateNodeAsync(person, null, TestContext.Current.CancellationToken);
        }

        // Query using multiple properties with custom labels
        var aliceSmith = await Graph.Nodes<PersonWithCustomPropertyLabels>()
            .Where(p => p.FirstName == "Alice" && p.LastName == "Smith")
            .SingleOrDefaultAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(aliceSmith);
        Assert.Equal("alice.smith@company.com", aliceSmith.Email);
        Assert.Equal(30, aliceSmith.Age);
    }

    [Fact]
    public async Task UpdateNodeWithAttributes_PreservesAttributeBehavior()
    {
        var person = new PersonWithIgnoredProperties
        {
            FirstName = "Original",
            LastName = "Name",
            InternalNote = "Should be ignored",
            CreatedAt = DateTime.UtcNow.AddDays(-1)
        };

        await Graph.CreateNodeAsync(person, null, TestContext.Current.CancellationToken);

        // Update with new values
        person.FirstName = "Updated";
        person.LastName = "Name";
        person.InternalNote = "Still ignored";
        person.CreatedAt = DateTime.UtcNow;

        await Graph.UpdateNodeAsync(person, null, TestContext.Current.CancellationToken);

        var retrieved = await Graph.GetNodeAsync<PersonWithIgnoredProperties>(person.Id, null, TestContext.Current.CancellationToken);

        Assert.Equal("Updated", retrieved.FirstName);
        Assert.Equal("Name", retrieved.LastName);

        // Ignored properties should still have default values
        Assert.Equal(string.Empty, retrieved.InternalNote);
    }
}