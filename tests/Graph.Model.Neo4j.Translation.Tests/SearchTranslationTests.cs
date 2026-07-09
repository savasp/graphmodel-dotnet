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
