using System.Threading.Tasks;
using Cvoya.Graph.Client.Neo4j;
using Cvoya.Graph.Provider.Model;
using Docker.DotNet.Models;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Configurations;
using DotNet.Testcontainers.Containers;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestPlatform.CrossPlatEngine.Helpers;
using Testcontainers.Neo4j;
using Xunit;

namespace Cvoya.Graph.Client.Neo4j.Tests;

public class Neo4jGraphProviderTests : Cvoya.Graph.Provider.Model.Tests.GraphProviderTestsBase, IAsyncLifetime
{
    private readonly Neo4jContainer neo4jContainer = new Neo4jBuilder()
        .WithEnterpriseEdition(true)
        .WithAutoRemove(true)
        .WithName("cvoya.neo4j.testing." + Guid.NewGuid().ToString("N"))
        .WithCleanUp(true)
        .WithImage("neo4j:2025-enterprise")
        .Build();

    public Neo4jGraphProviderTests()
    {
    }

    public override async Task InitializeAsync()
    {
        await this.neo4jContainer.StartAsync();
        await base.InitializeAsync();
    }

    public override async Task DisposeAsync()
    {
        await this.neo4jContainer.DisposeAsync();
        await base.DisposeAsync();
    }

    protected override IGraphProvider CreateClient()
    {
        var logger = LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<Neo4jGraphProvider>();
        return new Neo4jGraphProvider(this.neo4jContainer.GetConnectionString(), "neo4j", "password", logger);
    }

    public override Task ResetDatabaseAsync()
    {
        Neo4jTestGraphProviderFactory.ResetDatabase();
        return Task.CompletedTask;
    }
}
