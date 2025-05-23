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

public abstract class AdvancedQueryTestsBase
{
    private IGraph provider;

    protected AdvancedQueryTestsBase(IGraph provider)
    {
        this.provider = provider;
    }

    [Fact]
    public async Task CanQueryWithMultipleConditions()
    {
        var p1 = new Person { FirstName = "Alice", LastName = "Smith" };
        var p2 = new Person { FirstName = "Bob", LastName = "Smith" };
        var p3 = new Person { FirstName = "Charlie", LastName = "Jones" };
        await this.provider.CreateNode(p1);
        await this.provider.CreateNode(p2);
        await this.provider.CreateNode(p3);

        var smiths = this.provider.Nodes<Person>().Where(p => p.LastName == "Smith" && p.FirstName.StartsWith('A')).ToList();
        Assert.Single(smiths);
        Assert.Equal("Alice", smiths[0].FirstName);
    }

    [Fact]
    public async Task CanQueryWithOrderByAndTake()
    {
        await this.provider.CreateNode(new Person { FirstName = "Zed", LastName = "Alpha" });
        await this.provider.CreateNode(new Person { FirstName = "Ann", LastName = "Beta" });
        await this.provider.CreateNode(new Person { FirstName = "Bob", LastName = "Gamma" });

        var ordered = this.provider.Nodes<Person>().OrderBy(p => p.FirstName).Take(2).ToList();
        Assert.Equal(2, ordered.Count);
        Assert.Equal("Ann", ordered[0].FirstName);
        Assert.Equal("Bob", ordered[1].FirstName);
    }

    [Fact]
    public async Task CanQueryWithProjection()
    {
        await this.provider.CreateNode(new Person { FirstName = "Alice", LastName = "Smith" });
        await this.provider.CreateNode(new Person { FirstName = "Bob", LastName = "Smith" });
        var names = this.provider.Nodes<Person>().Where(p => p.LastName == "Smith").Select(p => p.FirstName).ToList();
        Assert.Contains("Alice", names);
        Assert.Contains("Bob", names);
    }

    [Fact]
    public async Task CanQueryWithCount()
    {
        await this.provider.CreateNode(new Person { FirstName = "Alice", LastName = "Smith" });
        await this.provider.CreateNode(new Person { FirstName = "Bob", LastName = "Smith" });
        var count = this.provider.Nodes<Person>().Count(p => p.LastName == "Smith");
        Assert.True(count >= 2);
    }

    [Fact]
    public async Task CanProjectToAnAnonymousType()
    {
        await this.provider.CreateNode(new Person { FirstName = "Alice", LastName = "Smith" });
        await this.provider.CreateNode(new Person { FirstName = "Bob", LastName = "Smith" });
        var projected = this.provider.Nodes<Person>()
            .Where(p => p.LastName == "Smith")
            .Select(p => new { Name = p.FirstName })
            .ToList();
        Assert.Equal(2, projected.Count);
        Assert.Contains(projected, p => p.Name == "Alice");
        Assert.Contains(projected, p => p.Name == "Bob");
    }

    [Fact]
    public async Task CanProjectToAnAnonymousTypeWithFunction()
    {
        await this.provider.CreateNode(new Person { FirstName = "Alice", LastName = "Smith" });
        await this.provider.CreateNode(new Person { FirstName = "Bob", LastName = "Smith" });
        var projected = this.provider.Nodes<Person>()
            .Where(p => p.LastName == "Smith")
            .Select(p => new { Name = p.FirstName + " " + p.LastName })
            .ToList();
        Assert.Equal(2, projected.Count);
        Assert.Contains(projected, p => p.Name == "Alice Smith");
        Assert.Contains(projected, p => p.Name == "Bob Smith");
    }

