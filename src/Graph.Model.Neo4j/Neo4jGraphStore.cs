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


using Neo4j.Driver;

namespace Cvoya.Graph.Model.Neo4j;

/// <summary>
/// Represents a Neo4j graph.
/// </summary>
public class Neo4jGraphStore : IAsyncDisposable
{
    private readonly IDriver _driver;

    /// <summary>
    /// Initializes a new instance of the <see cref="Neo4j.Graph"/> class.
    /// </summary>
    /// <param name="uri">The URI of the Neo4j database.</param>
    /// <param name="username">The username for authentication.</param>
    /// <param name="password">The password for authentication.</param>
    /// <param name="databaseName">The name of the database.</param>
    /// <param name="loggerFactory">The logger factory instance.</param>
    /// <remarks>
    /// The environment variables used for configuration, if not provided, are:
    /// - NEO4J_URI: The URI of the Neo4j database. Default: "bolt://localhost:7687".
    /// - NEO4J_USER: The username for authentication. Default: "neo4j".
    /// - NEO4J_PASSWORD: The password for authentication. Default: "password".
    /// - NEO4J_DATABASE: The name of the database. Default: "neo4j".
    /// </remarks>
    public Neo4jGraphStore(
        string? uri,
        string? username,
        string? password,
        string? databaseName = "neo4j",
        Microsoft.Extensions.Logging.ILoggerFactory? loggerFactory = null)
    {
        uri ??= Environment.GetEnvironmentVariable("NEO4J_URI") ?? "bolt://localhost:7687";
        username ??= Environment.GetEnvironmentVariable("NEO4J_USER") ?? "neo4j";
        password ??= Environment.GetEnvironmentVariable("NEO4J_PASSWORD") ?? "password";
        databaseName ??= Environment.GetEnvironmentVariable("NEO4J_DATABASE") ?? "neo4j";

        // Create the Neo4j driver
        _driver = GraphDatabase.Driver(uri, AuthTokens.Basic(username, password));
        Graph = new Graph(_driver, databaseName, loggerFactory);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Neo4jGraphStore"/> class with an existing Neo4j driver.
    /// </summary>
    /// <param name="driver">The Neo4j driver instance.</param>
    /// <param name="databaseName">The name of the database.</param>
    /// <param name="loggerFactory">The logger factory instance.</param>
    /// <remarks>
    /// The environment variable NEO4J_DATABASE can be used to specify the database name.
    /// If not provided, it defaults to "neo4j".
    /// </remarks>
    public Neo4jGraphStore(
        IDriver driver,
        string databaseName = "neo4j",
        Microsoft.Extensions.Logging.ILoggerFactory? loggerFactory = null)
    {
        ArgumentNullException.ThrowIfNull(driver, nameof(driver));
        databaseName ??= Environment.GetEnvironmentVariable("NEO4J_DATABASE") ?? "neo4j";
        _driver = driver;
        Graph = new Graph(driver, databaseName, loggerFactory);
    }

    /// <inheritdoc />
    public IGraph Graph { get; }

    /// <summary>
    /// Creates a new database if it does not already exist.
    /// </summary>
    /// <param name="driver">The Neo4j driver instance.</param>
    /// <param name="databaseName">The name of the database to create.</param>
    /// <remarks>
    /// This method uses the "system" database to execute the command to create a new database.
    /// It checks if the database already exists and only creates it if it does not.
    /// </remarks>
    public static async Task CreateDatabaseIfNotExistsAsync(IDriver driver, string databaseName)
    {
        using var session = driver.AsyncSession(o => o.WithDatabase("system"));
        await session.RunAsync($"CREATE DATABASE `{databaseName}` IF NOT EXISTS");
    }

    /// <summary>
    /// Disposes the Neo4j graph store and its resources asynchronously.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        await Graph.DisposeAsync();
        await _driver.DisposeAsync();
    }
}