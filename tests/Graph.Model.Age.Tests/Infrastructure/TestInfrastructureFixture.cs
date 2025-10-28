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
    private const int MaxConcurrentFixtures = 10;

    private readonly string connectionString;
    private readonly ILoggerFactory loggerFactory;
    private readonly SchemaRegistry schemaRegistry = new();

    // Limit concurrent graph acquisitions - this prevents connection pool exhaustion
    // when multiple tests within fixtures try to get graphs simultaneously
    private static SemaphoreSlim? globalGraphAcquisitionSemaphore;

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

    public async Task<AgeGraphStore> GetGraphAsync(bool getNewGraph)
    {
        if (databasePool == null)
        {
            throw new InvalidOperationException("Database pool not initialized");
        }

        // Acquire semaphore FIRST, regardless of which path we take
        // This ensures we never have more than MaxConcurrentFixtures graphs doing cleanup/operations
        if (!graphAcquisitionSemaphoreAcquired)
        {
            logger.LogDebug("Waiting for graph acquisition slot...");
            await globalGraphAcquisitionSemaphore!.WaitAsync();
            graphAcquisitionSemaphoreAcquired = true;
            logger.LogDebug("Graph acquisition slot acquired");
        }

        // Getting a new graph (semaphore already acquired above)
        logger.LogDebug("Requesting new graph from pool");
        var graphName = await databasePool.RequestDatabaseAsync();
        logger.LogDebug("Graph {GraphName} ready - semaphore will be held until disposal", graphName);
        var store = new AgeGraphStore(globalDataSource!, graphName, schemaRegistry, loggerFactory);
        return store;
    }

    public async Task ReturnGraphAsync(AgeGraphStore graphStore)
    {
        if (databasePool == null)
        {
            throw new InvalidOperationException("Database pool not initialized");
        }
        // For new graphs that won't be reused by this fixture, release them back to the pool
        logger.LogDebug("Returning new graph {GraphName} to pool", graphStore.GraphName);
        await databasePool.ReleaseDatabaseAsync(graphStore.GraphName);
        await graphStore.DisposeAsync();
        // Release graph acquisition semaphore when we return a new graph
        if (graphAcquisitionSemaphoreAcquired)
        {
            logger.LogDebug("Releasing graph acquisition slot (new graph returned)");
            if (globalGraphAcquisitionSemaphore is not null && globalGraphAcquisitionSemaphore.CurrentCount < MaxConcurrentFixtures)
            {
                globalGraphAcquisitionSemaphore.Release();
            }

            graphAcquisitionSemaphoreAcquired = false;
        }
    }
}