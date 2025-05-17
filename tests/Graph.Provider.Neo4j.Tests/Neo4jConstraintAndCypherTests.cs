using System;
using System.Linq;
using System.Threading.Tasks;
using Cvoya.Graph.Client.Neo4j;
using Cvoya.Graph.Provider.Model;
using Cvoya.Graph.Provider.Model.Tests;
using Xunit;

namespace Cvoya.Graph.Client.Neo4j.Tests;

public class Neo4jConstraintAndCypherTests : IClassFixture<Neo4jGraphProviderTests>
{
    private readonly IGraphProvider _client;

    public Neo4jConstraintAndCypherTests(Neo4jGraphProviderTests fixture)
    {
        _client = fixture.Client!;
    }

    [Fact]
    public async Task CreatingDuplicateNodeIdThrows()
    {
        var person = new GraphProviderTestsBase.Person { FirstName = "Unique", LastName = "Test" };
        var created = await _client.CreateNode(person);
        var duplicate = new GraphProviderTestsBase.Person { Id = created.Id, FirstName = "Other", LastName = "Test" };
        await Assert.ThrowsAsync<GraphProviderException>(() => _client.CreateNode(duplicate));
    }

    [Fact]
    public async Task CanExecuteCypherReturningMultipleNodes()
    {
        await _client.CreateNode(new GraphProviderTestsBase.Person { FirstName = "A", LastName = "X" });
        await _client.CreateNode(new GraphProviderTestsBase.Person { FirstName = "B", LastName = "X" });
        var cypher = "MATCH (n: Cvoya_Graph_Client_Model_Tests_GraphProviderTestsBase_Person) WHERE n.LastName = $ln RETURN n";
        var results = await _client.ExecuteCypher(cypher, new { ln = "X" });
        Assert.True(results.Count() >= 2);
    }

    [Fact]
    public async Task CanExecuteCypherWithNoResults()
    {
        var cypher = "MATCH (n: Cvoya_Graph_Client_Model_Tests_GraphProviderTestsBase_Person) WHERE n.FirstName = $fn RETURN n";
        var results = await _client.ExecuteCypher(cypher, new { fn = "Nonexistent" });
        Assert.Empty(results);
    }

    // Add more Neo4j-specific constraint and Cypher tests as needed
}
