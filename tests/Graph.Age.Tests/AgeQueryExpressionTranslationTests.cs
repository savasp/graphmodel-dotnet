// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Age.Tests;

using Cvoya.Graph.Age.Querying.Cypher.Visitors.Core;
using Cvoya.Graph.CompatibilityTests;

public sealed class AgeQueryExpressionTranslationTests
{
    [Fact]
    public async Task ComposedPredicateProjectionOrderingAndPaging_UsesAgeLoweredCypher()
    {
        await using var store = new AgeGraphStore(
            "Host=localhost;Port=5455;Username=postgres;Password=postgres;Database=postgres",
            "translation");
        var scope = "scope";
        var categories = new[] { "A", "C" };
        var prefix = "a";
        var targetScore = 15;
        var tolerance = 6;
        var year = 2024;
        var bonus = 2;
        var fallback = "(missing)";

#pragma warning disable CA1862 // Exercise the provider-translated normalization pipeline.
        var query = store.Graph.Nodes<QueryExpressionCoverageNode>()
            .Where(node =>
                node.Scope == scope &&
                categories.Contains(node.Category) &&
                (node.OptionalText == null || node.Name.Trim().ToLowerInvariant().StartsWith(prefix)) &&
                !node.Tags.Contains("blocked") &&
                Math.Abs(node.Score - targetScore) <= tolerance &&
                node.OccurredAt.Year == year)
            .OrderBy(node => node.Category)
            .ThenByDescending(node => node.Score)
            .Skip(1)
            .Take(3)
            .Select(node => new
            {
                node.Name,
                AdjustedScore = node.Score + bonus,
                Display = node.OptionalText == null ? fallback : node.OptionalText.ToUpperInvariant(),
            });
#pragma warning restore CA1862

        var translated = Translate(query);

        Assert.Contains("MATCH (src)", translated.Text, StringComparison.Ordinal);
        Assert.Contains("src.Category IN $p1", translated.Text, StringComparison.Ordinal);
        Assert.Contains("toLower(trim(src.Name)) STARTS WITH $p2", translated.Text, StringComparison.Ordinal);
        Assert.Contains("abs(src.Score - $p4) <= $p5", translated.Text, StringComparison.Ordinal);
        Assert.Contains("ORDER BY src.Category, src.Score DESC", translated.Text, StringComparison.Ordinal);
        Assert.Contains("SKIP 1\nLIMIT 3", translated.Text, StringComparison.Ordinal);
        Assert.Contains(
            "RETURN src.Name AS Name, src.Score + $p7 AS AdjustedScore, " +
            "CASE WHEN src.OptionalText IS NULL THEN $p8 ELSE toUpper(src.OptionalText) END AS Display\n" +
            "ORDER BY src.Category, src.Score DESC\nSKIP 1\nLIMIT 3",
            translated.Text,
            StringComparison.Ordinal);
        Assert.Equal(["Name", "AdjustedScore", "Display"], translated.ProjectionColumns);
    }

    private static Querying.Cypher.CypherQuery Translate(IQueryable query)
    {
        var visitor = new CypherQueryVisitor(query.ElementType);
        visitor.Visit(query.Expression);
        return visitor.Query;
    }
}
