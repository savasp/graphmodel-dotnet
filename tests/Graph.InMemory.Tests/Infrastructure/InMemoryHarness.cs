// Copyright CVOYA. Licensed under the Apache License, Version 2.0.
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

    // The honest set: the LINQ-to-objects engine executes everything the suite exercises except
    // server-side full-text search, which the provider deliberately does not implement.
    public CapabilitySet Capabilities => CapabilitySet.All.Except(GraphCapability.FullTextSearch);

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    public ValueTask<IGraph> GetGraphAsync(StoreIsolation isolation, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var store = new InMemoryGraphStore();
        return ValueTask.FromResult(store.Graph);
    }
}
