// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Neo4j.Tests;

using DotNet.Testcontainers.Containers;
using Testcontainers.Neo4j;

internal sealed class Neo4jTestInfrastructureWithContainer : ITestInfrastructure
{
    private const string ContainerUsername = "neo4j";
    private const string ContainerPassword = "password";
    private static Neo4jContainer? container;

    private static Neo4jContainer Container => container ??= new Neo4jBuilder("neo4j:5")
        .WithAutoRemove(true)
        .WithCleanUp(true)
        .WithEnvironment("NEO4J_AUTH", $"{ContainerUsername}/{ContainerPassword}")
        .WithEnvironment("NEO4J_PLUGINS", "[\"apoc\"]")
        .WithEnvironment("NEO4J_dbms_security_procedures_unrestricted", "apoc.*")
        .WithEnvironment("NEO4J_dbms_security_procedures_allowlist", "apoc.*")
        .WithEnvironment("apoc.trigger.enabled", "true")
        .Build();

    public string ConnectionString
    {
        get
        {
            if (!Active)
            {
                throw new InvalidOperationException("Container is not running");
            }

            var connectionString = Container.GetConnectionString();
            return connectionString.StartsWith("neo4j://", StringComparison.Ordinal)
                ? $"bolt://{connectionString["neo4j://".Length..]}"
                : connectionString;
        }
    }

    public string Username => ContainerUsername;

    public string Password => ContainerPassword;

    public async ValueTask InitializeAsync()
    {
        // Ensure the container is running and ready
        await EnsureReady();
    }

    public async ValueTask DisposeAsync()
    {
        if (container != null)
        {
            await container.DisposeAsync();
            container = null;
        }
    }

    private static bool Active => (container?.State ?? throw new InvalidOperationException("Container hasn't been initialized. You must call InitializeAsync() first."))
        == TestcontainersStates.Running;

    private static async Task EnsureReady()
    {
        if (Container.State != TestcontainersStates.Running)
        {
            await Container.StartAsync();
        }
    }
}
