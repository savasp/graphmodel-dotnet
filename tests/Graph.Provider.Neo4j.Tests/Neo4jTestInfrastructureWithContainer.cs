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
using DotNet.Testcontainers.Containers;
using Testcontainers.Neo4j;

namespace Cvoya.Graph.Provider.Neo4j.Tests;

internal class Neo4jTestInfrastructureWithContainer : ITestInfrastructure
{
    private static int numberOfInstances = 0;
    private static readonly Lock @lock = new();
    private static Neo4jContainer? container;

    private Neo4jGraphProvider provider;
    private TestDatabase testDatabase;

    public Neo4jTestInfrastructureWithContainer()
    {
        // Ensure the container is running and ready
        EnsureReady().Wait();

        if (container == null)
        {
            throw new InvalidOperationException("Container is not initialized.");
        }

        // Create the test database and provider. The container is set up to not use authentication.
        var connectionString = container.GetConnectionString().Replace("neo4j", "bolt");
        this.testDatabase = new TestDatabase(connectionString);
        this.provider = new Neo4jGraphProvider(connectionString, username: null, password: null, this.testDatabase.DatabaseName);
    }

    public IGraph GraphProvider => this.provider ?? throw new InvalidOperationException("Graph provider is not initialized.");

    public async Task GetReady()
    {
        Interlocked.Increment(ref numberOfInstances);
        // Clean the database before each test
        await this.testDatabase.Clean();
    }

    public async ValueTask DisposeAsync()
    {
        this.provider?.Dispose();
        this.testDatabase?.Dispose();

        var remaining = Interlocked.Decrement(ref numberOfInstances);
        if (container != null && remaining == 0)
        {
            await container.DisposeAsync();
            container = null;
        }
    }

    private Task<string> EnsureReady()
    {
        lock (@lock)
        {
            if (container != null && container.State == TestcontainersStates.Running)
            {
                return Task.FromResult(container.GetConnectionString());
            }

            if (container == null)
            {
                container = new Neo4jBuilder()
                    .WithEnterpriseEdition(true)
                    .WithAutoRemove(true)
                    .WithName("cvoya.neo4j.testing.shared")
                    .WithCleanUp(true)
                    .WithImage("neo4j:2025-enterprise")
                    .WithEnvironment("NEO4J_AUTH", "none")
                    .Build();
            }

            if (container.State != TestcontainersStates.Running)
            {
                container.StartAsync().Wait();
            }

            return Task.FromResult(container.GetConnectionString());
        }
    }
}