    [Fact]
    public async Task CanNavigateRelationshipsInMemory()
    {
        var alice = new Person { FirstName = "Alice", LastName = "Smith" };
        await this.provider.CreateNode(alice);
        var bob = new Person { FirstName = "Bob", LastName = "Smith" };
        await this.provider.CreateNode(bob);
        var charlie = new Person { FirstName = "Charlie", LastName = "Jones" };
        await this.provider.CreateNode(charlie);
        var knows1 = new Knows<Person, Person>(alice, bob) { Since = DateTime.UtcNow };
        var knows2 = new Knows<Person, Person>(bob, charlie) { Since = DateTime.UtcNow };
        await this.provider.CreateRelationship(knows1);
        await this.provider.CreateRelationship(knows2);

        // Fetch all people and relationships
        var people = this.provider.Nodes<Person>().ToList();
        var rels = await this.provider.GetRelationships<Knows<Person, Person>>([knows1.Id, knows2.Id]);

        // Find all people Bob knows (outgoing)
        var bobsFriends = rels.Where(r => r.SourceId == bob.Id)
                              .Select(r => people.FirstOrDefault(p => p.Id == r.TargetId))
                              .Where(p => p != null)
                              .ToList();
        Assert.Single(bobsFriends);
        Assert.Equal("Charlie", bobsFriends[0]!.FirstName);

        // Find all people who know Bob (incoming)
        var knowsBob = rels.Where(r => r.TargetId == bob.Id)
                           .Select(r => people.FirstOrDefault(p => p.Id == r.SourceId))
                           .Where(p => p != null)
                           .ToList();
        Assert.Single(knowsBob);
        Assert.Equal(alice.Id, knowsBob[0]!.Id);
    }

    [Fact]
    public async Task CanJoinNodesAndRelationshipsInMemory()
    {
        var alice = new Person { FirstName = "Alice", LastName = "Smith" };
        await this.provider.CreateNode(alice);
        var bob = new Person { FirstName = "Bob", LastName = "Smith" };
        await this.provider.CreateNode(bob);

        var knows = new Knows<Person, Person>(alice, bob) { Since = DateTime.UtcNow };
        await this.provider.CreateRelationship(knows);

        var people = this.provider.Nodes<Person>().ToList();
        var rels = await this.provider.GetRelationships<Knows<Person, Person>>([knows.Id]);

        // Join: Find all (person, friend) pairs
        var pairs = (from p in people
                     join k in rels on p.Id equals k.SourceId
                     join f in people on k.TargetId equals f.Id
                     select new { Person = p, Friend = f }).ToList();
        Assert.Single(pairs);
        Assert.Equal("Alice", pairs[0].Person.FirstName);
        Assert.Equal("Bob", pairs[0].Friend.FirstName);
    }

    [Fact]
    public async Task CanProjectWithStringFunctions()
    {
        await this.provider.CreateNode(new Person { FirstName = "Alice", LastName = "Smith" });
        await this.provider.CreateNode(new Person { FirstName = "Bob", LastName = "Smith" });
        var projected = this.provider.Nodes<Person>()
            .Where(p => p.LastName == "Smith")
            .Select(p => new
            {
                Upper = p.FirstName.ToUpper(),
                Lower = p.LastName.ToLower(),
                Trimmed = ("  " + p.FirstName + "  ").Trim(),
                Sub = p.FirstName.Substring(0, 1),
                Replaced = p.LastName.Replace("Smith", "S.")
            })
            .ToList();
        Assert.Equal(2, projected.Count);
        Assert.Contains(projected, p => p.Upper == "ALICE" && p.Lower == "smith" && p.Trimmed == "Alice" && p.Sub == "A" && p.Replaced == "S.");
        Assert.Contains(projected, p => p.Upper == "BOB" && p.Lower == "smith" && p.Trimmed == "Bob" && p.Sub == "B" && p.Replaced == "S.");
    }

