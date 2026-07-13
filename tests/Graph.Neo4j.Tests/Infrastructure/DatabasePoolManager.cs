// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Neo4j.Tests;

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using global::Neo4j.Driver;
using Microsoft.Extensions.Logging;

public sealed class DatabasePoolManager : IAsyncDisposable
{
    // -- Singleton instance of the DatabasePoolManager --
    private static Task<DatabasePoolManager>? instanceTask;
    private static readonly Lock initLock = new();
    private static readonly string runId = CreateRunId();

    public static Task<DatabasePoolManager> GetInstanceAsync(string connectionString, string username, string password, ILoggerFactory loggerFactory, int maxPoolSize = 10)
    {
        if (instanceTask != null) return instanceTask;

        lock (initLock)
        {
            instanceTask ??= CreateAsync(connectionString, username, password, loggerFactory, maxPoolSize);
        }

        return instanceTask;
    }

    internal static async ValueTask DisposeInstanceAsync()
    {
        Task<DatabasePoolManager>? currentInstanceTask;
        lock (initLock)
        {
            currentInstanceTask = instanceTask;
            instanceTask = null;
        }

        if (currentInstanceTask is null)
        {
            return;
        }

        // Wait for a still-running setup without rethrowing its failure: a faulted setup
        // already surfaced through the tests and leaves nothing to dispose.
        await Task.WhenAny(currentInstanceTask);
        if (!currentInstanceTask.IsCompletedSuccessfully)
        {
            return;
        }

        await (await currentInstanceTask).DisposeAsync();
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
    private const int MaxDatabaseNameLength = 63;
    private const int MaxDatabaseIndex = 999;
    private const string DatabaseNamePrefix = "graphtests";
    private const string Base36Characters = "0123456789abcdefghijklmnopqrstuvwxyz";

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
        logger.LogDebugDatabasePoolManager106();

        if (usesSharedDefaultDatabase)
        {
            await CleanDatabaseAsync(defaultDatabaseName);
            driver.Dispose();
            return;
        }

        // Drop only databases in this process run's namespace.
        for (int i = 0; i < maxPoolSize; i++)
        {
            var databaseName = GetDatabaseName(i);
            await DropDatabaseAsync(databaseName);
        }

        driver.Dispose();
    }

    public async Task<string> RequestDatabaseAsync()
    {
        logger.LogDebugDatabasePoolManager127();
        if (!await databasesAreAvailableSemaphore.WaitAsync(RequestTimeout))
        {
            throw new TimeoutException(
                $"Timed out after {RequestTimeout} waiting for an available database from the pool. " +
                "This likely means databases are not being released back to the pool, or setup failed.");
        }

        logger.LogDebugDatabasePoolManager135();
        availableDatabases.TryDequeue(out var databaseName);
        if (databaseName != null)
        {
            logger.LogDebugDatabasePoolManager139(databaseName);
            return databaseName;
        }
        throw new InvalidOperationException("No available databases in the pool. This should not have happened because of the semaphore.");
    }

    public async Task ReleaseDatabaseAsync(string databaseName)
    {
        logger.LogDebugDatabasePoolManager147(databaseName);
        await CleanDatabaseAsync(databaseName);
        availableDatabases.Enqueue(databaseName);
        databasesAreAvailableSemaphore.Release();
        logger.LogDebugDatabasePoolManager151(databaseName);
    }

    /// <summary>
    /// Assumes that the given database has already been acquired and is considered
    /// to be in use. Cleans the database by deleting all nodes and relationships.
    /// </summary>
    public async Task CleanDatabaseAsync(string databaseName)
    {
        ArgumentNullException.ThrowIfNull(databaseName, nameof(databaseName));
        if (!IsOwnedDatabase(databaseName))
        {
            throw new InvalidOperationException($"Database '{databaseName}' is not owned by this test process.");
        }

        logger.LogDebugDatabasePoolManager166(databaseName);
        await using var session = driver.AsyncSession(builder => builder.WithDatabase(databaseName));
        var result = await session.RunAsync("MATCH (n) DETACH DELETE n");
        await result.ConsumeAsync();
        logger.LogDebugDatabasePoolManager170(databaseName);
    }

    private async Task SetupDatabasesAsync()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        logger.LogInformationDatabasePoolManager176(maxPoolSize, MaxSetupConcurrency);

        // Use bounded parallelism to avoid exhausting the driver connection pool
        using var concurrencyLimiter = new SemaphoreSlim(MaxSetupConcurrency);
        int successCount = 0;
        int failCount = 0;
        var failures = new ConcurrentQueue<Exception>();

