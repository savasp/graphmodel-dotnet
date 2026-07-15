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

    [Fact]
    public async Task FiltersCorrelatedCallAggregatesConditionally()
    {
        await using var store = new AgeGraphStore(
            "Host=localhost;Port=5455;Username=postgres;Password=postgres;Database=postgres",
            "translation");
        var query = store.Graph.Nodes<Person>()
            .PathSegments<Person, Knows, Person>()
            .GroupBy(segment => segment.StartNode)
            .Select(group => new
            {
                AverageAdultAge = group
                    .Where(segment => segment.EndNode.Age >= 18)
                    .Average(segment => segment.EndNode.Age),
            });

        var rendered = Translate(query).Text;

        // The shared traversal match is unfiltered, so the aggregate must carry its own filter as
        // conditional aggregation rather than silently widening to every traversal row.
        Assert.DoesNotContain("CALL {", rendered);
        Assert.Contains("avg(CASE WHEN tgt.Age >= $p0 THEN tgt.Age ELSE null END) AS __age_projection0", rendered);
    }

    [Fact]
    public async Task FiltersOrderedCollectionsConditionally()
    {
        await using var store = new AgeGraphStore(
            "Host=localhost;Port=5455;Username=postgres;Password=postgres;Database=postgres",
            "translation");
        var query = store.Graph.Nodes<Person>()
            .PathSegments<Person, Knows, Person>()
            .GroupBy(segment => segment.StartNode)
            .Select(group => new
            {
                AdultFriends = group
                    .Where(segment => segment.EndNode.Age >= 18)
                    .OrderBy(segment => segment.EndNode.Age)
                    .Select(segment => segment.EndNode.FirstName)
                    .ToList(),
            });

        var rendered = Translate(query).Text;

        Assert.DoesNotContain("CALL {", rendered);
        Assert.Contains("collect(CASE WHEN tgt.Age >= $p0 THEN tgt.FirstName ELSE null END)", rendered);
        Assert.True(
            rendered.IndexOf("ORDER BY tgt.Age", StringComparison.Ordinal) <
            rendered.IndexOf("collect(", StringComparison.Ordinal));
    }

    [Fact]
    public async Task GuardsSegmentFilteredGroupingRows()
    {
        await using var store = new AgeGraphStore(
            "Host=localhost;Port=5455;Username=postgres;Password=postgres;Database=postgres",
            "translation");
        var query = store.Graph.Nodes<Person>()
            .PathSegments<Person, Knows, Person>()
            .Where(segment => segment.EndNode.Age >= 18)
            .GroupBy(segment => segment.StartNode)
            .Select(group => new
            {
                Name = group.Key.FirstName,
                Friends = group.Select(segment => segment.EndNode.FirstName).ToList(),
            });

        var rendered = Translate(query).Text;

        // A source whose traversal rows all fail the segment predicate must produce no result row
        // (LINQ yields no group for it), not an empty collection.
        Assert.Contains("count(CASE WHEN tgt.Age >= $p0 THEN r ELSE null END) AS __age_anchor_rows", rendered);
        Assert.Contains("WHERE __age_anchor_rows > 0", rendered);
        Assert.Contains("collect(CASE WHEN tgt.Age >= $p0 THEN tgt.FirstName ELSE null END)", rendered);
    }

    [Fact]
    public async Task RejectsFilteredNestedGrouping()
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
                    .Where(segment => segment.EndNode.Age >= 18)
                    .GroupBy(segment => segment.EndNode.Age >= 30 ? "Senior" : "Junior")
                    .Select(ageGroup => new { Group = ageGroup.Key, Count = ageGroup.Count() })
                    .ToList(),
            });

        var exception = Assert.Throws<GraphQueryTranslationException>(() => Translate(query));

        Assert.Contains("a filtered nested correlated grouping", exception.Message);
    }

    [Fact]
    public async Task RejectsMultipleOrderedCollections()
    {
        await using var store = new AgeGraphStore(
            "Host=localhost;Port=5455;Username=postgres;Password=postgres;Database=postgres",
            "translation");
        var query = store.Graph.Nodes<Person>()
            .PathSegments<Person, Knows, Person>()
            .GroupBy(segment => segment.StartNode)
            .Select(group => new
            {
                Names = group
                    .OrderBy(segment => segment.EndNode.Age)
                    .Select(segment => segment.EndNode.FirstName)
                    .ToList(),
                Ages = group
                    .OrderBy(segment => segment.EndNode.Age)
                    .Select(segment => segment.EndNode.Age)
                    .ToList(),
            });

        var exception = Assert.Throws<GraphQueryTranslationException>(() => Translate(query));

        Assert.Contains("multiple ordered correlated collections", exception.Message);
    }

    [Fact]
    public async Task LowersExistenceFiltersToOptionalMatchCounts()
    {
        await using var store = new AgeGraphStore(
            "Host=localhost;Port=5455;Username=postgres;Password=postgres;Database=postgres",
            "translation");
        var query = store.Graph.Nodes<Kennel>()
            .Where(kennel => kennel.Animals.Any(animal => animal.Name == "Rex"))
            .Select(kennel => kennel.Name);

        var rendered = Translate(query).Text;

        // AGE parses EXISTS { } but silently matches nothing, so the existence filter must become a
        // grouped optional-match count compared against zero.
        Assert.DoesNotContain("EXISTS {", rendered);
        Assert.Contains("OPTIONAL MATCH", rendered);
        Assert.Contains("count(__age_count0_relationship0) AS __age_count0", rendered);
        Assert.Contains("__age_count0 > 0", rendered);
    }

    [Fact]
    public async Task LowersCountFiltersToOptionalMatchCounts()
    {
        await using var store = new AgeGraphStore(
            "Host=localhost;Port=5455;Username=postgres;Password=postgres;Database=postgres",
            "translation");
        var query = store.Graph.Nodes<Person>()
            .Where(person => person.CountRelationships<Knows>(GraphTraversalDirection.Outgoing) > 2)
            .Select(person => person.FirstName);

        var rendered = Translate(query).Text;

        Assert.DoesNotContain("COUNT {", rendered);
        Assert.Contains("OPTIONAL MATCH", rendered);
        Assert.Contains("WHERE __age_count0 > $p0", rendered);
    }

    [Fact]
    public async Task LowersGroupKeyDegreeCountsAfterGrouping()
    {
        await using var store = new AgeGraphStore(
            "Host=localhost;Port=5455;Username=postgres;Password=postgres;Database=postgres",
            "translation");
        var query = store.Graph.Nodes<Person>()
            .PathSegments<Person, Knows, Person>()
            .GroupBy(segment => segment.StartNode)
            .Select(group => new
            {
                FriendNames = group.Select(segment => segment.EndNode.FirstName).ToList(),
                LivesAtCount = group.Key.CountRelationships<LivesAt>(GraphTraversalDirection.Outgoing),
            });

        var rendered = Translate(query).Text;

        // The degree count traverses a different pattern than the correlated collection, so it is
        // lowered as an optional-match stage after the grouping.
        Assert.DoesNotContain("COUNT {", rendered);
        Assert.Contains("collect(tgt.FirstName) AS __age_projection0", rendered);
        Assert.Contains("count(__age_count0_relationship0) AS __age_count0", rendered);
        Assert.True(
            rendered.IndexOf("collect(", StringComparison.Ordinal) <
            rendered.IndexOf("OPTIONAL MATCH", StringComparison.Ordinal));
    }

    [Fact]
    public async Task LowersAnchoredCountProjectionUnderExistenceFilter()
    {
        await using var store = new AgeGraphStore(
            "Host=localhost;Port=5455;Username=postgres;Password=postgres;Database=postgres",
            "translation");
        var query = store.Graph.Nodes<Kennel>()
            .Where(kennel => kennel.Animals.Any(animal => animal.Name == "Rex"))
            .Select(kennel => new { kennel.Name, Size = kennel.Animals.Count });

        var rendered = Translate(query).Text;

        // The existence filter and the size projection traverse the same pattern: one match feeds
        // both, the filter becomes a row guard, and the size counts every row (not just "Rex").
        Assert.DoesNotContain("EXISTS {", rendered);
        Assert.DoesNotContain("COUNT {", rendered);
        Assert.Contains("count(src_animals) AS __age_projection1", rendered);
        Assert.Contains("count(CASE WHEN src_animals.Name = $p0 THEN src_animals ELSE null END) AS __age_anchor_rows", rendered);
        Assert.Contains("WHERE __age_anchor_rows > 0", rendered);
    }

    private static Querying.Cypher.CypherQuery Translate(IQueryable query)
    {
        var visitor = new CypherQueryVisitor(query.ElementType);
        visitor.Visit(query.Expression);
        return visitor.Query;
    }
}
