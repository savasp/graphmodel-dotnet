// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Neo4j.Tests;

using System.Globalization;
using System.Net.Sockets;
using DotNet.Testcontainers.Builders;
using global::Neo4j.Driver;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Core;
using Serilog.Sinks.OpenTelemetry;

public class TestInfrastructureFixture : IAsyncLifetime
{
    private static readonly ITestInfrastructure testInfrastructure;

    private static readonly IConfigurationRoot configuration = new ConfigurationBuilder()
        .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
        .Build();

    private static readonly Logger serilogger = new LoggerConfiguration()
        .Enrich.FromLogContext()
        .Enrich.WithProcessId()
        .Enrich.WithThreadId()
        .Enrich.With(new WithCustomInfoEnricher())
        .ReadFrom.Configuration(configuration)
        .WriteTo.OpenTelemetry(options =>
        {
            options.Endpoint = "http://localhost:5341/ingest/otlp/v1/logs";
            options.Protocol = OtlpProtocol.HttpProtobuf;
        })
        .WriteTo.Console(formatProvider: CultureInfo.InvariantCulture)
        .CreateLogger();

    private static readonly ILoggerFactory loggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(builder =>
    {
        builder
            .AddConfiguration(configuration.GetSection("Logging"))
            .AddSerilog(serilogger);
    });

    private ILogger<TestInfrastructureFixture> logger = loggerFactory.CreateLogger<TestInfrastructureFixture>();
    private DatabasePoolManager? databasePool;
    private string? cachedDatabaseName;
    private Neo4jGraphStore? cachedStore;

    // Additional stores handed out for StoreIsolation.IndependentStore. They share the leased
    // database but must coexist with the cached store rather than replace it, so they are tracked
    // separately and disposed at fixture disposal.
    private readonly List<Neo4jGraphStore> independentStores = [];

    static TestInfrastructureFixture()
    {
        testInfrastructure = HasConfiguredNeo4j()
            ? new Neo4jTestInfrastructureWithDbInstance()
            : new Neo4jTestInfrastructureWithContainer();
    }

    public static ILoggerFactory LoggerFactory => loggerFactory;

    internal string CurrentDatabaseName => cachedDatabaseName
        ?? throw new InvalidOperationException("A test database has not been acquired.");

    internal static IDriver CreateIndependentDriver()
    {
        return GraphDatabase.Driver(
            testInfrastructure.ConnectionString,
            AuthTokens.Basic(testInfrastructure.Username, testInfrastructure.Password),
            builder => builder
                .WithMaxConnectionPoolSize(10)
                .WithMaxConnectionLifetime(TimeSpan.FromMinutes(2))
                .WithConnectionAcquisitionTimeout(TimeSpan.FromSeconds(30))
                .WithConnectionTimeout(TimeSpan.FromSeconds(30)));
    }

    public async ValueTask InitializeAsync()
    {
        await ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        logger.LogDebugTestInfrastructureFixture65();

        // Independent stores share the leased database, so close their drivers before the lease is
        // released and the database is cleaned for the next test class.
        foreach (var store in independentStores)
        {
            await store.DisposeAsync();
        }

        independentStores.Clear();

        // Release the database back to the pool so other test classes can use it
        if (cachedDatabaseName != null && databasePool != null)
        {
            if (cachedStore != null)
            {
                await cachedStore.DisposeAsync();
                cachedStore = null;
            }

            await databasePool.ReleaseDatabaseAsync(cachedDatabaseName);
            cachedDatabaseName = null;
        }

        GC.SuppressFinalize(this);
    }

    public async Task<IGraph> GetGraph(bool getNewDatabase)
    {
        await EnsureDatabasePoolAsync();

        if (databasePool == null)
        {
            throw new InvalidOperationException("Database pool not initialized");
        }

        if (!getNewDatabase && cachedDatabaseName != null)
        {
            logger.LogDebugTestInfrastructureFixture94(cachedDatabaseName);
            await databasePool.CleanDatabaseAsync(cachedDatabaseName);
        }
        else
        {
            logger.LogDebugTestInfrastructureFixture99();

            if (cachedDatabaseName != null)
            {
                await cachedStore!.DisposeAsync();
                await databasePool.ReleaseDatabaseAsync(cachedDatabaseName);
            }

            cachedDatabaseName = await databasePool.RequestDatabaseAsync();
            cachedStore = new Neo4jGraphStore(
                testInfrastructure!.ConnectionString,
                testInfrastructure.Username,
                testInfrastructure.Password,
                cachedDatabaseName,
                null,
                loggerFactory);
        }

        return cachedStore!.Graph;
    }

