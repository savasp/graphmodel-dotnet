// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Age.Tests;

using Cvoya.Graph.Age.Querying.Cypher.Visitors.Core;
using Cvoya.Graph.CompatibilityTests;

public sealed class AgeShortestPathTranslationTests
{
    [Fact]
    public async Task DeclinesShortestPathAtTranslationTime()
    {
        await using var store = new AgeGraphStore(
            "Host=localhost;Port=5455;Username=postgres;Password=postgres;Database=postgres",
            "translation");
        var query = store.Graph.Nodes<Person>()
            .ShortestPath<Knows, Person>(person => person.FirstName == "target");

        var exception = Assert.Throws<GraphQueryTranslationException>(() =>
        {
            var visitor = new CypherQueryVisitor(query.ElementType);
            visitor.Visit(query.Expression);
        });

        Assert.Contains(nameof(GraphCapability.ShortestPath), exception.Message, StringComparison.Ordinal);
        Assert.Contains("Apache AGE", exception.Message, StringComparison.Ordinal);
    }
}
