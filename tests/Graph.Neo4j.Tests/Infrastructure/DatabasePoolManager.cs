// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Neo4j.Tests;

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
            instanceTask ??= CreateAsync(connectionString, username, password, loggerFactory, maxPoolSize);
        }

        return instanceTask;
    }

    private static async Task<DatabasePoolManager> CreateAsync(string connectionString, string username, string password, ILoggerFactory loggerFactory, int databaseCount)
    {
        var manager = new DatabasePoolManager(connectionString, username, password, loggerFactory, databaseCount);
        await manager.SetupDatabasesAsync();
        return manager;
    }

    // -- Instance definition --

    private readonly SemaphoreSlim databasesAreAvailableSemaphore;
    private readonly ConcurrentQueue<string> availableDatabases = new();

    private readonly int maxPoolSize;
    private readonly IDriver driver;
    private readonly ILogger<DatabasePoolManager> logger;
    private readonly string defaultDatabaseName;
    private bool usesSharedDefaultDatabase;

    private static readonly TimeSpan RequestTimeout = TimeSpan.FromMinutes(2);
    private const int MaxSetupConcurrency = 4;

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
        this.defaultDatabaseName = Environment.GetEnvironmentVariable("NEO4J_DATABASE") ?? "neo4j";
        this.databasesAreAvailableSemaphore = new SemaphoreSlim(0, maxPoolSize);
    }

    public async ValueTask DisposeAsync()
    {
        logger.LogDebug("Disposing DatabasePool");

        if (usesSharedDefaultDatabase)
        {
            await CleanDatabaseAsync(defaultDatabaseName);
            driver.Dispose();
            return;
        }

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
        if (!await databasesAreAvailableSemaphore.WaitAsync(RequestTimeout))
        {
            throw new TimeoutException(
                $"Timed out after {RequestTimeout} waiting for an available database from the pool. " +
                "This likely means databases are not being released back to the pool, or setup failed.");
        }

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
        await CleanDatabaseAsync(databaseName);
        availableDatabases.Enqueue(databaseName);
        databasesAreAvailableSemaphore.Release();
        logger.LogDebug("Database {DatabaseName} is now available in the pool", databaseName);
    }

    /// <summary>
    /// Assumes that the given database has already been acquired and is considered
    /// to be in use. Cleans the database by deleting all nodes and relationships.
    /// </summary>
    public async Task CleanDatabaseAsync(string databaseName)
    {
        ArgumentNullException.ThrowIfNull(databaseName, nameof(databaseName));
        logger.LogDebug("Cleaning database {DatabaseName}", databaseName);
        await using var session = driver.AsyncSession(builder => builder.WithDatabase(databaseName));
        var result = await session.RunAsync("MATCH (n) DETACH DELETE n");
        await result.ConsumeAsync();
        logger.LogDebug("Database {DatabaseName} cleaned successfully", databaseName);
    }

    private async Task SetupDatabasesAsync()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        logger.LogInformation("Setting up {Count} databases in the pool (max concurrency: {Concurrency})", maxPoolSize, MaxSetupConcurrency);

        // Use bounded parallelism to avoid exhausting the driver connection pool
        using var concurrencyLimiter = new SemaphoreSlim(MaxSetupConcurrency);
        int successCount = 0;
        int failCount = 0;
        var failures = new ConcurrentQueue<Exception>();

        var tasks = Enumerable.Range(0, maxPoolSize).Select(async i =>
        {
            var databaseName = GenerateDatabaseName(i);
            await concurrencyLimiter.WaitAsync();
            try
            {
                logger.LogDebug("Creating database {DatabaseName} in the pool", databaseName);
                await CreateDatabaseIfNotExistsAsync(databaseName);
                await CleanDatabaseAsync(databaseName);
                availableDatabases.Enqueue(databaseName);
                databasesAreAvailableSemaphore.Release();
                var count = Interlocked.Increment(ref successCount);
                logger.LogDebug("Database {DatabaseName} is ready for use ({Count}/{Total}, {Elapsed:F1}s)", databaseName, count, maxPoolSize, sw.Elapsed.TotalSeconds);
            }
            catch (Exception ex)
            {
                failures.Enqueue(ex);
                Interlocked.Increment(ref failCount);
                logger.LogError(ex, "Failed to create database {DatabaseName}, skipping", databaseName);
            }
            finally
            {
                concurrencyLimiter.Release();
            }
        });

        await Task.WhenAll(tasks);

        if (successCount == 0)
        {
            if (failures.Any(IsMultiDatabaseUnsupported))
            {
                await SetupSharedDefaultDatabaseAsync(failCount, sw);
                return;
            }

            throw new InvalidOperationException(
                $"Failed to create any databases in the pool ({failCount} failures). " +
                "Ensure Neo4j is running and accessible.");
        }

        logger.LogInformation("Database pool setup complete: {SuccessCount} ready, {FailCount} failed in {Elapsed:F1}s",
            successCount, failCount, sw.Elapsed.TotalSeconds);
    }

    private async Task CreateDatabaseIfNotExistsAsync(string databaseName)
    {
        logger.LogDebug("Creating database {DatabaseName} if it does not exist", databaseName);
        await using var session = driver.AsyncSession(builder => builder.WithDatabase("system"));
        var result = await session.RunAsync($"CREATE DATABASE {databaseName} IF NOT EXISTS");
        await result.ConsumeAsync();
        await WaitForDatabaseOnlineAsync(databaseName);
        logger.LogDebug("Database {DatabaseName} is now ready", databaseName);
    }

    private async Task SetupSharedDefaultDatabaseAsync(int failCount, System.Diagnostics.Stopwatch sw)
    {
        usesSharedDefaultDatabase = true;
        logger.LogWarning(
            "Neo4j database administration is unavailable; using shared default database {DatabaseName} for tests ({FailCount} database-create failures)",
            defaultDatabaseName,
            failCount);

        await CleanDatabaseAsync(defaultDatabaseName);
        availableDatabases.Enqueue(defaultDatabaseName);
        databasesAreAvailableSemaphore.Release();

        logger.LogInformation("Shared default database {DatabaseName} is ready in {Elapsed:F1}s", defaultDatabaseName, sw.Elapsed.TotalSeconds);
    }

    private async Task DropDatabaseAsync(string databaseName)
    {
        logger.LogDebug("Scheduling drop of database {DatabaseName}", databaseName);
        try
        {
            logger.LogDebug("Dropping database {DatabaseName}", databaseName);
            await using var session = driver.AsyncSession(builder => builder.WithDatabase("system"));
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
                await using var session = driver.AsyncSession(builder => builder.WithDatabase(databaseName));
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
        return "GraphTests" + i.ToString("D3");
    }

    private static bool IsMultiDatabaseUnsupported(Exception exception)
    {
        if (exception.InnerException is not null && IsMultiDatabaseUnsupported(exception.InnerException))
        {
            return true;
        }

        var message = exception.Message;
        return message.Contains("CREATE DATABASE", StringComparison.OrdinalIgnoreCase)
            && (message.Contains("Unsupported", StringComparison.OrdinalIgnoreCase)
                || message.Contains("not supported", StringComparison.OrdinalIgnoreCase)
                || message.Contains("not allowed", StringComparison.OrdinalIgnoreCase)
                || message.Contains("Community", StringComparison.OrdinalIgnoreCase));
    }
}
