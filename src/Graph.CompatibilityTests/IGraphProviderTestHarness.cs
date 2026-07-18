// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.CompatibilityTests;

/// <summary>
/// The seam a provider implements to run the GraphModel compatibility suite against its own
/// backing store.
/// </summary>
/// <remarks>
/// One instance is created per test class (an xUnit class fixture). xUnit parallelizes test
/// classes, so implementations must tolerate concurrent instances and share expensive
/// process-wide state (containers, connection pools) statically, the way the in-tree Neo4j
/// harness does.
/// </remarks>
public interface IGraphProviderTestHarness : IAsyncLifetime
{
    /// <summary>
    /// Gets the provider's display name, used in capability skip reasons and compliance reports.
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// Gets the set of <see cref="GraphCapability"/> values this provider declares support for.
    /// Read once per test, before the store is acquired, to decide whether the test should skip.
    /// </summary>
    CapabilitySet Capabilities { get; }

    /// <summary>
    /// Gets an <see cref="IGraph"/> over an empty store. Called once per test with
    /// <see cref="StoreIsolation.CleanSharedStore"/> or <see cref="StoreIsolation.FreshStore"/>;
    /// cross-store contract tests additionally call it with
    /// <see cref="StoreIsolation.IndependentStore"/> to obtain a second store that must coexist
    /// with the one already returned.
    /// </summary>
    /// <param name="isolation">
    /// The isolation the returned store must provide - see <see cref="StoreIsolation"/>.
    /// </param>
    /// <param name="cancellationToken">A cancellation token for the acquisition.</param>
    /// <returns>An <see cref="IGraph"/> backed by an empty store.</returns>
    /// <exception cref="GraphProviderUnavailableException">
    /// The backing infrastructure (for example, a Docker-hosted database) could not be started or
    /// reached.
    /// </exception>
    ValueTask<IGraph> GetGraphAsync(StoreIsolation isolation, CancellationToken cancellationToken);

    /// <summary>
    /// Counts store nodes carrying <paramref name="label"/> whose <paramref name="propertyName"/>
    /// value is one of <paramref name="values"/>. The compatibility suite uses this narrow probe
    /// only for store-level orphan assertions that cannot be expressed through typed
    /// <see cref="IGraph"/> queries.
    /// </summary>
    /// <param name="graph">A graph previously returned by this harness.</param>
    /// <param name="label">The store node label to match.</param>
    /// <param name="propertyName">The stored property name to inspect.</param>
    /// <param name="values">The property values included in the count.</param>
    /// <param name="cancellationToken">A cancellation token for the probe.</param>
    /// <returns>The number of matching store nodes.</returns>
    ValueTask<int> CountNodesByPropertyAsync(
        IGraph graph,
        string label,
        string propertyName,
        IReadOnlyCollection<string> values,
        CancellationToken cancellationToken);

    /// <summary>
    /// Determines whether <paramref name="exception"/> is an expected provider-specific
    /// concurrency conflict from two transactions updating the same node.
    /// </summary>
    /// <param name="exception">The exception raised by an update or commit.</param>
    /// <returns><see langword="true"/> only for errors the provider may legitimately raise under
    /// write contention; otherwise, <see langword="false"/>.</returns>
    bool IsExpectedConcurrentUpdateException(Exception exception);
}
