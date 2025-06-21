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

using Microsoft.Extensions.Logging;

namespace Cvoya.Graph.Model.Neo4j.Tests;

internal class Neo4jTestInfrastructureWithDbInstance : ITestInfrastructure
{
    private const string Endpoint = "bolt://localhost:7687";

    private TestDatabase? testDatabase;
    private Neo4jGraphStore? provider;

    public Neo4jGraphStore GraphStore => provider ?? throw new InvalidOperationException("Graph store is not initialized.");

    public async Task Setup()
    {
        var connectionString = Environment.GetEnvironmentVariable("NEO4J_CONNECTION_STRING") ?? Endpoint;
        var password = Environment.GetEnvironmentVariable("NEO4J_PASSWORD") ?? "password";
        var username = Environment.GetEnvironmentVariable("NEO4J_USERNAME") ?? "neo4j";
        testDatabase = new TestDatabase(connectionString, username, password);
        await testDatabase.Setup();

        var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Debug);
        });
        provider = new Neo4jGraphStore(connectionString, username, password, testDatabase.DatabaseName, loggerFactory);
    }

    public async Task ResetDatabase()
    {
        if (testDatabase != null)
        {
            await testDatabase.Reset();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (provider != null)
        {
            await provider.DisposeAsync();
            provider = null;
        }

        if (testDatabase != null)
        {
            await testDatabase.DisposeAsync();
            testDatabase = null;
        }
    }
}
