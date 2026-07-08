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

namespace Cvoya.Graph.Model.CompatibilityTests;

using System.Reflection;

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
    StoreIsolation isolation = StoreIsolation.CleanSharedStore) : IGraphModelTest, IAsyncLifetime
{
    private static readonly string SuiteVersion = GetSuiteVersion();

    private readonly IGraphProviderTestHarness harness = harness ?? throw new ArgumentNullException(nameof(harness));

    private IGraph? graph;

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
        foreach (var capability in GetRequiredCapabilities())
        {
            if (!harness.Capabilities.Has(capability))
            {
                Assert.Skip(SkipReason(capability, harness.ProviderName));
                return;
            }
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
    }

    /// <summary>
    /// Builds the fixed, parseable skip reason used when a test requires a capability
    /// <paramref name="providerName"/> has not declared.
    /// </summary>
    /// <param name="capability">The missing capability.</param>
    /// <param name="providerName">The provider's display name.</param>
    /// <returns>
    /// <c>Capability '&lt;Name&gt;' not declared by provider '&lt;ProviderName&gt;'
    /// (Cvoya.Graph.Model.CompatibilityTests &lt;version&gt;)</c>, where <c>&lt;Name&gt;</c> is
    /// the <see cref="GraphCapability"/> member name verbatim.
    /// </returns>
    internal static string SkipReason(GraphCapability capability, string providerName) =>
        $"Capability '{capability}' not declared by provider '{providerName}' " +
        $"(Cvoya.Graph.Model.CompatibilityTests {SuiteVersion})";

    private static IEnumerable<GraphCapability> GetRequiredCapabilities()
    {
        var traits = TestContext.Current.Test?.Traits;
        if (traits is null || !traits.TryGetValue("Capability", out var values))
        {
            yield break;
        }

        foreach (var value in values)
        {
            yield return Enum.Parse<GraphCapability>(value);
        }
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