    [Fact]
    public async Task CanProjectWithMathFunctions()
    {
        await this.provider.CreateNode(new Person { FirstName = "Alice", LastName = "Smith", Age = 30 });
        await this.provider.CreateNode(new Person { FirstName = "Bob", LastName = "Smith", Age = 40 });
        var projected = this.provider.Nodes<Person>()
            .Where(p => p.LastName == "Smith")
            .Select(p => new
            {
                AgePlusTen = p.Age + 10,
                AgeTimesTwo = p.Age * 2,
                AgeDivTwo = p.Age / 2,
                AgeModTen = p.Age % 10,
                SqrtAge = Math.Sqrt(p.Age)
            })
            .ToList();
        Assert.Equal(2, projected.Count);
        Assert.Contains(projected, p => p.AgePlusTen == 40 && p.AgeTimesTwo == 60 && p.AgeDivTwo == 15 && p.AgeModTen == 0 && Math.Abs(p.SqrtAge - Math.Sqrt(30)) < 0.01);
        Assert.Contains(projected, p => p.AgePlusTen == 50 && p.AgeTimesTwo == 80 && p.AgeDivTwo == 20 && p.AgeModTen == 0 && Math.Abs(p.SqrtAge - Math.Sqrt(40)) < 0.01);
    }

    [Fact]
    public async Task CanProjectWithNestedComputedProperties()
    {
        await this.provider.CreateNode(new Person { FirstName = "Alice", LastName = "Smith", Age = 30 });
        await this.provider.CreateNode(new Person { FirstName = "Bob", LastName = "Smith", Age = 40 });
        var projected = this.provider.Nodes<Person>()
            .Where(p => p.LastName == "Smith")
            .Select(p => new
            {
                Complex = p.FirstName.ToUpper() + "-" + p.LastName.ToLower() + "-" + (p.Age + 1),
                Logic = p.Age > 35 ? "Senior" : "Junior"
            })
            .ToList();
        Assert.Equal(2, projected.Count);
        Assert.Contains(projected, p => p.Complex.StartsWith("ALICE-smith-") && p.Logic == "Junior");
        Assert.Contains(projected, p => p.Complex.StartsWith("BOB-smith-") && p.Logic == "Senior");
    }

    // --- EXTENSIVE LINQ TESTS FOR NODES AND RELATIONSHIPS ---

    [Fact]
    public async Task CanQueryRelationshipsWithFilterAndProjection()
    {
        var alice = new Person { FirstName = "Alice", LastName = "Smith" };
        var bob = new Person { FirstName = "Bob", LastName = "Smith" };
        await this.provider.CreateNode(alice);
        await this.provider.CreateNode(bob);
        var knows = new Knows<Person, Person>(alice, bob) { Since = DateTime.UtcNow };
        await this.provider.CreateRelationship(knows);

        var rels = this.provider.Relationships<Knows<Person, Person>>().Where(r => r.SourceId == alice.Id).Select(r => r.Since).ToList();
        Assert.Single(rels);
        Assert.True(rels[0] > DateTime.MinValue);

        // The project is on the right hand side of the expression
        rels = this.provider.Relationships<Knows<Person, Person>>().Where(r => alice.Id == r.SourceId).Select(r => r.Since).ToList();
        Assert.Single(rels);
        Assert.True(rels[0] > DateTime.MinValue);
    }

    [Fact]
    public async Task CanOrderAndProjectRelationships()
    {
        var alice = new Person { FirstName = "Alice" };
        var bob = new Person { FirstName = "Bob" };
        var charlie = new Person { FirstName = "Charlie" };
        await this.provider.CreateNode(alice);
        await this.provider.CreateNode(bob);
        await this.provider.CreateNode(charlie);
        var r1 = new Knows<Person, Person>(alice, bob) { Since = DateTime.UtcNow.AddDays(-2) };
        var r2 = new Knows<Person, Person>(alice, charlie) { Since = DateTime.UtcNow.AddDays(-1) };
        await this.provider.CreateRelationship(r1);
        await this.provider.CreateRelationship(r2);

        var ordered = this.provider.Relationships<Knows<Person, Person>>().OrderBy(r => r.Since).ToList();
        Assert.Equal(2, ordered.Count);
        Assert.True(ordered[0].Since < ordered[1].Since);
    }

