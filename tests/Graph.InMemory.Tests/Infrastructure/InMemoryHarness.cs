// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.InMemory.Tests;

using Cvoya.Graph.CompatibilityTests;

/// <summary>
/// The compatibility-suite harness for the in-memory provider. Store creation is in-process and
/// cheap, so both isolation levels are satisfied with a brand-new empty store; there is no
/// infrastructure that could be unavailable.
/// </summary>
public sealed class InMemoryHarness : IGraphProviderTestHarness
{
    public string ProviderName => "Cvoya.Graph.InMemory";

    // Declare only the optional capabilities the provider implements. The core LINQ/query
    // surface is not capability-gated; reserved features must not be advertised pre-emptively.
    public CapabilitySet Capabilities => CapabilitySet.Of(
        GraphCapability.Transactions,
        GraphCapability.ComplexPropertyCascade);

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    public ValueTask<IGraph> GetGraphAsync(StoreIsolation isolation, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var store = new InMemoryGraphStore();
        return ValueTask.FromResult(store.Graph);
    }
}
