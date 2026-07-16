// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Age.Tests;

using Cvoya.Graph.Age.Querying.Cypher.Visitors.Core;
using Cvoya.Graph.CompatibilityTests;

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
}
