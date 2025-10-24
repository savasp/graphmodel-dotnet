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

namespace Cvoya.Graph.Model.Age.Tests.Infrastructure;

using System.Collections.Concurrent;
using System.Threading;
using Microsoft.Extensions.Logging;
using Npgsql;
using Npgsql.Age;

/// <summary>
/// Manages a pool of AGE graphs in a PostgreSQL database for parallel test execution.
/// Each "database" is actually a separate AGE graph within the same PostgreSQL database.
/// </summary>
public sealed class DatabasePoolManager : IAsyncDisposable
{
    // -- Singleton instance of the DatabasePoolManager --
    private static Task<DatabasePoolManager>? instanceTask;
    private static readonly Lock initLock = new();

    public static Task<DatabasePoolManager> GetInstanceAsync(
        NpgsqlDataSource dataSource,
        ILoggerFactory loggerFactory,
        int maxPoolSize = 10)
    {
        if (instanceTask != null) return instanceTask;

        lock (initLock)
        {
            if (instanceTask == null)
            {
                instanceTask = CreateAsync(dataSource, loggerFactory, maxPoolSize);
            }
        }

        return instanceTask;
    }

    private static Task<DatabasePoolManager> CreateAsync(
        NpgsqlDataSource dataSource,
        ILoggerFactory loggerFactory,
        int graphCount)
    {
        var manager = new DatabasePoolManager(dataSource, loggerFactory, graphCount);
        _ = manager.SetupGraphsAsync(); // don't wait for setup to complete
        return Task.FromResult(manager);
    }

    // -- Instance definition --

    private readonly SemaphoreSlim graphsAreAvailableSemaphore;
    private readonly ConcurrentQueue<string> availableGraphs = new();

    private readonly int maxPoolSize;
    private readonly NpgsqlDataSource dataSource;
    private readonly ILogger<DatabasePoolManager> logger;

    private BackgroundWorker worker;

    private DatabasePoolManager(
        NpgsqlDataSource dataSource,
        ILoggerFactory loggerFactory,
        int maxPoolSize)
    {
        logger = loggerFactory.CreateLogger<DatabasePoolManager>();

        this.dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
        this.maxPoolSize = maxPoolSize;
        this.graphsAreAvailableSemaphore = new SemaphoreSlim(0, maxPoolSize);
        this.worker = new BackgroundWorker(loggerFactory);
    }

    public async ValueTask DisposeAsync()
    {
        logger.LogDebug("Disposing DatabasePool");

        await worker.DisposeAsync();

        // Drop all graphs
        for (int i = 0; i < maxPoolSize; i++)
        {
            var graphName = GenerateGraphName(i);
            await DropGraphAsync(graphName);
        }
    }

    public async Task<string> RequestDatabaseAsync()
    {
        logger.LogDebug("Requesting an available graph from the pool");
        await graphsAreAvailableSemaphore.WaitAsync();
        logger.LogDebug("A graph is available, acquiring it");
        availableGraphs.TryDequeue(out var graphName);
        if (graphName != null)
        {
            logger.LogDebug("Acquired graph {GraphName} from the pool", graphName);
            return graphName;
        }
        throw new InvalidOperationException("No available graphs in the pool. This should not have happened because of the semaphore.");
    }

    public async Task ReleaseDatabaseAsync(string graphName)
    {
        logger.LogDebug("Releasing graph {GraphName} back to the pool", graphName);
        // Don't drop/recreate here - connections may still be in the pool
        // The graph will be cleaned before next use anyway
        availableGraphs.Enqueue(graphName);
        graphsAreAvailableSemaphore.Release();
        logger.LogDebug("Graph {GraphName} is now available in the pool", graphName);
    }

