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

public abstract class AggregationTestsBase : ITestBase
{
    public abstract IGraph Graph { get; }

    public class PersonWithNumbers : INode
    {
        public string Id { get; init; } = Guid.NewGuid().ToString("N");
        public string FirstName { get; set; } = string.Empty;
        public int Age { get; set; }
        public long Salary { get; set; }
        public decimal NetWorth { get; set; }
        public double Height { get; set; }
        public float Weight { get; set; }
    }

    [Fact]
    public async Task SumInt_CalculatesCorrectTotal()
    {
        var people = new[]
        {
            new PersonWithNumbers { FirstName = "Alice", Age = 25 },
            new PersonWithNumbers { FirstName = "Bob", Age = 30 },
            new PersonWithNumbers { FirstName = "Charlie", Age = 35 }
        };

        foreach (var person in people)
        {
            await Graph.CreateNodeAsync(person, null, TestContext.Current.CancellationToken);
        }

        var totalAge = await Graph.Nodes<PersonWithNumbers>()
            .SumAsync(p => p.Age, TestContext.Current.CancellationToken);

        Assert.Equal(90, totalAge);
    }

    [Fact]
    public async Task SumLong_CalculatesCorrectTotal()
    {
        var people = new[]
        {
            new PersonWithNumbers { FirstName = "Alice", Salary = 50000L },
            new PersonWithNumbers { FirstName = "Bob", Salary = 75000L },
            new PersonWithNumbers { FirstName = "Charlie", Salary = 100000L }
        };

        foreach (var person in people)
        {
            await Graph.CreateNodeAsync(person, null, TestContext.Current.CancellationToken);
        }

        var totalSalary = await Graph.Nodes<PersonWithNumbers>()
            .SumAsync(p => p.Salary, TestContext.Current.CancellationToken);

        Assert.Equal(225000L, totalSalary);
    }

    [Fact]
    public async Task SumDecimal_CalculatesCorrectTotal()
    {
        var people = new[]
        {
            new PersonWithNumbers { FirstName = "Alice", NetWorth = 100000.50m },
            new PersonWithNumbers { FirstName = "Bob", NetWorth = 250000.75m },
            new PersonWithNumbers { FirstName = "Charlie", NetWorth = 500000.25m }
        };

        foreach (var person in people)
        {
            await Graph.CreateNodeAsync(person, null, TestContext.Current.CancellationToken);
        }

        var totalNetWorth = await Graph.Nodes<PersonWithNumbers>()
            .SumAsync(p => p.NetWorth, TestContext.Current.CancellationToken);

        Assert.Equal(850001.50m, totalNetWorth);
    }

    [Fact]
    public async Task SumDouble_CalculatesCorrectTotal()
    {
        var people = new[]
        {
            new PersonWithNumbers { FirstName = "Alice", Height = 5.5 },
            new PersonWithNumbers { FirstName = "Bob", Height = 6.0 },
            new PersonWithNumbers { FirstName = "Charlie", Height = 5.8 }
        };

        foreach (var person in people)
        {
            await Graph.CreateNodeAsync(person, null, TestContext.Current.CancellationToken);
        }

        var totalHeight = await Graph.Nodes<PersonWithNumbers>()
            .SumAsync(p => p.Height, TestContext.Current.CancellationToken);

        Assert.Equal(17.3, totalHeight, 1);
    }

    [Fact]
    public async Task SumFloat_CalculatesCorrectTotal()
    {
        var people = new[]
        {
            new PersonWithNumbers { FirstName = "Alice", Weight = 120.5f },
            new PersonWithNumbers { FirstName = "Bob", Weight = 180.2f },
            new PersonWithNumbers { FirstName = "Charlie", Weight = 165.8f }
        };

        foreach (var person in people)
        {
            await Graph.CreateNodeAsync(person, null, TestContext.Current.CancellationToken);
        }

        var totalWeight = await Graph.Nodes<PersonWithNumbers>()
            .SumAsync(p => p.Weight, TestContext.Current.CancellationToken);

        Assert.Equal(466.5f, totalWeight, 1);
    }

    [Fact]
    public async Task AverageInt_CalculatesCorrectAverage()
    {
        var people = new[]
        {
            new PersonWithNumbers { FirstName = "Alice", Age = 20 },
            new PersonWithNumbers { FirstName = "Bob", Age = 30 },
            new PersonWithNumbers { FirstName = "Charlie", Age = 40 }
        };

        foreach (var person in people)
        {
            await Graph.CreateNodeAsync(person, null, TestContext.Current.CancellationToken);
        }

        var averageAge = await Graph.Nodes<PersonWithNumbers>()
            .AverageAsync(p => p.Age, TestContext.Current.CancellationToken);

        Assert.Equal(30.0, averageAge, 1);
    }

