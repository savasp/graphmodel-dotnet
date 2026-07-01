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

namespace Cvoya.Graph.Model.Age.Tests.GraphModelTests;

using System;
using System.Linq;
using System.Threading.Tasks;
using Cvoya.Graph.Model;
using Cvoya.Graph.Model.Age.Tests.Infrastructure;
using Cvoya.Graph.Model.Tests;
using Xunit;

/// <summary>
/// Integration tests targeting uncovered code paths in the AGE provider.
/// Exercises edge cases in property type conversions, label extraction, and query patterns.
/// </summary>
public sealed class CoverageEdgeCaseTests(TestInfrastructureFixture fixture) :
    AgeTest(fixture)
{
    /// <summary>
    /// Node with property types that exercise edge case conversion paths.
    /// Uses only types supported by the generated serializer (no Guid, DateTimeOffset).
    /// </summary>
    public sealed record AllTypesNode : Node
    {
        public override IReadOnlyList<string> Labels { get; set; } = new List<string> { nameof(AllTypesNode) };
        public bool IsActive { get; init; }
        public long BigValue { get; init; }
        public double DoubleValue { get; init; }
        public float FloatValue { get; init; }
        public decimal DecimalValue { get; init; }
        public int? NullableInt { get; init; }
        public bool? NullableBool { get; init; }
    }

    /// <summary>
    /// Relationship with numeric property.
    /// </summary>
    public sealed record WeightedKnows : Relationship
    {
        public WeightedKnows() : base(string.Empty, string.Empty) { }
        public WeightedKnows(string startNodeId, string endNodeId) : base(startNodeId, endNodeId) { }

        public override string Type { get; set; } = "WEIGHTED_KNOWS";
        public double Weight { get; init; }
    }

    [Fact]
    public async Task CanCreateAndRetrieveNodeWithAllPropertyTypes()
    {
        // Arrange — use explicit values that exercise specific conversion paths
        var node = new AllTypesNode
        {
            IsActive = true,
            BigValue = 9_007_199_254_740_991L,  // near long.MaxValue to exercise long path
            DoubleValue = 3.141592653589793,
            FloatValue = 2.71828f,
            DecimalValue = 12345.6789m,
            NullableInt = 42,
            NullableBool = false
        };

        // Act — create and retrieve
        await Graph.CreateNodeAsync(node, null, TestContext.Current.CancellationToken);
        var retrieved = await Graph.GetNodeAsync<AllTypesNode>(node.Id, null, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(retrieved);
        Assert.Equal(node.IsActive, retrieved!.IsActive);
        Assert.Equal(node.BigValue, retrieved.BigValue);
        Assert.Equal(node.DoubleValue, retrieved.DoubleValue, 6);  // precision tolerance for float→double
        Assert.Equal(node.FloatValue, retrieved.FloatValue, 4);
        Assert.Equal(node.DecimalValue, retrieved.DecimalValue);
        Assert.Equal(node.NullableInt, retrieved.NullableInt);
        Assert.Equal(node.NullableBool, retrieved.NullableBool);
    }

    [Fact]
    public async Task CanCreateAndRetrieveNodeWithNullableNullValues()
    {
        // Arrange
        var node = new AllTypesNode
        {
            IsActive = false,
            BigValue = 0,
            DoubleValue = 0.0,
            FloatValue = 0.0f,
            DecimalValue = 0m,
            NullableInt = null,    // explicit null
            NullableBool = null    // explicit null
        };

        // Act
        await Graph.CreateNodeAsync(node, null, TestContext.Current.CancellationToken);
        var retrieved = await Graph.GetNodeAsync<AllTypesNode>(node.Id, null, TestContext.Current.CancellationToken);

        // Assert — nullables should remain null
        Assert.NotNull(retrieved);
        Assert.Null(retrieved!.NullableInt);
        Assert.Null(retrieved.NullableBool);
    }

    [Fact]
    public async Task CanCreateAndRetrieveNodeWithBoolEdgeCases()
    {
        // Arrange
        var node = new AllTypesNode
        {
            IsActive = false,
            BigValue = 1,
            DoubleValue = 0.5,
            FloatValue = 0.1f,
            DecimalValue = 0.01m
        };

        // Act
        await Graph.CreateNodeAsync(node, null, TestContext.Current.CancellationToken);

        // Query by the boolean property
        var results = await (await Graph.NodesAsync<AllTypesNode>())
            .Where(n => n.Id == node.Id && n.IsActive == false)
            .ToListAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Single(results);
        Assert.False(results[0].IsActive);
    }

    [Fact]
    public async Task CanCreateRelationshipWithNumericProperties()
    {
        // Arrange
        var alice = new Person { FirstName = "Alice", LastName = "Test" };
        var bob = new Person { FirstName = "Bob", LastName = "Test" };

        await Graph.CreateNodeAsync(alice, null, TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(bob, null, TestContext.Current.CancellationToken);

        var rel = new WeightedKnows(alice.Id, bob.Id) { Weight = 0.75 };

        // Act
        await Graph.CreateRelationshipAsync(rel, null, TestContext.Current.CancellationToken);
        var retrieved = await Graph.GetRelationshipAsync<WeightedKnows>(rel.Id, null, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(retrieved);
        Assert.Equal(0.75, retrieved!.Weight, 4);
    }

    [Fact]
    public async Task CanQueryWithDistinctOperator()
    {
        // Arrange
        var p1 = new Person { FirstName = "Alice", LastName = "Unique" };
        var p2 = new Person { FirstName = "Bob", LastName = "Unique" };
        var p3 = new Person { FirstName = "Alice", LastName = "Different" };

        await Graph.CreateNodeAsync(p1, null, TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(p2, null, TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(p3, null, TestContext.Current.CancellationToken);

        // Act — query with Distinct (requires explicit projection)
        var distinctNames = await (await Graph.NodesAsync<Person>())
            .Where(p => p.LastName == "Unique")
            .Select(p => p.LastName)
            .Distinct()
            .ToListAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Single(distinctNames);  // Only one distinct "Unique" value
        Assert.Equal("Unique", distinctNames[0]);
    }

    [Fact]
    public async Task CanQueryNodesWithSkip()
    {
        // Arrange
        await Graph.CreateNodeAsync(new Person { FirstName = "Alpha", LastName = "SkipTest" }, null, TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(new Person { FirstName = "Beta", LastName = "SkipTest" }, null, TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(new Person { FirstName = "Gamma", LastName = "SkipTest" }, null, TestContext.Current.CancellationToken);

        // Act — query with Skip and Take
        var results = await (await Graph.NodesAsync<Person>())
            .Where(p => p.LastName == "SkipTest")
            .OrderBy(p => p.FirstName)
            .Skip(1)
            .Take(2)
            .ToListAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(2, results.Count);
        Assert.Equal("Beta", results[0].FirstName);
        Assert.Equal("Gamma", results[1].FirstName);
    }

    [Fact]
    public async Task CanQueryWithFirstOperator()
    {
        // Arrange
        await Graph.CreateNodeAsync(new Person { FirstName = "First", LastName = "FirstTest" }, null, TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(new Person { FirstName = "Second", LastName = "FirstTest" }, null, TestContext.Current.CancellationToken);

        // Act — query with FirstAsync
        var first = await (await Graph.NodesAsync<Person>())
            .Where(p => p.LastName == "FirstTest")
            .OrderBy(p => p.FirstName)
            .FirstAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(first);
        Assert.Equal("First", first.FirstName);
    }

    [Fact]
    public async Task CanRetrieveNodeViaInheritedDerivedLabel()
    {
        // Arrange — create a Manager which has an extra label from Person
        var mgr = new Manager
        {
            FirstName = "Mgr",
            LastName = "Lead",
            Department = "Engineering",
            TeamSize = 5
        };

        await Graph.CreateNodeAsync(mgr, null, TestContext.Current.CancellationToken);

        // Act — retrieve as base type Person (checks label resolution)
        var asPerson = await Graph.GetNodeAsync<Person>(mgr.Id, null, TestContext.Current.CancellationToken);
        var asManager = await Graph.GetNodeAsync<Manager>(mgr.Id, null, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(asPerson);
        Assert.NotNull(asManager);
        Assert.Equal("Mgr", asManager!.FirstName);
        Assert.Equal("Lead", asManager.LastName);
        Assert.Equal("Engineering", asManager.Department);
    }

    [Fact]
    public async Task CanQueryWithLongAndDoubleFilter()
    {
        // Arrange
        var node = new AllTypesNode
        {
            IsActive = true,
            BigValue = 100_000_000_000L,
            DoubleValue = 99.99,
            FloatValue = 1.0f,
            DecimalValue = 1m
        };

        await Graph.CreateNodeAsync(node, null, TestContext.Current.CancellationToken);

        // Act — query with long comparison in LINQ
        var results = await (await Graph.NodesAsync<AllTypesNode>())
            .Where(n => n.BigValue >= 100_000_000_000L && n.DoubleValue < 100.0)
            .ToListAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains(results, r => r.Id == node.Id);
    }

    [Fact]
    public async Task CanQueryNodesWithAnyOperator()
    {
        // Arrange
        await Graph.CreateNodeAsync(new Person { FirstName = "AnyTest", LastName = "AnyLastName" }, null, TestContext.Current.CancellationToken);

        // Act — AnyAsync should return true for existing filter match
        var exists = await (await Graph.NodesAsync<Person>())
            .AnyAsync(p => p.LastName == "AnyLastName", TestContext.Current.CancellationToken);

        // Assert
        Assert.True(exists);

        // Should return false for non-matching filter
        var notExists = await (await Graph.NodesAsync<Person>())
            .AnyAsync(p => p.LastName == "NonExistentLastName", TestContext.Current.CancellationToken);

        Assert.False(notExists);
    }
}
