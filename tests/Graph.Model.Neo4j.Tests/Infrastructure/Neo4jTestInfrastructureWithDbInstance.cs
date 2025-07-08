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

internal class Neo4jTestInfrastructureWithDbInstance : ITestInfrastructure
{
    private const string Endpoint = "bolt://localhost:7687";

    public string ConnectionString { get; }
    public string Username { get; } = "neo4j";
    public string Password { get; } = "password";

    public Neo4jTestInfrastructureWithDbInstance()
    {
        ConnectionString = Environment.GetEnvironmentVariable("NEO4J_CONNECTION_STRING") ?? Endpoint;
        Password = Environment.GetEnvironmentVariable("NEO4J_PASSWORD") ?? "password";
        Username = Environment.GetEnvironmentVariable("NEO4J_USERNAME") ?? "neo4j";
    }

    public ValueTask InitializeAsync()
    {
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }
}
