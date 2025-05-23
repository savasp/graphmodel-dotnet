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
using Microsoft.Extensions.Logging;
using Testcontainers.Neo4j;

namespace Cvoya.Graph.Provider.Neo4j.Tests;

internal class Neo4jTestInfrastructureWithContainer : ITestInfrastructure
{
    private static int numberOfInstances = 0;
    private static readonly Lock @lock = new();
    private static Neo4jContainer? container;
    private Neo4jGraphProvider provider;

    public Neo4jTestInfrastructureWithContainer()
    {
        var connectionString = EnsureReady().GetAwaiter().GetResult();
        var logger = LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<Neo4jGraphProvider>();
        this.provider = new Neo4jGraphProvider(connectionString, username: "neo4j", password: "password", logger: logger);
    }

    public IGraph GraphProvider => this.provider;

    public async Task ResetDatabase()
    {
        await provider.ExecuteCypher("CREATE OR REPLACE DATABASE tests");
    }

    public async Task<string> EnsureReady()
    {
        if (container != null && container.State == TestcontainersStates.Running)
        {
            return container.GetConnectionString();
        }

        lock (@lock)
        {
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
        }

        if (container.State != TestcontainersStates.Running)
        {
            Interlocked.Increment(ref numberOfInstances);
            await container.StartAsync();
        }

        return container.GetConnectionString();
    }

    public async ValueTask DisposeAsync()
    {
        Interlocked.Decrement(ref numberOfInstances);
        if (container != null && Volatile.Read(ref numberOfInstances) == 0)
        {
            await container.DisposeAsync();
            container = null;
        }
    }
}
