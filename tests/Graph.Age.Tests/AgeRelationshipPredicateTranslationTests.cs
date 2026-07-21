// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Age.Tests;

using Cvoya.Graph.Age.Querying.Cypher;
using Cvoya.Graph.Age.Querying.Cypher.Visitors.Core;
using Cvoya.Graph.CompatibilityTests;
using Cvoya.Graph.Cypher;
using Cvoya.Graph.Cypher.Planning;
using Cvoya.Graph.Querying;

public sealed class AgeRelationshipPredicateTranslationTests
{
    [Fact]
    public async Task LowersRelationshipExistenceToOptionalMatchCount()
    {
        await using var store = new AgeGraphStore(
            "Host=localhost;Port=5455;Username=postgres;Password=postgres;Database=postgres",
            "translation");
        var epoch = DateTime.UnixEpoch;
        var query = store.Graph.Nodes<Person>()
            .WhereHasRelationship<Person, Knows>(
                GraphTraversalDirection.Both,
                relationship => relationship.Since >= epoch);

        var text = Translate(query).Text;

        Assert.DoesNotContain("EXISTS {", text, StringComparison.Ordinal);
        Assert.Contains("OPTIONAL MATCH", text, StringComparison.Ordinal);
        Assert.Contains("count(CASE WHEN", text, StringComparison.Ordinal);
        Assert.Contains("__age_count0 > 0", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LowersVariablePathRelationshipPredicateToListFilter()
    {
        await using var store = new AgeGraphStore(
            "Host=localhost;Port=5455;Username=postgres;Password=postgres;Database=postgres",
            "translation");
        var cutoff = DateTime.UnixEpoch;
        var query = store.Graph.Nodes<Person>()
            .Traverse<Knows, Person>(options => options
                .Depth(1, 3)
                .WhereRelationship<Knows>(relationship => relationship.Since >= cutoff));

        var text = Translate(query).Text;

        Assert.DoesNotContain("ALL(", text, StringComparison.Ordinal);
        Assert.Contains(
            "size([__age_relationship_hop0 IN range(0, size(r) - 1) WHERE " +
            "r[toInteger(__age_relationship_hop0)].Since >= $p0]) = size(r)",
            text,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task CommandSelection_LowersRelationshipExistenceToOptionalMatchCount()
    {
        await using var store = new AgeGraphStore(
            "Host=localhost;Port=5455;Username=postgres;Password=postgres;Database=postgres",
            "translation");
        var epoch = DateTime.UnixEpoch;
        var query = store.Graph.Nodes<Person>()
            .WhereHasRelationship<Person, Knows>(
                GraphTraversalDirection.Both,
                relationship => relationship.Since >= epoch);
        var selection = new GraphElementSelectionModel(
            GraphQueryModelBuilder.Build(query.Expression),
            GraphElementSelectionMode.Set);

        var statement = new CypherQueryPlanner(AgeDialect.CommandPlanningInstance).Plan(selection);
        statement = CypherQueryVisitor.LowerStatement(statement);
        var text = new CypherRenderer(AgeDialect.Instance).Render(statement).Text;

        Assert.DoesNotContain("EXISTS {", text, StringComparison.Ordinal);
        Assert.Contains("OPTIONAL MATCH", text, StringComparison.Ordinal);
        Assert.Contains("AS __age_count0", text, StringComparison.Ordinal);
        Assert.Contains("__age_count0 > 0", text, StringComparison.Ordinal);
    }

    private static Cvoya.Graph.Age.Querying.Cypher.CypherQuery Translate(IQueryable query)
    {
        var visitor = new CypherQueryVisitor(query.ElementType);
        visitor.Visit(query.Expression);
        return visitor.Query;
    }
}
