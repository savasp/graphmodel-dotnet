// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

using Cvoya.Graph.Cypher;
using Cvoya.Graph.Cypher.Ast;
using Cvoya.Graph.Cypher.Ast.Expressions;
using Cvoya.Graph.Neo4j.Querying.Cypher;
using Cvoya.Graph.Querying;

namespace Cvoya.Graph.Neo4j.Translation.Tests;

public class Neo4jDialectTests
{
    private const string RootNodeLabel = "__CvoyaRootNode";

    [Fact]
    public void DeclaresOnlyCapabilitiesWithUserVisibleSupport()
    {
        var capabilities = Neo4jDialect.Instance.Capabilities;

        Assert.Equal(
            CapabilitySet.Of(
                GraphCapability.FullTextSearch,
                GraphCapability.Transactions,
                GraphCapability.ComplexPropertyCascade,
                GraphCapability.CallSubqueries,
                GraphCapability.PatternSizeProjection,
                GraphCapability.MultiLabelMatch,
                GraphCapability.LabelFiltering,
                GraphCapability.OrderByEntity,
                GraphCapability.OptionalTraversal,
                GraphCapability.GroupByAggregation,
                GraphCapability.RelationshipPredicates,
                GraphCapability.ShortestPath,
                GraphCapability.SetOperations),
            capabilities);
    }

    [Fact]
    public void FullTextClause_RendersItsExplicitParameterReference()
    {
        var statement = new CypherStatement(
            [new FullTextSearchClause(SearchRootTarget.Nodes, new QueryParameter("p7"), "n")],
            new Dictionary<string, object?> { ["p7"] = "Ada" });

        var result = new CypherRenderer(Neo4jDialect.Instance).Render(statement);

        Assert.Equal(
            "CALL db.index.fulltext.queryNodes('node_fulltext_index', $p7) YIELD node AS n",
            result.Text);
        Assert.Equal("Ada", result.Parameters["p7"]);
    }

    [Theory]
    [InlineData("temporal.datetime", "datetime")]
    [InlineData("temporal.duration", "duration")]
    [InlineData("string.join", "apoc.text.join")]
    [InlineData("string.indexOf", "apoc.text.indexOf")]
    public void FunctionMappings_PreserveNeo4jSyntax(string neutralName, string expected)
    {
        Assert.Equal(expected, Neo4jDialect.Instance.RenderFunctionName(neutralName));
    }

    [Fact]
    public void RenderNodeLabels_RejectsTheReservedRootLabel()
    {
        var failure = Assert.Throws<GraphQueryTranslationException>(() =>
            Neo4jDialect.Instance.RenderNodeLabels([RootNodeLabel]));

        Assert.Contains("reserved for provider infrastructure", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RenderLabelTest_HidesTheReservedRootLabel()
    {
        var reservedOnly = Neo4jDialect.Instance.RenderLabelTest(
            "n",
            [RootNodeLabel],
            value => $"'{value}'");
        var mixed = Neo4jDialect.Instance.RenderLabelTest(
            "n",
            [RootNodeLabel, "Person"],
            value => $"'{value}'");

        Assert.Equal("false", reservedOnly);
        Assert.Equal("'Person' IN labels(n)", mixed);
    }
}
