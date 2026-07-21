// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.InMemory.Tests;

using Cvoya.Graph.CompatibilityTests;

/// <summary>Verifies the provider's explicit no-managed-index contract.</summary>
public sealed class ManagedIndexMaintenanceTests(InMemoryHarness harness)
    : InMemoryTest(harness, StoreIsolation.FreshStore)
{
    [Fact]
    public async Task RecreateManagedIndexesAsync_IsANoOp()
    {
        Assert.False(Graph.SchemaRegistry.IsInitialized);

        await Graph.RecreateManagedIndexesAsync(TestContext.Current.CancellationToken);

        Assert.False(Graph.SchemaRegistry.IsInitialized);
    }
}
