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

namespace Cvoya.Graph.Model.Neo4j.Translation.Tests;

using Cvoya.Graph.Model.Neo4j.Translation.Tests.Harness;
using Cvoya.Graph.Model.Neo4j.Translation.Tests.Model;

/// <summary>
/// Covers SelectMany, GroupBy, Join, and Union. None of these have a dedicated
/// <c>IGraphQueryable&lt;T&gt;</c>-typed extension method in the public surface (unlike
/// Where/Select/etc.), so the tests reach them via the standard <see cref="Queryable"/> static
/// LINQ methods, which <c>CypherQueryVisitor.IsLinqMethod</c> also recognizes
/// (<c>node.Method.DeclaringType == typeof(Queryable)</c>).
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
    /// NOTE (characterization): Union is deliberately unimplemented -
    /// <c>CypherQueryVisitor.HandleUnion</c> unconditionally throws.
    /// </summary>
    [Fact]
    public Task Union_ThrowsNotImplemented()
    {
        IQueryable<Person> first = Root.Nodes<Person>().Where(p => p.Age > 30);
        IQueryable<Person> second = Root.Nodes<Person>().Where(p => p.LastName == "Smith");
        var query = first.Union(second);
        return VerifyTranslationThrows(query);
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
