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

namespace Cvoya.Graph.Model.Neo4j.Tests;

using DotNet.Testcontainers.Containers;
using Testcontainers.Neo4j;


internal class Neo4jTestInfrastructureWithContainer : ITestInfrastructure
{
    private static Neo4jContainer container;

    private Neo4jGraphStore? store;
    private TestDatabase? testDatabase;

    static Neo4jTestInfrastructureWithContainer()
    {
        // Initialize the Neo4j test container. There is one per process.
        container = new Neo4jBuilder()
            .WithEnterpriseEdition(true)
            .WithAutoRemove(true)
            .WithName("cvoya.neo4j.testing.shared")
            .WithCleanUp(true)
            .WithImage("neo4j:2025-enterprise")
            .WithEnvironment("NEO4J_AUTH", "none")
            .WithEnvironment("NEO4JLABS_PLUGINS", "[\"apoc\"]")
            .WithEnvironment("NEO4J_dbms_security_procedures_unrestricted", "apoc.*")
            .WithEnvironment("NEO4J_dbms_security_procedures_allowlist", "apoc.*")
            .Build();
    }

    public Neo4jGraphStore GraphStore => store ?? throw new InvalidOperationException("Graph store is not initialized.");

    public async Task Setup()
    {
        // Ensure the container is running and ready
        await EnsureReady();

        if (container == null)
        {
            throw new InvalidOperationException("Container is not initialized.");
        }

        // Create the test database and provider. The container is set up to not use authentication.
        var connectionString = container.GetConnectionString().Replace("neo4j", "bolt");
        this.testDatabase = new TestDatabase(connectionString);
        await this.testDatabase.Setup();
        this.store = new Neo4jGraphStore(connectionString, username: null, password: null, this.testDatabase.DatabaseName);
    }

    private async Task<string> EnsureReady()
    {
        if (container.State != TestcontainersStates.Running)
        {
            await container.StartAsync();
        }

        return container.GetConnectionString();
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
        if (store != null)
        {
            await store.DisposeAsync();
            store = null;
        }

        if (testDatabase != null)
        {
            await testDatabase.DisposeAsync();
            testDatabase = null;
        }
    }
}