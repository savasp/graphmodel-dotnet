// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.CompatibilityTests;

/// <summary>
/// An xUnit assembly fixture (<c>[assembly: AssemblyFixture(typeof(ComplianceGuard))]</c>) that
/// fails a strict compliance run whose executed-test count falls short of what the suite expects
/// for the declared capability set.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="CompatibilityTest"/> reports every successful store acquisition to the static
/// ledger this type owns. <see cref="DisposeAsync"/> runs once, after every test in the assembly
/// has finished, and - only when <c>GRAPHMODEL_COMPLIANCE_STRICT=1</c> is set - asserts that the
/// ledger's executed count is at least <see cref="ComplianceInventory.MinimumExecuted(CapabilitySet)"/>
/// for the capabilities actually recorded. This guards specifically against a mis-wired provider
/// project that discovers and executes zero (or too few) tests: without it, "0 tests ran" and "N
/// tests ran and all passed" would both report success.
/// </para>
/// </remarks>
public sealed class ComplianceGuard : IAsyncDisposable
{
    /// <summary>
    /// The environment variable that arms strict executed-count compliance enforcement when set
    /// to <c>"1"</c>. Infrastructure failures are hard failures in every mode.
    /// </summary>
    public const string StrictModeEnvironmentVariable = "GRAPHMODEL_COMPLIANCE_STRICT";

    private static long executedCount;
    private static readonly Lock recordLock = new();
    private static CapabilitySet recordedCapabilities;

    /// <summary>
    /// Gets a value indicating whether strict compliance enforcement is armed, per
    /// <see cref="StrictModeEnvironmentVariable"/>.
    /// </summary>
    public static bool IsStrict => Environment.GetEnvironmentVariable(StrictModeEnvironmentVariable) == "1";

    /// <summary>
    /// Records that a compatibility test successfully acquired a store and is about to execute,
    /// along with the provider's declared capabilities at the time.
    /// </summary>
    /// <param name="declaredCapabilities">The executing provider's declared capabilities.</param>
    internal static void RecordExecution(CapabilitySet declaredCapabilities)
    {
        Interlocked.Increment(ref executedCount);

        lock (recordLock)
        {
            recordedCapabilities = declaredCapabilities;
        }
    }

    /// <summary>
    /// Gets the number of executions recorded so far via <see cref="RecordExecution(CapabilitySet)"/>.
    /// </summary>
    internal static long ExecutedCount => Interlocked.Read(ref executedCount);

    /// <summary>
    /// Gets the most recently recorded declared capability set, or the empty set if no execution
    /// has been recorded yet.
    /// </summary>
    internal static CapabilitySet RecordedCapabilities
    {
        get
        {
            lock (recordLock)
            {
                return recordedCapabilities;
            }
        }
    }

    /// <summary>
    /// Resets the static ledger. Exists only so the suite's own meta-tests can exercise the guard
    /// deterministically; providers never need to call this.
    /// </summary>
    internal static void ResetForTesting()
    {
        Interlocked.Exchange(ref executedCount, 0);

        lock (recordLock)
        {
            recordedCapabilities = default;
        }
    }

    /// <summary>
    /// When strict mode is armed, asserts that the executed-test ledger meets
    /// <see cref="ComplianceInventory.MinimumExecuted(CapabilitySet)"/> for the recorded
    /// capabilities, throwing <see cref="InvalidOperationException"/> if it does not. A no-op
    /// otherwise.
    /// </summary>
    public ValueTask DisposeAsync() => DisposeAsyncCore(IsStrict);

    /// <summary>
    /// The guard's actual assertion logic, parameterized on strict mode rather than reading
    /// <see cref="IsStrict"/> directly, so the suite's own meta-tests can exercise both branches
    /// deterministically without mutating the real process environment (and racing whatever else
    /// might be reading it concurrently).
    /// </summary>
    /// <param name="strict">Whether strict enforcement is armed.</param>
    internal static ValueTask DisposeAsyncCore(bool strict)
    {
        if (strict)
        {
            var executed = ExecutedCount;
            var declared = RecordedCapabilities;
            var minimum = ComplianceInventory.MinimumExecuted(declared);

            if (executed < minimum)
            {
                throw new InvalidOperationException(
                    $"GraphModel compliance guard: only {executed} compatibility test(s) executed, " +
                    $"but at least {minimum} were expected for the declared capability set. This " +
                    "usually means the compatibility suite failed to discover or run its tests (a " +
                    $"mis-wired provider project) rather than a genuine capability skip. Unset " +
                    $"{StrictModeEnvironmentVariable} to run locally without this guard.");
            }
        }

        return ValueTask.CompletedTask;
    }
}