    [Fact]
    public async Task CanQueryWithMultipleOrderByAndThenBy()
    {
        await this.provider.CreateNode(new Person { FirstName = "Ann", LastName = "Smith" });
        await this.provider.CreateNode(new Person { FirstName = "Ann", LastName = "Brown" });
        await this.provider.CreateNode(new Person { FirstName = "Bob", LastName = "Smith" });
        var ordered = this.provider.Nodes<Person>().OrderBy(p => p.FirstName).ThenBy(p => p.LastName).ToList();
        Assert.Equal(3, ordered.Count);
        Assert.Equal("Ann", ordered[0].FirstName);
        Assert.Equal("Brown", ordered[0].LastName);
    }

    [Fact]
    public async Task CanQueryWithDistinctAndSkip()
    {
        await this.provider.CreateNode(new Person { FirstName = "Ann", LastName = "Smith" });
        await this.provider.CreateNode(new Person { FirstName = "Ann", LastName = "Brown" });
        await this.provider.CreateNode(new Person { FirstName = "Bob", LastName = "Smith" });
        var names = this.provider.Nodes<Person>().Select(p => p.FirstName).Distinct().Skip(1).ToList();
        Assert.Single(names);
        Assert.Equal("Bob", names[0]);
    }

    [Fact]
    public async Task CanQueryWithAnyAllFirstSingleLast()
    {
        await this.provider.CreateNode(new Person { FirstName = "Ann", LastName = "Smith" });
        await this.provider.CreateNode(new Person { FirstName = "Bob", LastName = "Smith" });
        var anyAnn = this.provider.Nodes<Person>().Any(p => p.FirstName == "Ann");
        var allSmith = this.provider.Nodes<Person>().All(p => p.LastName == "Smith");
        var first = this.provider.Nodes<Person>().OrderBy(p => p.FirstName).First();
        var single = this.provider.Nodes<Person>().Single(p => p.FirstName == "Ann");
        var last = this.provider.Nodes<Person>().OrderBy(p => p.FirstName).Last();
        Assert.True(anyAnn);
        Assert.True(allSmith);
        Assert.Equal("Ann", first.FirstName);
        Assert.Equal("Ann", single.FirstName);
        Assert.Equal("Bob", last.FirstName);
    }

    [Fact]
    public async Task CanQueryWithStringAndMathFunctionsAdvanced()
    {
        await this.provider.CreateNode(new Person { FirstName = "Eve", LastName = "Smith", Age = 25 });
        var projected = this.provider.Nodes<Person>()
            .Select(p => new
            {
                Len = p.FirstName.Length,
                Contains = p.LastName.Contains("mi"),
                Abs = Math.Abs(p.Age - 30),
            })
            .ToList();

        Assert.Single(projected);
        Assert.Equal(3, projected[0].Len);
        Assert.True(projected[0].Contains);
        Assert.Equal(5, projected[0].Abs);
    }

    [Fact(Skip = "GroupBy/Aggregate not yet implemented")]
    public async Task CanQueryWithGroupByAndAggregate()
    {
        await this.provider.CreateNode(new Person { FirstName = "Ann", LastName = "Smith" });
        await this.provider.CreateNode(new Person { FirstName = "Bob", LastName = "Smith" });
        await this.provider.CreateNode(new Person { FirstName = "Ann", LastName = "Brown" });
        var grouped = this.provider.Nodes<Person>()
            .GroupBy(p => p.FirstName)
            .Select(g => new { Name = g.Key, Count = g.Count() })
            .ToList();

        Assert.Contains(grouped, g => g.Name == "Ann" && g.Count == 2);
        Assert.Contains(grouped, g => g.Name == "Bob" && g.Count == 1);
    }

