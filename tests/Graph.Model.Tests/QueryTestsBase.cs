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

public abstract class QueryTestsBase : ITestBase
{
    public abstract IGraph Graph { get; }

    [Fact]
    public async Task CanQueryNodesByProperty()
    {
        var p1 = new Person { FirstName = "Alice", LastName = "Smith" };
        var p2 = new Person { FirstName = "Bob", LastName = "Smith" };
        var p3 = new Person { FirstName = "Charlie", LastName = "Jones" };
        await this.Graph.CreateNodeAsync(p1);
        await this.Graph.CreateNodeAsync(p2);
        await this.Graph.CreateNodeAsync(p3);

        var smiths = await this.Graph.Nodes<Person>()
            .Where(p => p.LastName == "Smith")
            .ToListAsync();
        Assert.Contains(smiths, p => p.FirstName == "Alice");
        Assert.Contains(smiths, p => p.FirstName == "Bob");
        Assert.DoesNotContain(smiths, p => p.FirstName == "Charlie");
    }

    [Fact]
    public async Task CanQueryAllNodes()
    {
        var p1 = new Person { FirstName = "A" };
        var p2 = new Person { FirstName = "B" };
        await this.Graph.CreateNodeAsync(p1);
        await this.Graph.CreateNodeAsync(p2);

        var all = await this.Graph.Nodes<Person>().ToListAsync();
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
        await this.Graph.CreateNodeAsync(p1);
        await this.Graph.CreateNodeAsync(p2);
        await this.Graph.CreateNodeAsync(p3);

        var smithsOrdered = await this.Graph.Nodes<Person>()
            .Where(p => p.LastName == "Smith")
            .OrderBy(p => p.FirstName)
            .ToListAsync();
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
        await this.Graph.CreateNodeAsync(p1);
        await this.Graph.CreateNodeAsync(p2);
        await this.Graph.CreateNodeAsync(p3);

        var taken = await this.Graph.Nodes<Person>().OrderBy(p => p.FirstName).Take(2).ToListAsync();
        Assert.Equal(2, taken.Count);
        Assert.Equal("A", taken[0].FirstName);
        Assert.Equal("B", taken[1].FirstName);

        var skipped = await this.Graph.Nodes<Person>().OrderBy(p => p.FirstName).Skip(1).ToListAsync();
        Assert.Contains(skipped, p => p.FirstName == "B");
        Assert.Contains(skipped, p => p.FirstName == "C");
    }

    [Fact]
    public async Task CanQueryWithFirstAndSingle()
    {
        var p1 = new Person { FirstName = "A" };
        var p2 = new Person { FirstName = "B" };
        await this.Graph.CreateNodeAsync(p1);
        await this.Graph.CreateNodeAsync(p2);

        var first = await this.Graph.Nodes<Person>().OrderBy(p => p.FirstName).FirstAsync();
        Assert.Equal("A", first.FirstName);

        var single = await this.Graph.Nodes<Person>().SingleAsync(p => p.FirstName == "A");
        Assert.Equal("A", single.FirstName);
    }

    [Fact]
    public async Task CanQueryWithAnyAndCount()
    {
        var p1 = new Person { FirstName = "A" };
        var p2 = new Person { FirstName = "B" };
        await this.Graph.CreateNodeAsync(p1);
        await this.Graph.CreateNodeAsync(p2);

        var anyA = await this.Graph.Nodes<Person>().AnyAsync(p => p.FirstName == "A");
        Assert.True(anyA);

        var count = await this.Graph.Nodes<Person>().CountAsync();
        Assert.True(count >= 2);
    }

    [Fact]
    public async Task CanQueryWithLocalScopeVariableCapture()
    {
        var p1 = new Person { FirstName = "A" };
        await this.Graph.CreateNodeAsync(p1);

        var localName = "A";

        var a = await this.Graph.Nodes<Person>().Where(p => p.FirstName == localName).FirstOrDefaultAsync();
        Assert.NotNull(a);
        Assert.Equal(localName, a.FirstName);
    }

    [Fact]
    public async Task CanQueryWithLocalScopeObjectCapture()
    {
        var p1 = new Person { FirstName = "A" };
        var p2 = new Person { FirstName = "B" };
        await this.Graph.CreateNodeAsync(p1);
        await this.Graph.CreateNodeAsync(p2);

        var a = await this.Graph.Nodes<Person>().Where(p => p.FirstName == p1.FirstName).FirstOrDefaultAsync();
        Assert.NotNull(a);
        Assert.Equal(p1.FirstName, a.FirstName);
    }

}
