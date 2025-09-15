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

public interface IQueryTests : IGraphModelTest
{
    [Fact]
    public async Task CanQueryNodesByProperty()
    {
        var p1 = new Person { FirstName = "Alice", LastName = "Smith" };
        var p2 = new Person { FirstName = "Bob", LastName = "Smith" };
        var p3 = new Person { FirstName = "Charlie", LastName = "Jones" };
        await this.Graph.CreateNodeAsync(p1, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateNodeAsync(p2, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateNodeAsync(p3, null, TestContext.Current.CancellationToken);

        var smiths = await this.Graph.Nodes<Person>()
            .Where(p => p.LastName == "Smith")
            .ToListAsync(TestContext.Current.CancellationToken);
        Assert.Contains(smiths, p => p.FirstName == "Alice");
        Assert.Contains(smiths, p => p.FirstName == "Bob");
        Assert.DoesNotContain(smiths, p => p.FirstName == "Charlie");
    }

    [Fact]
    public async Task CanQueryNodesByMultipleProperties()
    {
        var p1 = new Person { FirstName = "Alice", LastName = "Smith" };
        var p2 = new Person { FirstName = "Bob", LastName = "Smith" };
        var p3 = new Person { FirstName = "Charlie", LastName = "Jones" };
        await this.Graph.CreateNodeAsync(p1, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateNodeAsync(p2, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateNodeAsync(p3, null, TestContext.Current.CancellationToken);

        var smiths = await this.Graph.Nodes<Person>()
            .Where(p => p.LastName == "Smith" && p.FirstName.StartsWith("A"))
            .ToListAsync(TestContext.Current.CancellationToken);
        Assert.Single(smiths);
        Assert.Equal("Alice", smiths[0].FirstName);
    }

    [Fact]
    public async Task CanQueryNodesWithComplexPropertyInWhere()
    {
        var p1 = new PersonWithComplexProperties { FirstName = "Alice", LastName = "Smith", Age = 30, Address = new AddressValue { City = "New York", Street = "123 Main St" } };
        var p2 = new PersonWithComplexProperties { FirstName = "Bob", LastName = "Smith", Age = 25, Address = new AddressValue { City = "Los Angeles", Street = "456 Elm St" } };
        var p3 = new PersonWithComplexProperties { FirstName = "Charlie", LastName = "Jones", Age = 35, Address = new AddressValue { City = "Chicago", Street = "789 Oak St" } };
        await this.Graph.CreateNodeAsync(p1, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateNodeAsync(p2, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateNodeAsync(p3, null, TestContext.Current.CancellationToken);

        var youngSmiths = await this.Graph.Nodes<PersonWithComplexProperties>()
            .Where(p => p.LastName == "Smith" && p.Age < 30 && p.Address.City == "Los Angeles")
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Single(youngSmiths);
        Assert.Equal("Bob", youngSmiths[0].FirstName);
    }

    [Fact]
    public async Task CanQueryNodesWithComplexPropertyInSelect()
    {
        var p1 = new PersonWithComplexProperties { FirstName = "Alice", LastName = "Smith", Age = 30, Address = new AddressValue { City = "New York", Street = "123 Main St" } };
        var p2 = new PersonWithComplexProperties { FirstName = "Bob", LastName = "Smith", Age = 25, Address = new AddressValue { City = "Los Angeles", Street = "456 Elm St" } };
        var p3 = new PersonWithComplexProperties { FirstName = "Charlie", LastName = "Jones", Age = 35, Address = new AddressValue { City = "Chicago", Street = "789 Oak St" } };
        await this.Graph.CreateNodeAsync(p1, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateNodeAsync(p2, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateNodeAsync(p3, null, TestContext.Current.CancellationToken);

        var peopleAndCities = await this.Graph.Nodes<PersonWithComplexProperties>()
            .Select(p => new { p.FirstName, p.Address.City })
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Equal(3, peopleAndCities.Count);
        Assert.Contains(peopleAndCities, pc => pc.FirstName == "Alice" && pc.City == "New York");
        Assert.Contains(peopleAndCities, pc => pc.FirstName == "Bob" && pc.City == "Los Angeles");
        Assert.Contains(peopleAndCities, pc => pc.FirstName == "Charlie" && pc.City == "Chicago");
    }

    [Fact]
    public async Task CanQueryNodesByMultipleComplexPropertiesInSequence()
    {
        var c1 = new Class1
        {
            Property1 = "Value1",
            Property2 = "Value2",
            A = new ComplexClassA
            {
                Property1 = "A1",
                Property2 = "A2",
                B = new ComplexClassB { Property1 = "B1" }
            },
            B = new ComplexClassB { Property1 = "B2" }
        };

        var c2 = new Class2
        {
            Property1 = "Value3",
            Property2 = "Value4",
            A = new List<ComplexClassA>
            {
                new ComplexClassA { Property1 = "A3", Property2 = "A4" }
            },
            B = new List<ComplexClassB>
            {
                new ComplexClassB { Property1 = "B3" }
            }
        };

        await this.Graph.CreateNodeAsync(c1, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateNodeAsync(c2, null, TestContext.Current.CancellationToken);

        var results = await this.Graph.Nodes<Class1>()
            .Where(c => c.A!.B!.Property1 == "B1" || c.B!.Property1 == "B2")
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Single(results);
        Assert.Equal("Value1", results[0].Property1);
        Assert.Equal("B2", results[0].B!.Property1);
    }

    [Fact]
    public async Task CanQueryAllNodes()
    {
        var p1 = new Person { FirstName = "A" };
        var p2 = new Person { FirstName = "B" };
        await this.Graph.CreateNodeAsync(p1, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateNodeAsync(p2, null, TestContext.Current.CancellationToken);

        var all = await this.Graph.Nodes<Person>().ToListAsync(TestContext.Current.CancellationToken);
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
        await this.Graph.CreateNodeAsync(p1, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateNodeAsync(p2, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateNodeAsync(p3, null, TestContext.Current.CancellationToken);

        var smithsOrdered = await this.Graph.Nodes<Person>()
            .Where(p => p.LastName == "Smith")
            .OrderBy(p => p.FirstName)
            .ToListAsync(TestContext.Current.CancellationToken);
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
        await this.Graph.CreateNodeAsync(p1, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateNodeAsync(p2, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateNodeAsync(p3, null, TestContext.Current.CancellationToken);

        var taken = await this.Graph.Nodes<Person>().OrderBy(p => p.FirstName).Take(2).ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(2, taken.Count);
        Assert.Equal("A", taken[0].FirstName);
        Assert.Equal("B", taken[1].FirstName);

        var skipped = await this.Graph.Nodes<Person>().OrderBy(p => p.FirstName).Skip(1).ToListAsync(TestContext.Current.CancellationToken);
        Assert.Contains(skipped, p => p.FirstName == "B");
        Assert.Contains(skipped, p => p.FirstName == "C");
    }

    [Fact]
    public async Task CanQueryWithTakeZero()
    {
        var p1 = new Person { FirstName = "A" };
        var p2 = new Person { FirstName = "B" };
        await this.Graph.CreateNodeAsync(p1, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateNodeAsync(p2, null, TestContext.Current.CancellationToken);

        var taken = await this.Graph.Nodes<Person>().Take(0).ToListAsync(TestContext.Current.CancellationToken);
        Assert.Empty(taken);
    }

    [Fact]
    public async Task CanQueryWithTakeLargerThanAvailable()
    {
        var p1 = new Person { FirstName = "A" };
        var p2 = new Person { FirstName = "B" };
        await this.Graph.CreateNodeAsync(p1, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateNodeAsync(p2, null, TestContext.Current.CancellationToken);

        var taken = await this.Graph.Nodes<Person>().OrderBy(p => p.FirstName).Take(10).ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(2, taken.Count);
        Assert.Equal("A", taken[0].FirstName);
        Assert.Equal("B", taken[1].FirstName);
    }

    [Fact]
    public async Task CanQueryWithTakeOne()
    {
        var p1 = new Person { FirstName = "A" };
        var p2 = new Person { FirstName = "B" };
        var p3 = new Person { FirstName = "C" };
        await this.Graph.CreateNodeAsync(p1, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateNodeAsync(p2, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateNodeAsync(p3, null, TestContext.Current.CancellationToken);

        var taken = await this.Graph.Nodes<Person>().OrderBy(p => p.FirstName).Take(1).ToListAsync(TestContext.Current.CancellationToken);
        Assert.Single(taken);
        Assert.Equal("A", taken[0].FirstName);
    }

    [Fact]
    public async Task CanQueryWithTakeAndWhere()
    {
        var p1 = new Person { FirstName = "Alice", LastName = "Smith" };
        var p2 = new Person { FirstName = "Bob", LastName = "Smith" };
        var p3 = new Person { FirstName = "Charlie", LastName = "Jones" };
        await this.Graph.CreateNodeAsync(p1, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateNodeAsync(p2, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateNodeAsync(p3, null, TestContext.Current.CancellationToken);

        var taken = await this.Graph.Nodes<Person>()
            .Where(p => p.LastName == "Smith")
            .OrderBy(p => p.FirstName)
            .Take(1)
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Single(taken);
        Assert.Equal("Alice", taken[0].FirstName);
    }

    [Fact]
    public async Task CanQueryWithTakeAndSelect()
    {
        var p1 = new Person { FirstName = "Alice", LastName = "Smith" };
        var p2 = new Person { FirstName = "Bob", LastName = "Smith" };
        await this.Graph.CreateNodeAsync(p1, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateNodeAsync(p2, null, TestContext.Current.CancellationToken);

        var taken = await this.Graph.Nodes<Person>()
            .OrderBy(p => p.FirstName)
            .Select(p => p.FirstName)
            .Take(1)
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Single(taken);
        Assert.Equal("Alice", taken[0]);
    }

    [Fact]
    public async Task CanQueryWithTakeAndDistinct()
    {
        var p1 = new Person { FirstName = "Alice", LastName = "Smith" };
        var p2 = new Person { FirstName = "Alice", LastName = "Jones" };
        var p3 = new Person { FirstName = "Bob", LastName = "Smith" };
        await this.Graph.CreateNodeAsync(p1, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateNodeAsync(p2, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateNodeAsync(p3, null, TestContext.Current.CancellationToken);

        var taken = await this.Graph.Nodes<Person>()
            .Select(p => p.FirstName)
            .Distinct()
            .OrderBy(name => name)
            .Take(2)
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, taken.Count);
        Assert.Equal("Alice", taken[0]);
        Assert.Equal("Bob", taken[1]);
    }

    [Fact]
    public async Task CanQueryWithFirstAndSingle()
    {
        var p1 = new Person { FirstName = "A" };
        var p2 = new Person { FirstName = "B" };
        await this.Graph.CreateNodeAsync(p1, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateNodeAsync(p2, null, TestContext.Current.CancellationToken);

        var first = await this.Graph.Nodes<Person>().OrderBy(p => p.FirstName).FirstAsync(TestContext.Current.CancellationToken);
        Assert.Equal("A", first.FirstName);

        var single = await this.Graph.Nodes<Person>().SingleAsync(p => p.FirstName == "A", TestContext.Current.CancellationToken);
        Assert.Equal("A", single.FirstName);
    }

    [Fact]
    public async Task CanQueryWithAnyAndCount()
    {
        var p1 = new Person { FirstName = "A" };
        var p2 = new Person { FirstName = "B" };
        await this.Graph.CreateNodeAsync(p1, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateNodeAsync(p2, null, TestContext.Current.CancellationToken);

        var anyA = await this.Graph.Nodes<Person>().AnyAsync(p => p.FirstName == "A", TestContext.Current.CancellationToken);
        Assert.True(anyA);

        var count = await this.Graph.Nodes<Person>().CountAsync(TestContext.Current.CancellationToken);
        Assert.True(count >= 2);
    }

    [Fact]
    public async Task CanQueryWithLocalScopeVariableCapture()
    {
        var p1 = new Person { FirstName = "A" };
        await this.Graph.CreateNodeAsync(p1, null, TestContext.Current.CancellationToken);

        var localName = "A";

        var a = await this.Graph.Nodes<Person>().Where(p => p.FirstName == localName).FirstOrDefaultAsync(TestContext.Current.CancellationToken);
        Assert.NotNull(a);
        Assert.Equal(localName, a.FirstName);
    }

    [Fact]
    public async Task CanQueryWithLocalScopeObjectCapture()
    {
        var p1 = new Person { FirstName = "A" };
        var p2 = new Person { FirstName = "B" };
        await this.Graph.CreateNodeAsync(p1, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateNodeAsync(p2, null, TestContext.Current.CancellationToken);

        var a = await this.Graph.Nodes<Person>().Where(p => p.FirstName == p1.FirstName).FirstOrDefaultAsync(TestContext.Current.CancellationToken);
        Assert.NotNull(a);
        Assert.Equal(p1.FirstName, a.FirstName);
    }

}
