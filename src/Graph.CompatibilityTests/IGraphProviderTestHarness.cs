// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.CompatibilityTests;

/// <summary>
/// The seam a provider implements to run the CVOYA Graph compatibility suite against its own
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
    /// <see cref="StoreIsolation.IndependentStore"/> to obtain a second store instance that must
    /// coexist with the one already returned - which, unlike the other levels, may share the first
    /// store's data.
    /// </summary>
    /// <param name="isolation">
    /// The isolation the returned store must provide - see <see cref="StoreIsolation"/>.
    /// </param>
    /// <param name="cancellationToken">A cancellation token for the acquisition.</param>
    /// <returns>
    /// An <see cref="IGraph"/> satisfying <paramref name="isolation"/>. The store is empty for
    /// <see cref="StoreIsolation.CleanSharedStore"/> and <see cref="StoreIsolation.FreshStore"/>;
    /// an <see cref="StoreIsolation.IndependentStore"/> may see data from a previously returned
    /// graph.
    /// </returns>
    /// <exception cref="GraphProviderUnavailableException">
    /// The backing infrastructure (for example, a Docker-hosted database) could not be started or
    /// reached.
    /// </exception>
    ValueTask<IGraph> GetGraphAsync(StoreIsolation isolation, CancellationToken cancellationToken);

    /// <summary>
    /// Seeds the compatibility suite's external node/relationship fixture through the provider's
    /// native storage API, bypassing CVOYA graph writes and provisioning helpers.
    /// </summary>
    /// <param name="graph">A graph previously returned by this harness.</param>
    /// <param name="marker">The unique marker stored on both nodes and their relationship.</param>
    /// <param name="cancellationToken">A cancellation token for the native seed operation.</param>
    /// <remarks>
    /// The seed contains two <see cref="ContractExternalNode"/> values (roles <c>source</c> and
    /// <c>target</c>) joined by one outgoing <see cref="ContractExternalRelationship"/>. A provider
    /// without external infrastructure, such as the in-memory reference provider, may use its
    /// lowest-level supported write path; database providers must execute native commands without
    /// invoking CVOYA schema or label provisioning.
    /// </remarks>
    ValueTask SeedExternalGraphAsync(
        IGraph graph,
        string marker,
        CancellationToken cancellationToken);

    /// <summary>
    /// Captures a stable, sorted description of backend schema/provisioning artifacts visible to
    /// the provider test store.
    /// </summary>
    /// <param name="graph">A graph previously returned by this harness.</param>
    /// <param name="cancellationToken">A cancellation token for the inspection.</param>
    /// <returns>
    /// Artifact identities sufficient to detect a read creating labels/tables, indexes,
    /// constraints, functions, or equivalent provider infrastructure.
    /// </returns>
    ValueTask<IReadOnlyCollection<string>> GetStoreArtifactsAsync(
        IGraph graph,
        CancellationToken cancellationToken);

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
