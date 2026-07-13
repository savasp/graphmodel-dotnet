// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Neo4j.Translation.Tests;

using Cvoya.Graph.Neo4j.Querying.Cypher.Visitors.Core;
using Cvoya.Graph.Neo4j.Translation.Tests.Harness;
using Cvoya.Graph.Neo4j.Translation.Tests.Model;

[Trait("Area", "FullTextSearch")]
public class FullTextSearchRewriteTests
{
    [Fact]
    public void Search_LowercasesAndAndJoinsTerms()
    {
        Assert.Equal("cloud AND computing", TranslateSearchParameter<Person>("Cloud Computing"));
    }

    [Fact]
    public void Search_SingleTerm_LowercasedWithNoAnd()
    {
        Assert.Equal("alice", TranslateSearchParameter<Person>("Alice"));
    }

    [Fact]
    public void Search_StripsMetacharactersToPlainTokens()
    {
        // The raw string carries live Lucene syntax; tokenization removes it so no parser injection
        // survives into the query value.
        Assert.Equal("vacation", TranslateSearchParameter<Person>("vacation~"));
        Assert.Equal("vacation", TranslateSearchParameter<Person>("vacation*"));
    }

    [Fact]
    public void Search_EmptyQuery_ProducesMatchNothingQuery()
    {
        // An empty term list must yield a query that matches nothing. Neo4j's Lucene parser throws
        // on an empty string, so we emit a pure-negative query (no positive clause -> no matches).
        Assert.Equal("-cvoyanomatchsentinel", TranslateSearchParameter<Person>("   ~*  "));
    }

    private static string TranslateSearchParameter<T>(string rawQuery)
        where T : class, INode
    {
        var query = Root.Nodes<T>().Search(rawQuery);
        var visitor = new CypherQueryVisitor(typeof(T), null);
        visitor.Visit(query.Expression);

        // The search value is the single string parameter; other parameters (labels, ranges) are not
        // strings, so locating it by type keeps the test independent of the generated parameter name.
        var stringParameters = visitor.Query.Parameters.Values.OfType<string>().ToList();
        return Assert.Single(stringParameters);
    }
}
