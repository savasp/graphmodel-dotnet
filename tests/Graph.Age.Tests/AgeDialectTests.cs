// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Age.Tests;

using Cvoya.Graph.Age.Querying.Cypher;
using Cvoya.Graph.Age.Querying.Cypher.Execution;

public sealed class AgeDialectTests
{
    [Fact]
    public void DeclaresOnlyVerifiedCapabilities()
    {
        var capabilities = AgeDialect.Instance.Capabilities;

        Assert.Equal(
            CapabilitySet.Of(
                GraphCapability.Transactions,
                GraphCapability.ComplexPropertyCascade,
                GraphCapability.MultiLabelMatch,
                GraphCapability.LabelFiltering,
                GraphCapability.OrderByEntity,
                GraphCapability.OptionalTraversal,
                GraphCapability.FullTextSearch,
                GraphCapability.CallSubqueries,
                GraphCapability.PatternSizeProjection,
                GraphCapability.GroupByAggregation),
            capabilities);
    }

    [Fact]
    public void RejectsUnsafeGraphNames()
    {
        Assert.Throws<ArgumentException>(() => new AgeGraphStore(
            "Host=localhost;Username=postgres;Password=postgres;Database=postgres",
            "graph'; DROP TABLE users; --"));
    }

    [Fact]
    public void RequiresExplicitConnectionCredentials()
    {
        Assert.Throws<InvalidOperationException>(() => new AgeGraphStore("   ", "safe_graph"));
    }

    [Fact]
    public void ChoosesDollarQuoteTagAbsentFromCypher()
    {
        var tag = AgeQueryRunner.ChooseDollarQuoteTag("RETURN '$cvoya_age_0$' AS text");

        Assert.Equal("$cvoya_age_1$", tag);
    }

    [Fact]
    public void PropertyAccessQuotesReservedKeywordsButLeavesOrdinaryNamesStable()
    {
        Assert.Equal("src.`match`", AgeDialect.Instance.RenderPropertyAccess("src", "match", false));
        Assert.Equal("src.Name", AgeDialect.Instance.RenderPropertyAccess("src", "Name", false));
    }

    [Fact]
    public void QuotesOnlyValidatedSqlProjectionColumns()
    {
        Assert.Equal("\"safe_column\"", AgeSqlIdentifier.Quote("safe_column", "projection column"));
        Assert.Throws<ArgumentException>(() => AgeSqlIdentifier.Quote("value agtype); DROP TABLE users; --", "projection column"));
    }
}