    /// <summary>
    /// Assumes that the given graph has already been acquired and is considered
    /// to be in use. Cleans the graph by deleting all nodes and relationships.
    /// </summary>
    public async Task CleanDatabaseAsync(string graphName)
    {
        ArgumentNullException.ThrowIfNull(graphName, nameof(graphName));
        logger.LogDebug("Scheduling cleaning of graph {GraphName}", graphName);
        await worker.Schedule(async () =>
        {
            logger.LogDebug("Cleaning graph {GraphName}", graphName);
            await using var connection = await dataSource.OpenConnectionAsync().ConfigureAwait(false);
            await ConfigureSessionAsync(connection, graphName).ConfigureAwait(false);

            // Delete all nodes and relationships using Cypher
            await using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = $"SELECT * FROM ag_catalog.cypher('{graphName}', $$ MATCH (n) DETACH DELETE n $$) as (a agtype)";
                await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
            }

            logger.LogDebug("Graph {GraphName} cleaned successfully", graphName);
        });
    }

    private async Task SetupGraphsAsync()
    {
        logger.LogDebug("Setting up graphs in the pool");
        for (var i = 0; i < maxPoolSize; i++)
        {
            var graphName = GenerateGraphName(i);

            // Schedule the creation of each graph
            // We don't schedule the creation of all the graphs at once
            // so that tests don't have to wait until all graphs are created
            // before they can start running. This is because
            // the scheduler queues all workloads.
            await worker.Schedule(async () =>
            {
                logger.LogDebug("Creating graph {GraphName} in the pool", graphName);
                await CreateOrReplaceGraphAsync(graphName);
                availableGraphs.Enqueue(graphName);
                logger.LogDebug("Graph {GraphName} is ready for use", graphName);
                graphsAreAvailableSemaphore.Release(); // Signal that a graph is available
            });
        }
    }

    private async Task CreateOrReplaceGraphAsync(string graphName)
    {
        logger.LogDebug("Creating or replacing graph {GraphName}", graphName);
        await using var connection = await dataSource.OpenConnectionAsync().ConfigureAwait(false);
        await ConfigureSessionAsync(connection, graphName).ConfigureAwait(false);

        // Drop graph if it already exists (ignore if it does not)
        await using (var drop = connection.CreateCommand())
        {
            // AGE functions require literal graph names, not parameters
            drop.CommandText = $"SELECT drop_graph('{graphName}', true)";
            try
            {
                // drop_graph returns a result set, we need to fully consume it
                await using var reader = await drop.ExecuteReaderAsync().ConfigureAwait(false);
                while (await reader.ReadAsync().ConfigureAwait(false))
                {
                    // Just consume the rows, we don't care about the result
                }
            }
            catch (PostgresException ex) when (ex.SqlState is "42704" or "3F000")
            {
                // Graph does not exist yet - ignore.
                logger.LogDebug("Graph {GraphName} did not exist prior to reset", graphName);
            }
        }

        // Create the graph
        await using (var create = connection.CreateCommand())
        {
            // AGE functions require literal graph names, not parameters
            create.CommandText = $"SELECT create_graph('{graphName}')";
            // create_graph returns a result set, we need to fully consume it
            await using var reader = await create.ExecuteReaderAsync().ConfigureAwait(false);
            while (await reader.ReadAsync().ConfigureAwait(false))
            {
                // Just consume the rows, we don't care about the result
            }
        }

        logger.LogDebug("Graph {GraphName} is now ready", graphName);
    }

    private async Task DropGraphAsync(string graphName)
    {
        logger.LogDebug("Scheduling drop of graph {GraphName}", graphName);
        try
        {
            logger.LogDebug("Dropping graph {GraphName}", graphName);
            await using var connection = await dataSource.OpenConnectionAsync().ConfigureAwait(false);
            await ConfigureSessionAsync(connection, graphName).ConfigureAwait(false);

            await using (var drop = connection.CreateCommand())
            {
                // AGE functions require literal graph names, not parameters
                drop.CommandText = $"SELECT drop_graph('{graphName}', true)";
                // drop_graph returns a result set, we need to fully consume it
                await using var reader = await drop.ExecuteReaderAsync().ConfigureAwait(false);
                while (await reader.ReadAsync().ConfigureAwait(false))
                {
                    // Just consume the rows, we don't care about the result
                }
            }

            logger.LogDebug("Graph {GraphName} dropped successfully", graphName);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to drop graph {GraphName}. It may not exist or is already dropped.", graphName);
            // Ignore errors during cleanup - the graph might not exist
        }
    }

    private static async Task ConfigureSessionAsync(NpgsqlConnection connection, string graphName)
    {
        await using (var loadCmd = connection.CreateCommand())
        {
            loadCmd.CommandText = "LOAD 'age'";
            await loadCmd.ExecuteNonQueryAsync().ConfigureAwait(false);
        }

        await using (var searchPathCmd = connection.CreateCommand())
        {
            searchPathCmd.CommandText = "SET search_path = ag_catalog, \"$user\", public";
            await searchPathCmd.ExecuteNonQueryAsync().ConfigureAwait(false);
        }
    }

    private string GenerateGraphName(int i)
    {
        return "graphmodeltests" + i.ToString("D3");
    }
}
