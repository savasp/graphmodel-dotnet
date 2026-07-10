// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Neo4j.Translation.Tests;

using Cvoya.Graph.Neo4j.Translation.Tests.Harness;
using Cvoya.Graph.Neo4j.Translation.Tests.Model;

public class OrderingPagingTranslationTests : TranslationTestBase
{
    [Fact]
    public Task OrderBy_SingleKey()
    {
        var query = Root.Nodes<Person>().OrderBy(p => p.Age);
        return VerifyTranslation(query);
    }

    [Fact]
    public Task OrderByDescending_SingleKey()
    {
        var query = Root.Nodes<Person>().OrderByDescending(p => p.Age);
        return VerifyTranslation(query);
    }

    [Fact]
    public Task OrderBy_ThenBy()
    {
        var query = Root.Nodes<Person>().OrderBy(p => p.LastName).ThenBy(p => p.FirstName);
        return VerifyTranslation(query);
    }

    [Fact]
    public Task OrderBy_ThenByDescending()
    {
        var query = Root.Nodes<Person>().OrderBy(p => p.LastName).ThenByDescending(p => p.Age);
        return VerifyTranslation(query);
    }

    [Fact]
    public Task Take_LimitsResults()
    {
        var query = Root.Nodes<Person>().Take(10);
        return VerifyTranslation(query);
    }

    [Fact]
    public Task Skip_SkipsResults()
    {
        var query = Root.Nodes<Person>().Skip(5);
        return VerifyTranslation(query);
    }

    [Fact]
    public Task Skip_ThenTake_Paging()
    {
        var query = Root.Nodes<Person>().OrderBy(p => p.LastName).Skip(20).Take(10);
        return VerifyTranslation(query);
    }

    [Fact]
    public Task Distinct_OnProjection()
    {
        var query = Root.Nodes<Person>().Select(p => p.LastName).Distinct();
        return VerifyTranslation(query);
    }
}
