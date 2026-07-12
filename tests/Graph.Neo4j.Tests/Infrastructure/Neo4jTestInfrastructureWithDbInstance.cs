// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Neo4j.Tests;

internal sealed class Neo4jTestInfrastructureWithDbInstance : ITestInfrastructure
{
    private const string Endpoint = "bolt://localhost:7687";

    public string ConnectionString { get; }
    public string Username { get; } = "neo4j";
    public string Password { get; } = "password";

    public Neo4jTestInfrastructureWithDbInstance()
    {
        ConnectionString = Environment.GetEnvironmentVariable("NEO4J_URI")
            ?? Environment.GetEnvironmentVariable("NEO4J_CONNECTION_STRING")
            ?? Endpoint;
        Password = Environment.GetEnvironmentVariable("NEO4J_PASSWORD") ?? "password";
        Username = Environment.GetEnvironmentVariable("NEO4J_USER")
            ?? Environment.GetEnvironmentVariable("NEO4J_USERNAME")
            ?? "neo4j";
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
