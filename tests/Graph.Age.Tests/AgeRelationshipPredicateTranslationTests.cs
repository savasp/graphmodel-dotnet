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
    public async Task DeclinesRelationshipExistenceAtTranslationTime()
    {
        await using var store = new AgeGraphStore(
            "Host=localhost;Port=5455;Username=postgres;Password=postgres;Database=postgres",
            "translation");
        var query = store.Graph.Nodes<Person>()
            .WhereHasRelationship<Person, Knows>(
                GraphTraversalDirection.Both,
                relationship => relationship.Since >= DateTime.UnixEpoch);

        var exception = Assert.Throws<GraphQueryTranslationException>(() =>
        {
            var visitor = new CypherQueryVisitor(query.ElementType);
            visitor.Visit(query.Expression);
        });

        Assert.Contains(nameof(GraphCapability.RelationshipPredicates), exception.Message, StringComparison.Ordinal);
        Assert.Contains("Apache AGE", exception.Message, StringComparison.Ordinal);
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
}
