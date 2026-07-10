// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.CompatibilityTests.Tests.Fakes;

/// <summary>
/// A <see cref="IGraphProviderTestHarness"/> that appends a label to <see cref="Events"/> at each
/// lifecycle point, so a test can drive it (and a <see cref="CompatibilityTest"/> bound to it)
/// directly and assert the resulting order.
/// </summary>
internal sealed class RecordingFakeHarness : IGraphProviderTestHarness
{
    public List<string> Events { get; } = [];

    public string ProviderName => "Cvoya.Graph.CompatibilityTests.Tests.RecordingFakeProvider";

    public CapabilitySet Capabilities => CapabilitySet.All;

    public ValueTask InitializeAsync()
    {
        Events.Add("harness-init");
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        Events.Add("harness-dispose");
        return ValueTask.CompletedTask;
    }

    public ValueTask<IGraph> GetGraphAsync(StoreIsolation isolation, CancellationToken cancellationToken)
    {
        Events.Add("store-acquire");
        return ValueTask.FromResult<IGraph>(new FakeGraph(() => Events.Add("store-dispose")));
    }
}
