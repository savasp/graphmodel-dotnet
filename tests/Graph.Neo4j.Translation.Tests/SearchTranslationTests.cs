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

    [Fact]
    public Task Search_ThenTraverse_WithComposition()
    {
        var query = Root.Nodes<Person>()
            .Search("Alice")
            .Traverse<Knows, Person>(options => options
                .Depth(1, 2)
                .Direction(GraphTraversalDirection.Incoming))
            .Where(person => person.Age > 21)
            .OrderBy(person => person.FirstName)
            .Skip(1)
            .Take(2)
            .Select(person => person.FirstName);

        return VerifyTranslation(query);
    }

    [Fact]
    public Task Search_ThenTraverse_ThenSearch()
    {
        var query = Root.Nodes<Person>()
            .Search("Alice")
            .Traverse<Knows, Person>()
            .Search("Bob");

        return VerifyTranslation(query);
    }

    [Fact]
    public Task Search_ThenPathSegments()
    {
        var query = Root.Nodes<Person>()
            .Search("Alice")
            .PathSegments<Person, Knows, Person>()
            .Where(segment => segment.Relationship.Since >= 2020)
            .Select(segment => new
            {
                StartKey = segment.StartNode.TestKey,
                EndKey = segment.EndNode.TestKey,
            });

        return VerifyTranslation(query);
    }

    [Fact]
    public Task Search_ThenTraversePaths()
    {
        var query = Root.Nodes<Person>()
            .Search("Alice")
            .TraversePaths<Knows, Person>(options => options
                .Depth(1, 2)
                .Direction(GraphTraversalDirection.Both))
            .Where(path => path.Segments.Count == 2);

        return VerifyTranslation(query);
    }
}
