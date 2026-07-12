// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.CompatibilityTests;

public interface IAggregationTests : IGraphTest
{
    public record PersonWithNumbers : Node
    {
        public string FirstName { get; set; } = string.Empty;
        public int Age { get; set; }
        public long Salary { get; set; }
        public decimal NetWorth { get; set; }
        public double Height { get; set; }
        public float Weight { get; set; }
        public DateTime CreatedAt { get; set; }
        public Guid TrackingId { get; set; }
    }

    [Fact]
    public async Task CanCountDistinctQuery()
    {
        var people = new[]
        {
            new PersonWithNumbers { FirstName = "Alice", Age = 30 },
            new PersonWithNumbers { FirstName = "Bob", Age = 30 },
            new PersonWithNumbers { FirstName = "Charlie", Age = 30 },
            new PersonWithNumbers { FirstName = "Dana", Age = 40 }
        };

        foreach (var person in people)
        {
            await Graph.CreateNodeAsync(person, null, TestContext.Current.CancellationToken);
        }

        var rawCount = await Graph.Nodes<PersonWithNumbers>()
            .Select(p => p.Age)
            .CountAsync(TestContext.Current.CancellationToken);
        var distinctCount = await Graph.Nodes<PersonWithNumbers>()
            .Select(p => p.Age)
            .Distinct()
            .CountAsync(TestContext.Current.CancellationToken);

        Assert.Equal(4, rawCount);
        Assert.Equal(2, distinctCount);
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

        // decimal Average() returns decimal (standard LINQ semantics), unlike int/long which average to double.
        Assert.Equal(200000.0m, averageNetWorth);
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
    public async Task CanCountOrderedQuery()
    {
        var group = $"issue-177-count-{Guid.NewGuid():N}";
        var people = new[]
        {
            new PersonWithNumbers { FirstName = group, Age = 30 },
            new PersonWithNumbers { FirstName = group, Age = 10 },
            new PersonWithNumbers { FirstName = group, Age = 20 }
        };

        foreach (var person in people)
        {
            await Graph.CreateNodeAsync(person, null, TestContext.Current.CancellationToken);
        }

        var query = Graph.Nodes<PersonWithNumbers>()
            .Where(p => p.FirstName == group);

        var unorderedCount = await query.CountAsync(TestContext.Current.CancellationToken);
        var orderedCount = await query
            .OrderBy(p => p.Age)
            .CountAsync(TestContext.Current.CancellationToken);

        Assert.Equal(3, unorderedCount);
        Assert.Equal(unorderedCount, orderedCount);
    }

    private static readonly int[] ExpectedAges = [10, 20];

    [Fact]
    public async Task CanCountOrderedLimitedQuery()
    {
        var group = $"issue-177-limited-{Guid.NewGuid():N}";
        var people = new[]
        {
            new PersonWithNumbers { FirstName = group, Age = 30 },
            new PersonWithNumbers { FirstName = group, Age = 10 },
            new PersonWithNumbers { FirstName = group, Age = 20 }
        };

        foreach (var person in people)
        {
            await Graph.CreateNodeAsync(person, null, TestContext.Current.CancellationToken);
        }

        var query = Graph.Nodes<PersonWithNumbers>()
            .Where(p => p.FirstName == group)
            .OrderBy(p => p.Age)
            .Take(2);

        var limitedCount = await query.CountAsync(TestContext.Current.CancellationToken);
        var limitedPeople = await query.ToListAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, limitedCount);
        Assert.Equal(ExpectedAges, limitedPeople.Select(p => p.Age).ToArray());
    }