    [Fact]
    public async Task CanProjectWithDateTimeFunctions()
    {
        // Arrange
        var eve = new Person { FirstName = "Eve", LastName = "Smith", Age = 25 };
        await this.provider.CreateNode(eve);

        // Act
        var projected = this.provider.Nodes<Person>()
            .Where(p => p.FirstName == "Eve")
            .Select(p => new
            {
                PersonName = p.FirstName,
                CurrentDateTime = DateTime.Now,
                CurrentDate = DateTime.Today,
                CurrentUtc = DateTime.UtcNow,
                Year = DateTime.Now.Year,
                Month = DateTime.Now.Month,
                Day = DateTime.Now.Day
            })
            .ToList();

        // Assert
        Assert.Single(projected);
        var result = projected[0];

        // Verify person name to ensure we got the right record
        Assert.Equal("Eve", result.PersonName);

        // DateTime.Now in Neo4j returns UTC, so compare with UtcNow
        Assert.True(Math.Abs((DateTime.UtcNow - result.CurrentDateTime).TotalSeconds) < 5);

        // DateTime.Today should be today's date at midnight UTC
        Assert.Equal(DateTime.UtcNow.Date, result.CurrentDate.Date);
        Assert.Equal(TimeSpan.Zero, result.CurrentDate.TimeOfDay);

        // DateTime.UtcNow should be close to current UTC time
        Assert.True(Math.Abs((DateTime.UtcNow - result.CurrentUtc).TotalSeconds) < 5);

        // Year, Month, Day should match current UTC date
        Assert.Equal(DateTime.UtcNow.Year, result.Year);
        Assert.Equal(DateTime.UtcNow.Month, result.Month);
        Assert.Equal(DateTime.UtcNow.Day, result.Day);
    }

    [Fact(Skip = "Collection Cypher functions not yet implemented")]
    public async Task CanProjectWithCollectionFunctions()
    {
        await this.provider.CreateNode(new Person { FirstName = "Eve", LastName = "Smith", Age = 25 });
        var projected = this.provider.Nodes<Person>()
            .Select(p => new
            {
                // Should map to Cypher collect(), size(), etc.
                // Example: FriendsCount = p.Friends.Count()
            })
            .ToList();
        // TODO: Enable asserts when implemented
    }

    [Fact(Skip = "Pattern comprehensions not yet implemented")]
    public async Task CanQueryWithPatternComprehension()
    {
        // TODO: Write test for pattern comprehensions when navigation/deep traversal is supported
    }

    [Fact(Skip = "Full-text search not yet implemented")]
    public async Task CanQueryWithFullTextSearch()
    {
        // TODO: Write test for full-text search Cypher integration
    }

    [Fact(Skip = "Subqueries not yet implemented")]
    public async Task CanQueryWithSubqueries()
    {
        // TODO: Write test for Cypher CALL { ... } subqueries
    }