    /// <summary>
    /// Acquires an additional graph backed by a separate store instance over the database this
    /// fixture already leased, leaving every previously handed-out graph untouched. Backs
    /// <see cref="CompatibilityTests.StoreIsolation.IndependentStore"/>, which cross-store misuse
    /// tests use to hold two live Neo4j stores at once.
    /// </summary>
    /// <remarks>
    /// Deliberately reuses the leased database instead of renting a second one. Those tests assert
    /// on store identity, and pointing both stores at the same database proves ownership is decided
    /// by instance identity rather than by matching connection settings. It also keeps the test
    /// class's footprint at one pooled database: renting a second one per class and holding it for
    /// the class's lifetime starves the pool once classes run in parallel, which times out database
    /// acquisition suite-wide.
    /// </remarks>
    public async Task<IGraph> GetIndependentGraph()
    {
        if (cachedDatabaseName == null)
        {
            throw new InvalidOperationException(
                "An independent graph requires a database; call GetGraph first.");
        }

        var store = new Neo4jGraphStore(
            testInfrastructure!.ConnectionString,
            testInfrastructure.Username,
            testInfrastructure.Password,
            cachedDatabaseName,
            null,
            loggerFactory);

        independentStores.Add(store);
        return store.Graph;
    }

    private async Task EnsureDatabasePoolAsync()
    {
        if (databasePool is not null)
        {
            return;
        }

        try
        {
            await testInfrastructure.InitializeAsync();

            databasePool = await DatabasePoolManager.GetInstanceAsync(
                testInfrastructure.ConnectionString,
                testInfrastructure.Username,
                testInfrastructure.Password,
                loggerFactory,
                maxPoolSize: 20);
        }
        catch (Exception ex) when (testInfrastructure is Neo4jTestInfrastructureWithContainer && IsContainerRuntimeUnavailable(ex))
        {
            throw new Neo4jTestInfrastructureUnavailableException(
                $"No Docker-compatible container runtime is available for Neo4j Testcontainers: {ex.Message}",
                ex);
        }
    }

    private static bool HasConfiguredNeo4j()
    {
        return !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("NEO4J_URI"))
            || !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("NEO4J_CONNECTION_STRING"))
            || IsDefaultNeo4jReachable();
    }

    private static bool IsDefaultNeo4jReachable()
    {
        try
        {
            using var client = new TcpClient();
            var connectTask = client.ConnectAsync("localhost", 7687);
            return connectTask.Wait(TimeSpan.FromMilliseconds(250)) && client.Connected;
        }
        catch (Exception ex) when (ex is SocketException
                                   or TimeoutException
                                   or ObjectDisposedException
                                   or InvalidOperationException)
        {
            return false;
        }
    }

    private static bool IsContainerRuntimeUnavailable(Exception exception)
    {
        if (exception is DockerUnavailableException)
        {
            return true;
        }

        if (exception.InnerException is not null && IsContainerRuntimeUnavailable(exception.InnerException))
        {
            return true;
        }

        var message = exception.Message;
        return (message.Contains("Docker", StringComparison.OrdinalIgnoreCase)
                || message.Contains("Podman", StringComparison.OrdinalIgnoreCase))
            && (message.Contains("daemon", StringComparison.OrdinalIgnoreCase)
                || message.Contains("socket", StringComparison.OrdinalIgnoreCase)
                || message.Contains("connect", StringComparison.OrdinalIgnoreCase)
                || message.Contains("unavailable", StringComparison.OrdinalIgnoreCase));
    }

    public sealed class Neo4jTestInfrastructureUnavailableException(string message, Exception innerException)
        : Exception(message, innerException);
}
