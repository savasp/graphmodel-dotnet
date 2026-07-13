// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.InMemory;

using Microsoft.Extensions.Logging;

/// <summary>
/// An in-process, in-memory graph store: the reference provider for the CVOYA graph abstraction
/// and a unit-test double for applications built on <see cref="IGraph"/>. Queries execute by
/// interpreting the shared provider-neutral query model with LINQ-to-objects; no query language
/// or external infrastructure is involved.
/// </summary>
/// <remarks>
/// This is a test double and executable specification, not a production database: data lives in
/// process memory, there is no persistence or clustering, queries are unindexed scans, and
/// commits are serialized through a single store-wide lock (transactions buffer their writes and
/// apply them atomically on commit). Full-text search is not supported.
/// </remarks>
public sealed class InMemoryGraphStore : IAsyncDisposable
{
    private readonly InMemoryStore _store = new();

    /// <summary>
    /// Initializes a new, empty in-memory graph store.
    /// </summary>
    /// <param name="schemaRegistry">An optional schema registry to share with other components;
    /// a fresh one is created when omitted.</param>
    /// <param name="loggerFactory">An optional logger factory.</param>
    public InMemoryGraphStore(SchemaRegistry? schemaRegistry = null, ILoggerFactory? loggerFactory = null)
    {
        Graph = new InMemoryGraph(_store, schemaRegistry ?? new SchemaRegistry(), loggerFactory);
    }

    /// <summary>
    /// Gets the <see cref="IGraph"/> over this store.
    /// </summary>
    public IGraph Graph { get; }

    /// <summary>
    /// Removes every node and relationship from the store. Useful for resetting a shared test
    /// double between tests.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    public Task ClearAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _store.Clear();
        return Task.CompletedTask;
    }

    internal int CountNodesByProperty(
        string label,
        string propertyName,
        IReadOnlyCollection<string> values)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);
        ArgumentNullException.ThrowIfNull(values);

        var expectedValues = values.ToHashSet(StringComparer.Ordinal);
        return _store.CurrentState.Nodes.Values.Count(node =>
            node.Labels.Contains(label, StringComparer.Ordinal) &&
            node.Properties.TryGetValue(propertyName, out var property) &&
            property.Value is string value &&
            expectedValues.Contains(value));
    }

    /// <summary>
    /// Disposes the store. The in-memory store owns no external resources; this exists for
    /// symmetry with provider stores that do.
    /// </summary>
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
