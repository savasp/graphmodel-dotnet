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
using System.Threading.Tasks;

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
        int maxPoolSize = 20)
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

    private static async Task<DatabasePoolManager> CreateAsync(
        NpgsqlDataSource dataSource,
        ILoggerFactory loggerFactory,
        int graphCount)
    {
        var manager = new DatabasePoolManager(dataSource, loggerFactory, graphCount);
        await manager.SetupGraphsAsync();
        return manager;
    }

    // -- Instance definition --

    private readonly SemaphoreSlim graphsAreAvailableSemaphore;
    private readonly ConcurrentQueue<string> availableGraphs = new();
    private readonly ConcurrentDictionary<string, bool> dirtyGraphs = new();

    private readonly int maxPoolSize;
    private readonly NpgsqlDataSource dataSource;
    private readonly ILogger<DatabasePoolManager> logger;

    private DatabasePoolManager(
        NpgsqlDataSource dataSource,
        ILoggerFactory loggerFactory,
        int maxPoolSize)
    {
        logger = loggerFactory.CreateLogger<DatabasePoolManager>();

        this.dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
        this.maxPoolSize = maxPoolSize;
        this.graphsAreAvailableSemaphore = new SemaphoreSlim(0, maxPoolSize);
    }

    public async ValueTask DisposeAsync()
    {
        logger.LogDebug("Disposing DatabasePool");

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

            // Lazy cleanup: Clean the graph NOW if it's dirty, before the test uses it
            // This ensures cleanup happens with the test's connection lifecycle, not during test execution
            if (dirtyGraphs.TryGetValue(graphName, out bool isDirty) && isDirty)
            {
                logger.LogDebug("Graph {GraphName} is dirty, cleaning it now before use", graphName);
                await CleanGraphNowAsync(graphName);
                dirtyGraphs[graphName] = false;
            }

            return graphName;
        }
        throw new InvalidOperationException("No available graphs in the pool. This should not have happened because of the semaphore.");
    }

    public async Task ReleaseDatabaseAsync(string graphName, bool shouldCleanOnNextUse = true)
    {
        logger.LogDebug("Releasing graph {GraphName} back to the pool", graphName);
        // Mark the graph as dirty only if it needs cleaning for the next test
        if (shouldCleanOnNextUse)
        {
            dirtyGraphs.TryAdd(graphName, true);
            dirtyGraphs[graphName] = true;
        }
        availableGraphs.Enqueue(graphName);
        graphsAreAvailableSemaphore.Release();
        logger.LogDebug("Graph {GraphName} is now available in the pool", graphName);
    }

    /// <summary>
    /// Cleans a graph immediately by deleting all nodes and relationships.
    /// This is called when a fixture is reusing its own graph between tests.
    /// </summary>
    public async Task CleanDatabaseAsync(string graphName)
    {
        ArgumentNullException.ThrowIfNull(graphName, nameof(graphName));
        logger.LogDebug("Cleaning graph {GraphName} synchronously for reuse", graphName);
        await CleanGraphNowAsync(graphName);
        // Mark as clean after successful cleanup
        dirtyGraphs[graphName] = false;
    }

    /// <summary>
    /// Immediately cleans a graph by deleting all nodes and relationships.
    /// Uses its own connection to allow parallel cleanup across graphs.
    /// </summary>
    private async Task CleanGraphNowAsync(string graphName)
    {
        logger.LogDebug("Cleaning graph {GraphName} with dedicated connection", graphName);

        await using var connection = await dataSource.OpenConnectionAsync();
        await ConfigureConnectionForAgeAsync(connection);

        await using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = $"SELECT * FROM ag_catalog.cypher('{graphName}', $$ MATCH (n) DETACH DELETE n $$) as (a agtype)";
            await cmd.ExecuteNonQueryAsync();
        }

        logger.LogDebug("Graph {GraphName} cleaned successfully", graphName);
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
            logger.LogDebug("Creating graph {GraphName} in the pool", graphName);
            await CreateOrReplaceGraphAsync(graphName);
            availableGraphs.Enqueue(graphName);
            logger.LogDebug("Graph {GraphName} is ready for use", graphName);
            graphsAreAvailableSemaphore.Release(); // Signal that a graph is available
        }
    }

    private async Task CreateOrReplaceGraphAsync(string graphName)
    {
        logger.LogDebug("Creating or replacing graph {GraphName}", graphName);

        await using var connection = await dataSource.OpenConnectionAsync();
        await ConfigureConnectionForAgeAsync(connection);

        // Drop graph if it already exists (ignore if it does not)
        await using (var drop = connection.DropGraphCommand(graphName))
        {
            try
            {
                await using var reader = await drop.ExecuteReaderAsync();
                while (await reader.ReadAsync()) { }
            }
            catch (PostgresException ex) when (ex.SqlState is "42704" or "3F000")
            {
                logger.LogDebug("Graph {GraphName} did not exist prior to reset", graphName);
            }
        }

        // Create the graph
        await using (var create = connection.CreateGraphCommand(graphName))
        {
            await using var reader = await create.ExecuteReaderAsync();
            while (await reader.ReadAsync()) { }
        }

        logger.LogDebug("Graph {GraphName} is now ready", graphName);
    }

    private async Task DropGraphAsync(string graphName)
    {
        logger.LogDebug("Dropping graph {GraphName}", graphName);

        await using var connection = await dataSource.OpenConnectionAsync();
        await ConfigureConnectionForAgeAsync(connection);

        await using (var drop = connection.DropGraphCommand(graphName))
        {
            try
            {
                await using var reader = await drop.ExecuteReaderAsync();
                while (await reader.ReadAsync()) { }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to drop graph {GraphName}. It may not exist.", graphName);
            }
        }

        logger.LogDebug("Graph {GraphName} dropped", graphName);
    }

    private string GenerateGraphName(int i)
    {
        return "graphmodeltests" + i.ToString("D3");
    }

    private async Task ConfigureConnectionForAgeAsync(NpgsqlConnection connection)
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
