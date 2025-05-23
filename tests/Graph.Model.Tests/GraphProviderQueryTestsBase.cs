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

public abstract class GraphProviderQueryTestsBase
{
    private IGraph provider { get; }

    protected GraphProviderQueryTestsBase(IGraph provider)
    {
        this.provider = provider;
    }

    [Fact]
    public async Task CanQueryNodesByProperty()
    {
        var p1 = new Person { FirstName = "Alice", LastName = "Smith" };
        var p2 = new Person { FirstName = "Bob", LastName = "Smith" };
        var p3 = new Person { FirstName = "Charlie", LastName = "Jones" };
        await this.provider.CreateNode(p1);
        await this.provider.CreateNode(p2);
        await this.provider.CreateNode(p3);

        var smiths = this.provider.Nodes<Person>().Where(p => p.LastName == "Smith").ToList();
        Assert.Contains(smiths, p => p.FirstName == "Alice");
        Assert.Contains(smiths, p => p.FirstName == "Bob");
        Assert.DoesNotContain(smiths, p => p.FirstName == "Charlie");
    }

    [Fact]
    public async Task CanQueryAllNodes()
    {
        var p1 = new Person { FirstName = "A" };
        var p2 = new Person { FirstName = "B" };
        await this.provider.CreateNode(p1);
        await this.provider.CreateNode(p2);

        var all = this.provider.Nodes<Person>().ToList();
        Assert.True(all.Count >= 2);
        Assert.Contains(all, p => p.FirstName == "A");
        Assert.Contains(all, p => p.FirstName == "B");
    }

    [Fact]
    public async Task CanQueryWithWhereAndOrderBy()
    {
        var p1 = new Person { FirstName = "Charlie", LastName = "Smith" };
        var p2 = new Person { FirstName = "Alice", LastName = "Smith" };
        var p3 = new Person { FirstName = "Bob", LastName = "Jones" };
        await this.provider.CreateNode(p1);
        await this.provider.CreateNode(p2);
        await this.provider.CreateNode(p3);

        var smithsOrdered = this.provider.Nodes<Person>()
            .Where(p => p.LastName == "Smith")
            .OrderBy(p => p.FirstName)
            .ToList();
        Assert.Equal(2, smithsOrdered.Count);
        Assert.Equal("Alice", smithsOrdered[0].FirstName);
        Assert.Equal("Charlie", smithsOrdered[1].FirstName);
    }

    [Fact]
    public async Task CanQueryWithTakeAndSkip()
    {
        var p1 = new Person { FirstName = "A" };
        var p2 = new Person { FirstName = "B" };
        var p3 = new Person { FirstName = "C" };
        await this.provider.CreateNode(p1);
        await this.provider.CreateNode(p2);
        await this.provider.CreateNode(p3);

        var taken = this.provider.Nodes<Person>().OrderBy(p => p.FirstName).Take(2).ToList();
        Assert.Equal(2, taken.Count);
        Assert.Equal("A", taken[0].FirstName);
        Assert.Equal("B", taken[1].FirstName);

        var skipped = this.provider.Nodes<Person>().OrderBy(p => p.FirstName).Skip(1).ToList();
        Assert.Contains(skipped, p => p.FirstName == "B");
        Assert.Contains(skipped, p => p.FirstName == "C");
    }

    [Fact]
    public async Task CanQueryWithFirstAndSingle()
    {
        var p1 = new Person { FirstName = "A" };
        var p2 = new Person { FirstName = "B" };
        await this.provider.CreateNode(p1);
        await this.provider.CreateNode(p2);

        var first = this.provider.Nodes<Person>().OrderBy(p => p.FirstName).First();
        Assert.Equal("A", first.FirstName);

        var single = this.provider.Nodes<Person>().Single(p => p.FirstName == "A");
        Assert.Equal("A", single.FirstName);
    }

    [Fact]
    public async Task CanQueryWithAnyAndCount()
    {
        var p1 = new Person { FirstName = "A" };
        var p2 = new Person { FirstName = "B" };
        await this.provider.CreateNode(p1);
        await this.provider.CreateNode(p2);

        var anyA = this.provider.Nodes<Person>().Any(p => p.FirstName == "A");
        Assert.True(anyA);

        var count = this.provider.Nodes<Person>().Count();
        Assert.True(count >= 2);
    }

    [Fact]
    public async Task CanQueryNodeViaRelationshipNavigation()
    {
        var alice = new PersonWithNavigationProperty { FirstName = "Alice", LastName = "Smith" };
        var bob = new PersonWithNavigationProperty { FirstName = "Bob", LastName = "Jones" };
        var knows = new Knows(alice, bob) { Since = DateTime.UtcNow };
        alice.Knows.Add(knows);

        await this.provider.CreateNode(alice);
        await this.provider.CreateNode(bob);

        // Query Alice and include her friends via Knows
        var people = this.provider.Nodes<PersonWithNavigationProperty>().ToList();
        var aliceFromDb = people.FirstOrDefault(p => p.FirstName == "Alice");

        Assert.NotNull(aliceFromDb);
        Assert.NotNull(aliceFromDb.Knows);
        // Check navigation property
        Assert.Contains(aliceFromDb.Knows, k => k.Target.FirstName == "Bob");
    }

    [Fact]
    public async Task CanProjectPropertiesViaNavigationProperty()
    {
        var alice = new PersonWithNavigationProperty { FirstName = "Alice", LastName = "Smith" };
        var bob = new PersonWithNavigationProperty { FirstName = "Bob", LastName = "Jones" };
        var knows = new Knows(alice, bob) { Since = DateTime.UtcNow };
        alice.Knows.Add(knows);
        await this.provider.CreateNode(alice);
        await this.provider.CreateNode(bob);

        // Project Alice's friends' names
        var friendsNames = this.provider.Nodes<PersonWithNavigationProperty>()
            .Where(p => p.FirstName == "Alice")
            .SelectMany(p => p.Knows.Select(k => k.Target.FirstName))
            .ToList();
        Assert.Contains("Bob", friendsNames);
    }
}
