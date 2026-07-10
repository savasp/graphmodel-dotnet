// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.CompatibilityTests.Tests;

/// <summary>
/// Pins <see cref="ComplianceGuard"/>'s enforcement: unarmed (non-strict) never throws regardless
/// of the ledger, and armed (strict) throws exactly when the ledger falls short of
/// <see cref="ComplianceInventory.MinimumExecuted(CapabilitySet)"/> - including the #67 §3 failure
/// mode of a mis-wired project that executes zero tests.
/// </summary>
/// <remarks>
/// Drives <see cref="ComplianceGuard.DisposeAsyncCore(bool)"/> directly with an explicit
/// <c>strict</c> flag rather than mutating <c>GRAPHMODEL_COMPLIANCE_STRICT</c>, so these
/// assertions never race a real environment variable another concurrently-running test might also
/// read. In the shared <see cref="ComplianceLedgerCollection"/> collection because every test here
/// resets and inspects the same static ledger.
/// </remarks>
[Collection(ComplianceLedgerCollection.Name)]
public sealed class ComplianceGuardTests
{
    [Fact]
    public async Task DisposeAsyncCore_NotStrict_ZeroExecuted_DoesNotThrow()
    {
        ComplianceGuard.ResetForTesting();

        var exception = await Record.ExceptionAsync(() => ComplianceGuard.DisposeAsyncCore(strict: false).AsTask());

        Assert.Null(exception);
    }

    [Fact]
    public async Task DisposeAsyncCore_Strict_ZeroExecutedAndNoRecordedCapabilities_ThrowsLoudly()
    {
        // The #67 §3 failure mode this guard exists to prevent: a mis-wired provider project that
        // discovers and runs zero tests must fail loudly under strict mode, not silently pass
        // because "0 >= 0".
        ComplianceGuard.ResetForTesting();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => ComplianceGuard.DisposeAsyncCore(strict: true).AsTask());

        Assert.Contains("0 compatibility test(s) executed", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DisposeAsyncCore_Strict_ExecutedMeetsMinimum_DoesNotThrow()
    {
        ComplianceGuard.ResetForTesting();

        var declared = CapabilitySet.All.Except(GraphCapability.FullTextSearch);
        var minimum = ComplianceInventory.MinimumExecuted(declared);
        for (var i = 0; i < minimum; i++)
        {
            ComplianceGuard.RecordExecution(declared);
        }

        var exception = await Record.ExceptionAsync(() => ComplianceGuard.DisposeAsyncCore(strict: true).AsTask());

        Assert.Null(exception);
    }

    [Fact]
    public async Task DisposeAsyncCore_Strict_ExecutedBelowMinimum_Throws()
    {
        ComplianceGuard.ResetForTesting();

        var declared = CapabilitySet.All;
        var minimum = ComplianceInventory.MinimumExecuted(declared);
        Assert.True(minimum > 0, "Precondition: the suite must have at least one mandatory test.");

        for (var i = 0; i < minimum - 1; i++)
        {
            ComplianceGuard.RecordExecution(declared);
        }

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => ComplianceGuard.DisposeAsyncCore(strict: true).AsTask());
    }
}
