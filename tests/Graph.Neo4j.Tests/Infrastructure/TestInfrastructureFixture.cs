// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Neo4j.Tests;

using System.Net.Sockets;
using DotNet.Testcontainers.Builders;
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
        .WriteTo.Console()
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

    static TestInfrastructureFixture()
    {
        testInfrastructure = HasConfiguredNeo4j()
            ? new Neo4jTestInfrastructureWithDbInstance()
            : new Neo4jTestInfrastructureWithContainer();
    }

    public ILoggerFactory LoggerFactory => loggerFactory;

    public async ValueTask InitializeAsync()
    {
        await ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        logger.LogDebug("Disposing test infrastructure fixture");

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
            logger.LogDebug("Reusing existing database: {DatabaseName}", cachedDatabaseName);
            await databasePool.CleanDatabaseAsync(cachedDatabaseName);
        }
        else
        {
            logger.LogDebug("Getting new database for test");

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
