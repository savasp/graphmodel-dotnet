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

using Microsoft.Extensions.Logging;
using Serilog.Context;

public class Neo4jTest : IAsyncLifetime, IClassFixture<TestInfrastructureFixture>
{
    private readonly TestInfrastructureFixture fixture;
    private IGraph? graph;
    private readonly bool getNewDatabase;
    protected IDisposable? correlationScope;
    private readonly ILogger<Neo4jTest> logger;

    public static class TestContextCorrelation
    {
        public static readonly AsyncLocal<string?> CorrelationId = new();
    }

    public Neo4jTest(TestInfrastructureFixture fixture, bool getNewDatabase = false)
    {
        this.fixture = fixture;
        this.getNewDatabase = getNewDatabase;
        this.logger = fixture.LoggerFactory.CreateLogger<Neo4jTest>();
    }

    public IGraph Graph => graph ?? throw new InvalidOperationException("Graph not initialized");

    public async ValueTask InitializeAsync()
    {
        var testName = TestContext.Current?.Test?.TestDisplayName ?? "UnknownTest";

        logger.LogInformation("Initializing test: {TestName}", testName);

        var testId = TestContext.Current?.Test?.UniqueID ?? Guid.NewGuid().ToString("N");
        TestContextCorrelation.CorrelationId.Value = testId;
        correlationScope = LogContext.PushProperty("CorrelationId", testId);

        graph = await fixture.GetGraph(getNewDatabase);

        logger.LogInformation("Test {TestName} initialized successfully", testName);
    }

    public async ValueTask DisposeAsync()
    {
        if (graph is not null)
        {
            await graph.DisposeAsync();
        }

        correlationScope?.Dispose();
    }
}