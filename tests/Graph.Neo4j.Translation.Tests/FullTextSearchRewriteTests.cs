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

    // ---- #374: the rewrite must reach search clauses nested in set-operation branches ----

    [Fact]
    public void Union_RewritesEachBranchParameterWithItsOwnRawQuery()
    {
        IQueryable<Person> left = Root.Nodes<Person>().Search("Cloud Computing");
        IQueryable<Person> right = Root.Nodes<Person>().Search("Data Science");

        var parameters = TranslateStringParameters<Person>(left.Union(right));

        Assert.Equal("cloud AND computing", parameters["u0_p0"]);
        Assert.Equal("data AND science", parameters["u1_p0"]);
    }

    [Fact]
    public void Concat_RewritesEachBranchParameterWithItsOwnRawQuery()
    {
        IQueryable<Person> left = Root.Nodes<Person>().Search("Cloud Computing");
        IQueryable<Person> right = Root.Nodes<Person>().Search("Data Science");

        var parameters = TranslateStringParameters<Person>(left.Concat(right));

        Assert.Equal("cloud AND computing", parameters["u0_p0"]);
        Assert.Equal("data AND science", parameters["u1_p0"]);
    }

    [Fact]
    public void LeftAssociatedUnion_RewritesEveryNestedBranchParameter()
    {
        IQueryable<Person> first = Root.Nodes<Person>().Search("Cloud Computing");
        IQueryable<Person> second = Root.Nodes<Person>().Search("Data Science");
        IQueryable<Person> third = Root.Nodes<Person>().Search("Machine Learning");

        // (first UNION second) UNION third nests one SetOperationClause inside another's left
        // branch, so the first union's parameters carry composed prefixes.
        var parameters = TranslateStringParameters<Person>(first.Union(second).Union(third));

        Assert.Equal("cloud AND computing", parameters["u0_u0_p0"]);
        Assert.Equal("data AND science", parameters["u0_u1_p0"]);
        Assert.Equal("machine AND learning", parameters["u1_p0"]);
    }

    [Fact]
    public void Union_BranchesWithNoTokenizableTerms_ProduceMatchNothingQueries()
    {
        // Whitespace-only queries are rejected at the query surface, so the reachable empty-token
        // case is a query whose terms tokenize away entirely.
        IQueryable<Person> whitespaceAndMetacharacters = Root.Nodes<Person>().Search("   ~*  ");
        IQueryable<Person> metacharacters = Root.Nodes<Person>().Search("~*");

        var parameters = TranslateStringParameters<Person>(whitespaceAndMetacharacters.Union(metacharacters));

        Assert.Equal("-cvoyanomatchsentinel", parameters["u0_p0"]);
        Assert.Equal("-cvoyanomatchsentinel", parameters["u1_p0"]);
    }

    [Fact]
    public void SearchClausesSharingOneParameter_RewriteExactlyOnce()
    {
        // Identical raw queries share one parameter through the planner registry's value dedup. A
        // double rewrite would tokenize the rewritten value again ("cloud AND and AND computing").
        var query = Root.Nodes<Person>()
            .Search("Cloud Computing")
            .Traverse<Knows, Person>()
            .Search("Cloud Computing");

        var parameters = TranslateStringParameters<Person>(query);

        var parameter = Assert.Single(parameters);
        Assert.Equal("cloud AND computing", parameter.Value);
    }

    private static string TranslateSearchParameter<T>(string rawQuery)
        where T : class, INode
    {
        var query = Root.Nodes<T>().Search(rawQuery);

        // The search value is the single string parameter; other parameters (labels, ranges) are not
        // strings, so locating it by type keeps the test independent of the generated parameter name.
        var stringParameters = TranslateStringParameters<T>(query).Values.ToList();
        return Assert.Single(stringParameters);
    }

    private static IReadOnlyDictionary<string, string> TranslateStringParameters<T>(IQueryable<T> query)
        where T : class, INode
    {
        var visitor = new CypherQueryVisitor(typeof(T), null);
        visitor.Visit(query.Expression);
        return visitor.Query.Parameters
            .Where(pair => pair.Value is string)
            .ToDictionary(pair => pair.Key, pair => (string)pair.Value!, StringComparer.Ordinal);
    }
}
