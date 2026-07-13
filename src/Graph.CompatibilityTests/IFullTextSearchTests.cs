// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.CompatibilityTests;

/// <summary>
/// Declaring <see cref="GraphCapability.FullTextSearch"/> guarantees: case-insensitive, exact-token
/// (whole-word) matching; a multi-term query matches an entity iff ALL terms match, in any order and
/// at any distance; the matched property set is exactly the entity's own
/// <c>[Property(IncludeInFullTextSearch)]</c> string properties (string-only by construction; for
/// dynamic entities, all string property values). Text on complex-property value nodes is NOT part of
/// the owning entity's match set. Ranking, stemming, phrase adjacency, prefix/wildcard, and matching
/// beyond the floor are provider-defined: the TCK asserts nothing about them and never asserts a
/// non-match for near-tokens (only for sub-tokens, which must not match). Search result order is
/// unspecified; ordering comes only from explicit <c>OrderBy</c>.
/// </summary>
[RequiresCapability(GraphCapability.FullTextSearch)]
public interface IFullTextSearchTests : IGraphTest
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
    [RequiresCapability(GraphCapability.ComplexPropertyCascade)]
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
        var results = await this.Graph.SearchNodes<PersonWithComplexProperties>("cloud").ToListAsync(TestContext.Current.CancellationToken);

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
        var results = await this.Graph.SearchRelationships<KnowsWell>("college").ToListAsync(TestContext.Current.CancellationToken);

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
        var results = await this.Graph.Search("SearchUser").ToListAsync(TestContext.Current.CancellationToken);

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
        var results = await this.Graph.SearchNodes("Wonder").ToListAsync(TestContext.Current.CancellationToken);

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
            HowWell = "uniquesearchterm12345"
        };

        await this.Graph.CreateRelationshipAsync(relationship, null, TestContext.Current.CancellationToken);

        // Search using generic IRelationship interface
        var results = await this.Graph.SearchRelationships("uniquesearchterm12345").ToListAsync(TestContext.Current.CancellationToken);

        Assert.Single(results);
        Assert.IsType<KnowsWell>(results[0]);
        var foundRelationship = (KnowsWell)results[0];
        Assert.Contains("uniquesearchterm12345", foundRelationship.HowWell);
    }

    [Fact]
    public async Task SearchReturnsEmptyResultsForNonMatchingQuery()
    {
        // Create test data
        var person = new Person { FirstName = "Alice", LastName = "Wonderland" };
        await this.Graph.CreateNodeAsync(person, null, TestContext.Current.CancellationToken);

        // Search for something that doesn't exist
        var results = await this.Graph.SearchNodes<Person>("NonExistentTerm").ToListAsync(TestContext.Current.CancellationToken);

        Assert.Empty(results);
    }

    [Fact]
    public async Task SearchIsNotCaseSensitive()
    {
        // Create test data
        var person = new Person { FirstName = "CaseSensitive", LastName = "TestCase" };
        await this.Graph.CreateNodeAsync(person, null, TestContext.Current.CancellationToken);

        // Search with different cases
        var lowerResults = await this.Graph.SearchNodes<Person>("casesensitive").ToListAsync(TestContext.Current.CancellationToken);
        var upperResults = await this.Graph.SearchNodes<Person>("CASESENSITIVE").ToListAsync(TestContext.Current.CancellationToken);
        var mixedResults = await this.Graph.SearchNodes<Person>("CaseSensitive").ToListAsync(TestContext.Current.CancellationToken);

        Assert.Single(lowerResults);
        Assert.Single(upperResults);
        Assert.Single(mixedResults);

        Assert.Equal(lowerResults[0].Id, upperResults[0].Id);
        Assert.Equal(upperResults[0].Id, mixedResults[0].Id);
    }

    [Fact]
    public async Task SearchWithWhereClause()
    {
        // Create test data
        var person = new Person { FirstName = "John", LastName = "Doe", Age = 30 };
        var person2 = new Person { FirstName = "Jane", LastName = "Doe", Age = 25 };
        await this.Graph.CreateNodeAsync(person, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateNodeAsync(person2, null, TestContext.Current.CancellationToken);

        // Search with where clause
        var results = await this.Graph.SearchNodes<Person>("John").Where(p => p.Age > 25).ToListAsync(TestContext.Current.CancellationToken);

        Assert.Single(results);
        Assert.Equal(person.Id, results[0].Id);
    }

    [Fact]
    public async Task SearchWithWhereClauseAndMultipleProperties()
    {
        // Create test data
        var person = new Person { FirstName = "John", LastName = "Doe", Age = 30 };
        var person2 = new Person { FirstName = "Jane", LastName = "Doe", Age = 25 };
        await this.Graph.CreateNodeAsync(person, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateNodeAsync(person2, null, TestContext.Current.CancellationToken);

        // Search with where clause and multiple properties
        var results = await this.Graph.SearchNodes<Person>("John").Where(p => p.Age > 25 && p.LastName == "Doe").ToListAsync(TestContext.Current.CancellationToken);

        Assert.Single(results);
        Assert.Equal(person.Id, results[0].Id);
    }

    [Fact]
    public async Task SearchWithSelectClause()
    {
        // Create test data
        var person = new Person { FirstName = "John", LastName = "Doe", Age = 30 };
        await this.Graph.CreateNodeAsync(person, null, TestContext.Current.CancellationToken);

        // Search with select clause
        var results = await this.Graph.SearchNodes<Person>("John").Select(p => p.LastName).ToListAsync(TestContext.Current.CancellationToken);

        Assert.Single(results);
        Assert.Equal("Doe", results[0]);
    }

    [Fact]
    public async Task CanSearchInLinqChain()
    {
        // Create test data
        var person1 = new Person { FirstName = "Alice", LastName = "Wonder", Bio = "Software engineer with expertise in cloud computing" };
        var person2 = new Person { FirstName = "Bob", LastName = "Builder", Bio = "Data scientist working on machine learning" };
        var person3 = new Person { FirstName = "Charlie", LastName = "Brown", Bio = "Product manager focused on user experience" };

        await this.Graph.CreateNodeAsync(person1, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateNodeAsync(person2, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateNodeAsync(person3, null, TestContext.Current.CancellationToken);

        // Search in LINQ chain
        var results = await this.Graph.Nodes<Person>()
            .Where(p => p.Age > 20)
            .Search("cloud computing")
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Single(results);
        Assert.Equal("Alice", results[0].FirstName);
        Assert.Contains("cloud computing", results[0].Bio);
    }

    [Fact]
    public async Task CanSearchInPathSegmentsChain()
    {
        // Create test data with relationships
        var user = new Person { FirstName = "John", LastName = "Doe", Bio = "User with memories" };
        var memory1 = new Person { FirstName = "Memory1", LastName = "Memory", Bio = "Important memory about vacation" };
        var memory2 = new Person { FirstName = "Memory2", LastName = "Memory", Bio = "Another memory about work" };

        await this.Graph.CreateNodeAsync(user, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateNodeAsync(memory1, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateNodeAsync(memory2, null, TestContext.Current.CancellationToken);

        var userMemory1 = new KnowsWell
        {
            StartNodeId = user.Id,
            EndNodeId = memory1.Id,
            HowWell = "Strong connection"
        };

        var userMemory2 = new KnowsWell
        {
            StartNodeId = user.Id,
            EndNodeId = memory2.Id,
            HowWell = "Weak connection"
        };

        await this.Graph.CreateRelationshipAsync(userMemory1, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateRelationshipAsync(userMemory2, null, TestContext.Current.CancellationToken);

        // Search in path segments chain
        var results = await this.Graph.Nodes<Person>()
            .Where(u => u.Id == user.Id)
            .PathSegments<Person, KnowsWell, Person>()
            .Select(p => p.EndNode)
            .Search("vacation")
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Single(results);
        Assert.Equal("Memory1", results[0].FirstName);
        Assert.Contains("vacation", results[0].Bio);
    }

    [Fact]
    public async Task CanSearchWithMultipleConditions()
    {
        // Create test data
        var person1 = new Person { FirstName = "Alice", LastName = "Wonder", Age = 30, Bio = "Software engineer with expertise in cloud computing" };
        var person2 = new Person { FirstName = "Bob", LastName = "Builder", Age = 25, Bio = "Data scientist working on machine learning" };
        var person3 = new Person { FirstName = "Charlie", LastName = "Brown", Age = 35, Bio = "Product manager focused on user experience" };

        await this.Graph.CreateNodeAsync(person1, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateNodeAsync(person2, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateNodeAsync(person3, null, TestContext.Current.CancellationToken);

        // Search with multiple conditions
#pragma warning disable CA1866 // Preserve the provider-translated string overload under test.
        var results = await this.Graph.Nodes<Person>()
            .Where(p => p.Age > 25)
            .Search("engineer")
            .Where(p => p.FirstName.StartsWith("A"))
            .ToListAsync(TestContext.Current.CancellationToken);
#pragma warning restore CA1866

        Assert.Single(results);
        Assert.Equal("Alice", results[0].FirstName);
        Assert.Contains("engineer", results[0].Bio);
    }

    [Fact]
    public async Task CanSearchWithSelectProjection()
    {
        // Create test data
        var person = new Person { FirstName = "Alice", LastName = "Wonder", Bio = "Software engineer with expertise in cloud computing" };
        await this.Graph.CreateNodeAsync(person, null, TestContext.Current.CancellationToken);

        // Search with select projection
        var results = await this.Graph.Nodes<Person>()
            .Search("cloud computing")
            .Select(p => new { p.FirstName, p.Bio })
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Single(results);
        Assert.Equal("Alice", results[0].FirstName);
        Assert.Contains("cloud computing", results[0].Bio);
    }

    [Fact]
    public async Task SearchInLinqChainReturnsEmptyForNonMatchingQuery()
    {
        // Create test data
        var person = new Person { FirstName = "Alice", LastName = "Wonder", Bio = "Software engineer" };
        await this.Graph.CreateNodeAsync(person, null, TestContext.Current.CancellationToken);

        // Search for something that doesn't exist
        var results = await this.Graph.Nodes<Person>()
            .Where(p => p.Age > 20)
            .Search("NonExistentTerm")
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Empty(results);
    }

    [Fact]
    public async Task SearchInLinqChainIsNotCaseSensitive()
    {
        // Create test data
        var person = new Person { FirstName = "Alice", LastName = "Wonder", Bio = "Software engineer with CLOUD computing expertise" };
        await this.Graph.CreateNodeAsync(person, null, TestContext.Current.CancellationToken);

        // Search with different cases
        var lowerResults = await this.Graph.Nodes<Person>()
            .Search("cloud computing")
            .ToListAsync(TestContext.Current.CancellationToken);

        var upperResults = await this.Graph.Nodes<Person>()
            .Search("CLOUD COMPUTING")
            .ToListAsync(TestContext.Current.CancellationToken);

        var mixedResults = await this.Graph.Nodes<Person>()
            .Search("Cloud Computing")
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Single(lowerResults);
        Assert.Single(upperResults);
        Assert.Single(mixedResults);

        Assert.Equal(lowerResults[0].Id, upperResults[0].Id);
        Assert.Equal(upperResults[0].Id, mixedResults[0].Id);
    }

    [Fact]
    public async Task SearchWorksWithInheritance()
    {
        // Create test data
        var person = new Person { FirstName = "Alice", LastName = "Wonder", Bio = "Software engineer with CLOUD computing expertise" };
        await this.Graph.CreateNodeAsync(person, null, TestContext.Current.CancellationToken);

        var manager = new Manager { FirstName = "Alice", LastName = "Builder", Bio = "Manager with expertise in project management", Department = "Construction" };
        await this.Graph.CreateNodeAsync(manager, null, TestContext.Current.CancellationToken);

        var personInDepartment = await this.Graph.SearchNodes<Person>("Construction").ToListAsync(TestContext.Current.CancellationToken);
        Assert.Single(personInDepartment);

        var personCalledAlice = await this.Graph.SearchNodes<Person>("Alice").ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(2, personCalledAlice.Count);
        Assert.Contains(personCalledAlice, p => p.LastName == "Wonder" && p is Person);
        Assert.Contains(personCalledAlice, p => p.LastName == "Builder" && p is Manager);
    }

    [Fact]
    public async Task CanSearchDynamicNodeWithFullTextSearch()
    {
        var person = new Person { FirstName = "Alice", LastName = "Wonder" };
        await this.Graph.CreateNodeAsync(person, null, TestContext.Current.CancellationToken);

        // Should find node when searching for property value
        var found = await this.Graph.SearchNodes<DynamicNode>("Wonder").ToListAsync(cancellationToken: TestContext.Current.CancellationToken);
        Assert.Single(found);
        Assert.Equal(person.FirstName, found[0].Properties["FirstName"]);
        Assert.Equal(person.LastName, found[0].Properties["LastName"]);
    }

    [Fact]
    public async Task CanSearchDynamicRelationshipWithFullTextSearch()
    {
        var person1 = new Person { FirstName = "Alice", LastName = "Wonder" };
        var person2 = new Person { FirstName = "Bob", LastName = "Builder" };
        await this.Graph.CreateNodeAsync(person1, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateNodeAsync(person2, null, TestContext.Current.CancellationToken);
        var knows = new KnowsWell
        {
            StartNodeId = person1.Id,
            EndNodeId = person2.Id,
            HowWell = "Good friends"
        };
        await this.Graph.CreateRelationshipAsync(knows, null, TestContext.Current.CancellationToken);

        // Should find relationship when searching for property value
        var found = await this.Graph.SearchRelationships<DynamicRelationship>("Good friends").ToListAsync(TestContext.Current.CancellationToken);
        Assert.Single(found);
        Assert.Equal("Good friends", found[0].Properties["HowWell"]);
    }

    // ---- IGraph.Search* convenience/`.Search()` operator equivalence (issue #94 scope item 8 /
    // testing requirement: "Search-operator equivalence with the old IGraph methods") ----

    [Fact]
    public async Task SearchNodesOfT_IsEquivalentToNodesOfTThenSearchOperator()
    {
        var person = new Person { FirstName = "Equivalence", LastName = "Wonder" };
        await this.Graph.CreateNodeAsync(person, null, TestContext.Current.CancellationToken);

        // IGraph.SearchNodes<T>(query) is documented (IGraph.cs XML doc) as a thin convenience
        // equivalent to graph.Nodes<T>(transaction).Search(query) - verify that equivalence holds
        // for real results, not just by reading the doc comment.
        var viaConvenience = await this.Graph.SearchNodes<Person>("Equivalence").ToListAsync(TestContext.Current.CancellationToken);
        var viaOperator = await this.Graph.Nodes<Person>().Search("Equivalence").ToListAsync(TestContext.Current.CancellationToken);

        Assert.Single(viaConvenience);
        Assert.Single(viaOperator);
        Assert.Equal(viaConvenience[0].Id, viaOperator[0].Id);
    }

    [Fact]
    public async Task SearchRelationshipsOfT_IsEquivalentToRelationshipsOfTThenSearchOperator()
    {
        var person1 = new Person { FirstName = "Alice", LastName = "Equivalence" };
        var person2 = new Person { FirstName = "Bob", LastName = "Equivalence" };
        await this.Graph.CreateNodeAsync(person1, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateNodeAsync(person2, null, TestContext.Current.CancellationToken);
        var knows = new KnowsWell
        {
            StartNodeId = person1.Id,
            EndNodeId = person2.Id,
            HowWell = "SearchEquivalenceMarker"
        };
        await this.Graph.CreateRelationshipAsync(knows, null, TestContext.Current.CancellationToken);

        var viaConvenience = await this.Graph.SearchRelationships<KnowsWell>("SearchEquivalenceMarker").ToListAsync(TestContext.Current.CancellationToken);
        var viaOperator = await this.Graph.Relationships<KnowsWell>().Search("SearchEquivalenceMarker").ToListAsync(TestContext.Current.CancellationToken);

        Assert.Single(viaConvenience);
        Assert.Single(viaOperator);
        Assert.Equal(viaConvenience[0].Id, viaOperator[0].Id);
    }

    // ---- #288: FullTextSearch semantic-contract floor ----

    [Fact]
    public async Task MultiTermSearch_MatchesOnlyEntitiesContainingAllTerms()
    {
        // Bio is a searchable string property; FirstName/LastName are kept term-free so the only
        // searchable tokens are the ones under test.
        var cloudOnly = new Person { FirstName = "AndCloud", LastName = "AndTerm", Bio = "expertise in cloud systems" };
        var computingOnly = new Person { FirstName = "AndComputing", LastName = "AndTerm", Bio = "distributed computing research" };
        var both = new Person { FirstName = "AndBoth", LastName = "AndTerm", Bio = "cloud computing platforms" };

        await this.Graph.CreateNodeAsync(cloudOnly, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateNodeAsync(computingOnly, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateNodeAsync(both, null, TestContext.Current.CancellationToken);

        var results = await this.Graph.SearchNodes<Person>("cloud computing").ToListAsync(TestContext.Current.CancellationToken);

        // ALL terms must match: only the node whose text contains both "cloud" and "computing".
        Assert.Single(results);
        Assert.Equal(both.Id, results[0].Id);
    }

    [Fact]
    public async Task Search_MatchesWholeTokenButNotSubToken()
    {
        var person = new Person { FirstName = "Holiday", LastName = "Planner", Bio = "planning a long vacation abroad" };
        await this.Graph.CreateNodeAsync(person, null, TestContext.Current.CancellationToken);

        var wholeToken = await this.Graph.SearchNodes<Person>("vacation").ToListAsync(TestContext.Current.CancellationToken);
        Assert.Single(wholeToken);
        Assert.Equal(person.Id, wholeToken[0].Id);

        // A sub-token must not match: "vaca" is not the whole word "vacation".
        var subToken = await this.Graph.SearchNodes<Person>("vaca").ToListAsync(TestContext.Current.CancellationToken);
        Assert.Empty(subToken);
    }

    [Fact]
    public async Task Search_WithMetacharacters_DoesNotThrowAndMatchesToken()
    {
        var person = new Person { FirstName = "Holiday", LastName = "Planner", Bio = "planning a long vacation abroad" };
        await this.Graph.CreateNodeAsync(person, null, TestContext.Current.CancellationToken);

        // Live Lucene syntax in the raw query must be sanitized, not parsed: these must neither throw
        // nor change semantics away from the plain "vacation" token.
        var tilde = await this.Graph.SearchNodes<Person>("vacation~").ToListAsync(TestContext.Current.CancellationToken);
        Assert.Single(tilde);
        Assert.Equal(person.Id, tilde[0].Id);

        var star = await this.Graph.SearchNodes<Person>("vacation*").ToListAsync(TestContext.Current.CancellationToken);
        Assert.Single(star);
        Assert.Equal(person.Id, star[0].Id);
    }

    [Fact]
    public async Task SearchAsTraversalSource_IsRejectedWithNamedError()
    {
        // TODO(#295): search-as-source is rejected at translation time until #295 lands. The query
        // never executes, so no fixture data is required; it is gated under FullTextSearch like the
        // rest of this suite.
        var query = this.Graph.Nodes<Person>()
            .Search("anything")
            .Traverse<Knows, Person>();

        var exception = await Assert.ThrowsAsync<GraphQueryTranslationException>(
            async () => await query.ToListAsync(TestContext.Current.CancellationToken));

        Assert.Contains("Search", exception.Message, StringComparison.Ordinal);
        Assert.Contains("Traverse", exception.Message, StringComparison.Ordinal);
    }
}
