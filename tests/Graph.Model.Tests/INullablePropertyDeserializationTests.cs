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

public interface INullablePropertyDeserializationTests : IGraphModelTest
{
    [Node]
    public class PersonWithNullableProperties : INode
    {
        public string Id { get; init; } = string.Empty;
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
            Id = "person1",
            Name = "John Doe"
            // CompletedAt, Age, and IsActive are intentionally not set
        };

        // Act
        await Graph.CreateNodeAsync(person, null, TestContext.Current.CancellationToken);

        // Retrieve the person from the database
        var retrievedPerson = await Graph.GetNodeAsync<PersonWithNullableProperties>(person.Id, null, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(retrievedPerson);
        Assert.Equal("person1", retrievedPerson.Id);
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
            Id = "person2",
            Name = "Jane Doe",
            CompletedAt = completedAt,
            Age = 30,
            IsActive = true
        };

        // Act
        await Graph.CreateNodeAsync(person, null, TestContext.Current.CancellationToken);

        // Retrieve the person from the database
        var retrievedPerson = await Graph.GetNodeAsync<PersonWithNullableProperties>(person.Id, null, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(retrievedPerson);
        Assert.Equal("person2", retrievedPerson.Id);
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
            Id = "person3",
            Name = "Bob Smith",
            Age = 25
            // CompletedAt and IsActive are intentionally not set
        };

        // Act
        await Graph.CreateNodeAsync(person, null, TestContext.Current.CancellationToken);

        // Retrieve the person from the database
        var retrievedPerson = await Graph.GetNodeAsync<PersonWithNullableProperties>(person.Id, null, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(retrievedPerson);
        Assert.Equal("person3", retrievedPerson.Id);
        Assert.Equal("Bob Smith", retrievedPerson.Name);
        Assert.Null(retrievedPerson.CompletedAt);
        Assert.Equal(25, retrievedPerson.Age);
        Assert.Null(retrievedPerson.IsActive);
    }
}
