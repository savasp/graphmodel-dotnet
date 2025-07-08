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

public interface IAdvancedQueryTests : IGraphModelTest
{
    [Fact]
    public async Task CanQueryWithMultipleConditions()
    {
        var p1 = new Person { FirstName = "Alice", LastName = "Smith" };
        var p2 = new Person { FirstName = "Bob", LastName = "Smith" };
        var p3 = new Person { FirstName = "Charlie", LastName = "Jones" };
        await this.Graph.CreateNodeAsync(p1, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateNodeAsync(p2, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateNodeAsync(p3, null, TestContext.Current.CancellationToken);

        var smiths = await this.Graph
            .Nodes<Person>()
            .Where(p => p.LastName == "Smith" && p.FirstName.StartsWith('A'))
            .ToListAsync(TestContext.Current.CancellationToken);
        Assert.Single(smiths);
        Assert.Equal("Alice", smiths[0].FirstName);
    }

    [Fact]
    public async Task CanQueryWithOrderByAndTake()
    {
        await this.Graph.CreateNodeAsync(new Person { FirstName = "Zed", LastName = "Alpha" }, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateNodeAsync(new Person { FirstName = "Ann", LastName = "Beta" }, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateNodeAsync(new Person { FirstName = "Bob", LastName = "Gamma" }, null, TestContext.Current.CancellationToken);

        var ordered = await this.Graph
            .Nodes<Person>()
            .OrderBy(p => p.FirstName).Take(2)
            .ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(2, ordered.Count);
        Assert.Equal("Ann", ordered[0].FirstName);
        Assert.Equal("Bob", ordered[1].FirstName);
    }

    [Fact]
    public async Task CanQueryWithProjection()
    {
        await this.Graph.CreateNodeAsync(new Person { FirstName = "Alice", LastName = "Smith" }, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateNodeAsync(new Person { FirstName = "Bob", LastName = "Smith" }, null, TestContext.Current.CancellationToken);
        var names = await this.Graph
            .Nodes<Person>()
            .Where(p => p.LastName == "Smith")
            .Select(p => p.FirstName)
            .ToListAsync(TestContext.Current.CancellationToken);
        Assert.Contains("Alice", names);
        Assert.Contains("Bob", names);
    }

    [Fact]
    public async Task CanQueryWithCount()
    {
        await this.Graph.CreateNodeAsync(new Person { FirstName = "Alice", LastName = "Smith" }, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateNodeAsync(new Person { FirstName = "Bob", LastName = "Smith" }, null, TestContext.Current.CancellationToken);
        var count = await this.Graph
            .Nodes<Person>()
            .CountAsync(TestContext.Current.CancellationToken);
        Assert.True(count >= 2);
    }

    [Fact]
    public async Task CanProjectToAnAnonymousType()
    {
        await this.Graph.CreateNodeAsync(new Person { FirstName = "Alice", LastName = "Smith" }, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateNodeAsync(new Person { FirstName = "Bob", LastName = "Smith" }, null, TestContext.Current.CancellationToken);
        var projected = await this.Graph.Nodes<Person>()
            .Where(p => p.LastName == "Smith")
            .Select(p => new { Name = p.FirstName })
            .ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(2, projected.Count);
        Assert.Contains(projected, p => p.Name == "Alice");
        Assert.Contains(projected, p => p.Name == "Bob");
    }

    [Fact]
    public async Task CanProjectToAnAnonymousTypeWithFunction()
    {
        await this.Graph.CreateNodeAsync(new Person { FirstName = "Alice", LastName = "Smith" }, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateNodeAsync(new Person { FirstName = "Bob", LastName = "Smith" }, null, TestContext.Current.CancellationToken);
        var projected = await this.Graph.Nodes<Person>()
            .Where(p => p.LastName == "Smith")
            .Select(p => new { Name = p.FirstName + " " + p.LastName })
            .ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(2, projected.Count);
        Assert.Contains(projected, p => p.Name == "Alice Smith");
        Assert.Contains(projected, p => p.Name == "Bob Smith");
    }

    [Fact]
    public async Task CanProjectWithAllSupportedFunctions()
    {
        // Arrange
        var person = new Person
        {
            FirstName = "John",
            LastName = "Doe",
            Age = 30,
            Bio = "Software Engineer"
        };
        await this.Graph.CreateNodeAsync(person, null, TestContext.Current.CancellationToken);

        // Act - test all supported functions in a single projection
        var projected = await this.Graph.Nodes<Person>()
            .Where(p => p.FirstName == "John")
            .Select(p => new
            {
                // String functions
                Upper = p.FirstName.ToUpper(),
                Lower = p.LastName.ToLower(),
                Trimmed = p.Bio.Trim(),
                Substring = p.Bio.Substring(0, 8),
                Replaced = p.FirstName.Replace("o", "0"),
                StartsWith = p.FirstName.StartsWith("J"),
                EndsWith = p.LastName.EndsWith("oe"),
                Contains = p.Bio.Contains("Engineer"),
                Length = p.Bio.Length,

                // Math functions
                AbsAge = Math.Abs(p.Age - 25),
                Ceiling = Math.Ceiling((double)p.Age / 7),
                Floor = Math.Floor((double)p.Age / 7),
                Round = Math.Round((double)p.Age / 7),
                Sqrt = Math.Sqrt(p.Age),
                Power = Math.Pow(2, 3),

                // DateTime functions
                Now = DateTime.Now,
                Today = DateTime.Today,
                UtcNow = DateTime.UtcNow,
                Year = DateTime.Now.Year,
                Month = DateTime.Now.Month,
                Day = DateTime.Now.Day
            })
            .ToListAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Single(projected);
        var result = projected[0];

        // String function results
        Assert.Equal("JOHN", result.Upper);
        Assert.Equal("doe", result.Lower);
        Assert.Equal("Software Engineer", result.Trimmed);
        Assert.Equal("Software", result.Substring);
        Assert.Equal("J0hn", result.Replaced);
        Assert.True(result.StartsWith);
        Assert.True(result.EndsWith);
        Assert.True(result.Contains);
        Assert.Equal(17, result.Length);

        // Math function results
        Assert.Equal(5, result.AbsAge);
        Assert.Equal(5.0, result.Ceiling);
        Assert.Equal(4.0, result.Floor);
        Assert.Equal(4.0, result.Round);
        Assert.Equal(Math.Sqrt(30), result.Sqrt);
        Assert.Equal(8.0, result.Power);

        // DateTime results (just check they're reasonable)
        Assert.True(result.Year >= 2020);
        Assert.True(result.Month >= 1 && result.Month <= 12);
        Assert.True(result.Day >= 1 && result.Day <= 31);
    }

    [Fact]
    public async Task CanNavigateRelationshipsInMemory()
    {
        var alice = new Person { FirstName = "Alice", LastName = "Smith" };
        await this.Graph.CreateNodeAsync(alice, null, TestContext.Current.CancellationToken);
        var bob = new Person { FirstName = "Bob", LastName = "Smith" };
        await this.Graph.CreateNodeAsync(bob, null, TestContext.Current.CancellationToken);
        var charlie = new Person { FirstName = "Charlie", LastName = "Jones" };
        await this.Graph.CreateNodeAsync(charlie, null, TestContext.Current.CancellationToken);
        var knows1 = new Knows { StartNodeId = alice.Id, EndNodeId = bob.Id, Since = DateTime.UtcNow };
        var knows2 = new Knows { StartNodeId = bob.Id, EndNodeId = charlie.Id, Since = DateTime.UtcNow };
        await this.Graph.CreateRelationshipAsync(knows1, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateRelationshipAsync(knows2, null, TestContext.Current.CancellationToken);

        // Fetch all people and relationships
        var people = await this.Graph
            .Nodes<Person>()
            .ToListAsync(TestContext.Current.CancellationToken);
        var relationships = await this.Graph
            .Relationships<Knows>()
            .ToListAsync(TestContext.Current.CancellationToken);

        // Find all people Bob knows (outgoing)
        var bobsFriends = relationships.Where(r => r.StartNodeId == bob.Id)
                              .Select(r => people.FirstOrDefault(p => p.Id == r.EndNodeId))
                              .Where(p => p != null)
                              .ToList();
        Assert.Single(bobsFriends);
        Assert.Equal("Charlie", bobsFriends[0]!.FirstName);

        // Find all people who know Bob (incoming)
        var knowsBob = relationships.Where(r => r.EndNodeId == bob.Id)
                           .Select(r => people.FirstOrDefault(p => p.Id == r.StartNodeId))
                           .Where(p => p != null)
                           .ToList();
        Assert.Single(knowsBob);
        Assert.Equal(alice.Id, knowsBob[0]!.Id);
    }

    [Fact]
    public async Task CanJoinNodesAndRelationshipsInMemory()
    {
        var alice = new Person { FirstName = "Alice", LastName = "Smith" };
        await this.Graph.CreateNodeAsync(alice, null, TestContext.Current.CancellationToken);
        var bob = new Person { FirstName = "Bob", LastName = "Smith" };
        await this.Graph.CreateNodeAsync(bob, null, TestContext.Current.CancellationToken);

        var knows = new Knows(alice, bob) { Since = DateTime.UtcNow };
        await this.Graph.CreateRelationshipAsync(knows, null, TestContext.Current.CancellationToken);

        var people = await this.Graph.Nodes<Person>().ToListAsync(TestContext.Current.CancellationToken);
        var rels = await this.Graph.Relationships<Knows>().ToListAsync(TestContext.Current.CancellationToken);

        // Join: Find all (person, friend) pairs
        var pairs = (from p in people
                     join k in rels on p.Id equals k.StartNodeId
                     join f in people on k.EndNodeId equals f.Id
                     select new { Person = p, Friend = f }).ToList();
        Assert.Single(pairs);
        Assert.Equal("Alice", pairs[0].Person.FirstName);
        Assert.Equal("Bob", pairs[0].Friend.FirstName);
    }

    [Fact]
    public async Task CanProjectWithStringFunctions()
    {
        await this.Graph.CreateNodeAsync(new Person { FirstName = "Alice", LastName = "Smith" }, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateNodeAsync(new Person { FirstName = "Bob", LastName = "Smith" }, null, TestContext.Current.CancellationToken);
        var projected = await this.Graph.Nodes<Person>()
            .Where(p => p.LastName == "Smith")
            .Select(p => new
            {
                Upper = p.FirstName.ToUpper(),
                Lower = p.LastName.ToLower(),
                Trimmed = ("  " + p.FirstName + "  ").Trim(),
                Sub = p.FirstName.Substring(0, 1),
                Replaced = p.LastName.Replace("Smith", "S.")
            })
            .ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(2, projected.Count);
        Assert.Contains(projected, p => p.Upper == "ALICE" && p.Lower == "smith" && p.Trimmed == "Alice" && p.Sub == "A" && p.Replaced == "S.");
        Assert.Contains(projected, p => p.Upper == "BOB" && p.Lower == "smith" && p.Trimmed == "Bob" && p.Sub == "B" && p.Replaced == "S.");
    }

    [Fact]
    public async Task CanProjectWithMathFunctions()
    {
        await this.Graph.CreateNodeAsync(new Person { FirstName = "Alice", LastName = "Smith", Age = 30 }, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateNodeAsync(new Person { FirstName = "Bob", LastName = "Smith", Age = 40 }, null, TestContext.Current.CancellationToken);
        var projected = await this.Graph.Nodes<Person>()
            .Where(p => p.LastName == "Smith")
            .Select(p => new
            {
                AgePlusTen = p.Age + 10,
                AgeTimesTwo = p.Age * 2,
                AgeDivTwo = p.Age / 2,
                AgeModTen = p.Age % 10,
                SqrtAge = Math.Sqrt(p.Age)
            })
            .ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(2, projected.Count);
        Assert.Contains(projected, p => p.AgePlusTen == 40 && p.AgeTimesTwo == 60 && p.AgeDivTwo == 15 && p.AgeModTen == 0 && Math.Abs(p.SqrtAge - Math.Sqrt(30)) < 0.01);
        Assert.Contains(projected, p => p.AgePlusTen == 50 && p.AgeTimesTwo == 80 && p.AgeDivTwo == 20 && p.AgeModTen == 0 && Math.Abs(p.SqrtAge - Math.Sqrt(40)) < 0.01);
    }

    [Fact]
    public async Task CanProjectWithNestedComputedProperties()
    {
        await this.Graph.CreateNodeAsync(new Person { FirstName = "Alice", LastName = "Smith", Age = 30 }, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateNodeAsync(new Person { FirstName = "Bob", LastName = "Smith", Age = 40 }, null, TestContext.Current.CancellationToken);
        var projected = await this.Graph.Nodes<Person>()
            .Where(p => p.LastName == "Smith")
            .Select(p => new
            {
                Complex = p.FirstName.ToUpper() + "-" + p.LastName.ToLower() + "-" + (p.Age + 1),
                Logic = p.Age > 35 ? "Senior" : "Junior"
            })
            .ToListAsync(TestContext.Current.CancellationToken);
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
        await this.Graph.CreateNodeAsync(alice, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateNodeAsync(bob, null, TestContext.Current.CancellationToken);
        var knows = new Knows(alice, bob) { Since = DateTime.UtcNow };
        await this.Graph.CreateRelationshipAsync(knows, null, TestContext.Current.CancellationToken);

        var rels = await this.Graph
            .Relationships<Knows>(null)
            .Where(r => r.StartNodeId == alice.Id)
            .Select(r => r.Since).ToListAsync(TestContext.Current.CancellationToken);
        Assert.Single(rels);
        Assert.True(rels[0] > DateTime.MinValue);

        rels = await this.Graph.Relationships<Knows>()
            .Where(r => alice.Id == r.StartNodeId)
            .Select(r => r.Since).ToListAsync(TestContext.Current.CancellationToken);
        Assert.Single(rels);
        Assert.True(rels[0] > DateTime.MinValue);
    }

    [Fact]
    public async Task CanOrderAndProjectRelationships()
    {
        var alice = new Person { FirstName = "Alice" };
        var bob = new Person { FirstName = "Bob" };
        var charlie = new Person { FirstName = "Charlie" };
        await this.Graph.CreateNodeAsync(alice, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateNodeAsync(bob, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateNodeAsync(charlie, null, TestContext.Current.CancellationToken);
        var r1 = new Knows(alice, bob) { Since = DateTime.UtcNow.AddDays(-2) };
        var r2 = new Knows(alice, charlie) { Since = DateTime.UtcNow.AddDays(-1) };
        await this.Graph.CreateRelationshipAsync(r1, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateRelationshipAsync(r2, null, TestContext.Current.CancellationToken);

        var rels = await this.Graph.Relationships<Knows>().ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(2, rels.Count);

        var ordered = await this.Graph.Relationships<Knows>().OrderBy(r => r.Since).ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(2, ordered.Count);
        Assert.True(ordered[0].Since < ordered[1].Since);
    }

    [Fact]
    public async Task CanQueryWithMultipleOrderByAndThenBy()
    {
        await this.Graph.CreateNodeAsync(new Person { FirstName = "Ann", LastName = "Smith" }, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateNodeAsync(new Person { FirstName = "Ann", LastName = "Brown" }, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateNodeAsync(new Person { FirstName = "Bob", LastName = "Smith" }, null, TestContext.Current.CancellationToken);
        var ordered = await this.Graph.Nodes<Person>().OrderBy(p => p.FirstName).ThenBy(p => p.LastName).ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(3, ordered.Count);
        Assert.Equal("Ann", ordered[0].FirstName);
        Assert.Equal("Brown", ordered[0].LastName);
        Assert.Equal("Ann", ordered[1].FirstName);
        Assert.Equal("Smith", ordered[1].LastName);
        Assert.Equal("Bob", ordered[2].FirstName);
        Assert.Equal("Smith", ordered[2].LastName);
    }

    [Fact]
    public async Task CanQueryWithDistinctAndSkip()
    {
        await this.Graph.CreateNodeAsync(new Person { FirstName = "Ann", LastName = "Smith" }, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateNodeAsync(new Person { FirstName = "Ann", LastName = "Brown" }, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateNodeAsync(new Person { FirstName = "Bob", LastName = "Smith" }, null, TestContext.Current.CancellationToken);
        var names = await this.Graph.Nodes<Person>().Select(p => p.FirstName).Distinct().Skip(1).ToListAsync(TestContext.Current.CancellationToken);
        Assert.Single(names);
        Assert.Equal("Bob", names[0]);
    }

    [Fact]
    public async Task CanQueryWithAnyAllFirstSingleLast()
    {
        await this.Graph.CreateNodeAsync(new Person { FirstName = "Ann", LastName = "Smith" }, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateNodeAsync(new Person { FirstName = "Bob", LastName = "Smith" }, null, TestContext.Current.CancellationToken);
        var anyAnn = await this.Graph.Nodes<Person>().AnyAsync(TestContext.Current.CancellationToken);
        var allSmith = await this.Graph.Nodes<Person>().AllAsync(p => p.LastName == "Smith", TestContext.Current.CancellationToken);
        var first = await this.Graph.Nodes<Person>().OrderBy(p => p.FirstName).FirstAsync(TestContext.Current.CancellationToken);
        var single = await this.Graph.Nodes<Person>().Where(p => p.FirstName == "Ann").SingleAsync(TestContext.Current.CancellationToken);
        var last = await this.Graph.Nodes<Person>().OrderBy(p => p.FirstName).LastAsync(TestContext.Current.CancellationToken);
        Assert.True(anyAnn);
        Assert.True(allSmith);
        Assert.Equal("Ann", first.FirstName);
        Assert.Equal("Ann", single.FirstName);
        Assert.Equal("Bob", last.FirstName);
    }

    [Fact]
    public async Task CanQueryWithStringAndMathFunctionsAdvanced()
    {
        await this.Graph.CreateNodeAsync(new Person { FirstName = "Eve", LastName = "Smith", Age = 25 }, null, TestContext.Current.CancellationToken);
        var projected = await this.Graph.Nodes<Person>()
            .Select(p => new
            {
                Len = p.FirstName.Length,
                Contains = p.LastName.Contains("mi"),
                Abs = Math.Abs(p.Age - 30),
            })
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Single(projected);
        Assert.Equal(3, projected[0].Len);
        Assert.True(projected[0].Contains);
        Assert.Equal(5, projected[0].Abs);
    }

    [Fact]
    public async Task CanQueryWithGroupByAndAggregate()
    {
        await this.Graph.CreateNodeAsync(new Person { FirstName = "Ann", LastName = "Smith" }, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateNodeAsync(new Person { FirstName = "Bob", LastName = "Smith" }, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateNodeAsync(new Person { FirstName = "Ann", LastName = "Brown" }, null, TestContext.Current.CancellationToken);
        var grouped = await this.Graph.Nodes<Person>()
            .GroupBy(p => p.FirstName)
            .Select(g => new { Name = g.Key, Count = g.Count() })
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Contains(grouped, g => g.Name == "Ann" && g.Count == 2);
        Assert.Contains(grouped, g => g.Name == "Bob" && g.Count == 1);
    }

    [Fact]
    public async Task CanProjectWithDateTimeFunctions()
    {
        // Arrange
        var eve = new Person { FirstName = "Eve", LastName = "Smith", Age = 25 };
        await this.Graph.CreateNodeAsync(eve, null, TestContext.Current.CancellationToken);

        // Capture reference time in UTC BEFORE the query
        var referenceTime = DateTime.UtcNow;

        // Act
        var projected = await this.Graph.Nodes<Person>()
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
            .ToListAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Single(projected);
        var result = projected[0];

        // Verify person name to ensure we got the right record
        Assert.Equal("Eve", result.PersonName);

        // DateTime.Now in Neo4j maps to localdatetime() - this returns local time
        var localReferenceTime = referenceTime.ToLocalTime();
        var actual = result.CurrentDateTime.ToLocalTime();
        Assert.True(Math.Abs((localReferenceTime - actual).TotalSeconds) < 120,
            $"CurrentDateTime difference too large: reference={localReferenceTime:yyyy-MM-dd HH:mm:ss}, actual={actual:yyyy-MM-dd HH:mm:ss}");

        // DateTime.Today should be today's date at midnight (local time)
        // Allow for timezone differences between test machine and Neo4j server
        var expectedDate = localReferenceTime.Date;
        var actualDate = result.CurrentDate.Date;
        var dateDifference = Math.Abs((expectedDate - actualDate).TotalDays);
        Assert.True(dateDifference <= 1,
            $"Date difference too large: expected={expectedDate:yyyy-MM-dd}, actual={actualDate:yyyy-MM-dd}, difference={dateDifference} days");
        Assert.Equal(TimeSpan.Zero, result.CurrentDate.TimeOfDay);

        // DateTime.UtcNow should be close to our UTC reference time
        actual = result.CurrentUtc;
        Assert.True(Math.Abs((referenceTime - actual).TotalSeconds) < 120,
            $"CurrentUtc difference too large: reference={referenceTime}, actual={actual}");

        // Year, Month, Day should match local reference time
        var localRef = referenceTime.ToLocalTime();
        Assert.True(Math.Abs(localRef.Year - result.Year) <= 1);
        Assert.True(Math.Abs(localRef.Month - result.Month) <= 1);
        Assert.True(Math.Abs(localRef.Day - result.Day) <= 1);
    }

    [Fact]
    public async Task CanProjectWithCollectionFunctions()
    {
        await this.Graph.CreateNodeAsync(new Person { FirstName = "Eve", LastName = "Smith", Age = 25 }, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateNodeAsync(new Person { FirstName = "Alice", LastName = "Johnson", Age = 30 }, null, TestContext.Current.CancellationToken);

        var projected = await this.Graph.Nodes<Person>()
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
            .ToListAsync(TestContext.Current.CancellationToken);

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

    [Fact(Skip = "Pattern comprehensions with nested collections not yet implemented")]
    public async Task CanQueryWithBasicPatternComprehension()
    {
        // Arrange: Create a simple social network
        var alice = new Person { FirstName = "Alice", Age = 30 };
        var bob = new Person { FirstName = "Bob", Age = 25 };
        var charlie = new Person { FirstName = "Charlie", Age = 35 };

        await this.Graph.CreateNodeAsync(alice, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateNodeAsync(bob, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateNodeAsync(charlie, null, TestContext.Current.CancellationToken);

        var knows1 = new Knows(alice, bob) { Since = DateTime.UtcNow.AddDays(-10) };
        var knows2 = new Knows(alice, charlie) { Since = DateTime.UtcNow.AddDays(-15) };

        await this.Graph.CreateRelationshipAsync(knows1, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateRelationshipAsync(knows2, null, TestContext.Current.CancellationToken);

        // Act: Get all friends with their details
        var friendsPattern = await this.Graph.Nodes<Person>()
            .Where(p => p.FirstName == "Alice")
            .PathSegments<Person, Knows, Person>()
            .GroupBy(p => p.StartNode)
            .Select(group => new
            {
                PersonName = group.Key.FirstName,
                FriendDetails = group.Select(p => new
                {
                    FriendName = p.EndNode.FirstName,
                    FriendAge = p.EndNode.Age,
                    KnownSince = p.Relationship.Since,
                    DaysKnown = (DateTime.UtcNow - p.Relationship.Since).Days
                }).ToList()
            })
            .FirstOrDefaultAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(friendsPattern);
        Assert.Equal("Alice", friendsPattern.PersonName);
        Assert.Equal(2, friendsPattern.FriendDetails.Count);
        Assert.Contains(friendsPattern.FriendDetails, f => f.FriendName == "Bob" && f.FriendAge == 25);
        Assert.Contains(friendsPattern.FriendDetails, f => f.FriendName == "Charlie" && f.FriendAge == 35);
    }

    [Fact(Skip = "Pattern comprehensions with nested collections not yet implemented")]
    public async Task CanQueryWithFilteredPatternComprehension()
    {
        // Arrange
        var alice = new Person { FirstName = "Alice", Age = 30 };
        var bob = new Person { FirstName = "Bob", Age = 25 };
        var charlie = new Person { FirstName = "Charlie", Age = 35 };

        await this.Graph.CreateNodeAsync(alice, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateNodeAsync(bob, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateNodeAsync(charlie, null, TestContext.Current.CancellationToken);

        await this.Graph.CreateRelationshipAsync(new Knows(alice, bob) { Since = DateTime.UtcNow.AddDays(-10) }, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateRelationshipAsync(new Knows(alice, charlie) { Since = DateTime.UtcNow.AddDays(-15) }, null, TestContext.Current.CancellationToken);

        // Act: Get only young friends (under 30)
        var youngFriendsPattern = await this.Graph.Nodes<Person>()
            .Where(p => p.FirstName == "Alice")
            .PathSegments<Person, Knows, Person>()
            .Where(path => path.EndNode.Age < 30)
            .GroupBy(ks => ks.StartNode)
            .Select(group => new
            {
                PersonName = group.Key.FirstName,
                YoungFriends = group.Select(g => g.EndNode.FirstName).ToList(),
                YoungFriendCount = group.Count()
            })
            .FirstOrDefaultAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(youngFriendsPattern);
        Assert.Equal("Alice", youngFriendsPattern.PersonName);
        Assert.Single(youngFriendsPattern.YoungFriends);
        Assert.Contains("Bob", youngFriendsPattern.YoungFriends);
        Assert.Equal(1, youngFriendsPattern.YoungFriendCount);
    }

    [Fact(Skip = "Pattern comprehensions with nested collections not yet implemented")]
    public async Task CanQueryWithAggregatedPatternComprehension()
    {
        // Arrange
        var alice = new Person { FirstName = "Alice", Age = 30 };
        var bob = new Person { FirstName = "Bob", Age = 25 };
        var charlie = new Person { FirstName = "Charlie", Age = 35 };

        await this.Graph.CreateNodeAsync(alice, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateNodeAsync(bob, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateNodeAsync(charlie, null, TestContext.Current.CancellationToken);

        await this.Graph.CreateRelationshipAsync(new Knows(alice, bob) { Since = DateTime.UtcNow.AddDays(-10) }, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateRelationshipAsync(new Knows(alice, charlie) { Since = DateTime.UtcNow.AddDays(-15) }, null, TestContext.Current.CancellationToken);

        // Act: Aggregate friend data
        var friendAggregation = await this.Graph.Nodes<Person>()
            .Where(p => p.FirstName == "Alice")
            .PathSegments<Person, Knows, Person>()
            .GroupBy(ks => ks.StartNode)
            .Select(group => new
            {
                PersonName = group.Key.FirstName,
                FriendCount = group.Count(),
                AverageFriendAge = group.Average(k => k.EndNode.Age),
                OldestFriend = group.Max(k => k.EndNode.Age),
                YoungestFriend = group.Min(k => k.EndNode.Age)
            })
            .FirstOrDefaultAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(friendAggregation);
        Assert.Equal("Alice", friendAggregation.PersonName);
        Assert.Equal(2, friendAggregation.FriendCount);
        Assert.Equal(30.0, friendAggregation.AverageFriendAge); // (25 + 35) / 2
        Assert.Equal(35, friendAggregation.OldestFriend);
        Assert.Equal(25, friendAggregation.YoungestFriend);
    }

    [Fact(Skip = "Pattern comprehensions with nested collections not yet implemented")]
    public async Task CanQueryWithTimeBasedPatternComprehension()
    {
        // Arrange
        var alice = new Person { FirstName = "Alice", Age = 30 };
        var bob = new Person { FirstName = "Bob", Age = 25 };
        var charlie = new Person { FirstName = "Charlie", Age = 35 };

        await this.Graph.CreateNodeAsync(alice, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateNodeAsync(bob, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateNodeAsync(charlie, null, TestContext.Current.CancellationToken);

        // Bob is a recent friend, Charlie is an old friend
        await this.Graph.CreateRelationshipAsync(new Knows(alice, bob) { Since = DateTime.UtcNow.AddDays(-5) }, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateRelationshipAsync(new Knows(alice, charlie) { Since = DateTime.UtcNow.AddDays(-20) }, null, TestContext.Current.CancellationToken);

        // Act: Get recent friendships (within last 12 days)
        var recentFriendships = await this.Graph.Nodes<Person>()
            .Where(p => p.FirstName == "Alice")
            .PathSegments<Person, Knows, Person>()
            .GroupBy(ks => ks.StartNode)
            .Select(group => new
            {
                PersonName = group.Key.FirstName,
                RecentFriends = group
                    .Where(k => k.Relationship.Since > DateTime.UtcNow.AddDays(-12))
                    .Select(k => k.EndNode.FirstName)
                    .ToList()
            })
            .FirstOrDefaultAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(recentFriendships);
        Assert.Equal("Alice", recentFriendships.PersonName);
        Assert.Single(recentFriendships.RecentFriends);
        Assert.Contains("Bob", recentFriendships.RecentFriends);
    }

    [Fact(Skip = "Too complex for now")]
    public async Task CanQueryWithOrderedPatternComprehension()
    {
        // Arrange
        var alice = new Person { FirstName = "Alice", Age = 30 };
        var bob = new Person { FirstName = "Bob", Age = 25 };
        var charlie = new Person { FirstName = "Charlie", Age = 35 };

        await this.Graph.CreateNodeAsync(alice, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateNodeAsync(bob, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateNodeAsync(charlie, null, TestContext.Current.CancellationToken);

        await this.Graph.CreateRelationshipAsync(new Knows(alice, bob), null, TestContext.Current.CancellationToken);
        await this.Graph.CreateRelationshipAsync(new Knows(alice, charlie), null, TestContext.Current.CancellationToken);

        // Act: Get friends ordered by age
        var orderedFriendsPattern = await this.Graph.Nodes<Person>()
            .Where(p => p.FirstName == "Alice")
            .PathSegments<Person, Knows, Person>()
            .GroupBy(ks => ks.StartNode)
            .Select(group => new
            {
                PersonName = group.Key.FirstName,
                FriendsByAge = group
                    .OrderBy(k => k.EndNode.Age)
                    .Select(k => new
                    {
                        Name = k.EndNode.FirstName,
                        Age = k.EndNode.Age
                    })
                    .ToList()
            })
            .FirstOrDefaultAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(orderedFriendsPattern);
        Assert.Equal("Alice", orderedFriendsPattern.PersonName);
        Assert.Equal(2, orderedFriendsPattern.FriendsByAge.Count);

        // Friends should be ordered by age: Bob (25), Charlie (35)
        Assert.Equal("Bob", orderedFriendsPattern.FriendsByAge[0].Name);
        Assert.Equal(25, orderedFriendsPattern.FriendsByAge[0].Age);
        Assert.Equal("Charlie", orderedFriendsPattern.FriendsByAge[1].Name);
        Assert.Equal(35, orderedFriendsPattern.FriendsByAge[1].Age);
    }

    [Fact(Skip = "Too complex for now")]
    public async Task CanQueryWithGroupedPatternComprehension()
    {
        // Arrange
        var alice = new Person { FirstName = "Alice", Age = 30 };
        var bob = new Person { FirstName = "Bob", Age = 25 };
        var charlie = new Person { FirstName = "Charlie", Age = 35 };
        var diana = new Person { FirstName = "Diana", Age = 28 };

        await this.Graph.CreateNodeAsync(alice, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateNodeAsync(bob, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateNodeAsync(charlie, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateNodeAsync(diana, null, TestContext.Current.CancellationToken);

        await this.Graph.CreateRelationshipAsync(new Knows(alice, bob), null, TestContext.Current.CancellationToken);
        await this.Graph.CreateRelationshipAsync(new Knows(alice, charlie), null, TestContext.Current.CancellationToken);
        await this.Graph.CreateRelationshipAsync(new Knows(alice, diana), null, TestContext.Current.CancellationToken);

        // Act: Group friends by age category
        var ageGroupedPattern = await this.Graph.Nodes<Person>()
            .Where(p => p.FirstName == "Alice")
            .PathSegments<Person, Knows, Person>()
            .GroupBy(ks => ks.StartNode)
            .Select(group => new
            {
                PersonName = group.Key.FirstName,
                AgeGroups = group
                    .GroupBy(k => k.EndNode.Age >= 30 ? "Senior" : "Junior")
                    .Select(g => new
                    {
                        Group = g.Key,
                        Count = g.Count(),
                        Names = g.Select(k => k.EndNode.FirstName).ToList()
                    })
                    .ToList()
            })
            .FirstOrDefaultAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(ageGroupedPattern);
        Assert.Equal("Alice", ageGroupedPattern.PersonName);
        Assert.Equal(2, ageGroupedPattern.AgeGroups.Count);

        var juniorGroup = ageGroupedPattern.AgeGroups.First(g => g.Group == "Junior");
        var seniorGroup = ageGroupedPattern.AgeGroups.First(g => g.Group == "Senior");

        Assert.Equal(2, juniorGroup.Count); // Bob (25), Diana (28)
        Assert.Contains("Bob", juniorGroup.Names);
        Assert.Contains("Diana", juniorGroup.Names);

        Assert.Equal(1, seniorGroup.Count); // Charlie (35)
        Assert.Contains("Charlie", seniorGroup.Names);
    }

    [Fact]
    public async Task CanQueryWithFullTextSearch_SimpleContains()
    {
        // Arrange: Create nodes with text content for full-text search
        var person1 = new Person { FirstName = "Alice", LastName = "Smith", Bio = "Software engineer passionate about artificial intelligence and machine learning" };
        var person2 = new Person { FirstName = "Bob", LastName = "Johnson", Bio = "Data scientist working on natural language processing and text analytics" };
        var person3 = new Person { FirstName = "Charlie", LastName = "Brown", Bio = "Product manager focused on user experience and interface design" };
        var person4 = new Person { FirstName = "Diana", LastName = "Wilson", Bio = "DevOps engineer specializing in cloud infrastructure and automation" };

        await this.Graph.CreateNodeAsync(person1, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateNodeAsync(person2, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateNodeAsync(person3, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateNodeAsync(person4, null, TestContext.Current.CancellationToken);

        // Simple text contains search
        var engineerResults = await this.Graph.Nodes<Person>()
            .Where(p => p.Bio.Contains("engineer"))
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, engineerResults.Count);
        Assert.Contains(engineerResults, p => p.FirstName == "Alice");
        Assert.Contains(engineerResults, p => p.FirstName == "Diana");
    }

    [Fact]
    public async Task CanQueryWithFullTextSearch_CaseInsensitive()
    {
        // Arrange: Create nodes with text content for full-text search
        var person1 = new Person { FirstName = "Alice", LastName = "Smith", Bio = "Software engineer passionate about artificial intelligence and machine learning" };
        var person2 = new Person { FirstName = "Bob", LastName = "Johnson", Bio = "Data scientist working on natural language processing and text analytics" };
        var person3 = new Person { FirstName = "Charlie", LastName = "Brown", Bio = "Product manager focused on user experience and interface design" };
        var person4 = new Person { FirstName = "Diana", LastName = "Wilson", Bio = "DevOps engineer specializing in cloud infrastructure and automation" };

        await this.Graph.CreateNodeAsync(person1, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateNodeAsync(person2, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateNodeAsync(person3, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateNodeAsync(person4, null, TestContext.Current.CancellationToken);

        // Case-insensitive search
        var aiResults = await this.Graph.Nodes<Person>()
            .Where(p => p.Bio.ToLower().Contains("artificial intelligence"))
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Single(aiResults);
        Assert.Equal("Alice", aiResults[0].FirstName);
    }

    [Fact]
    public async Task CanQueryWithFullTextSearch_MultipleWords()
    {
        // Arrange: Create nodes with text content for full-text search
        var person1 = new Person { FirstName = "Alice", LastName = "Smith", Bio = "Software engineer passionate about artificial intelligence and machine learning" };
        var person2 = new Person { FirstName = "Bob", LastName = "Johnson", Bio = "Data scientist working on natural language processing and text analytics" };
        var person3 = new Person { FirstName = "Charlie", LastName = "Brown", Bio = "Product manager focused on user experience and interface design" };
        var person4 = new Person { FirstName = "Diana", LastName = "Wilson", Bio = "DevOps engineer specializing in cloud infrastructure and automation" };

        await this.Graph.CreateNodeAsync(person1, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateNodeAsync(person2, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateNodeAsync(person3, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateNodeAsync(person4, null, TestContext.Current.CancellationToken);

        // Multiple word search with AND logic
        var techResults = await this.Graph.Nodes<Person>()
            .Where(p => p.Bio.ToLower().Contains("data") && p.Bio.ToLower().Contains("scientist"))
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Single(techResults);
        Assert.Equal("Bob", techResults[0].FirstName);
    }

    [Fact]
    public async Task CanQueryWithFullTextSearch_StartsWith()
    {
        // Arrange: Create nodes with text content for full-text search
        var person1 = new Person { FirstName = "Alice", LastName = "Smith", Bio = "Software engineer passionate about artificial intelligence and machine learning" };
        var person2 = new Person { FirstName = "Bob", LastName = "Johnson", Bio = "Data scientist working on natural language processing and text analytics" };
        var person3 = new Person { FirstName = "Charlie", LastName = "Brown", Bio = "Product manager focused on user experience and interface design" };
        var person4 = new Person { FirstName = "Diana", LastName = "Wilson", Bio = "DevOps engineer specializing in cloud infrastructure and automation" };

        await this.Graph.CreateNodeAsync(person1, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateNodeAsync(person2, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateNodeAsync(person3, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateNodeAsync(person4, null, TestContext.Current.CancellationToken);

        // StartsWith for prefix/suffix matching
        var startsWithResults = await this.Graph.Nodes<Person>()
            .Where(p => p.Bio.StartsWith("Software"))
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Single(startsWithResults);
        Assert.Equal("Alice", startsWithResults[0].FirstName);
    }

    [Fact]
    public async Task CanQueryWithFullTextSearch_EndsWith()
    {
        // Arrange: Create nodes with text content for full-text search
        var person1 = new Person { FirstName = "Alice", LastName = "Smith", Bio = "Software engineer passionate about artificial intelligence and machine learning" };
        var person2 = new Person { FirstName = "Bob", LastName = "Johnson", Bio = "Data scientist working on natural language processing and text analytics" };
        var person3 = new Person { FirstName = "Charlie", LastName = "Brown", Bio = "Product manager focused on user experience and interface design" };
        var person4 = new Person { FirstName = "Diana", LastName = "Wilson", Bio = "DevOps engineer specializing in cloud infrastructure and automation" };

        await this.Graph.CreateNodeAsync(person1, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateNodeAsync(person2, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateNodeAsync(person3, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateNodeAsync(person4, null, TestContext.Current.CancellationToken);

        // EndsWith for prefix/suffix matching
        var endsWithResults = await this.Graph.Nodes<Person>()
            .Where(p => p.Bio.EndsWith("automation"))
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Single(endsWithResults);
        Assert.Equal("Diana", endsWithResults[0].FirstName);
    }

    [Fact]
    public async Task CanQueryWithFullTextSearch_WithOtherFilters()
    {
        // Arrange: Create nodes with text content for full-text search
        var person1 = new Person { FirstName = "Alice", LastName = "Smith", Bio = "Software engineer passionate about artificial intelligence and machine learning" };
        var person2 = new Person { FirstName = "Bob", LastName = "Johnson", Bio = "Data scientist working on natural language processing and text analytics" };
        var person3 = new Person { FirstName = "Charlie", LastName = "Brown", Bio = "Product manager focused on user experience and interface design" };
        var person4 = new Person { FirstName = "Diana", LastName = "Wilson", Bio = "DevOps engineer specializing in cloud infrastructure and automation" };

        await this.Graph.CreateNodeAsync(person1, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateNodeAsync(person2, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateNodeAsync(person3, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateNodeAsync(person4, null, TestContext.Current.CancellationToken);

        // Combine text search with other filters
        var filteredResults = await this.Graph.Nodes<Person>()
            .Where(p => p.Bio.Contains("engineer") && p.FirstName.StartsWith("A"))
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Single(filteredResults);
        Assert.Equal("Alice", filteredResults[0].FirstName);
    }

    [Fact]
    public async Task CanQueryWithFullTextSearch_Project()
    {
        // Arrange: Create nodes with text content for full-text search
        var person1 = new Person { FirstName = "Alice", LastName = "Smith", Bio = "Software engineer passionate about artificial intelligence and machine learning" };
        var person2 = new Person { FirstName = "Bob", LastName = "Johnson", Bio = "Data scientist working on natural language processing and text analytics" };
        var person3 = new Person { FirstName = "Charlie", LastName = "Brown", Bio = "Product manager focused on user experience and interface design" };
        var person4 = new Person { FirstName = "Diana", LastName = "Wilson", Bio = "DevOps engineer specializing in cloud infrastructure and automation" };

        await this.Graph.CreateNodeAsync(person1, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateNodeAsync(person2, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateNodeAsync(person3, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateNodeAsync(person4, null, TestContext.Current.CancellationToken);

        // Project results with text matching
        var projectedResults = await this.Graph.Nodes<Person>()
            .Where(p => p.Bio.ToLower().Contains("data") || p.Bio.ToLower().Contains("user"))
            .Select(p => new
            {
                Name = p.FirstName + " " + p.LastName,
                HasDataKeyword = p.Bio.ToLower().Contains("data"),
                HasUserKeyword = p.Bio.ToLower().Contains("user"),
                BioLength = p.Bio.Length
            })
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, projectedResults.Count);

        var bobResult = projectedResults.First(r => r.Name.StartsWith("Bob"));
        Assert.True(bobResult.HasDataKeyword);
        Assert.False(bobResult.HasUserKeyword);

        var charlieResult = projectedResults.First(r => r.Name.StartsWith("Charlie"));
        Assert.False(charlieResult.HasDataKeyword);
        Assert.True(charlieResult.HasUserKeyword);
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

        await this.Graph.CreateNodeAsync(alice, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateNodeAsync(bob, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateNodeAsync(charlie, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateNodeAsync(david, null, TestContext.Current.CancellationToken);

        var knows1 = new Knows(alice, bob) { Since = DateTime.UtcNow.AddDays(-30) };
        var knows2 = new Knows(bob, charlie) { Since = DateTime.UtcNow.AddDays(-20) };
        var knows3 = new Knows(charlie, david) { Since = DateTime.UtcNow.AddDays(-10) };

        await this.Graph.CreateRelationshipAsync(knows1, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateRelationshipAsync(knows2, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateRelationshipAsync(knows3, null, TestContext.Current.CancellationToken);

        // Act & Assert: Test navigation at different levels

        // Test 1: Simple relationship query - who does Alice know?
        var aliceKnows = await this.Graph.Relationships<Knows>()
            .Where(k => k.StartNodeId == alice.Id)
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Single(aliceKnows);
        Assert.Equal(bob.Id, aliceKnows[0].EndNodeId);

        // Test 2: Get all people and relationships
        var allPeople = this.Graph.Nodes<Person>();
        var allKnows = this.Graph.Relationships<Knows>();

        // Find Bob's friends
        var bobsFriends = await allKnows
            .Where(k => k.StartNodeId == bob.Id)
            .Join(allPeople, k => k.EndNodeId, p => p.Id, (k, p) => p)
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Single(bobsFriends);
        Assert.Equal("Charlie", bobsFriends[0].FirstName);

        // Test 3: Find friends of friends
        // TODO: SelectMany with nested queries is not yet implemented - would require advanced Cypher generation
        // "Friends of friend" can be easily implemented with a simple graph traversal instead of Join() and SelectMany().
        // For now, implement this test using simple graph traversal instead

        // Get Alice's direct friends first
        var aliceDirectFriends = await allKnows
            .Where(k => k.StartNodeId == alice.Id)
            .Join(allPeople, k => k.EndNodeId, p => p.Id, (k, p) => p)
            .ToListAsync(TestContext.Current.CancellationToken);

        // Then get the friends of those friends
        var friendOfFriendIds = new List<string>();
        foreach (var friend in aliceDirectFriends)
        {
            var friendsFriends = await allKnows
                .Where(k => k.StartNodeId == friend.Id)
                .Join(allPeople, k => k.EndNodeId, p => p.Id, (k, p) => p)
                .ToListAsync(TestContext.Current.CancellationToken);

            friendOfFriendIds.AddRange(friendsFriends.Select(f => f.Id));
        }

        // Get the actual people (excluding Alice herself)
        var alicesFriendsOfFriends = friendOfFriendIds
            .Where(id => id != alice.Id)
            .Distinct()
            .ToList();

        Assert.Single(alicesFriendsOfFriends);
        // Verify Charlie is the friend of friend by getting the person
        charlie = await allPeople.Where(p => p.Id == alicesFriendsOfFriends[0]).FirstOrDefaultAsync(TestContext.Current.CancellationToken);
        Assert.NotNull(charlie);
        Assert.Equal("Charlie", charlie.FirstName);
    }

    [Fact(Skip = "Pattern comprehensions with nested Select().ToList() not yet implemented")]
    public async Task CanQueryWithTraversePathAndGroupBy()
    {
        // This test uses the Person class that has IList<Knows> Knows property
        var alice = new Person { FirstName = "Alice" };
        var bob = new Person { FirstName = "Bob" };
        var charlie = new Person { FirstName = "Charlie" };

        await this.Graph.CreateNodeAsync(alice, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateNodeAsync(bob, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateNodeAsync(charlie, null, TestContext.Current.CancellationToken);

        var knows1 = new Knows(alice, bob);
        var knows2 = new Knows(alice, charlie);

        await this.Graph.CreateRelationshipAsync(knows1, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateRelationshipAsync(knows2, null, TestContext.Current.CancellationToken);

        // Test projection with navigation properties
        // First, verify the data exists
        var aliceNode = await this.Graph.Nodes<Person>()
            .Where(p => p.FirstName == "Alice")
            .FirstOrDefaultAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(aliceNode);
        Assert.Equal("Alice", aliceNode.FirstName);

        // Add diagnostic queries to verify the data
        // First, check if relationships exist at all
        var allKnowsRelationships = await this.Graph.Relationships<Knows>().ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(2, allKnowsRelationships.Count); // This should pass if relationships are created

        foreach (var relationship in allKnowsRelationships)
        {
            Assert.NotNull(relationship.StartNodeId);
            Assert.NotNull(relationship.EndNodeId);
        }

        // Check if we can find relationships by source ID
        var aliceRelationships = await this.Graph.Relationships<Knows>()
            .Where(k => k.StartNodeId == alice.Id)
            .ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(2, aliceRelationships.Count); // This should also pass

        // Now try simple projection without TraversalDepth
        var simpleProjection = await this.Graph.Nodes<Person>()
            .Where(p => p.FirstName == "Alice")
            .Select(p => new
            {
                Name = p.FirstName
            })
            .FirstOrDefaultAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(simpleProjection);
        Assert.NotNull(simpleProjection.Name);
        Assert.Equal("Alice", simpleProjection.Name);

        var people = await this.Graph.Nodes<Person>()
            .Traverse<Person, Knows, Person>()
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.NotEmpty(people); // Ensure we have people
        Assert.Equal(2, people.Count); // Alice, Bob, Charlie

        // Get the paths using TraversePath
        var paths = await this.Graph.Nodes<Person>()
            .Traverse<Person, Knows, Person>()
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.NotEmpty(paths); // Ensure we have paths

        Assert.Equal(2, paths.Count); // Alice knows Bob and Charlie
        Assert.Contains(paths, p => p.FirstName == "Bob");
        Assert.Contains(paths, p => p.FirstName == "Charlie");

        // Now get the paths with TraversePath and a Where clause
        var filteredPaths = await this.Graph.Nodes<Person>()
            .Where(p => p.FirstName == "Alice")
            .Traverse<Person, Knows, Person>()
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, filteredPaths.Count);

        // Group the paths in memory instead of in the query
        var projectedAlice = await this.Graph.Nodes<Person>()
            .Where(p => p.FirstName == "Alice")
            .Traverse<Person, Knows, Person>()
            .GroupBy(path => path)
            .Select(group => new
            {
                Name = group.Key.FirstName,
                FriendCount = group.Count(),
                FriendNames = group.Select(p => p.FirstName).ToList()
            })
            .FirstOrDefaultAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(projectedAlice);
        Assert.Equal("Alice", projectedAlice.Name);
        Assert.Equal(2, projectedAlice.FriendCount);
        Assert.Contains("Bob", projectedAlice.FriendNames);
        Assert.Contains("Charlie", projectedAlice.FriendNames);
    }

    [Fact(Skip = "Pattern comprehensions with nested Select().ToList() not yet implemented")]
    public async Task CanCombineNodeAndRelationshipQueries()
    {
        // Setup
        var alice = new Person { FirstName = "Alice" };
        var bob = new Person { FirstName = "Bob" };
        var charlie = new Person { FirstName = "Charlie" };

        await this.Graph.CreateNodeAsync(alice, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateNodeAsync(bob, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateNodeAsync(charlie, null, TestContext.Current.CancellationToken);

        await this.Graph.CreateRelationshipAsync(new Knows(alice, bob), null, TestContext.Current.CancellationToken);
        await this.Graph.CreateRelationshipAsync(new Knows(bob, charlie), null, TestContext.Current.CancellationToken);

        // Execute separate queries and combine in memory
        var people = await this.Graph.Nodes<Person>().ToDictionaryAsync(p => p.Id, TestContext.Current.CancellationToken);
        var relationships = await this.Graph.Relationships<Knows>().ToListAsync(TestContext.Current.CancellationToken);

        // Build a connection map
        var connectionMap = relationships
            .GroupBy(r => r.StartNodeId)
            .Select(g => new
            {
                PersonName = people[g.Key].FirstName,
                Connections = g.Select(r => people[r.EndNodeId].FirstName).ToList()
            })
            .ToList();

        Assert.Equal(2, connectionMap.Count);
        Assert.Contains(connectionMap, m => m.PersonName == "Alice" && m.Connections.Contains("Bob"));
        Assert.Contains(connectionMap, m => m.PersonName == "Bob" && m.Connections.Contains("Charlie"));
    }

    [Fact(Skip = "Cross-collection correlation in projections not yet implemented")]
    public async Task CanProjectRelationshipCounts()
    {
        // Setup
        var alice = new Person { FirstName = "Alice" };
        var bob = new Person { FirstName = "Bob" };
        var charlie = new Person { FirstName = "Charlie" };
        var dave = new Person { FirstName = "Dave" };

        await this.Graph.CreateNodeAsync(alice, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateNodeAsync(bob, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateNodeAsync(charlie, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateNodeAsync(dave, null, TestContext.Current.CancellationToken);

        await this.Graph.CreateRelationshipAsync(new Knows(alice, bob), null, TestContext.Current.CancellationToken);
        await this.Graph.CreateRelationshipAsync(new Knows(alice, charlie), null, TestContext.Current.CancellationToken);
        await this.Graph.CreateRelationshipAsync(new Knows(alice, dave), null, TestContext.Current.CancellationToken);
        await this.Graph.CreateRelationshipAsync(new Knows(bob, charlie), null, TestContext.Current.CancellationToken);
        await this.Graph.CreateRelationshipAsync(new Knows(bob, dave), null, TestContext.Current.CancellationToken);
        await this.Graph.CreateRelationshipAsync(new Knows(charlie, dave), null, TestContext.Current.CancellationToken);

        // Get all relationships once
        var allRelationships = await this.Graph.Relationships<Knows>().ToListAsync(TestContext.Current.CancellationToken);

        // Project connection counts
        var connectionStats = await this.Graph.Nodes<Person>()
            .Select(p => new
            {
                Name = p.FirstName,
                OutgoingCount = allRelationships.Count(k => k.StartNodeId == p.Id),
                IncomingCount = allRelationships.Count(k => k.EndNodeId == p.Id),
                TotalConnections = allRelationships.Count(k => k.StartNodeId == p.Id || k.EndNodeId == p.Id)
            })
            .OrderByDescending(s => s.OutgoingCount)
            .ToListAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(4, connectionStats.Count);

        var aliceStats = connectionStats.First(s => s.Name == "Alice");
        Assert.Equal(3, aliceStats.OutgoingCount);
        Assert.Equal(0, aliceStats.IncomingCount);

        var daveStats = connectionStats.First(s => s.Name == "Dave");
        Assert.Equal(0, daveStats.OutgoingCount);
        Assert.Equal(3, daveStats.IncomingCount);
    }

    [Fact]
    public async Task CanRecognizeSpecialTypesAsNonComplexProperties_Node()
    {
        // Setup
        var alice = new Person { FirstName = "Alice" };
        var bob = new Person { FirstName = "Bob" };
        var charlie = new Person { FirstName = "Charlie" };
        var dave = new Person { FirstName = "Dave" };

        await this.Graph.CreateNodeAsync(alice, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateNodeAsync(bob, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateNodeAsync(charlie, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateNodeAsync(dave, null, TestContext.Current.CancellationToken);

        await this.Graph.CreateRelationshipAsync(new Knows(alice, bob), null, TestContext.Current.CancellationToken);
        await this.Graph.CreateRelationshipAsync(new Knows(alice, charlie), null, TestContext.Current.CancellationToken);
        await this.Graph.CreateRelationshipAsync(new Knows(alice, dave), null, TestContext.Current.CancellationToken);
        await this.Graph.CreateRelationshipAsync(new Knows(bob, charlie), null, TestContext.Current.CancellationToken);
        await this.Graph.CreateRelationshipAsync(new Knows(bob, dave), null, TestContext.Current.CancellationToken);
        await this.Graph.CreateRelationshipAsync(new Knows(charlie, dave), null, TestContext.Current.CancellationToken);
        await this.Graph.CreateRelationshipAsync(new Friend(alice, bob), null, TestContext.Current.CancellationToken);
        await this.Graph.CreateRelationshipAsync(new Friend(bob, charlie), null, TestContext.Current.CancellationToken);
        await this.Graph.CreateRelationshipAsync(new Friend(charlie, dave), null, TestContext.Current.CancellationToken);

        // Get Alice's relationships
        var connectionStats = await this.Graph.Nodes<Person>()
            .PathSegments<Person, IRelationship, Person>()
            .Select(ps => new
            {
                Name = ps.StartNode.FirstName,
                Node = ps.StartNode,
            })
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Equal(4, connectionStats.Where(cs => cs.Name == "Alice").Count());
        Assert.Equal(3, connectionStats.Where(cs => cs.Name == "Bob").Count());
        Assert.Equal(2, connectionStats.Where(cs => cs.Name == "Charlie").Count());
        Assert.DoesNotContain(connectionStats, cs => cs.Name == "Dave");
    }

    [Fact]
    public async Task CanRecognizeSpecialTypesAsNonComplexProperties_Relationship()
    {
        // Setup
        var alice = new Person { FirstName = "Alice" };
        var bob = new Person { FirstName = "Bob" };
        var charlie = new Person { FirstName = "Charlie" };
        var dave = new Person { FirstName = "Dave" };

        await this.Graph.CreateNodeAsync(alice, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateNodeAsync(bob, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateNodeAsync(charlie, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateNodeAsync(dave, null, TestContext.Current.CancellationToken);

        await this.Graph.CreateRelationshipAsync(new Knows(alice, bob), null, TestContext.Current.CancellationToken);
        await this.Graph.CreateRelationshipAsync(new Knows(alice, charlie), null, TestContext.Current.CancellationToken);
        await this.Graph.CreateRelationshipAsync(new Knows(alice, dave), null, TestContext.Current.CancellationToken);
        await this.Graph.CreateRelationshipAsync(new Knows(bob, charlie), null, TestContext.Current.CancellationToken);
        await this.Graph.CreateRelationshipAsync(new Knows(bob, dave), null, TestContext.Current.CancellationToken);
        await this.Graph.CreateRelationshipAsync(new Knows(charlie, dave), null, TestContext.Current.CancellationToken);
        await this.Graph.CreateRelationshipAsync(new Friend(alice, bob), null, TestContext.Current.CancellationToken);
        await this.Graph.CreateRelationshipAsync(new Friend(bob, charlie), null, TestContext.Current.CancellationToken);
        await this.Graph.CreateRelationshipAsync(new Friend(charlie, dave), null, TestContext.Current.CancellationToken);

        // Get Alice's relationships
        var connectionStats = await this.Graph.Nodes<Person>()
            .PathSegments<Person, IRelationship, Person>()
            .Select(ps => new
            {
                Id = ps.StartNode.Id,
                Name = ps.StartNode.FirstName,
                Relationship = ps.Relationship,
            })
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Equal(4, connectionStats.Where(cs => cs.Name == "Alice").Count());
        Assert.Equal(3, connectionStats.Where(cs => cs.Name == "Bob").Count());
        Assert.Equal(2, connectionStats.Where(cs => cs.Name == "Charlie").Count());
        Assert.DoesNotContain(connectionStats, cs => cs.Name == "Dave");
    }

    [Fact]
    public async Task CanRecognizeSpecialTypesAsNonComplexProperties_PathSegment()
    {
        // Setup
        var alice = new Person { FirstName = "Alice" };
        var bob = new Person { FirstName = "Bob" };
        var charlie = new Person { FirstName = "Charlie" };
        var dave = new Person { FirstName = "Dave" };

        await this.Graph.CreateNodeAsync(alice, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateNodeAsync(bob, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateNodeAsync(charlie, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateNodeAsync(dave, null, TestContext.Current.CancellationToken);

        await this.Graph.CreateRelationshipAsync(new Knows(alice, bob), null, TestContext.Current.CancellationToken);
        await this.Graph.CreateRelationshipAsync(new Knows(alice, charlie), null, TestContext.Current.CancellationToken);
        await this.Graph.CreateRelationshipAsync(new Knows(alice, dave), null, TestContext.Current.CancellationToken);
        await this.Graph.CreateRelationshipAsync(new Knows(bob, charlie), null, TestContext.Current.CancellationToken);
        await this.Graph.CreateRelationshipAsync(new Knows(bob, dave), null, TestContext.Current.CancellationToken);
        await this.Graph.CreateRelationshipAsync(new Knows(charlie, dave), null, TestContext.Current.CancellationToken);
        await this.Graph.CreateRelationshipAsync(new Friend(alice, bob), null, TestContext.Current.CancellationToken);
        await this.Graph.CreateRelationshipAsync(new Friend(bob, charlie), null, TestContext.Current.CancellationToken);
        await this.Graph.CreateRelationshipAsync(new Friend(charlie, dave), null, TestContext.Current.CancellationToken);

        // Get Alice's relationships
        var connectionStats = await this.Graph.Nodes<Person>()
            .PathSegments<Person, IRelationship, Person>()
            .Select(ps => new
            {
                StartNode = ps.StartNode,
                PathSegment = ps,
                EndNode = ps.EndNode
            })
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Equal(4, connectionStats.Where(cs => cs.StartNode.FirstName == "Alice").Count());
        Assert.Equal(3, connectionStats.Where(cs => cs.StartNode.FirstName == "Bob").Count());
        Assert.Equal(2, connectionStats.Where(cs => cs.StartNode.FirstName == "Charlie").Count());
        Assert.DoesNotContain(connectionStats, cs => cs.StartNode.FirstName == "Dave");
    }

}
