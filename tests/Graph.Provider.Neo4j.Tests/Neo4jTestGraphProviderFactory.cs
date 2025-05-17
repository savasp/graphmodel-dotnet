using System.Threading.Tasks;
using Cvoya.Graph.Client.Neo4j;
using Cvoya.Graph.Provider.Model;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Microsoft.Extensions.Logging;
using Testcontainers.Neo4j;

namespace Cvoya.Graph.Client.Neo4j.Tests;

public static class Neo4jTestGraphProviderFactory
{
    private static Neo4jContainer? _container;
    private static string? _connectionString;
    private static string? _username = "neo4j";
    private static string? _password = "neo4j";
    private static readonly object _lock = new();

    public static IGraphProvider Create()
    {
        EnsureContainerStarted().GetAwaiter().GetResult();
        var logger = LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<Neo4jGraphProvider>();
        return new Neo4jGraphProvider(_connectionString!, _username!, _password!, logger);
    }

    public static void ResetDatabase()
    {
        EnsureContainerStarted().GetAwaiter().GetResult();
        var logger = LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<Neo4jGraphProvider>();
        var client = new Neo4jGraphProvider(_connectionString!, _username!, _password!, logger);
        // Delete all nodes and relationships
        client.ExecuteCypher("MATCH (n) DETACH DELETE n").GetAwaiter().GetResult();
    }

    private static async Task EnsureContainerStarted()
    {
        if (_container != null && _container.State == TestcontainersStates.Running)
            return;
        lock (_lock)
        {
            if (_container == null)
            {
                _container = new Neo4jBuilder()
                    .WithEnterpriseEdition(true)
                    .WithAutoRemove(true)
                    .WithName("cvoya.neo4j.testing.shared")
                    .WithCleanUp(true)
                    .WithImage("neo4j:2025-enterprise")
                    .Build();
                // Username and password are set to defaults
            }
        }
        if (_container.State != TestcontainersStates.Running)
        {
            await _container.StartAsync();
            _connectionString = _container.GetConnectionString();
        }
    }
}
