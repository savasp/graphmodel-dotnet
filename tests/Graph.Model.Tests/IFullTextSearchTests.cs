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

public interface IFullTextSearchTests : IGraphModelTest
{
    [Fact]
    public async Task CanSearchNodesWithFullTextSearch()
    {
        // Create test data
        var person1 = new Person { FirstName = "John", LastName = "Doe" };
        var person2 = new Person { FirstName = "Jane", LastName = "Smith" };
        var person3 = new Person { FirstName = "Bob", LastName = "Johnson" };

        // Create the first person - this should trigger schema creation including fulltext index
        await this.Graph.CreateNodeAsync(person1, null, TestContext.Current.CancellationToken);

        await this.Graph.CreateNodeAsync(person2, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateNodeAsync(person3, null, TestContext.Current.CancellationToken);

        // Search for "John"
        var results = await this.Graph.SearchNodes<Person>("John").ToListAsync(TestContext.Current.CancellationToken);

        Assert.Single(results);
        Assert.Equal("John", results[0].FirstName);
    }

    [Fact]
    public async Task CanSearchNodesWithComplexPropertiesAndFullTextSearch()
    {
        // Create test data with complex properties
        var person1 = new PersonWithComplexProperties
        {
            FirstName = "Alice",
            LastName = "Wonder",
            Bio = "Software engineer with expertise in cloud computing",
            Address = new AddressValue { Street = "123 Cloud Street", City = "Tech City" }
        };

        var person2 = new PersonWithComplexProperties
        {
            FirstName = "Bob",
            LastName = "Builder",
            Bio = "Data scientist working on machine learning projects",
            Address = new AddressValue { Street = "456 Data Avenue", City = "AI Town" }
        };

        await this.Graph.CreateNodeAsync(person1, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateNodeAsync(person2, null, TestContext.Current.CancellationToken);

        // Search for "cloud" - this should find Alice and load her complex Address property
        var results = await this.Graph.SearchNodes<PersonWithComplexProperties>("cloud").ToListAsync();

        Assert.Single(results);
        var foundPerson = results[0];
        Assert.Equal("Alice", foundPerson.FirstName);
        Assert.Equal("Wonder", foundPerson.LastName);
        Assert.Contains("cloud computing", foundPerson.Bio);

        // Verify complex property is loaded
        Assert.NotNull(foundPerson.Address);
        Assert.Equal("123 Cloud Street", foundPerson.Address.Street);
        Assert.Equal("Tech City", foundPerson.Address.City);
    }

    [Fact]
    public async Task CanSearchRelationshipsWithFullTextSearch()
    {
        // Create test data
        var person1 = new Person { FirstName = "Alice" };
        var person2 = new Person { FirstName = "Bob" };

        await this.Graph.CreateNodeAsync(person1, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateNodeAsync(person2, null, TestContext.Current.CancellationToken);

        var friendship = new KnowsWell
        {
            StartNodeId = person1.Id,
            EndNodeId = person2.Id,
            HowWell = "Close friendship since college"
        };

        await this.Graph.CreateRelationshipAsync(friendship, null, TestContext.Current.CancellationToken);

        // Search for "college"
        var results = await this.Graph.SearchRelationships<KnowsWell>("college").ToListAsync();

        Assert.Single(results);
        Assert.Contains("college", results[0].HowWell);
    }

    [Fact]
    public async Task CanSearchAllEntitiesWithFullTextSearch()
    {
        // Create test data
        var person = new Person { FirstName = "TestUser", LastName = "SearchUser" };
        var friend = new Person { FirstName = "Friend", LastName = "User" };

        await this.Graph.CreateNodeAsync(person, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateNodeAsync(friend, null, TestContext.Current.CancellationToken);

        var relationship = new KnowsWell
        {
            StartNodeId = person.Id,
            EndNodeId = friend.Id,
            HowWell = "SearchUser knows Friend"
        };

        await this.Graph.CreateRelationshipAsync(relationship, null, TestContext.Current.CancellationToken);

        // Search for "SearchUser" across all entities
        var results = await this.Graph.Search("SearchUser").ToListAsync();

        Assert.True(results.Count >= 1, "Should find at least one entity containing 'SearchUser'");
        Assert.True(results.Any(e => e is INode), "Should find node entities");
    }

    [Fact]
    public async Task CanSearchNodesWithGenericInterface()
    {
        // Create test data
        var person1 = new Person { FirstName = "Alice", LastName = "Wonder" };
        var person2 = new Person { FirstName = "Bob", LastName = "Builder" };

        await this.Graph.CreateNodeAsync(person1, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateNodeAsync(person2, null, TestContext.Current.CancellationToken);

        // Search using generic INode interface
        var results = await this.Graph.SearchNodes("Wonder").ToListAsync();

        Assert.Single(results);
        Assert.IsType<Person>(results[0]);
        var foundPerson = (Person)results[0];
        Assert.Equal("Alice", foundPerson.FirstName);
    }

    [Fact]
    public async Task CanSearchRelationshipsWithGenericInterface()
    {
        // Create test data
        var person1 = new Person { FirstName = "Alice" };
        var person2 = new Person { FirstName = "Bob" };

        await this.Graph.CreateNodeAsync(person1, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateNodeAsync(person2, null, TestContext.Current.CancellationToken);

        var relationship = new KnowsWell
        {
            StartNodeId = person1.Id,
            EndNodeId = person2.Id,
            HowWell = "unique_search_term_12345"
        };

        await this.Graph.CreateRelationshipAsync(relationship, null, TestContext.Current.CancellationToken);

        // Search using generic IRelationship interface
        var results = await this.Graph.SearchRelationships("unique_search_term_12345").ToListAsync();

        Assert.Single(results);
        Assert.IsType<KnowsWell>(results[0]);
        var foundRelationship = (KnowsWell)results[0];
        Assert.Contains("unique_search_term_12345", foundRelationship.HowWell);
    }

    [Fact]
    public async Task SearchReturnsEmptyResultsForNonMatchingQuery()
    {
        // Create test data
        var person = new Person { FirstName = "Alice", LastName = "Wonderland" };
        await this.Graph.CreateNodeAsync(person, null, TestContext.Current.CancellationToken);

        // Search for something that doesn't exist
        var results = await this.Graph.SearchNodes<Person>("NonExistentTerm").ToListAsync();

        Assert.Empty(results);
    }

    [Fact]
    public async Task SearchIsNotCaseSensitive()
    {
        // Create test data
        var person = new Person { FirstName = "CaseSensitive", LastName = "TestCase" };
        await this.Graph.CreateNodeAsync(person, null, TestContext.Current.CancellationToken);

        // Search with different cases
        var lowerResults = await this.Graph.SearchNodes<Person>("casesensitive").ToListAsync();
        var upperResults = await this.Graph.SearchNodes<Person>("CASESENSITIVE").ToListAsync();
        var mixedResults = await this.Graph.SearchNodes<Person>("CaseSensitive").ToListAsync();

        Assert.Single(lowerResults);
        Assert.Single(upperResults);
        Assert.Single(mixedResults);

        Assert.Equal(lowerResults[0].Id, upperResults[0].Id);
        Assert.Equal(upperResults[0].Id, mixedResults[0].Id);
    }
}