    [Fact]
    public async Task CanCountLimitedUnorderedQuery()
    {
        var group = $"issue-185-limited-{Guid.NewGuid():N}";
        var people = new[]
        {
            new PersonWithNumbers { FirstName = group, Age = 10 },
            new PersonWithNumbers { FirstName = group, Age = 20 },
            new PersonWithNumbers { FirstName = group, Age = 30 }
        };

        foreach (var person in people)
        {
            await Graph.CreateNodeAsync(person, null, TestContext.Current.CancellationToken);
        }

        var limitedCount = await Graph.Nodes<PersonWithNumbers>()
            .Where(p => p.FirstName == group)
            .Take(2)
            .CountAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, limitedCount);
    }

    [Fact]
    public async Task CanCountEmptyLimitedUnorderedQuery()
    {
        var group = $"issue-185-empty-limited-{Guid.NewGuid():N}";
        var people = new[]
        {
            new PersonWithNumbers { FirstName = group, Age = 10 },
            new PersonWithNumbers { FirstName = group, Age = 20 }
        };

        foreach (var person in people)
        {
            await Graph.CreateNodeAsync(person, null, TestContext.Current.CancellationToken);
        }

        var limitedCount = await Graph.Nodes<PersonWithNumbers>()
            .Where(p => p.FirstName == group)
            .Take(0)
            .CountAsync(TestContext.Current.CancellationToken);

        Assert.Equal(0, limitedCount);
    }

    [Fact]
    public async Task CanCountSkippedUnorderedQuery()
    {
        var group = $"issue-185-skipped-{Guid.NewGuid():N}";
        var people = new[]
        {
            new PersonWithNumbers { FirstName = group, Age = 10 },
            new PersonWithNumbers { FirstName = group, Age = 20 },
            new PersonWithNumbers { FirstName = group, Age = 30 }
        };

        foreach (var person in people)
        {
            await Graph.CreateNodeAsync(person, null, TestContext.Current.CancellationToken);
        }

        var skippedCount = await Graph.Nodes<PersonWithNumbers>()
            .Where(p => p.FirstName == group)
            .Skip(2)
            .CountAsync(TestContext.Current.CancellationToken);

        Assert.Equal(1, skippedCount);
    }

    [Fact]
    public async Task OrderBySkipTakeAggregateQueries_ApplyWindowBeforeAggregate()
    {
        var group = $"issue-185-window-aggregates-{Guid.NewGuid():N}";
        var people = new[]
        {
            new PersonWithNumbers { FirstName = group, Age = 10 },
            new PersonWithNumbers { FirstName = group, Age = 20 },
            new PersonWithNumbers { FirstName = group, Age = 30 },
            new PersonWithNumbers { FirstName = group, Age = 40 }
        };

        foreach (var person in people)
        {
            await Graph.CreateNodeAsync(person, null, TestContext.Current.CancellationToken);
        }

        var query = Graph.Nodes<PersonWithNumbers>()
            .Where(p => p.FirstName == group)
            .OrderBy(p => p.Age)
            .Skip(1)
            .Take(2);

        var sum = await query.SumAsync(p => p.Age, TestContext.Current.CancellationToken);
        var average = await query.AverageAsync(p => p.Age, TestContext.Current.CancellationToken);
        var min = await query.MinAsync(p => p.Age, TestContext.Current.CancellationToken);
        var max = await query.MaxAsync(p => p.Age, TestContext.Current.CancellationToken);

        Assert.Equal(50, sum);
        Assert.Equal(25.0, average, 1);
        Assert.Equal(20, min);
        Assert.Equal(30, max);
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
    public async Task MinAsync_EmptySource_NonNullableSelector_Throws()
    {
        var missingGroup = $"MinAsync-empty-{Guid.NewGuid():N}";

        var query = Graph.Nodes<PersonWithNumbers>()
            .Where(p => p.FirstName == missingGroup);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => query.MinAsync(p => p.Age, TestContext.Current.CancellationToken));

        Assert.Equal("Sequence contains no elements", exception.Message);
    }

    [Fact]
    public async Task MaxAsync_EmptySource_NonNullableSelector_Throws()
    {
        var missingGroup = $"MaxAsync-empty-{Guid.NewGuid():N}";

        var query = Graph.Nodes<PersonWithNumbers>()
            .Where(p => p.FirstName == missingGroup);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => query.MaxAsync(p => p.Age, TestContext.Current.CancellationToken));

        Assert.Equal("Sequence contains no elements", exception.Message);
    }

    [Fact]
    public async Task MinAsync_EmptySource_NullableSelector_ReturnsNull()
    {
        var missingGroup = $"MinAsync-nullable-empty-{Guid.NewGuid():N}";

        var result = await Graph.Nodes<PersonWithNumbers>()
            .Where(p => p.FirstName == missingGroup)
            .MinAsync(p => (int?)p.Age, TestContext.Current.CancellationToken);

        Assert.Null(result);
    }

    [Fact]
    public async Task MaxAsync_EmptySource_NullableSelector_ReturnsNull()
    {
        var missingGroup = $"MaxAsync-nullable-empty-{Guid.NewGuid():N}";

        var result = await Graph.Nodes<PersonWithNumbers>()
            .Where(p => p.FirstName == missingGroup)
            .MaxAsync(p => (int?)p.Age, TestContext.Current.CancellationToken);

        Assert.Null(result);
    }

    [Fact]
    public async Task MinAsync_NonEmptySource_NullableSelector_ReturnsValue()
    {
        var group = $"MinAsync-nullable-nonempty-{Guid.NewGuid():N}";
        var people = new[]
        {
            new PersonWithNumbers { FirstName = group, Age = 30 },
            new PersonWithNumbers { FirstName = group, Age = 10 },
            new PersonWithNumbers { FirstName = group, Age = 20 }
        };

        foreach (var person in people)
        {
            await Graph.CreateNodeAsync(person, null, TestContext.Current.CancellationToken);
        }

        var result = await Graph.Nodes<PersonWithNumbers>()
            .Where(p => p.FirstName == group)
            .MinAsync(p => (int?)p.Age, TestContext.Current.CancellationToken);

        Assert.Equal(10, result);
    }

    [Fact]
    public async Task MaxAsync_NonEmptySource_NullableSelector_ReturnsValue()
    {
        var group = $"MaxAsync-nullable-nonempty-{Guid.NewGuid():N}";
        var people = new[]
        {
            new PersonWithNumbers { FirstName = group, Age = 30 },
            new PersonWithNumbers { FirstName = group, Age = 10 },
            new PersonWithNumbers { FirstName = group, Age = 20 }
        };

        foreach (var person in people)
        {
            await Graph.CreateNodeAsync(person, null, TestContext.Current.CancellationToken);
        }

        var result = await Graph.Nodes<PersonWithNumbers>()
            .Where(p => p.FirstName == group)
            .MaxAsync(p => (int?)p.Age, TestContext.Current.CancellationToken);

        Assert.Equal(30, result);
    }

    [Fact]
    public async Task MinAsync_NonNullableDateTimeSelector_ReturnsValue()
    {
        var group = $"MinAsync-datetime-{Guid.NewGuid():N}";
        var earliest = new DateTime(2026, 1, 1, 8, 0, 0, DateTimeKind.Utc);
        var middle = new DateTime(2026, 1, 2, 8, 0, 0, DateTimeKind.Utc);
        var latest = new DateTime(2026, 1, 3, 8, 0, 0, DateTimeKind.Utc);
        var people = new[]
        {
            new PersonWithNumbers { FirstName = group, CreatedAt = middle },
            new PersonWithNumbers { FirstName = group, CreatedAt = latest },
            new PersonWithNumbers { FirstName = group, CreatedAt = earliest }
        };

        foreach (var person in people)
        {
            await Graph.CreateNodeAsync(person, null, TestContext.Current.CancellationToken);
        }

        var result = await Graph.Nodes<PersonWithNumbers>()
            .Where(p => p.FirstName == group)
            .MinAsync(p => p.CreatedAt, TestContext.Current.CancellationToken);

        Assert.Equal(earliest, result.ToUniversalTime());
    }

    [Fact]
    public async Task MaxAsync_NonNullableDateTimeSelector_ReturnsValue()
    {
        var group = $"MaxAsync-datetime-{Guid.NewGuid():N}";
        var earliest = new DateTime(2026, 1, 1, 8, 0, 0, DateTimeKind.Utc);
        var middle = new DateTime(2026, 1, 2, 8, 0, 0, DateTimeKind.Utc);
        var latest = new DateTime(2026, 1, 3, 8, 0, 0, DateTimeKind.Utc);
        var people = new[]
        {
            new PersonWithNumbers { FirstName = group, CreatedAt = middle },
            new PersonWithNumbers { FirstName = group, CreatedAt = latest },
            new PersonWithNumbers { FirstName = group, CreatedAt = earliest }
        };

        foreach (var person in people)
        {
            await Graph.CreateNodeAsync(person, null, TestContext.Current.CancellationToken);
        }

        var result = await Graph.Nodes<PersonWithNumbers>()
            .Where(p => p.FirstName == group)
            .MaxAsync(p => p.CreatedAt, TestContext.Current.CancellationToken);

        Assert.Equal(latest, result.ToUniversalTime());
    }

    [Fact]
    public async Task MinAsync_NonNullableGuidSelector_ReturnsValue()
    {
        var group = $"MinAsync-guid-{Guid.NewGuid():N}";
        var first = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var middle = Guid.Parse("00000000-0000-0000-0000-000000000002");
        var last = Guid.Parse("00000000-0000-0000-0000-000000000003");
        var people = new[]
        {
            new PersonWithNumbers { FirstName = group, TrackingId = middle },
            new PersonWithNumbers { FirstName = group, TrackingId = last },
            new PersonWithNumbers { FirstName = group, TrackingId = first }
        };

        foreach (var person in people)
        {
            await Graph.CreateNodeAsync(person, null, TestContext.Current.CancellationToken);
        }

        var result = await Graph.Nodes<PersonWithNumbers>()
            .Where(p => p.FirstName == group)
            .MinAsync(p => p.TrackingId, TestContext.Current.CancellationToken);

        Assert.Equal(first, result);
    }

    [Fact]
    public async Task MaxAsync_NonNullableGuidSelector_ReturnsValue()
    {
        var group = $"MaxAsync-guid-{Guid.NewGuid():N}";
        var first = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var middle = Guid.Parse("00000000-0000-0000-0000-000000000002");
        var last = Guid.Parse("00000000-0000-0000-0000-000000000003");
        var people = new[]
        {
            new PersonWithNumbers { FirstName = group, TrackingId = middle },
            new PersonWithNumbers { FirstName = group, TrackingId = last },
            new PersonWithNumbers { FirstName = group, TrackingId = first }
        };

        foreach (var person in people)
        {
            await Graph.CreateNodeAsync(person, null, TestContext.Current.CancellationToken);
        }

        var result = await Graph.Nodes<PersonWithNumbers>()
            .Where(p => p.FirstName == group)
            .MaxAsync(p => p.TrackingId, TestContext.Current.CancellationToken);

        Assert.Equal(last, result);
    }

    [Fact]
    public async Task OrderByTakeMinAsync_EmptyWindow_Throws()
    {
        var group = $"issue-185-186-empty-window-{Guid.NewGuid():N}";
        await Graph.CreateNodeAsync(
            new PersonWithNumbers { FirstName = group, Age = 10 },
            null,
            TestContext.Current.CancellationToken);

        var query = Graph.Nodes<PersonWithNumbers>()
            .Where(p => p.FirstName == group)
            .OrderBy(p => p.Age)
            .Take(0);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => query.MinAsync(p => p.Age, TestContext.Current.CancellationToken));

        Assert.Equal("Sequence contains no elements", exception.Message);
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
        await using var transaction = await Graph.GetTransactionAsync(TestContext.Current.CancellationToken);

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
