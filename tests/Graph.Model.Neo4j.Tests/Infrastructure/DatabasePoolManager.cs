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

using System.Collections.Concurrent;
using System.Threading;
using global::Neo4j.Driver;
using Microsoft.Extensions.Logging;

public sealed class DatabasePoolManager : IAsyncDisposable
{
    // -- Singleton instance of the DatabasePoolManager --
    private static Task<DatabasePoolManager>? instanceTask;
    private static readonly Lock initLock = new();

    public static Task<DatabasePoolManager> GetInstanceAsync(string connectionString, string username, string password, ILoggerFactory loggerFactory, int maxPoolSize = 10)
    {
        if (instanceTask != null) return instanceTask;

        lock (initLock)
        {
            if (instanceTask == null)
            {
                instanceTask = CreateAsync(connectionString, username, password, loggerFactory, maxPoolSize);
            }
        }

        return instanceTask;
    }

    private static Task<DatabasePoolManager> CreateAsync(string connectionString, string username, string password, ILoggerFactory loggerFactory, int databaseCount)
    {
        var manager = new DatabasePoolManager(connectionString, username, password, loggerFactory, databaseCount);
        _ = manager.SetupDatabasesAsync(); // don't wait for setup to complete
        return Task.FromResult(manager);
    }

    // -- Instance definition --

    private readonly SemaphoreSlim databasesAreAvailableSemaphore;
    private readonly ConcurrentQueue<string> availableDatabases = new();

    private readonly int maxPoolSize;
    private readonly IDriver driver;
    private readonly ILogger<DatabasePoolManager> logger;

    private BackgroundWorker worker;

    private DatabasePoolManager(string connectionString, string username, string password, ILoggerFactory loggerFactory, int maxPoolSize)
    {
        logger = loggerFactory.CreateLogger<DatabasePoolManager>();

        // Configure Neo4j driver with appropriate settings for test scenarios
        static void configBuilder(ConfigBuilder cb) => cb
            .WithMaxConnectionPoolSize(10)
            .WithMaxConnectionLifetime(TimeSpan.FromMinutes(2))
            .WithConnectionAcquisitionTimeout(TimeSpan.FromSeconds(30))
            .WithConnectionTimeout(TimeSpan.FromSeconds(30));

        this.driver = GraphDatabase.Driver(
            connectionString ?? throw new ArgumentNullException(nameof(connectionString)),
            AuthTokens.Basic(
                username ?? throw new ArgumentNullException(nameof(username)),
                password ?? throw new ArgumentNullException(nameof(password))),
                configBuilder);

        this.maxPoolSize = maxPoolSize;
        this.databasesAreAvailableSemaphore = new SemaphoreSlim(0, maxPoolSize);
        this.worker = new BackgroundWorker(loggerFactory);
    }

    public async ValueTask DisposeAsync()
    {
        logger.LogDebug("Disposing DatabasePool");

        await worker.DisposeAsync();

        // Drop all databases
        for (int i = 0; i < maxPoolSize; i++)
        {
            var databaseName = GenerateDatabaseName(i);
            await DropDatabaseAsync(databaseName);
        }

        driver.Dispose();
    }

    public async Task<string> RequestDatabaseAsync()
    {
        logger.LogDebug("Requesting an available database from the pool");
        await databasesAreAvailableSemaphore.WaitAsync();
        logger.LogDebug("A database is available, acquiring it");
        availableDatabases.TryDequeue(out var databaseName);
        if (databaseName != null)
        {
            logger.LogDebug("Acquired database {DatabaseName} from the pool", databaseName);
            return databaseName;
        }
        throw new InvalidOperationException("No available databases in the pool. This should not have happened because of the semaphore.");
    }

    public async Task ReleaseDatabaseAsync(string databaseName)
    {
        logger.LogDebug("Releasing database {DatabaseName} back to the pool", databaseName);
        await worker.Schedule(async () =>
        {
            await CreateOrReplaceDatabaseAsync(databaseName);
            availableDatabases.Enqueue(databaseName);
            // Let the semaphore know that a database is available
            databasesAreAvailableSemaphore.Release();
        });

        logger.LogDebug("Database {DatabaseName} is now available in the pool", databaseName);
    }

