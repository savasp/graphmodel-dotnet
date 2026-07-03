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
