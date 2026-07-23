// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.CompatibilityTests;

using System.Reflection;

/// <summary>
/// An xUnit assembly fixture (<c>[assembly: AssemblyFixture(typeof(ComplianceGuard))]</c>) that
/// fails a strict compliance run when any compatibility test method expected for the declared
/// capability set did not execute.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="CompatibilityTest"/> reports each provider binding before capability gating and every
/// successful store acquisition to the static ledger this type owns. <see cref="DisposeAsync"/>
/// runs once, after every test in the assembly has finished, and - only when
/// <c>GRAPHMODEL_COMPLIANCE_STRICT=1</c> is set - rejects replaced default test bodies and asserts
/// that the ledger contains every method identity expected for the capabilities actually recorded.
/// Theory rows share one identity, and provider-specific tests have identities outside the expected
/// set, so neither can compensate for a missing compatibility method.
/// </para>
/// </remarks>
public sealed class ComplianceGuard : IAsyncDisposable
{
    /// <summary>
    /// The environment variable that arms strict method-identity compliance enforcement when set
    /// to <c>"1"</c>. Infrastructure failures are hard failures in every mode.
    /// </summary>
    public const string StrictModeEnvironmentVariable = "GRAPHMODEL_COMPLIANCE_STRICT";

    private static readonly Lock recordLock = new();
    private static readonly HashSet<string> executedMethodIdentities = new(StringComparer.Ordinal);
    private static readonly HashSet<string> providerOverrideMappings = new(StringComparer.Ordinal);
    private static long recordedCaseCount;
    private static int missingMethodMetadataCount;
    private static bool capabilitiesRecorded;
    private static CapabilitySet recordedCapabilities;

    /// <summary>
    /// Gets a value indicating whether strict compliance enforcement is armed, per
    /// <see cref="StrictModeEnvironmentVariable"/>.
    /// </summary>
    public static bool IsStrict => Environment.GetEnvironmentVariable(StrictModeEnvironmentVariable) == "1";

    /// <summary>
    /// Records that a compatibility test method successfully acquired a store and is about to
    /// execute, along with the provider's declared capabilities at the time.
    /// </summary>
    /// <param name="method">The running test method, or <see langword="null"/> when the host did
    /// not expose method metadata.</param>
    /// <param name="declaredCapabilities">The executing provider's declared capabilities.</param>
    internal static void RecordExecution(MethodInfo? method, CapabilitySet declaredCapabilities)
    {
        lock (recordLock)
        {
            recordedCaseCount++;
            recordedCapabilities = declaredCapabilities;
            capabilitiesRecorded = true;

            if (method is null)
            {
                missingMethodMetadataCount++;
            }
            else
            {
                executedMethodIdentities.Add(ComplianceInventory.MethodIdentity(method));
            }
        }
    }

    /// <summary>
    /// Records how a provider binding maps the discovered compatibility method before capability
    /// gating, so a replacement cannot hide behind an undeclared capability.
    /// </summary>
    /// <param name="method">The discovered test method, or <see langword="null"/> when unavailable.</param>
    /// <param name="bindingType">The concrete provider test binding.</param>
    internal static void RecordBinding(MethodInfo? method, Type bindingType)
    {
        ArgumentNullException.ThrowIfNull(bindingType);
        if (method is null || ProviderOverrideMapping(method, bindingType) is not { } mapping)
        {
            return;
        }

        lock (recordLock)
        {
            providerOverrideMappings.Add(mapping);
        }
    }

    /// <summary>
    /// Gets the number of test-case initializations recorded so far. This diagnostic count is not
    /// used as method coverage because multiple theory rows initialize the same method repeatedly.
    /// </summary>
    internal static long RecordedCaseCount
    {
        get
        {
            lock (recordLock)
            {
                return recordedCaseCount;
            }
        }
    }