    /// <summary>
    /// Assumes that the given database has already been acquired and is considered
    /// to be in use. Cleans the database by deleting all nodes and relationships.
    /// </summary>
    public async Task CleanDatabaseAsync(string databaseName)
    {
        ArgumentNullException.ThrowIfNull(databaseName, nameof(databaseName));
        logger.LogDebug("Scheduling cleaning of database {DatabaseName}", databaseName);
        await worker.Schedule(async () =>
        {
            logger.LogDebug("Cleaning database {DatabaseName}", databaseName);
            await using var session = driver.AsyncSession(builder => builder.WithDatabase(databaseName));
            var result = await session.RunAsync("MATCH (n) DETACH DELETE n");
            await result.ConsumeAsync();
            logger.LogDebug("Database {DatabaseName} cleaned successfully", databaseName);
        });
    }

    private async Task SetupDatabasesAsync()
    {
        logger.LogDebug("Setting up databases in the pool");
        for (var i = 0; i < maxPoolSize; i++)
        {
            var databaseName = GenerateDatabaseName(i);

            // Schedule the creation of each database
            // We don't schedule the creation of all the databases at once
            // so that tests don't have to wait until all databases are created
            // before they can start running. This is because
            // the scheduler queues all workloads.
            await worker.Schedule(async () =>
            {
                logger.LogDebug("Creating database {DatabaseName} in the pool", databaseName);
                await CreateOrReplaceDatabaseAsync(databaseName);
                availableDatabases.Enqueue(databaseName);
                logger.LogDebug("Database {DatabaseName} is ready for use", databaseName);
                databasesAreAvailableSemaphore.Release(); // Signal that a database is available
            });
        }
    }

    private async Task CreateOrReplaceDatabaseAsync(string databaseName)
    {
        logger.LogDebug("Creating or replacing database {DatabaseName}", databaseName);
        using var session = driver.AsyncSession(builder => builder.WithDatabase("system"));
        var result = await session.RunAsync($"CREATE OR REPLACE DATABASE {databaseName}");
        await result.ConsumeAsync();
        await WaitForDatabaseOnlineAsync(databaseName);
        logger.LogDebug("Database {DatabaseName} is now ready", databaseName);
    }

    private async Task DropDatabaseAsync(string databaseName)
    {
        logger.LogDebug("Scheduling drop of database {DatabaseName}", databaseName);
        try
        {
            logger.LogDebug("Dropping database {DatabaseName}", databaseName);
            using var session = driver.AsyncSession(builder => builder.WithDatabase("system"));
            var result = await session.RunAsync($"DROP DATABASE {databaseName}");
            await result.ConsumeAsync();
            logger.LogDebug("Database {DatabaseName} dropped successfully", databaseName);
        }
        catch (Exception)
        {
            logger.LogWarning("Failed to drop database {DatabaseName}. It may not exist or is already dropped.", databaseName);
            // Ignore errors during cleanup - the database might not exist
        }
    }

    private async Task WaitForDatabaseOnlineAsync(string databaseName, int maxAttempts = 30, int delayMs = 1000)
    {
        logger.LogDebug("Waiting for database {DatabaseName} to become available for driver connections", databaseName);
        // Wait until the driver can actually connect to the database
        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            try
            {
                using var session = driver.AsyncSession(builder => builder.WithDatabase(databaseName));
                var result = await session.RunAsync("RETURN 1");
                await result.ConsumeAsync();
                logger.LogDebug("Database {DatabaseName} is now online", databaseName);
                return; // Successfully connected to the database
            }
            catch (Neo4jException ex) when (ex.Message.Contains("not found") || ex.Message.Contains("does not exist") || ex.Message.Contains("not available"))
            {
                // Database not yet available for driver connections
                logger.LogDebug("Database {DatabaseName} not yet available, attempt {Attempt}/{MaxAttempts}", databaseName, attempt + 1, maxAttempts);
            }
            catch (Exception ex)
            {
                // Other errors, continue waiting
                logger.LogDebug("Error checking database {DatabaseName}, attempt {Attempt}/{MaxAttempts}: {Error}", databaseName, attempt + 1, maxAttempts, ex.Message);
            }

            await Task.Delay(delayMs);
        }

        throw new Exception($"Database '{databaseName}' did not become available for driver connections in time.");
    }

    private string GenerateDatabaseName(int i)
    {
        return "GraphModelTests" + i.ToString("D3");
    }
}
