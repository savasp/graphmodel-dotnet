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

using Cvoya.Graph.Model.Age.Core;
using Microsoft.Extensions.Logging;
using Xunit;
using Npgsql;
using Npgsql.Age;

/// <summary>
/// Provides lifecycle management for the AGE graph pool used by the shared test suite.
/// Uses a database pool manager to enable parallel test execution with isolated graph instances.
/// </summary>
public sealed class TestInfrastructureFixture : IAsyncLifetime
{
    // Shared data source singleton to limit total connections across ALL test fixtures
    private static NpgsqlDataSource? globalDataSource;
    private static readonly Lock dataSourceLock = new();

    // Limit concurrent test fixtures to match the graph pool size
    private static SemaphoreSlim? globalFixtureSemaphore;
    private const int MaxConcurrentFixtures = 20;

    private readonly string connectionString;
    private readonly ILoggerFactory loggerFactory;
    private readonly SchemaRegistry schemaRegistry = new();

    // Limit concurrent graph acquisitions - this prevents connection pool exhaustion
    // when multiple tests within fixtures try to get graphs simultaneously
    private static SemaphoreSlim? globalGraphAcquisitionSemaphore;

    // Semaphore to limit concurrent fresh graph operations to avoid connection pool exhaustion
    private static readonly SemaphoreSlim freshGraphOperationsSemaphore = new(10, 10); // Max 10 concurrent operations

    private ILogger<TestInfrastructureFixture> logger;
    private DatabasePoolManager? databasePool;
    private bool fixtureSemaphoreAcquired = false;
    private bool graphAcquisitionSemaphoreAcquired = false;