    [Fact]
    public async Task CanQueryWithDeepNavigation()
    {
        // Arrange: Create a social network graph
        // Alice -> knows -> Bob -> knows -> Charlie -> knows -> David
        var alice = new Person { FirstName = "Alice", LastName = "Smith" };
        var bob = new Person { FirstName = "Bob", LastName = "Jones" };
        var charlie = new Person { FirstName = "Charlie", LastName = "Brown" };
        var david = new Person { FirstName = "David", LastName = "Wilson" };

        await this.provider.CreateNode(alice);
        await this.provider.CreateNode(bob);
        await this.provider.CreateNode(charlie);
        await this.provider.CreateNode(david);

        var knows1 = new Knows<Person, Person>(alice, bob) { Since = DateTime.UtcNow.AddDays(-30) };
        var knows2 = new Knows<Person, Person>(bob, charlie) { Since = DateTime.UtcNow.AddDays(-20) };
        var knows3 = new Knows<Person, Person>(charlie, david) { Since = DateTime.UtcNow.AddDays(-10) };

        await this.provider.CreateRelationship(knows1);
        await this.provider.CreateRelationship(knows2);
        await this.provider.CreateRelationship(knows3);

        // Act & Assert: Test navigation at different levels

        // Test 1: Simple relationship query - who does Alice know?
        var aliceKnows = this.provider.Relationships<Knows<Person, Person>>()
            .Where(k => k.SourceId == alice.Id)
            .ToList();

        Assert.Single(aliceKnows);
        Assert.Equal(bob.Id, aliceKnows[0].TargetId);

        // Test 2: Get all people and relationships, then navigate in memory
        var allPeople = this.provider.Nodes<Person>().ToList();
        var allKnows = this.provider.Relationships<Knows<Person, Person>>().ToList();

        // Find Bob's friends in memory
        var bobsFriends = allKnows
            .Where(k => k.SourceId == bob.Id)
            .Join(allPeople, k => k.TargetId, p => p.Id, (k, p) => p)
            .ToList();

        Assert.Single(bobsFriends);
        Assert.Equal("Charlie", bobsFriends[0].FirstName);

        // Test 3: Find friends of friends using in-memory navigation
        var alicesFriendsOfFriends = allKnows
            .Where(k => k.SourceId == alice.Id)
            .SelectMany(k1 => allKnows
                .Where(k2 => k2.SourceId == k1.TargetId)
                .Select(k2 => allPeople.First(p => p.Id == k2.TargetId)))
            .ToList();

        Assert.Single(alicesFriendsOfFriends);
        Assert.Equal("Charlie", alicesFriendsOfFriends[0].FirstName);
    }

    [Fact]
    public async Task CanQueryNodesWithNavigationProperties()
    {
        // This test uses the PersonWithNavigationProperty class that has IList<Knows> Knows property
        var alice = new PersonWithNavigationProperty { FirstName = "Alice" };
        var bob = new PersonWithNavigationProperty { FirstName = "Bob" };
        var charlie = new PersonWithNavigationProperty { FirstName = "Charlie" };

        await this.provider.CreateNode(alice);
        await this.provider.CreateNode(bob);
        await this.provider.CreateNode(charlie);

        var knows1 = new Knows<PersonWithNavigationProperty, PersonWithNavigationProperty> { Source = alice, Target = bob };
        var knows2 = new Knows<PersonWithNavigationProperty, PersonWithNavigationProperty> { Source = alice, Target = charlie };

        await this.provider.CreateRelationship(knows1);
        await this.provider.CreateRelationship(knows2);

        // Test projection with navigation properties (this is what we fixed earlier)
        // First, verify the data exists
        var aliceNode = this.provider.Nodes<PersonWithNavigationProperty>()
            .Where(p => p.FirstName == "Alice")
            .FirstOrDefault();

        Assert.NotNull(aliceNode);
        Assert.Equal("Alice", aliceNode.FirstName);

        // Now try simple projection without TraversalDepth
        var simpleProjection = this.provider.Nodes<PersonWithNavigationProperty>()
            .Where(p => p.FirstName == "Alice")
            .Select(p => new
            {
                Name = p.FirstName
            })
            .FirstOrDefault();

        Assert.NotNull(simpleProjection);
        Assert.NotNull(simpleProjection.Name);
        Assert.Equal("Alice", simpleProjection.Name);

        // Now test with TraversalDepth
        var projectedAlice = this.provider.Nodes<PersonWithNavigationProperty>(
            new GraphOperationOptions { TraversalDepth = 1 })
            .Where(p => p.FirstName == "Alice")
            .Select(p => new
            {
                Name = p.FirstName,
                FriendCount = p.Knows.Count,
                FriendNames = p.Knows.Select(k => k.Target!.FirstName)
            })
            .FirstOrDefault();

        Assert.NotNull(projectedAlice);
        Assert.NotNull(projectedAlice.Name);
        Assert.Equal("Alice", projectedAlice.Name);
        Assert.Equal(2, projectedAlice.FriendCount);
        Assert.Contains("Bob", projectedAlice.FriendNames);
        Assert.Contains("Charlie", projectedAlice.FriendNames);
    }

