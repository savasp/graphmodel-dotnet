// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Age.Tests;

using Cvoya.Graph.Age.Querying;

/// <summary>
/// Provider-free tests for the phase-1 full-text SQL construction. These assert the exact SQL shape
/// per search kind (typed / untyped / dynamic / relationship) so that the semantics the GIN-index
/// work (#291) must preserve are pinned. The only user input, the search text, always travels as the
/// <c>@query</c> bind parameter with the <c>'simple'</c> regconfig.
/// </summary>
public sealed class AgeFullTextSearchTests
{
    private static readonly string N = System.Environment.NewLine;

    [Fact]
    public void BuildTypedSql_SingleCandidate_RendersTsvectorLabelTestAndBoundQuery()
    {
        var sql = AgeFullTextSearch.BuildTypedSql(
            "cvoya_g1",
            "CvoyaNode",
            [new AgeFullTextSearch.FullTextCandidate("Person", ["FirstName", "LastName", "Bio"])]);

        Assert.Equal(
            "SELECT t.props ->> 'Id' AS id" + N +
            "FROM (SELECT properties::text::jsonb AS props FROM \"cvoya_g1\".\"CvoyaNode\") AS t" + N +
            "WHERE (to_tsvector('simple', concat_ws(' ', t.props ->> 'FirstName', t.props ->> 'LastName', " +
            "t.props ->> 'Bio')) @@ plainto_tsquery('simple', @query) " +
            "AND jsonb_exists(t.props -> 'inheritance_labels', 'Person'))" + N +
            "LIMIT 10001",
            sql);
    }

    [Fact]
    public void BuildTypedSql_MultipleCandidates_OrsPerTypePredicates()
    {
        var sql = AgeFullTextSearch.BuildTypedSql(
            "cvoya_g1",
            "CvoyaNode",
            [
                new AgeFullTextSearch.FullTextCandidate("Person", ["Bio"]),
                new AgeFullTextSearch.FullTextCandidate("Manager", ["Bio", "Department"]),
            ]);

        // Inheritance is expressed as one disjunct per concrete type, each with its own label test and
        // its own searchable properties (so Manager's Department participates only for Manager rows).
        Assert.Contains("jsonb_exists(t.props -> 'inheritance_labels', 'Person')", sql, StringComparison.Ordinal);
        Assert.Contains("jsonb_exists(t.props -> 'inheritance_labels', 'Manager')", sql, StringComparison.Ordinal);
        Assert.Contains("t.props ->> 'Department'", sql, StringComparison.Ordinal);
        Assert.Contains($"){N}   OR (", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildTypedSql_RelationshipTable_TargetsRelationshipTable()
    {
        var sql = AgeFullTextSearch.BuildTypedSql(
            "cvoya_g1",
            "CvoyaRelationship",
            [new AgeFullTextSearch.FullTextCandidate("KnowsWell", ["HowWell"])]);

        Assert.Contains("FROM \"cvoya_g1\".\"CvoyaRelationship\"", sql, StringComparison.Ordinal);
        Assert.Contains("jsonb_exists(t.props -> 'inheritance_labels', 'KnowsWell')", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildDynamicSql_AggregatesAllValuesExcludingInternalKeys()
    {
        var sql = AgeFullTextSearch.BuildDynamicSql("cvoya_g1", "CvoyaNode");

        Assert.Equal(
            "SELECT t.props ->> 'Id' AS id" + N +
            "FROM (SELECT properties::text::jsonb AS props FROM \"cvoya_g1\".\"CvoyaNode\") AS t" + N +
            "WHERE to_tsvector('simple', (" + N +
            "        SELECT concat_ws(' ', array_agg(kv.value))" + N +
            "        FROM jsonb_each_text(t.props) AS kv(key, value)" + N +
            "        WHERE kv.key NOT IN ('inheritance_labels', '__graphModelEntityKind__', '__metadata__')" + N +
            "    )) @@ plainto_tsquery('simple', @query)" + N +
            "LIMIT 10001",
            sql);
    }

    [Fact]
    public void BuildTypedSql_EscapesSingleQuotesInLabelsAndProperties()
    {
        var sql = AgeFullTextSearch.BuildTypedSql(
            "cvoya_g1",
            "CvoyaNode",
            [new AgeFullTextSearch.FullTextCandidate("O'Brien", ["Ap'os"])]);

        Assert.Contains("t.props ->> 'Ap''os'", sql, StringComparison.Ordinal);
        Assert.Contains("jsonb_exists(t.props -> 'inheritance_labels', 'O''Brien')", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void IdSetLimit_IsTenThousand()
    {
        // The id list rides to AGE as a single agtype parameter blob; the phase-1 SQL fetches one past
        // the limit so the provider can fail informatively rather than build an unbounded parameter.
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

        Assert.Contains(AgeFullTextSearch.MaxMatchedIds.ToString(System.Globalization.CultureInfo.InvariantCulture),
            exception.Message, StringComparison.Ordinal);
        Assert.Contains("Narrow the query", exception.Message, StringComparison.Ordinal);
    }
}
