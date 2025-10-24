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
    private string? cachedGraphName;
    private AgeGraphStore? cachedStore;
    private bool fixtureSemaphoreAcquired = false;
    private bool graphAcquisitionSemaphoreAcquired = false;

    public TestInfrastructureFixture()
    {
        connectionString = Environment.GetEnvironmentVariable("AGE_CONNECTION_STRING")
            ?? "Host=localhost;Port=5432;Username=postgres;Password=postgres;Database=postgres";

        loggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Information);
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
                    
                    // PostgreSQL default max_connections is usually 100
                    // Semaphores limit concurrency to 10, but each operation may need multiple connections
                    // (transaction + cleanup + background operations). Double the pool for headroom.
                    var builder = new NpgsqlConnectionStringBuilder(connectionString)
                    {
                        MaxPoolSize = 100,  // 10 concurrent graphs × ~6 connections each (headroom for complex tests)
                        MinPoolSize = 5,   // Keep some connections warm
                        ConnectionIdleLifetime = 300,  // Keep connections alive longer to maximize reuse
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
            if (cachedGraphName != null && cachedStore != null)
            {
                await cachedStore.DisposeAsync();
                if (databasePool != null)
                {
                    await databasePool.ReleaseDatabaseAsync(cachedGraphName);
                }
                
                // Release graph acquisition semaphore when we dispose the graph
                if (graphAcquisitionSemaphoreAcquired)
                {
                    logger.LogDebug("Releasing graph acquisition slot (graph disposed)");
                    globalGraphAcquisitionSemaphore!.Release();
                    graphAcquisitionSemaphoreAcquired = false;
                }
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

    public async Task<IGraph> GetGraphAsync(bool getNewGraph)
    {
        if (databasePool == null)
        {
            throw new InvalidOperationException("Database pool not initialized");
        }

        if (!getNewGraph && cachedGraphName != null)
        {
            logger.LogDebug("Reusing existing graph: {GraphName}", cachedGraphName);
            await databasePool.CleanDatabaseAsync(cachedGraphName);
            return cachedStore!.Graph;
        }

        // Release previous graph if we had one
        if (cachedGraphName != null && graphAcquisitionSemaphoreAcquired)
        {
            logger.LogDebug("Releasing previous graph {GraphName}", cachedGraphName);
            await cachedStore!.DisposeAsync();
            await databasePool.ReleaseDatabaseAsync(cachedGraphName);
            
            // Release the old graph's semaphore slot
            logger.LogDebug("Releasing previous graph acquisition slot");
            globalGraphAcquisitionSemaphore!.Release();
            graphAcquisitionSemaphoreAcquired = false;
        }

        // Getting a new graph - acquire and HOLD the semaphore for the graph's lifetime
        logger.LogDebug("Getting new graph for test - waiting for graph acquisition slot...");
        await globalGraphAcquisitionSemaphore!.WaitAsync();
        graphAcquisitionSemaphoreAcquired = true;
        
        logger.LogDebug("Graph acquisition slot acquired");
        cachedGraphName = await databasePool.RequestDatabaseAsync();
        cachedStore = new AgeGraphStore(globalDataSource!, cachedGraphName, schemaRegistry, loggerFactory);
        logger.LogDebug("Graph {GraphName} ready - semaphore will be held until disposal", cachedGraphName);

        return cachedStore!.Graph;
    }
}
