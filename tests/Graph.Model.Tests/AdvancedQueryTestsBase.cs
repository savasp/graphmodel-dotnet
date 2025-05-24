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

    [Fact]
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

        // Capture the reference time at the start of the test
        var referenceTime = DateTime.UtcNow;

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

        // DateTime.Now in Neo4j returns UTC, so compare with reference time
        Assert.True(Math.Abs((referenceTime - result.CurrentDateTime).TotalSeconds) < 10);

        // DateTime.Today should be today's date at midnight UTC
        Assert.Equal(referenceTime.Date, result.CurrentDate.Date);
        Assert.Equal(TimeSpan.Zero, result.CurrentDate.TimeOfDay);

        // DateTime.UtcNow should be close to reference time
        Assert.True(Math.Abs((referenceTime - result.CurrentUtc).TotalSeconds) < 10);

        // Year, Month, Day should match reference time (allowing for small time differences)
        Assert.True(Math.Abs(referenceTime.Year - result.Year) <= 1);
        Assert.True(Math.Abs(referenceTime.Month - result.Month) <= 1);
        Assert.True(Math.Abs(referenceTime.Day - result.Day) <= 1);
    }

    [Fact]
    public async Task CanProjectWithCollectionFunctions()
    {
        await this.provider.CreateNode(new Person { FirstName = "Eve", LastName = "Smith", Age = 25 });
        await this.provider.CreateNode(new Person { FirstName = "Alice", LastName = "Johnson", Age = 30 });

        var projected = this.provider.Nodes<Person>()
            .Select(p => new
            {
                Name = p.FirstName,
                // Map to Cypher size() function
                NameLength = p.FirstName.Length,
                // Map to Cypher substring() function  
                FirstChar = p.FirstName.Substring(0, 1),
                // Map to Cypher conditional (CASE expression)
                AgeCategory = p.Age >= 30 ? "Adult" : "Young",
                // Map to string concatenation
                FullName = p.FirstName + " " + p.LastName
            })
            .ToList();

        Assert.Equal(2, projected.Count);

        var eve = projected.First(p => p.Name == "Eve");
        Assert.Equal("Eve", eve.Name);
        Assert.Equal(3, eve.NameLength);
        Assert.Equal("E", eve.FirstChar);
        Assert.Equal("Young", eve.AgeCategory);
        Assert.Equal("Eve Smith", eve.FullName);

        var alice = projected.First(p => p.Name == "Alice");
        Assert.Equal("Alice", alice.Name);
        Assert.Equal(5, alice.NameLength);
        Assert.Equal("A", alice.FirstChar);
        Assert.Equal("Adult", alice.AgeCategory);
        Assert.Equal("Alice Johnson", alice.FullName);
    }

    [Fact]
    public async Task CanQueryWithPatternComprehension()
    {
        // Arrange: Create a social network with multiple levels of relationships
        var alice = new PersonWithNavigationProperty { FirstName = "Alice", Age = 30 };
        var bob = new PersonWithNavigationProperty { FirstName = "Bob", Age = 25 };
        var charlie = new PersonWithNavigationProperty { FirstName = "Charlie", Age = 35 };
        var diana = new PersonWithNavigationProperty { FirstName = "Diana", Age = 28 };
        var eve = new PersonWithNavigationProperty { FirstName = "Eve", Age = 32 };

        await this.provider.CreateNode(alice);
        await this.provider.CreateNode(bob);
        await this.provider.CreateNode(charlie);
        await this.provider.CreateNode(diana);
        await this.provider.CreateNode(eve);

        // Create a network: Alice knows Bob and Charlie, Bob knows Diana, Charlie knows Eve
        var knows1 = new Knows<PersonWithNavigationProperty, PersonWithNavigationProperty>
        {
            Source = alice,
            Target = bob,
            Since = DateTime.UtcNow.AddDays(-10)
        };
        var knows2 = new Knows<PersonWithNavigationProperty, PersonWithNavigationProperty>
        {
            Source = alice,
            Target = charlie,
            Since = DateTime.UtcNow.AddDays(-15)
        };
        var knows3 = new Knows<PersonWithNavigationProperty, PersonWithNavigationProperty>
        {
            Source = bob,
            Target = diana,
            Since = DateTime.UtcNow.AddDays(-5)
        };
        var knows4 = new Knows<PersonWithNavigationProperty, PersonWithNavigationProperty>
        {
            Source = charlie,
            Target = eve,
            Since = DateTime.UtcNow.AddDays(-8)
        };

        await this.provider.CreateRelationship(knows1);
        await this.provider.CreateRelationship(knows2);
        await this.provider.CreateRelationship(knows3);
        await this.provider.CreateRelationship(knows4);

        // Test 1: Pattern comprehension - get all friends with their details
        var friendsPattern = this.provider.Nodes<PersonWithNavigationProperty>(
            new GraphOperationOptions { TraversalDepth = 1 })
            .Where(p => p.FirstName == "Alice")
            .Select(p => new
            {
                PersonName = p.FirstName,
                FriendDetails = p.Knows.Select(k => new
                {
                    FriendName = k.Target!.FirstName,
                    FriendAge = k.Target.Age,
                    KnownSince = k.Since,
                    DaysKnown = (DateTime.UtcNow - k.Since).Days
                }).ToList()
            })
            .FirstOrDefault();

        Assert.NotNull(friendsPattern);
        Assert.Equal("Alice", friendsPattern.PersonName);
        Assert.Equal(2, friendsPattern.FriendDetails.Count);
        Assert.Contains(friendsPattern.FriendDetails, f => f.FriendName == "Bob" && f.FriendAge == 25);
        Assert.Contains(friendsPattern.FriendDetails, f => f.FriendName == "Charlie" && f.FriendAge == 35);

        // Test 2: Pattern comprehension with filtering - get only young friends
        var youngFriendsPattern = this.provider.Nodes<PersonWithNavigationProperty>(
            new GraphOperationOptions { TraversalDepth = 1 })
            .Where(p => p.FirstName == "Alice")
            .Select(p => new
            {
                PersonName = p.FirstName,
                YoungFriends = p.Knows
                    .Where(k => k.Target!.Age < 30)
                    .Select(k => k.Target!.FirstName)
                    .ToList(),
                YoungFriendCount = p.Knows.Count(k => k.Target!.Age < 30)
            })
            .FirstOrDefault();

        Assert.NotNull(youngFriendsPattern);
        Assert.Equal("Alice", youngFriendsPattern.PersonName);
        Assert.Single(youngFriendsPattern.YoungFriends);
        Assert.Contains("Bob", youngFriendsPattern.YoungFriends);
        Assert.Equal(1, youngFriendsPattern.YoungFriendCount);

        // Test 3: Complex pattern comprehension - aggregate friend data
        var friendAggregation = this.provider.Nodes<PersonWithNavigationProperty>(
            new GraphOperationOptions { TraversalDepth = 1 })
            .Where(p => p.FirstName == "Alice")
            .Select(p => new
            {
                PersonName = p.FirstName,
                FriendCount = p.Knows.Count,
                AverageFriendAge = p.Knows.Average(k => k.Target!.Age),
                OldestFriend = p.Knows.Max(k => k.Target!.Age),
                YoungestFriend = p.Knows.Min(k => k.Target!.Age),
                RecentFriendships = p.Knows
                    .Where(k => k.Since > DateTime.UtcNow.AddDays(-12))
                    .Select(k => k.Target!.FirstName)
                    .ToList()
            })
            .FirstOrDefault();

        Assert.NotNull(friendAggregation);
        Assert.Equal("Alice", friendAggregation.PersonName);
        Assert.Equal(2, friendAggregation.FriendCount);
        Assert.Equal(30.0, friendAggregation.AverageFriendAge); // (25 + 35) / 2
        Assert.Equal(35, friendAggregation.OldestFriend);
        Assert.Equal(25, friendAggregation.YoungestFriend);
        Assert.Single(friendAggregation.RecentFriendships);
        Assert.Contains("Bob", friendAggregation.RecentFriendships);

        // Test 4: Multi-level pattern comprehension - friends of friends
        var multiLevelPattern = this.provider.Nodes<PersonWithNavigationProperty>(
            new GraphOperationOptions { TraversalDepth = 2 })
            .Where(p => p.FirstName == "Alice")
            .Select(p => new
            {
                PersonName = p.FirstName,
                DirectFriends = p.Knows.Select(k => k.Target!.FirstName).ToList(),
                FriendsOfFriends = p.Knows
                    .SelectMany(k => k.Target!.Knows.Select(fof => fof.Target!.FirstName))
                    .Distinct()
                    .ToList(),
                SocialNetworkSize = p.Knows
                    .SelectMany(k => k.Target!.Knows.Select(fof => fof.Target!.FirstName))
                    .Distinct()
                    .Count()
            })
            .FirstOrDefault();

        Assert.NotNull(multiLevelPattern);
        Assert.Equal("Alice", multiLevelPattern.PersonName);
        Assert.Equal(2, multiLevelPattern.DirectFriends.Count);
        Assert.Contains("Bob", multiLevelPattern.DirectFriends);
        Assert.Contains("Charlie", multiLevelPattern.DirectFriends);

        // Friends of friends should include Diana (Bob's friend) and Eve (Charlie's friend)
        Assert.Equal(2, multiLevelPattern.FriendsOfFriends.Count);
        Assert.Contains("Diana", multiLevelPattern.FriendsOfFriends);
        Assert.Contains("Eve", multiLevelPattern.FriendsOfFriends);
        Assert.Equal(2, multiLevelPattern.SocialNetworkSize);

        // Test 5: Pattern comprehension with ordering and grouping
        var orderedFriendsPattern = this.provider.Nodes<PersonWithNavigationProperty>(
            new GraphOperationOptions { TraversalDepth = 1 })
            .Where(p => p.FirstName == "Alice")
            .Select(p => new
            {
                PersonName = p.FirstName,
                FriendsByAge = p.Knows
                    .OrderBy(k => k.Target!.Age)
                    .Select(k => new
                    {
                        Name = k.Target!.FirstName,
                        Age = k.Target.Age
                    })
                    .ToList(),
                AgeGroups = p.Knows
                    .GroupBy(k => k.Target!.Age >= 30 ? "Senior" : "Junior")
                    .Select(g => new
                    {
                        Group = g.Key,
                        Count = g.Count(),
                        Names = g.Select(k => k.Target!.FirstName).ToList()
                    })
                    .ToList()
            })
            .FirstOrDefault();

        Assert.NotNull(orderedFriendsPattern);
        Assert.Equal("Alice", orderedFriendsPattern.PersonName);

        // Friends should be ordered by age: Bob (25), Charlie (35)
        Assert.Equal(2, orderedFriendsPattern.FriendsByAge.Count);
        Assert.Equal("Bob", orderedFriendsPattern.FriendsByAge[0].Name);
        Assert.Equal(25, orderedFriendsPattern.FriendsByAge[0].Age);
        Assert.Equal("Charlie", orderedFriendsPattern.FriendsByAge[1].Name);
        Assert.Equal(35, orderedFriendsPattern.FriendsByAge[1].Age);

        // Age groups: Junior (Bob), Senior (Charlie)
        Assert.Equal(2, orderedFriendsPattern.AgeGroups.Count);
        var juniorGroup = orderedFriendsPattern.AgeGroups.First(g => g.Group == "Junior");
        var seniorGroup = orderedFriendsPattern.AgeGroups.First(g => g.Group == "Senior");

        Assert.Equal(1, juniorGroup.Count);
        Assert.Contains("Bob", juniorGroup.Names);
        Assert.Equal(1, seniorGroup.Count);
        Assert.Contains("Charlie", seniorGroup.Names);
    }

    [Fact]
    public async Task CanQueryWithFullTextSearch()
    {
        // Arrange: Create nodes with text content for full-text search
        var person1 = new Person { FirstName = "Alice", LastName = "Smith", Bio = "Software engineer passionate about artificial intelligence and machine learning" };
        var person2 = new Person { FirstName = "Bob", LastName = "Johnson", Bio = "Data scientist working on natural language processing and text analytics" };
        var person3 = new Person { FirstName = "Charlie", LastName = "Brown", Bio = "Product manager focused on user experience and interface design" };
        var person4 = new Person { FirstName = "Diana", LastName = "Wilson", Bio = "DevOps engineer specializing in cloud infrastructure and automation" };

        await this.provider.CreateNode(person1);
        await this.provider.CreateNode(person2);
        await this.provider.CreateNode(person3);
        await this.provider.CreateNode(person4);

        // Act & Assert: Test various full-text search scenarios

        // Test 1: Simple text contains search
        var engineerResults = this.provider.Nodes<Person>()
            .Where(p => p.Bio.Contains("engineer"))
            .ToList();

        // Let's also check all people in the database
        var allPeople = this.provider.Nodes<Person>().ToList();

        Assert.Equal(2, engineerResults.Count);
        Assert.Contains(engineerResults, p => p.FirstName == "Alice");
        Assert.Contains(engineerResults, p => p.FirstName == "Diana");

        // Test 2: Case-insensitive search
        var aiResults = this.provider.Nodes<Person>()
            .Where(p => p.Bio.ToLower().Contains("artificial intelligence"))
            .ToList();

        Assert.Single(aiResults);
        Assert.Equal("Alice", aiResults[0].FirstName);

        // Test 3: Multiple word search with AND logic
        var techResults = this.provider.Nodes<Person>()
            .Where(p => p.Bio.ToLower().Contains("data") && p.Bio.ToLower().Contains("scientist"))
            .ToList();

        Assert.Single(techResults);
        Assert.Equal("Bob", techResults[0].FirstName);

        // Test 4: Multiple word search with OR logic
        var designOrCloudResults = this.provider.Nodes<Person>()
            .Where(p => p.Bio.Contains("design") || p.Bio.Contains("cloud"))
            .ToList();

        Assert.Equal(2, designOrCloudResults.Count);
        Assert.Contains(designOrCloudResults, p => p.FirstName == "Charlie");
        Assert.Contains(designOrCloudResults, p => p.FirstName == "Diana");

        // Test 5: StartsWith and EndsWith for prefix/suffix matching
        var startsWithResults = this.provider.Nodes<Person>()
            .Where(p => p.Bio.StartsWith("Software"))
            .ToList();

        Assert.Single(startsWithResults);
        Assert.Equal("Alice", startsWithResults[0].FirstName);

        var endsWithResults = this.provider.Nodes<Person>()
            .Where(p => p.Bio.EndsWith("automation"))
            .ToList();

        Assert.Single(endsWithResults);
        Assert.Equal("Diana", endsWithResults[0].FirstName);

        // Test 6: Combine text search with other filters
        var filteredResults = this.provider.Nodes<Person>()
            .Where(p => p.Bio.Contains("engineer") && p.FirstName.StartsWith("A"))
            .ToList();

        Assert.Single(filteredResults);
        Assert.Equal("Alice", filteredResults[0].FirstName);

        // Test 7: Project results with text matching
        var projectedResults = this.provider.Nodes<Person>()
            .Where(p => p.Bio.ToLower().Contains("data") || p.Bio.ToLower().Contains("user"))
            .Select(p => new
            {
                Name = p.FirstName + " " + p.LastName,
                HasDataKeyword = p.Bio.ToLower().Contains("data"),
                HasUserKeyword = p.Bio.ToLower().Contains("user"),
                BioLength = p.Bio.Length
            })
            .ToList();

        Assert.Equal(2, projectedResults.Count);

        var bobResult = projectedResults.First(r => r.Name.StartsWith("Bob"));
        Assert.True(bobResult.HasDataKeyword);
        Assert.False(bobResult.HasUserKeyword);

        var charlieResult = projectedResults.First(r => r.Name.StartsWith("Charlie"));
        Assert.False(charlieResult.HasDataKeyword);
        Assert.True(charlieResult.HasUserKeyword);
    }

    [Fact(Skip = "Subqueries not yet implemented - requires Cypher CALL { } syntax support")]
    public Task CanQueryWithSubqueries()
    {
        // TODO: Implement when Neo4jExpressionVisitor supports generating CALL { } blocks
        // for complex nested queries that would benefit from subquery execution.
        //
        // Examples of queries that should generate subqueries:
        // 1. Cross-collection queries (nodes referencing relationships)
        // 2. Complex aggregations with multiple levels
        // 3. Conditional query execution based on node properties
        // 4. Performance optimizations for large graph traversals

        Assert.True(true); // Placeholder until implementation
        return Task.CompletedTask;
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
