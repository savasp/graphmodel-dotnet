using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Cvoya.Graph.Client.Neo4j;
using Cvoya.Graph.Provider.Model;
using Cvoya.Graph.Provider.Model.Tests;
using Xunit;

namespace Cvoya.Graph.Client.Neo4j.Tests;

public class Neo4jCypherQueryTests : IClassFixture<Neo4jGraphProviderTests>
{
    private readonly IGraphProvider _client;

    public Neo4jCypherQueryTests(Neo4jGraphProviderTests fixture)
    {
        _client = fixture.Client!;
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
