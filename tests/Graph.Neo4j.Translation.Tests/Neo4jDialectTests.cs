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
}
