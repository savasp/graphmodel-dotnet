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
    private static Neo4jContainer? container;

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
            .WithEnvironment("apoc.trigger.enabled", "true")
            .Build();
    }

    public string ConnectionString => Active
        ? container?.GetConnectionString().Replace("neo4j", "bolt") ?? throw new InvalidOperationException("Container hasn't been initialized. You must call InitializeAsync() first.")
        : throw new InvalidOperationException("Container is not running");

    public string Username => "neo4j"; // Default username for Neo4j without authentication
    public string Password => "password"; // Default password for Neo4j without authentication

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

    private bool Active => (container?.State ?? throw new InvalidOperationException("Container hasn't been initialized. You must call InitializeAsync() first."))
        == TestcontainersStates.Running;

    private async Task EnsureReady()
    {
        if (container is null)
        {
            throw new InvalidOperationException("Container hasn't been initialized. You must call InitializeAsync() first.");
        }

        if (container.State != TestcontainersStates.Running)
        {
            await container.StartAsync();
        }
    }
}