    public TestInfrastructureFixture()
    {
        connectionString = Environment.GetEnvironmentVariable("AGE_CONNECTION_STRING")
            ?? "Host=localhost;Port=5432;Username=postgres;Password=postgres;Database=postgres";

        loggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Debug);
            builder.AddConsole();
            builder.AddXUnit();
        });
        NpgsqlLoggingConfiguration.InitializeLogging(loggerFactory, parameterLoggingEnabled: true);

        logger = loggerFactory.CreateLogger<TestInfrastructureFixture>();

        // Ensure global data source and semaphore are initialized
        if (globalDataSource == null)
        {
            lock (dataSourceLock)
            {
                if (globalDataSource == null)
                {
                    // Initialize semaphores
                    globalFixtureSemaphore = new SemaphoreSlim(MaxConcurrentFixtures, MaxConcurrentFixtures);
                    globalGraphAcquisitionSemaphore = new SemaphoreSlim(MaxConcurrentFixtures, MaxConcurrentFixtures);

                    // PostgreSQL max_connections = 100, but we need headroom for:
                    // - System processes (~10)
                    // - VS Code connections (~5)
                    // - Manual queries (~5)
                    // Safe limit: 80 connections for tests
                    // With 10 concurrent test fixtures × 2-3 connections each = 20-30 active
                    // Plus cleanup connections and transaction overhead = ~40-50 total
                    var builder = new NpgsqlConnectionStringBuilder(connectionString)
                    {
                        ConnectionIdleLifetime = 30,  // Release idle connections faster (was 300)
                        ConnectionPruningInterval = 5,  // Check for idle connections every 5 seconds
                        Timeout = 30  // Increase timeout to wait for connections instead of failing fast
                    };
                    var dataSourceBuilder = new NpgsqlDataSourceBuilder(builder.ConnectionString);
                    dataSourceBuilder.UseAge();  // AGE extension handles connection initialization
                    globalDataSource = dataSourceBuilder.Build();
                }
            }
        }
    }

    public ILoggerFactory LoggerFactory => loggerFactory;
    public async ValueTask InitializeAsync()
    {
        // Wait for a slot to become available - limits concurrent test fixtures
        logger.LogDebug("Waiting for test fixture slot...");
        await globalFixtureSemaphore!.WaitAsync();
        fixtureSemaphoreAcquired = true;
        logger.LogDebug("Test fixture slot acquired");

        // Initialize schema registry
        logger.LogDebug("Starting schema registry initialization...");
        await schemaRegistry.InitializeAsync();
        logger.LogDebug("Schema registry initialized");

        databasePool = await DatabasePoolManager.GetInstanceAsync(
            globalDataSource!,
            loggerFactory,
            maxPoolSize: MaxConcurrentFixtures);
    }

    public async ValueTask DisposeAsync()
    {
        logger.LogDebug("Disposing test infrastructure");

        try
        {
            // Release graph acquisition semaphore when we dispose the graph
            if (graphAcquisitionSemaphoreAcquired)
            {
                logger.LogDebug("Releasing graph acquisition slot (graph disposed)");
                globalGraphAcquisitionSemaphore!.Release();
                graphAcquisitionSemaphoreAcquired = false;
            }
        }
        finally
        {
            // Always release the fixture semaphore slot so other tests can run
            if (fixtureSemaphoreAcquired)
            {
                logger.LogDebug("Releasing test fixture slot");
                globalFixtureSemaphore!.Release();
                fixtureSemaphoreAcquired = false;
            }
        }

        // Don't dispose loggerFactory here - it's shared with the singleton DatabasePoolManager
        // which may still be using it for background operations
    }

    public async Task<AgeGraphStore> GetGraphAsync(bool getNewGraph = false)
    {
        if (getNewGraph)
        {
            logger.LogInformation("Creating completely fresh graph for test isolation");

            // Create a completely new AgeGraphStore instance instead of using the pool
            // This ensures we get a truly clean graph without relying on cleanup mechanisms
            var freshGraphName = "fresh_test_graph_" + Guid.NewGuid().ToString("N")[..8];

            // Create the fresh graph in PostgreSQL first
            await CreateFreshGraphAsync(freshGraphName);

            var freshStore = new AgeGraphStore(globalDataSource!, freshGraphName, loggerFactory, schemaRegistry);

            logger.LogInformation("Successfully created fresh graph {GraphName} bypassing pool", freshGraphName);
            return freshStore;
        }

        // Get a graph name from the pool and create an AgeGraphStore for it
        var graphName = await databasePool!.RequestDatabaseAsync();
        var pooledStore = new AgeGraphStore(globalDataSource!, graphName, loggerFactory, schemaRegistry);

        return pooledStore;
    }

    private async Task CreateFreshGraphAsync(string graphName)
    {
        // Use a semaphore to limit concurrent fresh graph operations and avoid connection pool exhaustion
        await freshGraphOperationsSemaphore.WaitAsync();
        try
        {
            // Create a temporary connection to create the fresh graph
            await using var tempConnection = await globalDataSource!.OpenConnectionAsync();

            // Configure the connection for AGE
            await using (var loadCmd = tempConnection.CreateCommand())
            {
                loadCmd.CommandText = "LOAD 'age'";
                await loadCmd.ExecuteNonQueryAsync().ConfigureAwait(false);
            }

            await using (var searchPathCmd = tempConnection.CreateCommand())
            {
                searchPathCmd.CommandText = "SET search_path = ag_catalog, \"$user\", public";
                await searchPathCmd.ExecuteNonQueryAsync().ConfigureAwait(false);
            }

            // Create the graph
            await using (var create = tempConnection.CreateGraphCommand(graphName))
            {
                // create_graph returns a result set, we need to fully consume it
                await using var reader = await create.ExecuteReaderAsync().ConfigureAwait(false);
                while (await reader.ReadAsync().ConfigureAwait(false))
                {
                    // Just consume the rows, we don't care about the result
                }
            }

            logger.LogDebug("Fresh graph {GraphName} created in PostgreSQL", graphName);
        }
        finally
        {
            freshGraphOperationsSemaphore.Release();
        }
    }

    private async Task DropFreshGraphAsync(string graphName)
    {
        await freshGraphOperationsSemaphore.WaitAsync();
        try
        {
            // Create a temporary connection to drop the fresh graph
            await using var tempConnection = await globalDataSource!.OpenConnectionAsync();

            // Configure the connection for AGE
            await using (var loadCmd = tempConnection.CreateCommand())
            {
                loadCmd.CommandText = "LOAD 'age'";
                await loadCmd.ExecuteNonQueryAsync().ConfigureAwait(false);
            }

            await using (var searchPathCmd = tempConnection.CreateCommand())
            {
                searchPathCmd.CommandText = "SET search_path = ag_catalog, \"$user\", public";
                await searchPathCmd.ExecuteNonQueryAsync().ConfigureAwait(false);
            }

            // Drop the graph
            await using (var drop = tempConnection.DropGraphCommand(graphName))
            {
                // drop_graph returns a result set, we need to fully consume it
                await using var reader = await drop.ExecuteReaderAsync().ConfigureAwait(false);
                while (await reader.ReadAsync().ConfigureAwait(false))
                {
                    // Just consume the rows, we don't care about the result
                }
            }

            logger.LogDebug("Fresh graph {GraphName} dropped from PostgreSQL", graphName);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to drop fresh graph {GraphName}, but continuing", graphName);
        }
        finally
        {
            freshGraphOperationsSemaphore.Release();
        }
    }

    public async Task ReturnGraphAsync(AgeGraphStore graphStore)
    {
        if (databasePool == null)
        {
            throw new InvalidOperationException("Database pool not initialized");
        }

        // Check if this is a fresh graph (has fresh_test_graph_ prefix)
        if (graphStore.GraphName.StartsWith("fresh_test_graph_"))
        {
            logger.LogDebug("Disposing fresh graph {GraphName} without returning to pool", graphStore.GraphName);

            // For fresh graphs, dispose the store and drop the graph from PostgreSQL
            await graphStore.DisposeAsync();
            await DropFreshGraphAsync(graphStore.GraphName);
            return;
        }

        // For pooled graphs, return them to the pool
        logger.LogDebug("Returning pooled graph {GraphName} to pool", graphStore.GraphName);
        // Mark as dirty since the graph needs cleaning before the next test uses it
        await databasePool.ReleaseDatabaseAsync(graphStore.GraphName, shouldCleanOnNextUse: true);
        await graphStore.DisposeAsync();
        // Release graph acquisition semaphore when we return a pooled graph
        if (graphAcquisitionSemaphoreAcquired)
        {
            logger.LogDebug("Releasing graph acquisition slot (pooled graph returned)");
            if (globalGraphAcquisitionSemaphore is not null && globalGraphAcquisitionSemaphore.CurrentCount < MaxConcurrentFixtures)
            {
                globalGraphAcquisitionSemaphore.Release();
            }

            graphAcquisitionSemaphoreAcquired = false;
        }
    }
}
