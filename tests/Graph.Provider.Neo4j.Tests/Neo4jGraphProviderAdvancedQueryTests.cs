using Cvoya.Graph.Provider.Model;
using Cvoya.Graph.Provider.Model.Tests;

namespace Cvoya.Graph.Client.Neo4j.Tests;

public class Neo4jGraphProviderAdvancedQueryTests : GraphProviderAdvancedQueryTestsBase
{
    private IGraphProvider client = Neo4jTestGraphProviderFactory.Create();
    public override Task ResetDatabaseAsync()
    {
        Neo4jTestGraphProviderFactory.ResetDatabase();
        return Task.CompletedTask;
    }

    protected override IGraphProvider CreateClient() => this.client;
}
