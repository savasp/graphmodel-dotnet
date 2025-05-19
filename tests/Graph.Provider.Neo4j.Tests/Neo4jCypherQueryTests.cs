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
using Cvoya.Graph.Provider.Model;
using Cvoya.Graph.Provider.Model.Tests;

namespace Cvoya.Graph.Provider.Neo4j.Tests;

public class Neo4jCypherQueryTests : IClassFixture<Neo4jGraphProviderTests>
{
    private readonly IGraphProvider _client;

    public Neo4jCypherQueryTests(Neo4jGraphProviderTests fixture)
    {
        _client = fixture.Provider!;
    }

    [Fact]
    public async Task CanExecuteRawCypherQuery()
    {
        var person = new GraphProviderTestsBase.Person { FirstName = "Raw", LastName = "Cypher" };
        var created = await _client.CreateNode(person);
        var cypher = "MATCH (n: Cvoya_Graph_Client_Model_Tests_GraphProviderTestsBase_Person) WHERE n.Id = $id RETURN n";
        var results = await _client.ExecuteCypher(cypher, new { id = created.Id });
        Assert.Single(results);
    }

    [Fact]
    public async Task CanExecuteCypherWithParameters()
    {
        var person = new GraphProviderTestsBase.Person { FirstName = "Param", LastName = "Test" };
        var created = await _client.CreateNode(person);
        var cypher = "MATCH (n: Cvoya_Graph_Client_Model_Tests_GraphProviderTestsBase_Person) WHERE n.FirstName = $firstName RETURN n";
        var results = await _client.ExecuteCypher(cypher, new { firstName = "Param" });
        Assert.NotEmpty(results);
    }

    // Add more Neo4j-specific Cypher query tests as needed
}
*/