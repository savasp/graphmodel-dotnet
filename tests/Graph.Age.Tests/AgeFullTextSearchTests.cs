// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Age.Tests;

using Cvoya.Graph.Age.Querying;

/// <summary>Provider-free coverage for AGE phase-one full-text SQL construction.</summary>
public sealed class AgeFullTextSearchTests
{
    [Fact]
    public void BuildTypedSql_NativeNodeTable_ReturnsGraphidContextAndUsesIncludedProperties()
    {
        var sql = AgeFullTextSearch.BuildTypedSql(
            "cvoya_g1",
            "Person",
            [new AgeFullTextSearch.FullTextCandidate("Person", ["Id", "FirstName", "Bio"])]);

        Assert.Contains("SELECT id::text::bigint AS graph_id", sql, StringComparison.Ordinal);
        Assert.Contains("'Node' AS entity_kind", sql, StringComparison.Ordinal);
        Assert.Contains("FROM ONLY \"cvoya_g1\".\"Person\"", sql, StringComparison.Ordinal);
        Assert.Contains("(properties::text::jsonb) ->> 'Id'", sql, StringComparison.Ordinal);
        Assert.Contains("(properties::text::jsonb) ->> 'FirstName'", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("->> 'Id' AS id", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("jsonb_exists((properties::text::jsonb) -> 'inheritance_labels'", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("age_fulltext_blob", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildTypedSql_RelationshipTable_PreservesRelationshipKind()
    {
        var sql = AgeFullTextSearch.BuildTypedSql(
            "cvoya_g1",
            "WORKS_REALLY_WELL_WITH",
            [new AgeFullTextSearch.FullTextCandidate("KnowsWell", ["HowWell"])],
            relationship: true);

        Assert.Contains("'Relationship' AS entity_kind", sql, StringComparison.Ordinal);
        Assert.Contains("FROM ONLY \"cvoya_g1\".\"WORKS_REALLY_WELL_WITH\"", sql, StringComparison.Ordinal);
        Assert.Contains("->> 'HowWell'", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("__graphModelComplexProperty", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildDynamicSql_UsesFunctionFreeAllStringFallback_AndTreatsIdNormally()
    {
        var sql = AgeFullTextSearch.BuildDynamicSql("cvoya_g1", "Person");

        Assert.Contains("jsonb_each((properties::text::jsonb))", sql, StringComparison.Ordinal);
        Assert.Contains("jsonb_typeof(age_fulltext_value.value) = 'string'", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("age_fulltext_blob", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("NOT IN ('Id'", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void SearchablePhysicalTable_RejectsAgeBaseAndProviderReservedTables()
    {
        Assert.False(AgeFullTextSearch.IsSearchablePhysicalTable("_ag_label_vertex"));
        Assert.False(AgeFullTextSearch.IsSearchablePhysicalTable("_ag_label_edge"));
        Assert.False(AgeFullTextSearch.IsSearchablePhysicalTable("CvoyaNode"));
        Assert.False(AgeFullTextSearch.IsSearchablePhysicalTable("CvoyaRelationship"));
        Assert.True(AgeFullTextSearch.IsSearchablePhysicalTable("Person"));
        Assert.True(AgeFullTextSearch.IsSearchablePhysicalTable("EXTERNAL_SEARCH_EDGE"));
    }

    [Fact]
    public void BuildTypedSql_EscapesSingleQuotesInProperties()
    {
        var sql = AgeFullTextSearch.BuildTypedSql(
            "cvoya_g1",
            "Person",
            [new AgeFullTextSearch.FullTextCandidate("O'Brien", ["Ap'os"])]);

        Assert.Contains("->> 'Ap''os'", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void CombinedQuery_DeduplicatesBeforeApplyingTheTenThousandLimit()
    {
        var sql = AgeFullTextSearch.BuildDynamicSql("cvoya_g1", "Person");

        Assert.StartsWith("SELECT DISTINCT graph_id, entity_kind", sql, StringComparison.Ordinal);
        Assert.EndsWith("LIMIT 10001", sql, StringComparison.Ordinal);
        Assert.Equal(10_000, AgeFullTextSearch.MaxMatchedIds);
    }

    [Fact]
    public void EnforceIdSetLimit_AtOrBelowLimit_DoesNotThrow()
    {
        AgeFullTextSearch.EnforceIdSetLimit(0);
        AgeFullTextSearch.EnforceIdSetLimit(AgeFullTextSearch.MaxMatchedIds);
    }

    [Fact]
    public void EnforceIdSetLimit_AboveLimit_ThrowsActionableError()
    {
        var exception = Assert.Throws<GraphException>(
            () => AgeFullTextSearch.EnforceIdSetLimit(AgeFullTextSearch.MaxMatchedIds + 1));

        Assert.Contains(
            AgeFullTextSearch.MaxMatchedIds.ToString(System.Globalization.CultureInfo.InvariantCulture),
            exception.Message,
            StringComparison.Ordinal);
        Assert.Contains("Narrow the query", exception.Message, StringComparison.Ordinal);
    }
}
