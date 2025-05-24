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
using Microsoft.Extensions.Logging;
using Neo4j.Driver;

namespace Cvoya.Graph.Provider.Neo4j.Tests;

internal class Neo4jTestInfrastructureWithDbInstance : ITestInfrastructure
{
    private static Microsoft.Extensions.Logging.ILogger logger = LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<Neo4jGraphProvider>();
    private const string Endpoint = "bolt://localhost:7687";

    private readonly Neo4jGraphProvider provider;
    private readonly string databaseName = "tests" + Guid.NewGuid().ToString("N");

    public Neo4jTestInfrastructureWithDbInstance()
    {
        CreateDatabase().GetAwaiter().GetResult();
        this.provider = new Neo4jGraphProvider(Endpoint, "neo4j", "password", this.databaseName, logger);
    }

    public IGraph GraphProvider => this.provider;

    public async Task ResetDatabase()
    {
        await using var driver = GraphDatabase.Driver(Endpoint, AuthTokens.Basic("neo4j", "password"));
        await using var session = driver.AsyncSession(builder => builder.WithDatabase(this.databaseName));
        await session.RunAsync("MATCH (n) DETACH DELETE n");
    }

    public Task<string> EnsureReady()
    {
        return Task.FromResult(Endpoint);
    }

    public async ValueTask DisposeAsync()
    {
        await this.ExecuteCypherOnSystemDb($"DROP DATABASE {this.databaseName} IF EXISTS");
        this.provider.Dispose();
    }

    private async Task CreateDatabase()
    {
        await this.ExecuteCypherOnSystemDb($"CREATE DATABASE {this.databaseName} IF NOT EXISTS");

        // Wait for the database to be online
        await WaitForDatabaseOnline();
    }

    private async Task WaitForDatabaseOnline(int maxAttempts = 30, int delayMs = 500)
    {
        await using var driver = GraphDatabase.Driver(Endpoint, AuthTokens.Basic("neo4j", "password"));

        // First, wait for SHOW DATABASES to report online
        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            await using var session = driver.AsyncSession(builder => builder.WithDatabase("system"));
            var result = await session.RunAsync($"SHOW DATABASES YIELD name, currentStatus WHERE name = '{this.databaseName}' RETURN currentStatus");
            var record = await result.SingleOrDefaultAsync();
            var status = record?["currentStatus"].As<string>();
            if (status == "online")
            {
                break;
            }
            await Task.Delay(delayMs);
        }

        // Second, wait until the driver can actually connect to the database
        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            try
            {
                await using var session = driver.AsyncSession(builder => builder.WithDatabase(this.databaseName));
                var result = await session.RunAsync("RETURN 1");
                await result.ConsumeAsync();
                return;
            }
            catch (Neo4jException ex) when (ex.Message.Contains("not found") || ex.Message.Contains("does not exist"))
            {
                // Database not yet available for driver
            }
            await Task.Delay(delayMs);
        }
        throw new Exception($"Database '{this.databaseName}' did not become available for driver connections in time.");
    }

    private async Task ExecuteCypherOnSystemDb(string cypher)
    {
        await using var driver = GraphDatabase.Driver(Endpoint, AuthTokens.Basic("neo4j", "password"));
        await using var session = driver.AsyncSession(builder => builder.WithDatabase("system"));
        await session.RunAsync(cypher);
    }
}
