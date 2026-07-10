// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.CompatibilityTests.Tests;

using Cvoya.Graph.CompatibilityTests.Tests.Fakes;

/// <summary>
/// Pins <see cref="CompatibilityTest.InitializeAsync"/>'s capability-skip behavior end to end,
/// through the real xUnit trait-discovery path: <see cref="FakeHarness"/> declares everything
/// except <see cref="GraphCapability.FullTextSearch"/>, and this class carries one method that
/// requires it (which must be reported skipped, never executed) alongside one plain method (which
/// must execute and successfully acquire the fake store).
/// </summary>
/// <remarks>
/// In the shared <see cref="ComplianceLedgerCollection"/> collection because
/// <see cref="ExecutesAndAcquiresGraph"/> increments <see cref="ComplianceGuard"/>'s static
/// ledger; see that collection's definition for why.
/// </remarks>
[Collection(ComplianceLedgerCollection.Name)]
public sealed class CapabilitySkipTests(FakeHarness harness) : FakeProviderTest(harness)
{
    [Fact]
    [RequiresCapability(GraphCapability.FullTextSearch)]
    public void RequiresUndeclaredCapability_IsSkippedNotExecuted()
    {
        // FakeHarness does not declare FullTextSearch, so CompatibilityTest.InitializeAsync must
        // have called Assert.Skip and returned before this body ever ran. If this executes, the
        // skip mechanism is broken.
        Assert.Fail(
            "This test requires GraphCapability.FullTextSearch, which FakeHarness does not " +
            "declare. It should have been skipped by CompatibilityTest.InitializeAsync, not executed.");
    }

    [Fact]
    public void ExecutesAndAcquiresGraph()
    {
        // No capability requirement: this must run normally and have a usable Graph.
        Assert.NotNull(Graph);
        Assert.NotNull(Graph.SchemaRegistry);
    }
}

/// <summary>
/// A fake optional-feature area gated at the <b>interface</b> level, mirroring the exact shape the
/// shipped suite uses (e.g. <see cref="IFullTextSearchTests"/>). This is the shape xunit.v3 does
/// <b>not</b> surface as a runtime <c>Capability</c> trait on a default-interface-method test, so
/// the capability-skip path must resolve it by reflecting the running method's declaring interface,
/// not by reading traits.
/// </summary>
[RequiresCapability(GraphCapability.FullTextSearch)]
public interface IFakeFullTextArea : IGraphTest
{
    [Fact]
    void InterfaceGatedTest_MustSkip() =>
        Assert.Fail(
            "This test's declaring interface requires GraphCapability.FullTextSearch, which " +
            "FakeHarness does not declare. CompatibilityTest.InitializeAsync must skip it by " +
            "reflecting the interface-level requirement, not executed.");
}

/// <summary>
/// Regression cover for the xunit.v3 interface-trait gap: binds the interface-level gate
/// <see cref="IFakeFullTextArea"/> to a harness that does not declare the capability and proves the
/// skip fires for the shape the shipped suite actually uses (the method-level shape in
/// <see cref="CapabilitySkipTests"/> works via traits; this one only works via method reflection).
/// </summary>
[Collection(ComplianceLedgerCollection.Name)]
public sealed class InterfaceLevelCapabilitySkipTests(FakeHarness harness)
    : FakeProviderTest(harness), IFakeFullTextArea;
