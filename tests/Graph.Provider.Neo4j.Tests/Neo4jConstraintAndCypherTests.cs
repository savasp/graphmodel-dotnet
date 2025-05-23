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

/*
using Cvoya.Graph.Model;
using Cvoya.Graph.Model.Tests;

namespace Cvoya.Graph.Provider.Neo4j.Tests;

public class Neo4jConstraintAndCypherTests : IClassFixture<Neo4jGraphProviderTests>
{
    private readonly IGraphProvider provider;

    public Neo4jConstraintAndCypherTests(Neo4jGraphProviderTests fixture)
    {
        provider = fixture.Provider!;
    }

    [Fact]
    public async Task CreatingDuplicateNodeIdThrows()
    {
        var person = new GraphProviderTestsBase.Person { FirstName = "Unique", LastName = "Test" };
        var created = await provider.CreateNode(person);
        var duplicate = new GraphProviderTestsBase.Person { Id = created.Id, FirstName = "Other", LastName = "Test" };
        await Assert.ThrowsAsync<GraphException>(() => provider.CreateNode(duplicate));
    }

    [Fact]
    public async Task CanExecuteCypherReturningMultipleNodes()
    {
        await provider.CreateNode(new GraphProviderTestsBase.Person { FirstName = "A", LastName = "X" });
        await provider.CreateNode(new GraphProviderTestsBase.Person { FirstName = "B", LastName = "X" });
        var cypher = "MATCH (n: Cvoya_Graph_Client_Model_Tests_GraphProviderTestsBase_Person) WHERE n.LastName = $ln RETURN n";
        var results = await provider.ExecuteCypher(cypher, new { ln = "X" });
        Assert.True(results.Count() >= 2);
    }

    [Fact]
    public async Task CanExecuteCypherWithNoResults()
    {
        var cypher = "MATCH (n: Cvoya_Graph_Client_Model_Tests_GraphProviderTestsBase_Person) WHERE n.FirstName = $fn RETURN n";
        var results = await provider.ExecuteCypher(cypher, new { fn = "Nonexistent" });
        Assert.Empty(results);
    }

    // Add more Neo4j-specific constraint and Cypher tests as needed
}
*/