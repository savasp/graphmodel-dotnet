// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Age.Tests;

using Cvoya.Graph.Age.Querying.Cypher.Visitors.Core;
using Cvoya.Graph.CompatibilityTests;

public sealed class AgeOptionalTraversalTranslationTests
{
    [Fact]
    public async Task RendersSourceFilterBeforeOptionalMatchAndNullableProjection()
    {
        await using var store = new AgeGraphStore(
            "Host=localhost;Port=5455;Username=postgres;Password=postgres;Database=postgres",
            "translation");
        var query = store.Graph.Nodes<Person>()
            .Where(person => person.FirstName == "source")
            .OptionalTraverse<Knows, Person>();

        var visitor = new CypherQueryVisitor(query.ElementType);
        visitor.Visit(query.Expression);
        var text = visitor.Query.Text;

        Assert.Contains("OPTIONAL MATCH", text, StringComparison.Ordinal);
        Assert.True(
            text.IndexOf("WHERE", StringComparison.Ordinal) <
            text.IndexOf("OPTIONAL MATCH (src)-[", StringComparison.Ordinal));
        Assert.Contains("AS Source", text, StringComparison.Ordinal);
        Assert.Contains("AS Target", text, StringComparison.Ordinal);
    }
}
