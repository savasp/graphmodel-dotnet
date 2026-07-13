// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.CompatibilityTests;

using System.Reflection;
using Xunit.v3;

/// <summary>
/// The base class every compatibility test interface binding derives from (via a provider's own
/// intermediate base class, e.g. <c>Neo4jTest</c>). Runs the per-test choreography every provider
/// needs exactly once: skip on a missing declared capability, acquire the store, record the
/// execution for <see cref="ComplianceGuard"/>, then dispose the store.
/// </summary>
/// <param name="harness">The provider's test harness.</param>
/// <param name="isolation">The store isolation this test requires. Defaults to
/// <see cref="StoreIsolation.CleanSharedStore"/>.</param>
public abstract class CompatibilityTest(
    IGraphProviderTestHarness harness,
    StoreIsolation isolation = StoreIsolation.CleanSharedStore) : IGraphTest, IAsyncLifetime
{
    private static readonly string SuiteVersion = GetSuiteVersion();

    private readonly IGraphProviderTestHarness harness = harness ?? throw new ArgumentNullException(nameof(harness));

    private IGraph? graph;

    /// <inheritdoc/>
    public IGraphProviderTestHarness Harness => harness;

    /// <inheritdoc/>
    public IGraph Graph => graph
        ?? throw new InvalidOperationException(
            "Graph is not available: InitializeAsync has not completed, or the test was skipped.");

    /// <summary>
    /// Skips the test if it requires a <see cref="GraphCapability"/> the harness does not
    /// declare, then acquires the store from the harness and records the execution with
    /// <see cref="ComplianceGuard"/>.
    /// </summary>
    public virtual async ValueTask InitializeAsync()
    {
        foreach (var capability in GetRequiredCapabilities().Where(c => !harness.Capabilities.Has(c)))
        {
            Assert.Skip(SkipReason(capability, harness.ProviderName));
            return;
        }

        try
        {
            graph = await harness.GetGraphAsync(isolation, TestContext.Current.CancellationToken);
        }
        catch (GraphProviderUnavailableException ex)
        {
            if (ComplianceGuard.IsStrict)
            {
                throw;
            }

            Assert.Skip(ex.Message);
            return;
        }

        ComplianceGuard.RecordExecution(harness.Capabilities);
    }

    /// <summary>
    /// Disposes the acquired store, if it implements <see cref="IAsyncDisposable"/> or
    /// <see cref="IDisposable"/>.
    /// </summary>
    public virtual async ValueTask DisposeAsync()
    {
        switch (graph)
        {
            case IAsyncDisposable asyncDisposable:
                await asyncDisposable.DisposeAsync();
                break;
            case IDisposable disposable:
                disposable.Dispose();
                break;
        }

        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Builds the fixed, parseable skip reason used when a test requires a capability
    /// <paramref name="providerName"/> has not declared.
    /// </summary>
    /// <param name="capability">The missing capability.</param>
    /// <param name="providerName">The provider's display name.</param>
    /// <returns>
    /// <c>Capability '&lt;Name&gt;' not declared by provider '&lt;ProviderName&gt;'
    /// (Cvoya.Graph.CompatibilityTests &lt;version&gt;)</c>, where <c>&lt;Name&gt;</c> is
    /// the <see cref="GraphCapability"/> member name verbatim.
    /// </returns>
    internal static string SkipReason(GraphCapability capability, string providerName) =>
        $"Capability '{capability}' not declared by provider '{providerName}' " +
        $"(Cvoya.Graph.CompatibilityTests {SuiteVersion})";

    private static IReadOnlyCollection<GraphCapability> GetRequiredCapabilities()
    {
        // Resolve the running test's method and read RequiresCapability at BOTH the method and its
        // declaring-interface level, reusing the same reflection ComplianceInventory uses to size
        // MinimumExecuted - so the runtime skip decision and the expected-count never disagree.
        //
        // We deliberately do NOT read xUnit's aggregated trait collection: in xunit.v3 (3.2.2) an
        // interface-TYPE-level ITraitAttribute on a default-interface-method test is not surfaced
        // onto the running test's traits (only method-level ones are), and the suite gates whole
        // optional areas at the interface level (e.g. IFullTextSearchTests). Reflecting the method
        // covers both shapes; trusting traits silently ran interface-gated tests unskipped.
        if (TestContext.Current.TestMethod is IXunitTestMethod { Method: { } method })
        {
            return ComplianceInventory.RequiredCapabilities(method);
        }

        // Fallback for a host that cannot surface the MethodInfo: method-level trait aggregation
        // still works, so honor those rather than silently running a gated test.
        return GetRequiredCapabilitiesFromTraits();
    }

    private static IReadOnlyCollection<GraphCapability> GetRequiredCapabilitiesFromTraits()
    {
        var traits = TestContext.Current.Test?.Traits;
        if (traits is null || !traits.TryGetValue("Capability", out var values))
        {
            return [];
        }

        return [.. values.Select(Enum.Parse<GraphCapability>)];
    }

    private static string GetSuiteVersion()
    {
        var informational = typeof(CompatibilityTest).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

        if (string.IsNullOrEmpty(informational))
        {
            return typeof(CompatibilityTest).Assembly.GetName().Version?.ToString() ?? "unknown";
        }

        // Strip the SourceLink commit-hash suffix ("+<sha>"), if any, so the reported version
        // matches the repo's VERSION file exactly (the suite locks its version to it).
        var plusIndex = informational.IndexOf('+', StringComparison.Ordinal);
        return plusIndex >= 0 ? informational[..plusIndex] : informational;
    }
}
