// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Neo4j.Translation.Tests;

using Cvoya.Graph.Neo4j.Translation.Tests.Harness;
using Cvoya.Graph.Neo4j.Translation.Tests.Model;

public class SearchTranslationTests : TranslationTestBase
{
    [Fact]
    public Task Search_OnNodeQueryable()
    {
        var query = Root.Nodes<Person>().Search("Alice");
        return VerifyTranslation(query);
    }

    [Fact]
    public Task Search_OnRelationshipQueryable()
    {
        var query = Root.Relationships<WorksAt>().Search("engineer");
        return VerifyTranslation(query);
    }

    [Fact]
    public Task Search_ThenWhere()
    {
        var query = Root.Nodes<Person>().Search("Alice").Where(p => p.Age > 21);
        return VerifyTranslation(query);
    }

    [Fact]
    public Task Traverse_ThenSearch()
    {
        var query = Root.Nodes<Person>().Traverse<Knows, Person>().Search("Alice");
        return VerifyTranslation(query);
    }
}
