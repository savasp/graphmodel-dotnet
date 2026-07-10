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

namespace Cvoya.Graph.Neo4j.Translation.Tests;

using Cvoya.Graph.Neo4j.Translation.Tests.Harness;
using Cvoya.Graph.Neo4j.Translation.Tests.Model;

public class SelectTranslationTests : TranslationTestBase
{
    [Fact]
    public Task Select_Identity()
    {
        var query = Root.Nodes<Person>().Select(p => p);
        return VerifyTranslation(query);
    }

    [Fact]
    public Task Select_AnonymousType()
    {
        var query = Root.Nodes<Person>().Select(p => new { p.FirstName, p.LastName, p.Age });
        return VerifyTranslation(query);
    }

    [Fact]
    public Task Select_ScalarProperty()
    {
        var query = Root.Nodes<Person>().Select(p => p.FirstName);
        return VerifyTranslation(query);
    }

    [Fact]
    public Task Select_ComputedScalarExpression()
    {
        var query = Root.Nodes<Person>().Select(p => p.FirstName + " " + p.LastName);
        return VerifyTranslation(query);
    }

    [Fact]
    public Task Select_PathSegmentStartNodeProperty()
    {
        var query = Root.Nodes<Person>()
            .PathSegments<Person, Knows, Person>()
            .Select(ps => ps.StartNode);
        return VerifyTranslation(query);
    }

    [Fact]
    public Task Select_PathSegmentEndNodeProperty()
    {
        var query = Root.Nodes<Person>()
            .PathSegments<Person, Knows, Person>()
            .Select(ps => ps.EndNode);
        return VerifyTranslation(query);
    }

    [Fact]
    public Task Select_PathSegmentRelationshipProperty()
    {
        var query = Root.Nodes<Person>()
            .PathSegments<Person, Knows, Person>()
            .Select(ps => ps.Relationship);
        return VerifyTranslation(query);
    }

    [Fact]
    public Task Select_AfterWhere()
    {
        var query = Root.Nodes<Person>().Where(p => p.Age > 18).Select(p => p.FirstName);
        return VerifyTranslation(query);
    }

    [Fact]
    public Task Select_ComplexCollectionProjection()
    {
        var query = Root.Nodes<Person>().Select(p => p.Offices.Select(office => office.City));
        return VerifyTranslation(query);
    }
}
