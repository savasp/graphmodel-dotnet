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

public class WhereTranslationTests : TranslationTestBase
{
    [Fact]
    public Task Where_SimplePredicate()
    {
        var query = Root.Nodes<Person>().Where(p => p.Age > 30);
        return VerifyTranslation(query);
    }

    [Fact]
    public Task Where_MultipleConditions_AndOr()
    {
        var query = Root.Nodes<Person>().Where(p => (p.Age > 30 && p.FirstName == "Alice") || p.LastName == "Smith");
        return VerifyTranslation(query);
    }

    [Fact]
    public Task Where_ChainedWhere()
    {
        var query = Root.Nodes<Person>().Where(p => p.Age > 18).Where(p => p.Age < 65);
        return VerifyTranslation(query);
    }

    [Fact]
    public Task Where_NullComparison()
    {
        var query = Root.Nodes<Person>().Where(p => p.HomeAddress != null);
        return VerifyTranslation(query);
    }

    [Fact]
    public Task Where_EnumComparison()
    {
        var query = Root.Nodes<Person>().Where(p => p.Status == EmploymentStatus.Active);
        return VerifyTranslation(query);
    }

    [Fact]
    public Task Where_ClosureCapturedVariable()
    {
        var minAge = 21;
        var query = Root.Nodes<Person>().Where(p => p.Age >= minAge);
        return VerifyTranslation(query);
    }

    [Fact]
    public Task Where_CapturedQueryableConstantContainsEntityId()
    {
        IQueryable<string> ids = new[] { "person-1", "person-2" }.AsQueryable();
        var query = Root.Nodes<Person>().Where(p => ids.Contains(p.Id));
        return VerifyTranslation(query);
    }

    [Fact]
    public Task Where_ComplexPropertyNavigation()
    {
        var query = Root.Nodes<Person>().Where(p => p.HomeAddress!.City == "Seattle");
        return VerifyTranslation(query);
    }

    [Fact]
    public Task Where_OnRelationshipQueryable()
    {
        var query = Root.Relationships<Knows>().Where(k => k.Since > 2020);
        return VerifyTranslation(query);
    }
}
