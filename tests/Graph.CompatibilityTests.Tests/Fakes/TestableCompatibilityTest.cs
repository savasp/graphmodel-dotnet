// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.CompatibilityTests.Tests.Fakes;

/// <summary>
/// The minimal concrete <see cref="CompatibilityTest"/> needed to drive the base class's
/// <see cref="CompatibilityTest.InitializeAsync"/>/<see cref="CompatibilityTest.DisposeAsync"/>
/// choreography directly from a test body (rather than relying on xUnit's own dispatch), so
/// lifecycle ordering can be asserted deterministically in one place.
/// </summary>
internal sealed class TestableCompatibilityTest(IGraphProviderTestHarness harness) : CompatibilityTest(harness)
{
    public IGraph GraphForAssertions => Graph;
}
