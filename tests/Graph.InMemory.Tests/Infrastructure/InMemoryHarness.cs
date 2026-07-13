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
    private readonly Dictionary<IGraph, InMemoryGraphStore> stores = new(ReferenceEqualityComparer.Instance);

    public string ProviderName => "Cvoya.Graph.InMemory";

    // Declare only the optional capabilities the provider implements. The core LINQ/query
    // surface is not capability-gated; reserved features must not be advertised pre-emptively.
    public CapabilitySet Capabilities => CapabilitySet.Of(
        GraphCapability.Transactions,
        GraphCapability.ComplexPropertyCascade);

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public async ValueTask DisposeAsync()
    {
        foreach (var store in stores.Values)
        {
            await store.DisposeAsync();
        }

        stores.Clear();
    }

    public ValueTask<IGraph> GetGraphAsync(StoreIsolation isolation, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var store = new InMemoryGraphStore();
        stores.Add(store.Graph, store);
        return ValueTask.FromResult(store.Graph);
    }

    public ValueTask<int> CountNodesByPropertyAsync(
        IGraph graph,
        string label,
        string propertyName,
        IReadOnlyCollection<string> values,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!stores.TryGetValue(graph, out var store))
        {
            throw new ArgumentException("The graph was not created by this harness.", nameof(graph));
        }

        return ValueTask.FromResult(store.CountNodesByProperty(label, propertyName, values));
    }

    public bool IsExpectedConcurrentUpdateException(Exception exception) => false;
}
