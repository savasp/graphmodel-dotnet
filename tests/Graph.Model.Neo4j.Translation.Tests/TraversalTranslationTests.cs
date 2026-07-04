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

public class TraversalTranslationTests : TranslationTestBase
{
    [Fact]
    public Task PathSegments_Basic()
    {
        var query = Root.Nodes<Person>().PathSegments<Person, Knows, Person>();
        return VerifyTranslation(query);
    }

    [Fact]
    public Task Traverse_NoDepthOrDirection()
    {
        var query = Root.Nodes<Person>().Traverse<Person, Knows, Person>();
        return VerifyTranslation(query);
    }

    [Fact]
    public Task Traverse_WithMaxDepth()
    {
        var query = Root.Nodes<Person>().Traverse<Person, Knows, Person>(3);
        return VerifyTranslation(query);
    }

    [Fact]
    public Task Traverse_WithMinAndMaxDepth()
    {
        var query = Root.Nodes<Person>().Traverse<Person, Knows, Person>(1, 3);
        return VerifyTranslation(query);
    }

    [Fact]
    public Task Traverse_WithDirection()
    {
        var query = Root.Nodes<Person>().Traverse<Person, Knows, Person>(GraphTraversalDirection.Incoming);
        return VerifyTranslation(query);
    }

    [Fact]
    public Task Traverse_ToDifferentNodeType()
    {
        var query = Root.Nodes<Person>().Traverse<Person, WorksAt, Company>();
        return VerifyTranslation(query);
    }

    /// <summary>
    /// NOTE (characterization): <c>ReverseTraverse&lt;TStartNode, TRelationship, TEndNode&gt;</c>
    /// is a client-side extension method that eagerly composes
    /// <c>PathSegments().Direction(Incoming).Select(ps => ps.EndNode)</c> and calls
    /// <c>source.Provider.CreateQuery&lt;T&gt;</c> immediately rather than deferring - so the
    /// literal method name "ReverseTraverse" never appears in an expression tree passed to
    /// <c>CypherQueryVisitor</c>. The "ReverseTraverse" case in
    /// <c>CypherQueryVisitor.HandleLinqMethod</c> is therefore dead code; this test snapshots the
    /// resulting Cypher, which is identical in shape to <c>PathSegments().Direction(Incoming)</c>.
    /// </summary>
    [Fact]
    public Task ReverseTraverse_ProducesPathSegmentsDirectionSelectShape()
    {
        var query = Root.Nodes<Person>().ReverseTraverse<Person, Knows, Person>();
        return VerifyTranslation(query);
    }

    [Fact]
    public Task TraverseRelationships_ReturnsRelationshipsNotNodes()
    {
        var query = Root.Nodes<Person>().TraverseRelationships<Person, Knows, Person>();
        return VerifyTranslation(query);
    }

    [Fact]
    public Task Direction_Outgoing_OnPathSegments()
    {
        var query = Root.Nodes<Person>()
            .PathSegments<Person, Knows, Person>()
            .Direction(GraphTraversalDirection.Outgoing);
        return VerifyTranslation(query);
    }

    [Fact]
    public Task Direction_Both_OnPathSegments()
    {
        var query = Root.Nodes<Person>()
            .PathSegments<Person, Knows, Person>()
            .Direction(GraphTraversalDirection.Both);
        return VerifyTranslation(query);
    }

    [Fact]
    public Task WithDepth_MaxOnly_OnPathSegments()
    {
        var query = Root.Nodes<Person>()
            .PathSegments<Person, Knows, Person>()
            .WithDepth(4);
        return VerifyTranslation(query);
    }

    [Fact]
    public Task WithDepth_MinAndMax_OnPathSegments()
    {
        var query = Root.Nodes<Person>()
            .PathSegments<Person, Knows, Person>()
            .WithDepth(2, 4);
        return VerifyTranslation(query);
    }

    [Fact]
    public Task PathSegments_WithWhereOnEndNode()
    {
        var query = Root.Nodes<Person>()
            .PathSegments<Person, Knows, Person>()
            .Where(ps => ps.EndNode.Age > 21);
        return VerifyTranslation(query);
    }
}
