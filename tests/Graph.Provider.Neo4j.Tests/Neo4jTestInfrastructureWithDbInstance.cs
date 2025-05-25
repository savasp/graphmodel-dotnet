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

namespace Cvoya.Graph.Provider.Neo4j.Tests;

internal class Neo4jTestInfrastructureWithDbInstance : ITestInfrastructure
{
    private const string Endpoint = "bolt://localhost:7687";

    private readonly TestDatabase testDatabase;
    private readonly Neo4jGraphProvider provider;

    public Neo4jTestInfrastructureWithDbInstance()
    {
        // Read the connection endpoint, username, and password from environment variables
        var connectionString = Environment.GetEnvironmentVariable("NEO4J_CONNECTION_STRING") ?? Endpoint;
        var password = Environment.GetEnvironmentVariable("NEO4J_PASSWORD") ?? "password";
        var username = Environment.GetEnvironmentVariable("NEO4J_USERNAME") ?? "neo4j";
        this.testDatabase = new TestDatabase(connectionString, username, password);
        this.provider = new Neo4jGraphProvider(Endpoint, username, password, this.testDatabase.DatabaseName);

    }

    public IGraph GraphProvider => this.provider;

    public async Task GetReady()
    {
        await this.testDatabase.Clean();
    }

    public ValueTask DisposeAsync()
    {
        this.provider.Dispose();
        this.testDatabase.Dispose();

        return ValueTask.CompletedTask;
    }
}
