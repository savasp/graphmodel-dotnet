// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.CompatibilityTests.Tests;

using System.Reflection;

/// <summary>
/// Pins <see cref="ComplianceGuard"/>'s method-identity enforcement, including theory-row
/// deduplication and the zero-test failure mode of a mis-wired provider project.
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
    private static readonly MethodInfo TheoryMethod = typeof(IQueryTests)
        .GetMethod(nameof(IQueryTests.CanQueryWithTakeEdgeCases))!;

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
        Assert.Contains("no provider capabilities were recorded", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RecordExecution_SameTheoryMethodThreeTimes_CoversOneMethod()
    {
        ComplianceGuard.ResetForTesting();

        for (var row = 0; row < 3; row++)
        {
            ComplianceGuard.RecordExecution(TheoryMethod, CapabilitySet.All);
        }

        var identity = ComplianceInventory.MethodIdentity(TheoryMethod);

        Assert.Equal(3, ComplianceGuard.RecordedCaseCount);
        Assert.Equal([identity], ComplianceGuard.ExecutedMethodIdentities);
    }

    [Fact]
    public async Task DisposeAsyncCore_Strict_ExactEligibleIdentitySet_DoesNotThrow()
    {
        ComplianceGuard.ResetForTesting();

        var declared = CapabilitySet.All.Except(GraphCapability.FullTextSearch);
        foreach (var method in ComplianceInventory.ExpectedTestMethods(declared))
        {
            ComplianceGuard.RecordExecution(method, declared);
        }

        var exception = await Record.ExceptionAsync(() => ComplianceGuard.DisposeAsyncCore(strict: true).AsTask());

        Assert.Null(exception);
    }

    [Fact]
    public async Task DisposeAsyncCore_Strict_DuplicateRowsCannotHideMissingMethod()
    {
        ComplianceGuard.ResetForTesting();

        var declared = CapabilitySet.All;
        var expectedMethods = ComplianceInventory.ExpectedTestMethods(declared);
        var missingMethod = expectedMethods.First(method => method != TheoryMethod);
        var missingIdentity = ComplianceInventory.MethodIdentity(missingMethod);

        foreach (var method in expectedMethods.Where(method => method != missingMethod))
        {
            ComplianceGuard.RecordExecution(method, declared);
        }

        // Three rows of one healthy theory push the raw case count above the old scalar floor.
        // Method-identity coverage must still expose the omitted contract method.
        for (var row = 0; row < 3; row++)
        {
            ComplianceGuard.RecordExecution(TheoryMethod, declared);
        }

        Assert.True(ComplianceGuard.RecordedCaseCount > ComplianceInventory.MinimumExecuted(declared));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => ComplianceGuard.DisposeAsyncCore(strict: true).AsTask());

        Assert.Contains(missingIdentity, exception.Message, StringComparison.Ordinal);
        Assert.Contains("Missing method identities", exception.Message, StringComparison.Ordinal);
        Assert.Contains("duplicate theory rows", exception.Message, StringComparison.Ordinal);
        Assert.Contains(nameof(GraphCapability.FullTextSearch), exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DisposeAsyncCore_Strict_MissingMethodMetadata_ThrowsActionableWiringError()
    {
        ComplianceGuard.ResetForTesting();
        ComplianceGuard.RecordExecution(method: null, declaredCapabilities: default);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => ComplianceGuard.DisposeAsyncCore(strict: true).AsTask());

        Assert.Contains("did not expose method metadata", exception.Message, StringComparison.Ordinal);
        Assert.Contains("TestContext.Current.TestMethod", exception.Message, StringComparison.Ordinal);
        Assert.Contains("IXunitTestMethod", exception.Message, StringComparison.Ordinal);
        Assert.Contains("declared capabilities [none]", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RecordExecution_ConcurrentDuplicateRows_RemainsThreadSafe()
    {
        ComplianceGuard.ResetForTesting();

        Parallel.For(
            fromInclusive: 0,
            toExclusive: 1_000,
            _ => ComplianceGuard.RecordExecution(TheoryMethod, CapabilitySet.All));

        Assert.Equal(1_000, ComplianceGuard.RecordedCaseCount);
        Assert.Single(ComplianceGuard.ExecutedMethodIdentities);
        Assert.Equal(CapabilitySet.All, ComplianceGuard.RecordedCapabilities);
    }

    [Fact]
    public async Task ResetForTesting_ClearsEntireLedger()
    {
        ComplianceGuard.ResetForTesting();
        ComplianceGuard.RecordExecution(method: null, declaredCapabilities: default);

        ComplianceGuard.ResetForTesting();

        Assert.Equal(0, ComplianceGuard.RecordedCaseCount);
        Assert.Empty(ComplianceGuard.ExecutedMethodIdentities);
        Assert.Equal(default, ComplianceGuard.RecordedCapabilities);

        foreach (var method in ComplianceInventory.ExpectedTestMethods(CapabilitySet.All))
        {
            ComplianceGuard.RecordExecution(method, CapabilitySet.All);
        }

        var exception = await Record.ExceptionAsync(
            () => ComplianceGuard.DisposeAsyncCore(strict: true).AsTask());

        Assert.Null(exception);
    }
}
