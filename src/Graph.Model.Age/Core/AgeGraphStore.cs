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

namespace Cvoya.Graph.Model.Age.Core;

using Cvoya.Graph.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Npgsql.Age;

/// <summary>
/// Represents a connection factory for Apache AGE graphs hosted in PostgreSQL.
/// </summary>
public sealed class AgeGraphStore : IAsyncDisposable
{
    private readonly NpgsqlDataSource dataSource;
    private readonly bool ownsDataSource;
    private readonly string graphName;
    private readonly SchemaRegistry schemaRegistry;
    private readonly ILoggerFactory loggerFactory;
    private readonly Task<AgeGraph> graphInitTask;

    /// <summary>
    /// Initializes a new instance of the <see cref="AgeGraphStore"/> class.
    /// </summary>
    /// <param name="connectionString">PostgreSQL connection string.</param>
    /// <param name="graphName">
    /// Logical AGE graph name. Defaults to the value of the <c>AGE_GRAPH</c> environment variable or <c>"graph_model"</c>.
    /// </param>
    /// <param name="schemaRegistry">Optional schema registry instance.</param>
    /// <param name="loggerFactory">Optional logger factory.</param>
    public AgeGraphStore(
        string? connectionString,
        string? graphName = null,
        SchemaRegistry? schemaRegistry = null,
        ILoggerFactory? loggerFactory = null)
    {
        connectionString ??= Environment.GetEnvironmentVariable("AGE_CONNECTION_STRING") ?? "Host=localhost;Port=5432;Username=postgres;Password=postgres;Database=postgres";
        graphName ??= Environment.GetEnvironmentVariable("AGE_GRAPH") ?? "graph_model";

        var builder = new NpgsqlDataSourceBuilder(connectionString);
        builder.UseAge();

        dataSource = builder.Build();
        ownsDataSource = true;

        this.graphName = graphName;
        this.schemaRegistry = schemaRegistry ?? new SchemaRegistry();
        this.loggerFactory = loggerFactory ?? NullLoggerFactory.Instance;

        // Initialize the graph asynchronously
        graphInitTask = InitializeGraphAsync();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AgeGraphStore"/> class using an existing data source.
    /// </summary>
    /// <param name="dataSource">A configured <see cref="NpgsqlDataSource"/> with AGE enabled.</param>
    /// <param name="graphName">Logical AGE graph name.</param>
    /// <param name="schemaRegistry">Optional schema registry instance.</param>
    /// <param name="loggerFactory">Optional logger factory.</param>
    public AgeGraphStore(
        NpgsqlDataSource dataSource,
        string graphName,
        SchemaRegistry? schemaRegistry = null,
        ILoggerFactory? loggerFactory = null)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        ArgumentException.ThrowIfNullOrWhiteSpace(graphName);

        this.dataSource = dataSource;
        this.graphName = graphName;
        this.schemaRegistry = schemaRegistry ?? new SchemaRegistry();
        this.loggerFactory = loggerFactory ?? NullLoggerFactory.Instance;
        ownsDataSource = false;

        // Initialize the graph asynchronously
        graphInitTask = InitializeGraphAsync();
    }

    /// <summary>
    /// Gets the graph abstraction for the configured AGE data source.
    /// This property will wait for the connection to be established and configured.
    /// </summary>
    public IGraph Graph => graphInitTask.GetAwaiter().GetResult();

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        var graph = await graphInitTask.ConfigureAwait(false);
        await graph.DisposeAsync().ConfigureAwait(false);

        if (ownsDataSource)
        {
            await dataSource.DisposeAsync().ConfigureAwait(false);
        }
    }

    internal NpgsqlDataSource DataSource => dataSource;

    internal string GraphName => graphName;

    internal SchemaRegistry SchemaRegistry => schemaRegistry;

    private async Task<AgeGraph> InitializeGraphAsync()
    {
        // Open a connection
        var connection = await dataSource.OpenConnectionAsync().ConfigureAwait(false);

        try
        {
            // Configure the connection for AGE
            await ConfigureConnectionAsync(connection).ConfigureAwait(false);

            // Create the graph instance with the open, configured connection
            return new AgeGraph(connection, graphName, schemaRegistry, loggerFactory);
        }
        catch
        {
            // If initialization fails, dispose the connection
            await connection.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    private static async Task ConfigureConnectionAsync(NpgsqlConnection connection)
    {
        // Load AGE extension
        await using (var loadCmd = connection.CreateCommand())
        {
            loadCmd.CommandText = "LOAD 'age'";
            await loadCmd.ExecuteNonQueryAsync().ConfigureAwait(false);
        }

        // Set search path
        await using (var searchPathCmd = connection.CreateCommand())
        {
            searchPathCmd.CommandText = "SET search_path = ag_catalog, \"$user\", public";
            await searchPathCmd.ExecuteNonQueryAsync().ConfigureAwait(false);
        }
    }
}