        var tasks = Enumerable.Range(0, maxPoolSize).Select(async i =>
        {
            var databaseName = GetDatabaseName(i);
            await concurrencyLimiter.WaitAsync();
            try
            {
                logger.LogDebugDatabasePoolManager190(databaseName);
                await CreateDatabaseIfNotExistsAsync(databaseName);
                await CleanDatabaseAsync(databaseName);
                availableDatabases.Enqueue(databaseName);
                databasesAreAvailableSemaphore.Release();
                var count = Interlocked.Increment(ref successCount);
                logger.LogDebugDatabasePoolManager196(databaseName, count, maxPoolSize, sw.Elapsed.TotalSeconds);
            }
            catch (Exception ex)
            {
                failures.Enqueue(ex);
                Interlocked.Increment(ref failCount);
                logger.LogErrorDatabasePoolManager202(ex, databaseName);
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

        logger.LogInformationDatabasePoolManager225(successCount, failCount, sw.Elapsed.TotalSeconds);
    }

    private async Task CreateDatabaseIfNotExistsAsync(string databaseName)
    {
        logger.LogDebugDatabasePoolManager231(databaseName);
        await using var session = driver.AsyncSession(builder => builder.WithDatabase("system"));
        var result = await session.RunAsync($"CREATE DATABASE {EscapeDatabaseName(databaseName)} IF NOT EXISTS");
        await result.ConsumeAsync();
        await WaitForDatabaseOnlineAsync(databaseName);
        logger.LogDebugDatabasePoolManager236(databaseName);
    }

    private async Task SetupSharedDefaultDatabaseAsync(int failCount, System.Diagnostics.Stopwatch sw)
    {
        usesSharedDefaultDatabase = true;
        logger.LogWarningDatabasePoolManager242(defaultDatabaseName, failCount);

        await CleanDatabaseAsync(defaultDatabaseName);
        availableDatabases.Enqueue(defaultDatabaseName);
        databasesAreAvailableSemaphore.Release();

        logger.LogInformationDatabasePoolManager251(defaultDatabaseName, sw.Elapsed.TotalSeconds);
    }

    private async Task DropDatabaseAsync(string databaseName)
    {
        logger.LogDebugDatabasePoolManager256(databaseName);
        try
        {
            logger.LogDebugDatabasePoolManager259(databaseName);
            await using var session = driver.AsyncSession(builder => builder.WithDatabase("system"));
            var result = await session.RunAsync($"DROP DATABASE {EscapeDatabaseName(databaseName)}");
            await result.ConsumeAsync();
            logger.LogDebugDatabasePoolManager263(databaseName);
        }
        catch (Exception)
        {
            logger.LogWarningDatabasePoolManager267(databaseName);
            // Ignore errors during cleanup - the database might not exist
        }
    }

    private async Task WaitForDatabaseOnlineAsync(string databaseName, int maxAttempts = 30, int delayMs = 1000)
    {
        logger.LogDebugDatabasePoolManager274(databaseName);
        // Wait until the driver can actually connect to the database
        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            try
            {
                await using var session = driver.AsyncSession(builder => builder.WithDatabase(databaseName));
                var result = await session.RunAsync("RETURN 1");
                await result.ConsumeAsync();
                logger.LogDebugDatabasePoolManager283(databaseName);
                return; // Successfully connected to the database
            }
            catch (Neo4jException ex) when (ex.Message.Contains("not found") || ex.Message.Contains("does not exist") || ex.Message.Contains("not available"))
            {
                // Database not yet available for driver connections
                logger.LogDebugDatabasePoolManager289(databaseName, attempt + 1, maxAttempts);
            }
            catch (Exception ex)
            {
                // Other errors, continue waiting
                logger.LogDebugDatabasePoolManager294(databaseName, attempt + 1, maxAttempts, ex.Message);
            }

            await Task.Delay(delayMs);
        }

        throw new TimeoutException($"Database '{databaseName}' did not become available for driver connections in time.");
    }

    internal static string GetDatabaseName(int index)
    {
        return GetDatabaseName(index, runId);
    }

    internal static string GetDatabaseName(int index, string databaseRunId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databaseRunId);
        if (index is < 0 or > MaxDatabaseIndex)
        {
            throw new ArgumentOutOfRangeException(nameof(index), index, $"Database pool indexes must be between 0 and {MaxDatabaseIndex}.");
        }

        if (databaseRunId.Any(character => !char.IsAsciiLetterLower(character) && !char.IsAsciiDigit(character) && character is not '-'))
        {
            throw new ArgumentException("Database run IDs must contain only lowercase ASCII letters, digits, or dashes.", nameof(databaseRunId));
        }

        var databaseName = $"{DatabaseNamePrefix}-{databaseRunId}-{index:D3}";
        if (databaseName.Length > MaxDatabaseNameLength)
        {
            throw new ArgumentException($"The resulting Neo4j database name must not exceed {MaxDatabaseNameLength} characters.", nameof(databaseRunId));
        }

        return databaseName;
    }

    private bool IsOwnedDatabase(string databaseName)
    {
        if (usesSharedDefaultDatabase)
        {
            // Community Neo4j and CI keep the existing single-database fallback.
            return string.Equals(databaseName, defaultDatabaseName, StringComparison.Ordinal);
        }

        return databaseName.StartsWith($"{DatabaseNamePrefix}-{runId}-", StringComparison.Ordinal);
    }

    private static string CreateRunId()
    {
        ulong startMarker;
        try
        {
            using var process = Process.GetCurrentProcess();
            startMarker = (ulong)process.StartTime.ToUniversalTime().Ticks;
        }
        catch (Exception exception) when (exception
            is InvalidOperationException
            or NotSupportedException
            or System.ComponentModel.Win32Exception)
        {
            // Process start-time introspection can be restricted on some hosts; a random
            // marker still disambiguates PID reuse, which is all the start time is for.
            startMarker = BitConverter.ToUInt64(Guid.NewGuid().ToByteArray(), 0);
        }

        var processId = ConvertToBase36((ulong)Environment.ProcessId);
        return $"p{processId}t{ConvertToBase36(startMarker)}";
    }

    private static string EscapeDatabaseName(string databaseName)
    {
        Debug.Assert(!databaseName.Contains('`'), "Generated database names must not contain backticks.");
        return $"`{databaseName}`";
    }

    private static string ConvertToBase36(ulong value)
    {
        Span<char> characters = stackalloc char[13];
        var position = characters.Length;

        do
        {
            characters[--position] = Base36Characters[(int)(value % 36)];
            value /= 36;
        }
        while (value > 0);

        return new string(characters[position..]);
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
