// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.CompatibilityTests;

/// <summary>
/// Contract for scalar-key grouping with per-group aggregation (#306): grouping a node set by a
/// scalar key and projecting <c>Key</c> and/or <c>Count/LongCount/Sum/Average/Min/Max</c> aggregates,
/// via both a <c>Select</c> over the <see cref="IGrouping{TKey,TElement}"/> and the result-selector
/// <c>GroupBy</c> overload. Providers that do not declare <see cref="GraphCapability.GroupByAggregation"/>
/// skip the whole interface.
/// </summary>
[RequiresCapability(GraphCapability.GroupByAggregation)]
public interface IGroupByTests : IGraphTest
{
    public record DepartmentMember : Node
    {
        public string Name { get; set; } = string.Empty;
        public string Department { get; set; } = string.Empty;
        public int Age { get; set; }
        public long Salary { get; set; }
    }

    private async Task SeedAsync(string group)
    {
        var employees = new[]
        {
            new DepartmentMember { Name = "Alice", Department = "Sales", Age = 30, Salary = 50000L },
            new DepartmentMember { Name = "Bob", Department = "Sales", Age = 40, Salary = 70000L },
            new DepartmentMember { Name = "Charlie", Department = "Engineering", Age = 25, Salary = 90000L },
            new DepartmentMember { Name = "Diana", Department = "Engineering", Age = 35, Salary = 110000L },
            new DepartmentMember { Name = "Eve", Department = "Engineering", Age = 45, Salary = 130000L },
            new DepartmentMember { Name = "Frank", Department = "Marketing", Age = 50, Salary = 60000L },
        };

        foreach (var employee in employees)
        {
            employee.Name = $"{group}:{employee.Name}";
            employee.Department = $"{group}:{employee.Department}";
            await Graph.CreateNodeAsync(employee, null, TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public async Task GroupByScalarKey_CountPerGroup()
    {
        var group = $"gb-count-{Guid.NewGuid():N}";
        await SeedAsync(group);

        var counts = await Graph.Nodes<DepartmentMember>()
            .Where(e => e.Name.StartsWith(group))
            .GroupBy(e => e.Department)
            .Select(g => new { Department = g.Key, Count = g.Count() })
            .ToListAsync(TestContext.Current.CancellationToken);

        var byDepartment = counts.ToDictionary(row => row.Department, row => row.Count);
        Assert.Equal(3, byDepartment.Count);
        Assert.Equal(2, byDepartment[$"{group}:Sales"]);
        Assert.Equal(3, byDepartment[$"{group}:Engineering"]);
        Assert.Equal(1, byDepartment[$"{group}:Marketing"]);
    }

    [Fact]
    public async Task GroupByScalarKey_LongCountPerGroup()
    {
        var group = $"gb-longcount-{Guid.NewGuid():N}";
        await SeedAsync(group);

        var counts = await Graph.Nodes<DepartmentMember>()
            .Where(e => e.Name.StartsWith(group))
            .GroupBy(e => e.Department)
            .Select(g => new { Department = g.Key, Count = g.LongCount() })
            .ToListAsync(TestContext.Current.CancellationToken);

        var byDepartment = counts.ToDictionary(row => row.Department, row => row.Count);
        Assert.Equal(3, byDepartment.Count);
        Assert.Equal(2L, byDepartment[$"{group}:Sales"]);
        Assert.Equal(3L, byDepartment[$"{group}:Engineering"]);
        Assert.Equal(1L, byDepartment[$"{group}:Marketing"]);
    }

    [Fact]
    public async Task GroupByScalarKey_LongCountResultSelectorOverload()
    {
        var group = $"gb-longcount-result-{Guid.NewGuid():N}";
        await SeedAsync(group);

        var summaries = await Graph.Nodes<DepartmentMember>()
            .Where(e => e.Name.StartsWith(group))
            .GroupBy(
                e => e.Department,
                (department, employees) => new { Department = department, Count = employees.LongCount() })
            .ToListAsync(TestContext.Current.CancellationToken);

        var byDepartment = summaries.ToDictionary(row => row.Department, row => row.Count);
        Assert.Equal(2L, byDepartment[$"{group}:Sales"]);
        Assert.Equal(3L, byDepartment[$"{group}:Engineering"]);
        Assert.Equal(1L, byDepartment[$"{group}:Marketing"]);
    }

    [Fact]
    public async Task GroupByScalarKey_LongCountEmptySource_YieldsNoGroups()
    {
        var group = $"gb-longcount-empty-{Guid.NewGuid():N}";

        var counts = await Graph.Nodes<DepartmentMember>()
            .Where(e => e.Name.StartsWith(group))
            .GroupBy(e => e.Department)
            .Select(g => new { Department = g.Key, Count = g.LongCount() })
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Empty(counts);
    }

    [Fact]
    public async Task GroupByScalarKey_PredicateLongCount_Throws()
    {
        var exception = await Assert.ThrowsAsync<NotSupportedException>(async () =>
            await Graph.Nodes<DepartmentMember>()
                .GroupBy(employee => employee.Department)
                .Select(group => new { group.Key, Seniors = group.LongCount(employee => employee.Age >= 40) })
                .ToListAsync(TestContext.Current.CancellationToken));

        Assert.Contains("LongCount(predicate)", exception.Message);
        Assert.Contains("filter before GroupBy", exception.Message);
    }

    [Fact]
    public async Task GroupByScalarKey_KeyOnly_YieldsDistinctKeys()
    {
        var group = $"gb-keys-{Guid.NewGuid():N}";
        await SeedAsync(group);

        var departments = await Graph.Nodes<DepartmentMember>()
            .Where(e => e.Name.StartsWith(group))
            .GroupBy(e => e.Department)
            .Select(g => g.Key)
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Equal(
            new HashSet<string> { $"{group}:Sales", $"{group}:Engineering", $"{group}:Marketing" },
            departments.ToHashSet());
    }

    [Fact]
    public async Task GroupByScalarKey_MultipleAggregates()
    {
        var group = $"gb-agg-{Guid.NewGuid():N}";
        await SeedAsync(group);

        var summaries = await Graph.Nodes<DepartmentMember>()
            .Where(e => e.Name.StartsWith(group))
            .GroupBy(e => e.Department)
            .Select(g => new
            {
                Department = g.Key,
                Count = g.Count(),
                TotalSalary = g.Sum(e => e.Salary),
                AverageAge = g.Average(e => e.Age),
                Youngest = g.Min(e => e.Age),
                Oldest = g.Max(e => e.Age),
            })
            .ToListAsync(TestContext.Current.CancellationToken);

        var engineering = summaries.Single(s => s.Department == $"{group}:Engineering");
        Assert.Equal(3, engineering.Count);
        Assert.Equal(330000L, engineering.TotalSalary);
        Assert.Equal(35.0, engineering.AverageAge, 1);
        Assert.Equal(25, engineering.Youngest);
        Assert.Equal(45, engineering.Oldest);
    }

    [Fact]
    public async Task GroupByScalarKey_ResultSelectorOverload()
    {
        var group = $"gb-result-{Guid.NewGuid():N}";
        await SeedAsync(group);

        var summaries = await Graph.Nodes<DepartmentMember>()
            .Where(e => e.Name.StartsWith(group))
            .GroupBy(
                e => e.Department,
                (department, employees) => new { Department = department, Count = employees.Count() })
            .ToListAsync(TestContext.Current.CancellationToken);

        var byDepartment = summaries.ToDictionary(row => row.Department, row => row.Count);
        Assert.Equal(2, byDepartment[$"{group}:Sales"]);
        Assert.Equal(3, byDepartment[$"{group}:Engineering"]);
        Assert.Equal(1, byDepartment[$"{group}:Marketing"]);
    }

    [Fact]
    public async Task GroupByScalarKey_ComputedKeyExpression()
    {
        var group = $"gb-computed-{Guid.NewGuid():N}";
        await SeedAsync(group);

        var byBand = await Graph.Nodes<DepartmentMember>()
            .Where(e => e.Name.StartsWith(group))
            .GroupBy(e => e.Age >= 40 ? "Senior" : "Junior")
            .Select(g => new { Band = g.Key, Count = g.Count() })
            .ToListAsync(TestContext.Current.CancellationToken);

        var byLabel = byBand.ToDictionary(row => row.Band, row => row.Count);
        Assert.Equal(3, byLabel["Senior"]); // Bob(40), Eve(45), Frank(50)
        Assert.Equal(3, byLabel["Junior"]); // Alice(30), Charlie(25), Diana(35)
    }

    [Fact]
    public async Task GroupByScalarKey_EmptySource_YieldsNoGroups()
    {
        var group = $"gb-empty-{Guid.NewGuid():N}";

        var counts = await Graph.Nodes<DepartmentMember>()
            .Where(e => e.Name.StartsWith(group))
            .GroupBy(e => e.Department)
            .Select(g => new { Department = g.Key, Count = g.Count() })
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Empty(counts);
    }

    [Fact]
    public async Task GroupByScalarKey_PreCancelledToken_Throws()
    {
        var group = $"gb-cancel-{Guid.NewGuid():N}";
        await SeedAsync(group);

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await Graph.Nodes<DepartmentMember>()
                .Where(e => e.Name.StartsWith(group))
                .GroupBy(e => e.Department)
                .Select(g => new { Department = g.Key, Count = g.Count() })
                .ToListAsync(cts.Token));
    }

    [Fact]
    public async Task GroupByEntityKey_Throws()
    {
        var group = $"gb-entity-{Guid.NewGuid():N}";
        await SeedAsync(group);

        await Assert.ThrowsAsync<NotSupportedException>(async () =>
            await Graph.Nodes<DepartmentMember>()
                .Where(e => e.Name.StartsWith(group))
                .GroupBy(e => e)
                .Select(g => new { Count = g.Count() })
                .ToListAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GroupByThenOuterOrdering_Throws()
    {
        var group = $"gb-order-{Guid.NewGuid():N}";
        await SeedAsync(group);

        await Assert.ThrowsAsync<NotSupportedException>(async () =>
            await Graph.Nodes<DepartmentMember>()
                .Where(e => e.Name.StartsWith(group))
                .GroupBy(e => e.Department)
                .Select(g => new { Department = g.Key, Count = g.Count() })
                .OrderBy(row => row.Department)
                .ToListAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GroupByThenOuterFilter_Throws()
    {
        var group = $"gb-filter-{Guid.NewGuid():N}";
        await SeedAsync(group);

        await Assert.ThrowsAsync<NotSupportedException>(async () =>
            await Graph.Nodes<DepartmentMember>()
                .Where(e => e.Name.StartsWith(group))
                .GroupBy(e => e.Department)
                .Select(g => new { Department = g.Key, Count = g.Count() })
                .Where(row => row.Count > 1)
                .ToListAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GroupByAfterComplexPropertyNavigation_Throws()
    {
        await Assert.ThrowsAsync<NotSupportedException>(async () =>
            await Graph.Nodes<PersonWithComplexProperties>()
                .GroupBy(person => person.Address.City)
                .Select(group => new { City = group.Key, Count = group.Count() })
                .ToListAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GroupByScalarKey_CollectionProjection_Throws()
    {
        await Assert.ThrowsAsync<NotSupportedException>(async () =>
            await Graph.Nodes<DepartmentMember>()
                .GroupBy(employee => employee.Department)
                .Select(group => group.Select(employee => employee.Name).ToList())
                .ToListAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GroupByScalarKey_PredicateCount_Throws()
    {
        await Assert.ThrowsAsync<NotSupportedException>(async () =>
            await Graph.Nodes<DepartmentMember>()
                .GroupBy(employee => employee.Department)
                .Select(group => new { group.Key, Seniors = group.Count(employee => employee.Age >= 40) })
                .ToListAsync(TestContext.Current.CancellationToken));
    }
}
