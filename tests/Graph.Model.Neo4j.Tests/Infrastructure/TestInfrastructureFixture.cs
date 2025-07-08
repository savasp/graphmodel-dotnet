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
        var useContainers = Environment.GetEnvironmentVariable("USE_NEO4J_CONTAINERS");
        if (string.IsNullOrEmpty(useContainers))
        {
            useContainers = Environment.GetEnvironmentVariable("CI") ?? "false";
        }

        if (bool.Parse(useContainers))
        {
            testInfrastructure = new Neo4jTestInfrastructureWithContainer();
        }
        else
        {
            testInfrastructure = new Neo4jTestInfrastructureWithDbInstance();
        }
    }

    public ILoggerFactory LoggerFactory => loggerFactory;

    public async ValueTask InitializeAsync()
    {
        databasePool = await DatabasePoolManager.GetInstanceAsync(
            testInfrastructure.ConnectionString,
            testInfrastructure.Username,
            testInfrastructure.Password,
            loggerFactory,
            maxPoolSize: 20);
    }

    public async ValueTask DisposeAsync()
    {
        logger.LogDebug("Disposing database pool and test infrastructure");
        await testInfrastructure.DisposeAsync();
    }

    public async Task<IGraph> GetGraph(bool getNewDatabase)
    {
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
}