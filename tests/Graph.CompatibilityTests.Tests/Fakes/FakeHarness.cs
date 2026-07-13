// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.CompatibilityTests.Tests.Fakes;

/// <summary>
/// A minimal <see cref="IGraphProviderTestHarness"/> that declares everything except
/// <see cref="GraphCapability.FullTextSearch"/>, used to exercise the real capability-skip path
/// through <see cref="CompatibilityTest"/>. Must have a public parameterless constructor - xUnit
/// class fixtures require one.
/// </summary>
public sealed class FakeHarness : IGraphProviderTestHarness
{
    public string ProviderName => "Cvoya.Graph.CompatibilityTests.Tests.FakeProvider";

    public CapabilitySet Capabilities { get; } = CapabilitySet.All.Except(GraphCapability.FullTextSearch);

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    public ValueTask<IGraph> GetGraphAsync(StoreIsolation isolation, CancellationToken cancellationToken) =>
        ValueTask.FromResult<IGraph>(new FakeGraph());

    public ValueTask<int> CountNodesByPropertyAsync(
        IGraph graph,
        string label,
        string propertyName,
        IReadOnlyCollection<string> values,
        CancellationToken cancellationToken) => ValueTask.FromResult(0);

    public bool IsExpectedConcurrentUpdateException(Exception exception) => false;
}
