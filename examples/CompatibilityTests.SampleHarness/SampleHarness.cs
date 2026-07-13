// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.CompatibilityTests.SampleHarness;

using Cvoya.Graph.CompatibilityTests;

/// <summary>
/// Doc-shaped skeleton showing what a provider implements to certify against the GraphModel
/// compatibility suite. This harness has no real backing store - <see cref="GetGraphAsync"/>
/// always throws <see cref="GraphProviderUnavailableException"/> - so binding classes here compile
/// against the suite but every test they inherit skips or fails when actually run. See
/// <c>docs/provider-implementers-guide.md</c> ("Certifying a provider") for the real workflow.
/// </summary>
public sealed class SampleHarness : IGraphProviderTestHarness
{
    /// <inheritdoc/>
    public string ProviderName => "Cvoya.Graph.SampleProvider";

    /// <summary>
    /// A real provider declares exactly the capabilities its backing store supports. This sample
    /// lists two common capabilities purely as an illustration.
    /// </summary>
    public CapabilitySet Capabilities { get; } = CapabilitySet.Of(
        GraphCapability.Transactions,
        GraphCapability.ComplexPropertyCascade);

    /// <inheritdoc/>
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    /// <inheritdoc/>
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    /// <summary>
    /// A real provider would start/connect to its backing store here and return an
    /// <see cref="IGraph"/> over an empty instance of it. This sample has none, so it always
    /// throws - the same way a real harness would if its infrastructure (e.g. Docker) were
    /// unavailable.
    /// </summary>
    public ValueTask<IGraph> GetGraphAsync(StoreIsolation isolation, CancellationToken cancellationToken) =>
        throw new GraphProviderUnavailableException("The sample harness has no backing store.");

    /// <inheritdoc/>
    public ValueTask<int> CountNodesByPropertyAsync(
        IGraph graph,
        string label,
        string propertyName,
        IReadOnlyCollection<string> values,
        CancellationToken cancellationToken) =>
        throw new GraphProviderUnavailableException("The sample harness has no backing store.");

    /// <inheritdoc/>
    public bool IsExpectedConcurrentUpdateException(Exception exception) => false;
}
