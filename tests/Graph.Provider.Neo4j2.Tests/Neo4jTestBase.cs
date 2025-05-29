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

using Cvoya.Graph.Model;
using Cvoya.Graph.Model.Tests;
using Cvoya.Graph.Provider.Neo4j;
using Microsoft.Extensions.Logging;
using Neo4j.Driver;
using Testcontainers.Neo4j;

namespace Cvoya.Graph.Provider.Neo4j.Tests;

/// <summary>
/// Base test class for Neo4j provider tests using Docker containers
/// </summary>
public class Neo4jTestBase : ITestBase, IAsyncDisposable
{
    private readonly Neo4jContainer _neo4jContainer;
    private Neo4jGraphProvider? _graph;
    private bool _disposed;

    public Neo4jTestBase()
    {
        _neo4jContainer = new Neo4jBuilder()
            .WithImage("neo4j:5.25")
            .WithEnvironment("NEO4J_AUTH", "neo4j/testpassword")
            .WithEnvironment("NEO4J_PLUGINS", "[\"apoc\"]")
            .WithPortBinding(7474, 7474)
            .WithPortBinding(7687, 7687)
            .Build();
    }

    public IGraph Graph
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _graph ?? throw new InvalidOperationException("Test not initialized. Call InitializeAsync first.");
        }
    }

    public async Task InitializeAsync()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(Neo4jTestBase));

        await _neo4jContainer.StartAsync();

        var connectionString = _neo4jContainer.GetConnectionString();
        var uri = new Uri(connectionString);

        _graph = new Neo4jGraphProvider(
            uri: $"bolt://{uri.Host}:{uri.Port}",
            username: "neo4j",
            password: "testpassword",
            databaseName: "neo4j"
        );

        // Wait for the database to be ready and clear any existing data
        await ClearDatabaseAsync();
    }

    private async Task ClearDatabaseAsync()
    {
        const int maxRetries = 30;
        const int delayMs = 1000;

        for (int i = 0; i < maxRetries; i++)
        {
            try
            {
                using var driver = GraphDatabase.Driver(
                    $"bolt://{_neo4jContainer.Hostname}:7687",
                    AuthTokens.Basic("neo4j", "testpassword"));

                await using var session = driver.AsyncSession();
                await session.RunAsync("MATCH (n) DETACH DELETE n");
                return;
            }
            catch (Exception)
            {
                if (i == maxRetries - 1)
                    throw;

                await Task.Delay(delayMs);
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        try
        {
            if (_graph != null)
                await _graph.DisposeAsync();

            await _neo4jContainer.DisposeAsync();
        }
        finally
        {
            _disposed = true;
        }
    }
}
