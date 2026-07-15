// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Age.Tests;

using Cvoya.Graph.Age.Querying.Cypher.Visitors.Core;
using Cvoya.Graph.CompatibilityTests;

public sealed class AgeCorrelatedProjectionPassTests
{
    [Fact]
    public async Task LowersCorrelatedCollectionsAndCountsToOneGroupedMatch()
    {
        await using var store = new AgeGraphStore(
            "Host=localhost;Port=5455;Username=postgres;Password=postgres;Database=postgres",
            "translation");
        var query = store.Graph.Nodes<Person>()
            .Where(person => person.FirstName == "Alice")
            .PathSegments<Person, Knows, Person>()
            .GroupBy(segment => segment.StartNode)
            .Select(group => new
            {
                Name = group.Key.FirstName,
                FriendCount = group.Count(),
                FriendNames = group.Select(segment => segment.EndNode.FirstName).ToList(),
            });

        var rendered = Translate(query);

        Assert.DoesNotContain("EXISTS {", rendered.Text);
        Assert.DoesNotContain("COUNT {", rendered.Text);
        Assert.DoesNotContain("CALL {", rendered.Text);
        Assert.DoesNotContain("[(", rendered.Text);
        Assert.Contains("MATCH (src)-[r]->(tgt)", rendered.Text);
        Assert.Contains("count(r) AS __age_projection1", rendered.Text);
        Assert.Contains("collect(tgt.FirstName) AS __age_projection2", rendered.Text);
    }

    [Fact]
    public async Task LowersAggregateAndOrderedSubqueriesToGroupedExpressions()
    {
        await using var store = new AgeGraphStore(
            "Host=localhost;Port=5455;Username=postgres;Password=postgres;Database=postgres",
            "translation");
        var aggregateQuery = store.Graph.Nodes<Person>()
            .PathSegments<Person, Knows, Person>()
            .GroupBy(segment => segment.StartNode)
            .Select(group => new
            {
                AverageAge = group.Average(segment => segment.EndNode.Age),
                OldestAge = group.Max(segment => segment.EndNode.Age),
            });
        var orderedQuery = store.Graph.Nodes<Person>()
            .PathSegments<Person, Knows, Person>()
            .GroupBy(segment => segment.StartNode)
            .Select(group => new
            {
                Friends = group
                    .OrderBy(segment => segment.EndNode.Age)
                    .Select(segment => segment.EndNode.FirstName)
                    .ToList(),
            });

        var aggregate = Translate(aggregateQuery).Text;
        var ordered = Translate(orderedQuery).Text;

        Assert.DoesNotContain("CALL {", aggregate);
        Assert.Contains("avg(tgt.Age) AS __age_projection0", aggregate);
        Assert.Contains("max(tgt.Age) AS __age_projection1", aggregate);
        Assert.DoesNotContain("CALL {", ordered);
        Assert.True(
            ordered.IndexOf("ORDER BY tgt.Age", StringComparison.Ordinal) <
            ordered.IndexOf("collect(tgt.FirstName)", StringComparison.Ordinal));
    }

    [Fact]
    public async Task LowersNestedGroupingToTwoAgeSupportedGroupingStages()
    {
        await using var store = new AgeGraphStore(
            "Host=localhost;Port=5455;Username=postgres;Password=postgres;Database=postgres",
            "translation");
        var query = store.Graph.Nodes<Person>()
            .PathSegments<Person, Knows, Person>()
            .GroupBy(segment => segment.StartNode)
            .Select(group => new
            {
                AgeGroups = group
                    .GroupBy(segment => segment.EndNode.Age >= 30 ? "Senior" : "Junior")
                    .Select(ageGroup => new
                    {
                        Group = ageGroup.Key,
                        Count = ageGroup.Count(),
                        Names = ageGroup.Select(segment => segment.EndNode.FirstName).ToList(),
                    })
                    .ToList(),
            });

        var rendered = Translate(query).Text;

        Assert.DoesNotContain("CALL {", rendered);
        Assert.Contains("WITH src, __key AS __key, count(tgt) AS __agg1", rendered);
        Assert.Contains("collect({ Group: __key, Count: __agg1, Names: __agg2 }) AS __group0", rendered);
    }

    [Fact]
    public async Task LowersRelationshipCountsSequentiallyAndReusesCountForOrdering()
    {
        await using var store = new AgeGraphStore(
            "Host=localhost;Port=5455;Username=postgres;Password=postgres;Database=postgres",
            "translation");
        var query = store.Graph.Nodes<Person>()
            .Select(person => new
            {
                person.FirstName,
                Outgoing = person.CountRelationships<Knows>(GraphTraversalDirection.Outgoing),
                Incoming = person.CountRelationships<Knows>(GraphTraversalDirection.Incoming),
                Total = person.CountRelationships<Knows>(GraphTraversalDirection.Both),
            })
            .OrderByDescending(result => result.Outgoing);

        var rendered = Translate(query).Text;

        Assert.DoesNotContain("COUNT {", rendered);
        Assert.Equal(3, rendered.Split("OPTIONAL MATCH", StringSplitOptions.None).Length - 1);
        Assert.Contains("count(__age_count0_relationship0) AS __age_count0", rendered);
        Assert.Contains("ORDER BY __age_count0 DESC", rendered);
    }

    [Fact]
    public async Task LowersComplexCollectionSizeAndEscapesItsReservedProjectionAlias()
    {
        await using var store = new AgeGraphStore(
            "Host=localhost;Port=5455;Username=postgres;Password=postgres;Database=postgres",
            "translation");
        var query = store.Graph.Nodes<Kennel>()
            .Select(kennel => new { kennel.Name, Count = kennel.Animals.Count });

        var rendered = Translate(query);

        Assert.DoesNotContain("COUNT {", rendered.Text);
        Assert.Contains("count(__age_count0_relationship0) AS __age_count0", rendered.Text);
        Assert.Contains("__age_count0 AS `Count`", rendered.Text);
        Assert.Equal(["Name", "Count"], rendered.ProjectionColumns);
    }

    private static Querying.Cypher.CypherQuery Translate(IQueryable query)
    {
        var visitor = new CypherQueryVisitor(query.ElementType);
        visitor.Visit(query.Expression);
        return visitor.Query;
    }
}
