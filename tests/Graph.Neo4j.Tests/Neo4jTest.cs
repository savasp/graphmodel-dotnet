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

namespace Cvoya.Graph.Neo4j.Tests;

using Cvoya.Graph.CompatibilityTests;
using Microsoft.Extensions.Logging;
using Serilog.Context;

/// <summary>
/// The Neo4j binding of <see cref="CompatibilityTest"/>: adds correlation-scoped logging around
/// the base class's capability-skip/store-acquisition choreography.
/// </summary>
public abstract class Neo4jTest(Neo4jHarness harness, StoreIsolation isolation = StoreIsolation.CleanSharedStore)
    : CompatibilityTest(harness, isolation), IClassFixture<Neo4jHarness>
{
    private readonly ILogger<Neo4jTest> logger = harness.LoggerFactory.CreateLogger<Neo4jTest>();
    protected IDisposable? correlationScope;

    public static class TestContextCorrelation
    {
        public static readonly AsyncLocal<string?> CorrelationId = new();
    }

    public override async ValueTask InitializeAsync()
    {
        var testName = TestContext.Current?.Test?.TestDisplayName ?? "UnknownTest";

        logger.LogInformation("Initializing test: {TestName}", testName);

        var testId = TestContext.Current?.Test?.UniqueID ?? Guid.NewGuid().ToString("N");
        TestContextCorrelation.CorrelationId.Value = testId;
        correlationScope = LogContext.PushProperty("CorrelationId", testId);

        await base.InitializeAsync();

        logger.LogInformation("Test {TestName} initialized successfully", testName);
    }

    public override async ValueTask DisposeAsync()
    {
        correlationScope?.Dispose();
        await base.DisposeAsync();
    }
}