    /// <summary>
    /// Gets a snapshot of the distinct executed method identities.
    /// </summary>
    internal static IReadOnlySet<string> ExecutedMethodIdentities
    {
        get
        {
            lock (recordLock)
            {
                return executedMethodIdentities.ToHashSet(StringComparer.Ordinal);
            }
        }
    }

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
        lock (recordLock)
        {
            recordedCaseCount = 0;
            executedMethodIdentities.Clear();
            providerOverrideMappings.Clear();
            missingMethodMetadataCount = 0;
            capabilitiesRecorded = false;
            recordedCapabilities = default;
        }
    }

    /// <summary>
    /// When strict mode is armed, asserts that the executed-method ledger contains every identity
    /// expected for the recorded capabilities, throwing <see cref="InvalidOperationException"/>
    /// if it does not. A no-op otherwise.
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
        if (!strict)
        {
            return ValueTask.CompletedTask;
        }

        var snapshot = Snapshot();
        if (!snapshot.CapabilitiesRecorded)
        {
            throw new InvalidOperationException(
                "GraphModel compliance guard: 0 compatibility test(s) executed and no provider " +
                "capabilities were recorded. This usually means the compatibility suite failed " +
                "to discover or run its tests (a mis-wired provider project). " +
                $"Unset {StrictModeEnvironmentVariable} to run locally without this guard.");
        }

        var capabilityContext = FormatCapabilities(snapshot.DeclaredCapabilities);
        if (snapshot.MissingMethodMetadataCount > 0)
        {
            throw new InvalidOperationException(
                $"GraphModel compliance guard: {snapshot.MissingMethodMetadataCount} successful " +
                "compatibility test initialization(s) did not expose method metadata for declared " +
                $"capabilities [{capabilityContext}]. Strict method-identity enforcement requires " +
                "TestContext.Current.TestMethod to expose an IXunitTestMethod with a MethodInfo. " +
                "Check that the provider project uses the supported xUnit v3 host and binds the " +
                "compatibility interfaces as documented.");
        }

        if (snapshot.ProviderOverrideMappings.Count > 0)
        {
            var mappings = string.Join(
                Environment.NewLine,
                snapshot.ProviderOverrideMappings.Order(StringComparer.Ordinal).Select(mapping => $"  - {mapping}"));
            throw new InvalidOperationException(
                $"GraphModel compliance guard: {snapshot.ProviderOverrideMappings.Count} provider " +
                "binding override(s) replaced packaged compatibility-test bodies. A provider-specific " +
                "implementation can assert behavior opposite to the inherited method name while still " +
                "recording the same inventory identity. Bind default test bodies unchanged; represent " +
                "a legitimate divergence as an explicitly named, capability/expectation-gated method " +
                $"in the packaged TCK.{Environment.NewLine}Provider override mappings:{Environment.NewLine}{mappings}");
        }

        var expected = ComplianceInventory.ExpectedMethodIdentities(snapshot.DeclaredCapabilities);
        var missing = expected
            .Except(snapshot.ExecutedMethodIdentities, StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();

        if (missing.Length > 0)
        {
            var covered = expected.Count - missing.Length;
            var missingList = string.Join(Environment.NewLine, missing.Select(identity => $"  - {identity}"));

            throw new InvalidOperationException(
                $"GraphModel compliance guard: {covered} of {expected.Count} required compatibility " +
                $"test method(s) executed for declared capabilities [{capabilityContext}]. The ledger " +
                $"recorded {snapshot.RecordedCaseCount} successful test case initialization(s) and " +
                $"{snapshot.ExecutedMethodIdentities.Count} distinct method identity/identities; " +
                "duplicate theory rows and provider-specific tests do not satisfy missing contract " +
                $"methods.{Environment.NewLine}Missing method identities:{Environment.NewLine}{missingList}");
        }

        return ValueTask.CompletedTask;
    }

    private static LedgerSnapshot Snapshot()
    {
        lock (recordLock)
        {
            return new LedgerSnapshot(
                recordedCaseCount,
                executedMethodIdentities.ToHashSet(StringComparer.Ordinal),
                providerOverrideMappings.ToHashSet(StringComparer.Ordinal),
                missingMethodMetadataCount,
                capabilitiesRecorded,
                recordedCapabilities);
        }
    }

    private static string FormatCapabilities(CapabilitySet capabilities)
    {
        var declared = Enum.GetValues<GraphCapability>()
            .Where(capabilities.Has)
            .Select(capability => capability.ToString())
            .ToArray();

        return declared.Length == 0 ? "none" : string.Join(", ", declared);
    }

    private static string? ProviderOverrideMapping(MethodInfo method, Type bindingType)
    {
        var declaringInterface = method.DeclaringType;
        if (declaringInterface is null || !declaringInterface.IsInterface)
        {
            return null;
        }

        var map = bindingType.GetInterfaceMap(declaringInterface);
        for (var index = 0; index < map.InterfaceMethods.Length; index++)
        {
            if (!SameMethod(map.InterfaceMethods[index], method))
            {
                continue;
            }

            var target = map.TargetMethods[index];
            return SameMethod(target, method)
                ? null
                : $"{bindingType.FullName}: {ComplianceInventory.MethodIdentity(method)} -> " +
                    $"{target.DeclaringType?.FullName}.{target.Name}";
        }

        return $"{bindingType.FullName}: {ComplianceInventory.MethodIdentity(method)} -> <unresolved>";
    }

    private static bool SameMethod(MethodInfo left, MethodInfo right) =>
        left.Module == right.Module && left.MetadataToken == right.MetadataToken;

    private readonly record struct LedgerSnapshot(
        long RecordedCaseCount,
        IReadOnlySet<string> ExecutedMethodIdentities,
        IReadOnlySet<string> ProviderOverrideMappings,
        int MissingMethodMetadataCount,
        bool CapabilitiesRecorded,
        CapabilitySet DeclaredCapabilities);
}
