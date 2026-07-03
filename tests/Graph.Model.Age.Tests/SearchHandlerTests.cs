// Copyright 2025 Savas Parastatidis
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//      https://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

namespace Cvoya.Graph.Model.Age.Tests;

using System;
using Cvoya.Graph.Model;
using Cvoya.Graph.Model.Age.Querying.Cypher.Visitors.Core;
using Cvoya.Graph.Model.Age.Querying.Linq.Queryables;
using Xunit;

/// <summary>
/// Tests for SearchHandler via AgeFullTextSearchExpression routed through AgeCypherQueryVisitor.
/// </summary>
public sealed class SearchHandlerTests
{
    [Fact]
    public void HandleAgeFullTextSearch_NodeType_EmitsWhereWithRegexCondition()
    {
        var context = new CypherQueryContext(typeof(PersonNode));
        var visitor = new AgeCypherQueryVisitor(context);

        var searchExpr = new AgeFullTextSearchExpression("John", typeof(PersonNode));
        visitor.Visit(searchExpr);

        var cypher = context.GetQuery();

        Assert.Contains("MATCH", cypher);
        Assert.Contains("WHERE", cypher);
        Assert.Contains("=~", cypher);
    }

    [Fact]
    public void HandleAgeFullTextSearch_IEntitySearch_EmitsMatch()
    {
        var context = new CypherQueryContext(typeof(IEntity));
        var visitor = new AgeCypherQueryVisitor(context);

        var searchExpr = new AgeFullTextSearchExpression("test", typeof(IEntity));
        visitor.Visit(searchExpr);

        var cypher = context.GetQuery();

        Assert.Contains("MATCH", cypher);
        Assert.Contains("WHERE", cypher);
    }

    [Fact]
    public void HandleAgeFullTextSearch_MultipleStringProperties_CreatesOrCondition()
    {
        var context = new CypherQueryContext(typeof(PersonWithAddressNode));
        var visitor = new AgeCypherQueryVisitor(context);

        var searchExpr = new AgeFullTextSearchExpression("query", typeof(PersonWithAddressNode));
        visitor.Visit(searchExpr);

        var cypher = context.GetQuery();

        Assert.Contains("OR", cypher);
    }

    [Fact]
    public void VisitExtension_AgeFullTextSearchExpression_RoutesToSearchHandler()
    {
        var context = new CypherQueryContext(typeof(PersonNode));
        var visitor = new AgeCypherQueryVisitor(context);

        var searchExpr = new AgeFullTextSearchExpression("Alice", typeof(PersonNode));
        visitor.Visit(searchExpr);

        var cypher = context.GetQuery();

        Assert.Contains("$param_0", cypher);
        Assert.Contains("MATCH", cypher);
    }

    [Fact]
    public void SearchIsExtensionMethod_NotOnQueryableType()
    {
        var method = typeof(TestGraphQueryable<PersonNode>).GetMethod("Search");
        Assert.Null(method);
    }

    [Fact]
    public void SearchWithSpecialCharacters_ParameterizesValue()
    {
        var context = new CypherQueryContext(typeof(PersonNode));
        var visitor = new AgeCypherQueryVisitor(context);

        var searchExpr = new AgeFullTextSearchExpression("it's \"wild\" $$ dollar", typeof(PersonNode));
        visitor.Visit(searchExpr);

        var cypher = context.GetQuery();

        Assert.Contains("$param_0", cypher);
        Assert.DoesNotContain("it's", cypher);
        Assert.DoesNotContain("$$", cypher);
    }

    [Fact]
    public void SearchWithApostrophe_ParameterizesValue()
    {
        var context = new CypherQueryContext(typeof(PersonNode));
        var visitor = new AgeCypherQueryVisitor(context);

        var searchExpr = new AgeFullTextSearchExpression("O'Brien", typeof(PersonNode));
        visitor.Visit(searchExpr);

        var cypher = context.GetQuery();

        Assert.Contains("$param_0", cypher);
        Assert.DoesNotContain("O'Brien", cypher);
    }

    [Fact]
    public void SearchWithBackslash_ParameterizesValue()
    {
        var context = new CypherQueryContext(typeof(PersonNode));
        var visitor = new AgeCypherQueryVisitor(context);

        var searchExpr = new AgeFullTextSearchExpression("test\\path", typeof(PersonNode));
        visitor.Visit(searchExpr);

        var cypher = context.GetQuery();

        Assert.Contains("$param_0", cypher);
        Assert.DoesNotContain("\\path", cypher);
    }

    [Fact]
    public void SearchWithInjectionAttempt_ParameterizesValue()
    {
        var context = new CypherQueryContext(typeof(PersonNode));
        var visitor = new AgeCypherQueryVisitor(context);

        // Attempt SQL/Cypher injection via search string
        var searchExpr = new AgeFullTextSearchExpression("' OR 1=1 RETURN * //", typeof(PersonNode));
        visitor.Visit(searchExpr);

        var cypher = context.GetQuery();

        Assert.Contains("$param_0", cypher);
        // The injection payload should NOT appear as raw text in the Cypher output.
        // (It is safely stored in the ParameterStore and referenced via $param_0.)
        Assert.DoesNotContain("1=1", cypher);
        // Do NOT assert !Contains("RETURN") — Cypher output naturally has RETURN keyword
    }

    [Fact]
    public void HandleAgeFullTextSearch_NodeType_EmitsParameterReference()
    {
        var context = new CypherQueryContext(typeof(PersonNode));
        var visitor = new AgeCypherQueryVisitor(context);

        var searchExpr = new AgeFullTextSearchExpression("John", typeof(PersonNode));
        visitor.Visit(searchExpr);

        var cypher = context.GetQuery();

        Assert.Contains("$param_0", cypher);
    }

    [Fact]
    public void HandleAgeFullTextSearch_MultipleStringProperties_EmitsParameterReference()
    {
        var context = new CypherQueryContext(typeof(PersonWithAddressNode));
        var visitor = new AgeCypherQueryVisitor(context);

        var searchExpr = new AgeFullTextSearchExpression("query", typeof(PersonWithAddressNode));
        visitor.Visit(searchExpr);

        var cypher = context.GetQuery();

        Assert.Contains("$param_0", cypher);
        Assert.Contains("OR", cypher);
    }

    [Fact]
    public void HandleAgeFullTextSearch_IEntitySearch_EmitsParameterReference()
    {
        var context = new CypherQueryContext(typeof(IEntity));
        var visitor = new AgeCypherQueryVisitor(context);

        var searchExpr = new AgeFullTextSearchExpression("test", typeof(IEntity));
        visitor.Visit(searchExpr);

        var cypher = context.GetQuery();

        Assert.Contains("$param_0", cypher);
        Assert.Contains("MATCH", cypher);
        Assert.Contains("WHERE", cypher);
    }
}
