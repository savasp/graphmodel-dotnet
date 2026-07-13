// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

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
    private readonly ILogger<Neo4jTest> logger = Neo4jHarness.LoggerFactory.CreateLogger<Neo4jTest>();
    protected IDisposable? CorrelationScope { get; private set; }

    public static class TestContextCorrelation
    {
        public static readonly AsyncLocal<string?> CorrelationId = new();
    }

    public override async ValueTask InitializeAsync()
    {
        var testName = TestContext.Current?.Test?.TestDisplayName ?? "UnknownTest";

        logger.LogInformationNeo4jTest29(testName);

        var testId = TestContext.Current?.Test?.UniqueID ?? Guid.NewGuid().ToString("N");
        TestContextCorrelation.CorrelationId.Value = testId;
        CorrelationScope = LogContext.PushProperty("CorrelationId", testId);

        await base.InitializeAsync();

        logger.LogInformationNeo4jTest37(testName);
    }

    public override async ValueTask DisposeAsync()
    {
        CorrelationScope?.Dispose();
        await base.DisposeAsync();
        GC.SuppressFinalize(this);
    }
}