    [Fact]
    public async Task AverageLong_CalculatesCorrectAverage()
    {
        var people = new[]
        {
            new PersonWithNumbers { FirstName = "Alice", Salary = 40000L },
            new PersonWithNumbers { FirstName = "Bob", Salary = 60000L },
            new PersonWithNumbers { FirstName = "Charlie", Salary = 80000L }
        };

        foreach (var person in people)
        {
            await Graph.CreateNodeAsync(person, null, TestContext.Current.CancellationToken);
        }

        var averageSalary = await Graph.Nodes<PersonWithNumbers>()
            .AverageAsync(p => p.Salary, TestContext.Current.CancellationToken);

        Assert.Equal(60000.0, averageSalary, 1);
    }

    [Fact]
    public async Task AverageDecimal_CalculatesCorrectAverage()
    {
        var people = new[]
        {
            new PersonWithNumbers { FirstName = "Alice", NetWorth = 100000m },
            new PersonWithNumbers { FirstName = "Bob", NetWorth = 200000m },
            new PersonWithNumbers { FirstName = "Charlie", NetWorth = 300000m }
        };

        foreach (var person in people)
        {
            await Graph.CreateNodeAsync(person, null, TestContext.Current.CancellationToken);
        }

        var averageNetWorth = await Graph.Nodes<PersonWithNumbers>()
            .AverageAsync(p => p.NetWorth, TestContext.Current.CancellationToken);

        Assert.Equal(200000.0, averageNetWorth);
    }

    [Fact]
    public async Task CountNodes_ReturnsCorrectCount()
    {
        var people = new[]
        {
            new PersonWithNumbers { FirstName = "Alice" },
            new PersonWithNumbers { FirstName = "Bob" },
            new PersonWithNumbers { FirstName = "Charlie" },
            new PersonWithNumbers { FirstName = "Diana" }
        };

        foreach (var person in people)
        {
            await Graph.CreateNodeAsync(person, null, TestContext.Current.CancellationToken);
        }

        var count = await Graph.Nodes<PersonWithNumbers>()
            .CountAsync(TestContext.Current.CancellationToken);

        Assert.True(count >= 4); // May be more due to other tests
    }

    [Fact]
    public async Task CountNodesWithPredicate_ReturnsCorrectCount()
    {
        var people = new[]
        {
            new PersonWithNumbers { FirstName = "Alice", Age = 25 },
            new PersonWithNumbers { FirstName = "Bob", Age = 35 },
            new PersonWithNumbers { FirstName = "Charlie", Age = 45 },
            new PersonWithNumbers { FirstName = "Diana", Age = 20 }
        };

        foreach (var person in people)
        {
            await Graph.CreateNodeAsync(person, null, TestContext.Current.CancellationToken);
        }

        var adultCount = await Graph.Nodes<PersonWithNumbers>()
            .CountAsync(p => p.Age >= 30, TestContext.Current.CancellationToken);

        Assert.Equal(2, adultCount);
    }

    [Fact]
    public async Task SumWithFilter_CalculatesCorrectTotal()
    {
        var people = new[]
        {
            new PersonWithNumbers { FirstName = "Alice", Age = 25, Salary = 50000L },
            new PersonWithNumbers { FirstName = "Bob", Age = 35, Salary = 75000L },
            new PersonWithNumbers { FirstName = "Charlie", Age = 45, Salary = 100000L },
            new PersonWithNumbers { FirstName = "Diana", Age = 20, Salary = 40000L }
        };

        foreach (var person in people)
        {
            await Graph.CreateNodeAsync(person, null, TestContext.Current.CancellationToken);
        }

        var totalSalaryOver30 = await Graph.Nodes<PersonWithNumbers>()
            .Where(p => p.Age >= 30)
            .SumAsync(p => p.Salary, TestContext.Current.CancellationToken);

        Assert.Equal(175000L, totalSalaryOver30);
    }

