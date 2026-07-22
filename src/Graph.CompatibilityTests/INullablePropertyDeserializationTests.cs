// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.CompatibilityTests;

public interface INullablePropertyDeserializationTests : IGraphTest
{
    [Node]
    public record PersonWithNullableProperties : Node
    {
        public string Name { get; set; } = string.Empty;
        public DateTime? CompletedAt { get; set; }
        public int? Age { get; set; }
        public bool? IsActive { get; set; }
    }

    [Fact]
    public async Task DeserializeEntityWithMissingNullableProperties_ShouldSetToNull()
    {
        // Arrange
        // Create a person with only the required properties, leaving nullable properties missing
        var person = new PersonWithNullableProperties
        {
            Name = "John Doe"
            // CompletedAt, Age, and IsActive are intentionally not set
        };

        // Act
        await Graph.CreateNodeAsync(person, null, TestContext.Current.CancellationToken);

        // Retrieve the person from the database
        var retrievedPerson = await Graph.Nodes<PersonWithNullableProperties>()
            .Where(candidate => candidate.Name == person.Name)
            .SingleAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(retrievedPerson);
        Assert.Equal("John Doe", retrievedPerson.Name);
        Assert.Null(retrievedPerson.CompletedAt);
        Assert.Null(retrievedPerson.Age);
        Assert.Null(retrievedPerson.IsActive);
    }

    [Fact]
    public async Task DeserializeEntityWithProvidedNullableProperties_ShouldDeserializeCorrectly()
    {
        // Arrange
        var completedAt = DateTime.UtcNow;
        var person = new PersonWithNullableProperties
        {
            Name = "Jane Doe",
            CompletedAt = completedAt,
            Age = 30,
            IsActive = true
        };

        // Act
        await Graph.CreateNodeAsync(person, null, TestContext.Current.CancellationToken);

        // Retrieve the person from the database
        var retrievedPerson = await Graph.Nodes<PersonWithNullableProperties>()
            .Where(candidate => candidate.Name == person.Name)
            .SingleAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(retrievedPerson);
        Assert.Equal("Jane Doe", retrievedPerson.Name);
        Assert.Equal(completedAt, retrievedPerson.CompletedAt);
        Assert.Equal(30, retrievedPerson.Age);
        Assert.True(retrievedPerson.IsActive);
    }

    [Fact]
    public async Task DeserializeEntityWithSomeNullablePropertiesProvided_ShouldHandleMixedCase()
    {
        // Arrange
        var person = new PersonWithNullableProperties
        {
            Name = "Bob Smith",
            Age = 25
            // CompletedAt and IsActive are intentionally not set
        };

        // Act
        await Graph.CreateNodeAsync(person, null, TestContext.Current.CancellationToken);

        // Retrieve the person from the database
        var retrievedPerson = await Graph.Nodes<PersonWithNullableProperties>()
            .Where(candidate => candidate.Name == person.Name)
            .SingleAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(retrievedPerson);
        Assert.Equal("Bob Smith", retrievedPerson.Name);
        Assert.Null(retrievedPerson.CompletedAt);
        Assert.Equal(25, retrievedPerson.Age);
        Assert.Null(retrievedPerson.IsActive);
    }
}
