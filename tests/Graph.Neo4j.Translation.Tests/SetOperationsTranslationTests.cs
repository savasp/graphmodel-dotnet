// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Neo4j.Translation.Tests;

using Cvoya.Graph.Neo4j.Translation.Tests.Harness;
using Cvoya.Graph.Neo4j.Translation.Tests.Model;

/// <summary>
/// Covers standard LINQ set and relational operations alongside the graph-typed surface.
/// </summary>
public class SetOperationsTranslationTests : TranslationTestBase
{
    [Fact]
    public Task SelectMany_ThrowsNotSupported()
    {
        IQueryable<Person> source = Root.Nodes<Person>();
        var query = source.SelectMany(p => p.Nicknames);
        return VerifyTranslationThrows(query);
    }

    [Fact]
    public Task GroupBy_ThrowsNotSupported()
    {
        IQueryable<Person> source = Root.Nodes<Person>();
        var query = source.GroupBy(p => p.LastName);
        return VerifyTranslationThrows(query);
    }

    /// <summary>
    /// Standard <see cref="Queryable.Union{TSource}(IQueryable{TSource}, IEnumerable{TSource})"/>
    /// uses the same shared set-operation model as the graph-typed overload.
    /// </summary>
    [Fact]
    public Task Union_ThrowsNotImplemented()
    {
        IQueryable<Person> first = Root.Nodes<Person>().Where(p => p.Age > 30);
        IQueryable<Person> second = Root.Nodes<Person>().Where(p => p.LastName == "Smith");
        var query = first.Union(second);
        return VerifyTranslation(query);
    }

    [Fact]
    public Task Join_NodesWithRelationships()
    {
        IQueryable<Knows> relationships = Root.Relationships<Knows>();
        IQueryable<Person> people = Root.Nodes<Person>();

        var query = relationships.Join(
            people,
            r => r.EndNodeId,
            p => p.Id,
            (r, p) => p);

        return VerifyTranslation(query);
    }

    [Fact]
    public Task Join_AsymmetricStartNodeKey_SelectsInnerNode()
    {
        IQueryable<Knows> relationships = Root.Relationships<Knows>();
        IQueryable<Person> people = Root.Nodes<Person>();

        var query = relationships.Join(
            people,
            r => r.StartNodeId,
            p => p.Id,
            (r, p) => p);

        return VerifyTranslation(query);
    }
}
