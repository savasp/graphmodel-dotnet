// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Age.Tests;

using Cvoya.Graph.Age.Querying;

/// <summary>Provider-free coverage for AGE phase-one full-text SQL construction.</summary>
public sealed class AgeFullTextSearchTests
{
    [Fact]
    public void BuildTypedSql_LegacyTable_ReturnsGraphidContextAndUsesIncludedProperties()
    {
        var sql = AgeFullTextSearch.BuildTypedSql(
            "cvoya_g1",
            "CvoyaNode",
            [new AgeFullTextSearch.FullTextCandidate("Person", ["Id", "FirstName", "Bio"])]);

        Assert.Contains("SELECT id::text::bigint AS graph_id", sql, StringComparison.Ordinal);
        Assert.Contains("'Node' AS entity_kind", sql, StringComparison.Ordinal);
        Assert.Contains("'CvoyaNode' AS storage_name", sql, StringComparison.Ordinal);
        Assert.Contains("FROM ONLY \"cvoya_g1\".\"CvoyaNode\"", sql, StringComparison.Ordinal);
        Assert.Contains("(properties::text::jsonb) ->> 'Id'", sql, StringComparison.Ordinal);
        Assert.Contains("(properties::text::jsonb) ->> 'FirstName'", sql, StringComparison.Ordinal);
        Assert.Contains("jsonb_exists((properties::text::jsonb) -> 'inheritance_labels', 'Person')", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("->> 'Id' AS id", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildTypedSql_NativeTable_DoesNotRequireLegacyMetadata()
    {
        var sql = AgeFullTextSearch.BuildTypedSql(
            "cvoya_g1",
            "Person",
            [new AgeFullTextSearch.FullTextCandidate("Person", ["Bio"])]);

        Assert.Contains("FROM ONLY \"cvoya_g1\".\"Person\"", sql, StringComparison.Ordinal);
        Assert.Contains("(properties::text::jsonb) ->> 'Bio'", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("jsonb_exists((properties::text::jsonb) -> 'inheritance_labels'", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildTypedSql_MultipleLegacyCandidates_OrsPerTypePredicates()
    {
        var sql = AgeFullTextSearch.BuildTypedSql(
            "cvoya_g1",
            "CvoyaNode",
            [
                new AgeFullTextSearch.FullTextCandidate("Person", ["Bio"]),
                new AgeFullTextSearch.FullTextCandidate("Manager", ["Bio", "Department"]),
            ]);

        Assert.Contains("'Person'", sql, StringComparison.Ordinal);
        Assert.Contains("'Manager'", sql, StringComparison.Ordinal);
        Assert.Contains("->> 'Department'", sql, StringComparison.Ordinal);
        Assert.Contains("OR", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildTypedSql_RelationshipTable_PreservesRelationshipKind()
    {
        var sql = AgeFullTextSearch.BuildTypedSql(
            "cvoya_g1",
            "CvoyaRelationship",
            [new AgeFullTextSearch.FullTextCandidate("KnowsWell", ["HowWell"])],
            relationship: true);

        Assert.Contains("'Relationship' AS entity_kind", sql, StringComparison.Ordinal);
        Assert.Contains("FROM ONLY \"cvoya_g1\".\"CvoyaRelationship\"", sql, StringComparison.Ordinal);
        Assert.Contains("'__graphModelComplexProperty'", sql, StringComparison.Ordinal);
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
    public void BuildTypedSql_UsesManagedCoarsePredicateOnlyWhenIndexIsKnownPresent()
    {
        var fallback = AgeFullTextSearch.BuildTypedSql(
            "cvoya_g1",
            "CvoyaNode",
            [new AgeFullTextSearch.FullTextCandidate("Person", ["Bio"])]);
        var accelerated = AgeFullTextSearch.BuildTypedSql(
            "cvoya_g1",
            "CvoyaNode",
            [new AgeFullTextSearch.FullTextCandidate("Person", ["Bio"])],
            hasManagedIndex: true);

        Assert.DoesNotContain("age_fulltext_blob", fallback, StringComparison.Ordinal);
        Assert.Contains("\"cvoya_g1\".age_fulltext_blob(properties)", accelerated, StringComparison.Ordinal);
        Assert.Contains("->> 'Bio'", accelerated, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildTypedSql_EscapesSingleQuotesInLabelsAndProperties()
    {
        var sql = AgeFullTextSearch.BuildTypedSql(
            "cvoya_g1",
            "CvoyaNode",
            [new AgeFullTextSearch.FullTextCandidate("O'Brien", ["Ap'os"])]);

        Assert.Contains("->> 'Ap''os'", sql, StringComparison.Ordinal);
        Assert.Contains("'O''Brien'", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void CombinedQuery_DeduplicatesBeforeApplyingTheTenThousandLimit()
    {
        var sql = AgeFullTextSearch.BuildDynamicSql("cvoya_g1", "Person");

        Assert.Contains("GROUP BY graph_id, entity_kind", sql, StringComparison.Ordinal);
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