    [Fact]
    public async Task CanCombineNodeAndRelationshipQueries()
    {
        // Setup
        var alice = new Person { FirstName = "Alice" };
        var bob = new Person { FirstName = "Bob" };
        var charlie = new Person { FirstName = "Charlie" };

        await this.provider.CreateNode(alice);
        await this.provider.CreateNode(bob);
        await this.provider.CreateNode(charlie);

        await this.provider.CreateRelationship(new Knows<Person, Person>(alice, bob));
        await this.provider.CreateRelationship(new Knows<Person, Person>(bob, charlie));

        // Execute separate queries and combine in memory
        var people = this.provider.Nodes<Person>().ToDictionary(p => p.Id);
        var relationships = this.provider.Relationships<Knows<Person, Person>>().ToList();

        // Build a connection map
        var connectionMap = relationships
            .GroupBy(r => r.SourceId)
            .Select(g => new
            {
                PersonName = people[g.Key].FirstName,
                Connections = g.Select(r => people[r.TargetId].FirstName).ToList()
            })
            .ToList();

        Assert.Equal(2, connectionMap.Count);
        Assert.Contains(connectionMap, m => m.PersonName == "Alice" && m.Connections.Contains("Bob"));
        Assert.Contains(connectionMap, m => m.PersonName == "Bob" && m.Connections.Contains("Charlie"));
    }

    [Fact]
    public async Task CanProjectRelationshipCounts()
    {
        // Setup
        var alice = new Person { FirstName = "Alice" };
        var bob = new Person { FirstName = "Bob" };
        var charlie = new Person { FirstName = "Charlie" };
        var dave = new Person { FirstName = "Dave" };

        await this.provider.CreateNode(alice);
        await this.provider.CreateNode(bob);
        await this.provider.CreateNode(charlie);
        await this.provider.CreateNode(dave);

        // Alice knows everyone, Bob knows 2, Charlie knows 1, Dave knows none
        await this.provider.CreateRelationship(new Knows<Person, Person>(alice, bob));
        await this.provider.CreateRelationship(new Knows<Person, Person>(alice, charlie));
        await this.provider.CreateRelationship(new Knows<Person, Person>(alice, dave));
        await this.provider.CreateRelationship(new Knows<Person, Person>(bob, charlie));
        await this.provider.CreateRelationship(new Knows<Person, Person>(bob, dave));
        await this.provider.CreateRelationship(new Knows<Person, Person>(charlie, dave));

        // Get all relationships once
        var allRelationships = this.provider.Relationships<Knows<Person, Person>>().ToList();

        // Project connection counts
        var connectionStats = this.provider.Nodes<Person>()
            .ToList() // Execute the query
            .Select(p => new
            {
                Name = p.FirstName,
                OutgoingCount = allRelationships.Count(k => k.SourceId == p.Id),
                IncomingCount = allRelationships.Count(k => k.TargetId == p.Id),
                TotalConnections = allRelationships.Count(k => k.SourceId == p.Id || k.TargetId == p.Id)
            })
            .OrderByDescending(s => s.OutgoingCount)
            .ToList();

        // Assert
        Assert.Equal(4, connectionStats.Count);

        var aliceStats = connectionStats.First(s => s.Name == "Alice");
        Assert.Equal(3, aliceStats.OutgoingCount);
        Assert.Equal(0, aliceStats.IncomingCount);

        var daveStats = connectionStats.First(s => s.Name == "Dave");
        Assert.Equal(0, daveStats.OutgoingCount);
        Assert.Equal(3, daveStats.IncomingCount);
    }
}