    [Fact]
    public async Task AverageWithFilter_CalculatesCorrectAverage()
    {
        var people = new[]
        {
            new PersonWithNumbers { FirstName = "Alice", Age = 25, Height = 5.4 },
            new PersonWithNumbers { FirstName = "Bob", Age = 35, Height = 6.0 },
            new PersonWithNumbers { FirstName = "Charlie", Age = 45, Height = 5.8 },
            new PersonWithNumbers { FirstName = "Diana", Age = 20, Height = 5.2 }
        };

        foreach (var person in people)
        {
            await Graph.CreateNodeAsync(person, null, TestContext.Current.CancellationToken);
        }

        var averageHeightOver30 = await Graph.Nodes<PersonWithNumbers>()
            .Where(p => p.Age >= 30)
            .AverageAsync(p => p.Height, TestContext.Current.CancellationToken);

        Assert.Equal(5.9, averageHeightOver30, 1);
    }

    [Fact]
    public async Task SumEmptySet_ReturnsZero()
    {
        var sum = await Graph.Nodes<PersonWithNumbers>()
            .Where(p => p.FirstName == "NonExistent")
            .SumAsync(p => p.Age, TestContext.Current.CancellationToken);

        Assert.Equal(0, sum);
    }

    [Fact]
    public async Task AverageEmptySet_ThrowsException()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await Graph.Nodes<PersonWithNumbers>()
                .Where(p => p.FirstName == "NonExistent")
                .AverageAsync(p => p.Age, TestContext.Current.CancellationToken);
        });
    }

    [Fact]
    public async Task CountEmptySet_ReturnsZero()
    {
        var count = await Graph.Nodes<PersonWithNumbers>()
            .Where(p => p.FirstName == "NonExistent")
            .CountAsync(TestContext.Current.CancellationToken);

        Assert.Equal(0, count);
    }

    [Fact]
    public async Task AggregationWithComplexQuery_WorksCorrectly()
    {
        var people = new[]
        {
            new PersonWithNumbers { FirstName = "Alice", Age = 25, Salary = 50000L },
            new PersonWithNumbers { FirstName = "Bob", Age = 35, Salary = 75000L },
            new PersonWithNumbers { FirstName = "Charlie", Age = 45, Salary = 100000L },
            new PersonWithNumbers { FirstName = "Diana", Age = 55, Salary = 120000L },
            new PersonWithNumbers { FirstName = "Eve", Age = 28, Salary = 60000L }
        };

        foreach (var person in people)
        {
            await Graph.CreateNodeAsync(person, null, TestContext.Current.CancellationToken);
        }

        // Find average salary of people aged 30-50
        var averageSalaryMiddleAged = await Graph.Nodes<PersonWithNumbers>()
            .Where(p => p.Age >= 30 && p.Age <= 50)
            .OrderBy(p => p.Age)
            .AverageAsync(p => p.Salary, TestContext.Current.CancellationToken);

        Assert.Equal(87500.0, averageSalaryMiddleAged, 1);
    }

    [Fact]
    public async Task MultipleAggregationsOnSameData_ReturnConsistentResults()
    {
        var people = new[]
        {
            new PersonWithNumbers { FirstName = "Alice", Age = 20 },
            new PersonWithNumbers { FirstName = "Bob", Age = 30 },
            new PersonWithNumbers { FirstName = "Charlie", Age = 40 },
            new PersonWithNumbers { FirstName = "Diana", Age = 60 }
        };

        foreach (var person in people)
        {
            await Graph.CreateNodeAsync(person, null, TestContext.Current.CancellationToken);
        }

        var query = Graph.Nodes<PersonWithNumbers>()
            .Where(p => p.FirstName.Length > 3);

        var sum = await query.SumAsync(p => p.Age, TestContext.Current.CancellationToken);
        var average = await query.AverageAsync(p => p.Age, TestContext.Current.CancellationToken);
        var count = await query.CountAsync(TestContext.Current.CancellationToken);

        Assert.Equal(120, sum);
        Assert.Equal(40.0, average);
        Assert.Equal(3, count);
        Assert.Equal(sum / count, average);
    }

    [Fact]
    public async Task AggregationInTransaction_WorksCorrectly()
    {
        await using var transaction = await Graph.GetTransactionAsync();

        var people = new[]
        {
            new PersonWithNumbers { FirstName = "Alice", Age = 25 },
            new PersonWithNumbers { FirstName = "Bob", Age = 35 }
        };

        foreach (var person in people)
        {
            await Graph.CreateNodeAsync(person, transaction, TestContext.Current.CancellationToken);
        }

        var sum = await Graph.Nodes<PersonWithNumbers>(transaction)
            .Where(p => p.FirstName == "Alice" || p.FirstName == "Bob")
            .SumAsync(p => p.Age, TestContext.Current.CancellationToken);

        Assert.Equal(60, sum);

        await transaction.CommitAsync();
    }
}