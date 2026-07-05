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
        catch (Exception ex) when (testInfrastructure is Neo4jTestInfrastructureWithContainer && IsDockerUnavailable(ex))
        {
            throw new Neo4jTestInfrastructureUnavailableException(
                $"Docker is not available for Neo4j Testcontainers: {ex.Message}",
                ex);
        }
    }

    private static bool HasConfiguredNeo4j()
    {
        return !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("NEO4J_URI"))
            || !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("NEO4J_CONNECTION_STRING"));
    }

    private static bool IsDockerUnavailable(Exception exception)
    {
        if (exception is DockerUnavailableException)
        {
            return true;
        }

        if (exception.InnerException is not null && IsDockerUnavailable(exception.InnerException))
        {
            return true;
        }

        var message = exception.Message;
        return message.Contains("Docker", StringComparison.OrdinalIgnoreCase)
            && (message.Contains("daemon", StringComparison.OrdinalIgnoreCase)
                || message.Contains("socket", StringComparison.OrdinalIgnoreCase)
                || message.Contains("connect", StringComparison.OrdinalIgnoreCase)
                || message.Contains("unavailable", StringComparison.OrdinalIgnoreCase));
    }

    public sealed class Neo4jTestInfrastructureUnavailableException(string message, Exception innerException)
        : Exception(message, innerException);
}
