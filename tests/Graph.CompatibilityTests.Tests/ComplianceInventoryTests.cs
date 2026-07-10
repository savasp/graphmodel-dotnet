// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.CompatibilityTests.Tests;

using System.Reflection;

/// <summary>
/// Pins <see cref="ComplianceInventory"/>'s reflection-based counts against ground truth: the
/// suite assembly's actual <c>I*Tests</c> interfaces and their <see cref="RequiresCapabilityAttribute"/>
/// attribution.
/// </summary>
public sealed class ComplianceInventoryTests
{
    /// <summary>
    /// The total is a snapshot of the suite at the time this test was written. It is expected to
    /// change as the suite grows - when it does, update this constant alongside the PR that adds
    /// or removes test methods, so this test keeps proving "the reflection count matches reality"
    /// rather than silently drifting.
    /// </summary>
    private const int ExpectedTotalTestMethods = 347;

    [Fact]
    public void TotalTestMethods_MatchesKnownSuiteSize()
    {
        Assert.Equal(ExpectedTotalTestMethods, ComplianceInventory.TotalTestMethods);
    }

    [Fact]
    public void TotalTestMethods_MatchesIndependentReflectionCount()
    {
        // Re-derives the count independently of ComplianceInventory's own implementation, so this
        // test can't pass merely because both counts share the same (potentially buggy) logic.
        var expected = typeof(IGraphTest).Assembly.GetTypes()
            .Where(t => t.IsInterface && t.IsPublic && typeof(IGraphTest).IsAssignableFrom(t))
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .Count(m => m.GetCustomAttributes(inherit: false).Any(a => a is FactAttribute));

        Assert.Equal(expected, ComplianceInventory.TotalTestMethods);
    }

    [Fact]
    public void MinimumExecuted_All_EqualsTotalTestMethods()
    {
        Assert.Equal(ComplianceInventory.TotalTestMethods, ComplianceInventory.MinimumExecuted(CapabilitySet.All));
    }

    [Fact]
    public void MinimumExecuted_AllExceptFullTextSearch_ExcludesFullTextSearchMethods()
    {
        var fullTextSearchMethodCount = typeof(IFullTextSearchTests)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Count(m => m.GetCustomAttributes(inherit: false).Any(a => a is FactAttribute));

        var declared = CapabilitySet.All.Except(GraphCapability.FullTextSearch);

        Assert.Equal(
            ComplianceInventory.TotalTestMethods - fullTextSearchMethodCount,
            ComplianceInventory.MinimumExecuted(declared));
    }

    [Fact]
    public void MinimumExecuted_EmptySet_ExcludesEveryCapabilityGatedMethod()
    {
        var minimum = ComplianceInventory.MinimumExecuted(default);

        Assert.True(minimum > 0, "At least one mandatory (non-capability-gated) test must exist.");
        Assert.True(minimum < ComplianceInventory.TotalTestMethods,
            "IFullTextSearchTests methods require a capability, so declaring none must exclude them.");
    }

    [Fact]
    public void ExpectedCapabilitySkips_IsTotalMinusMinimumExecuted()
    {
        var declared = CapabilitySet.All.Except(GraphCapability.FullTextSearch);

        Assert.Equal(
            ComplianceInventory.TotalTestMethods - ComplianceInventory.MinimumExecuted(declared),
            ComplianceInventory.ExpectedCapabilitySkips(declared));
    }

    [Fact]
    public void ExpectedCapabilitySkips_All_IsZero()
    {
        Assert.Equal(0, ComplianceInventory.ExpectedCapabilitySkips(CapabilitySet.All));
    }

    [Fact]
    public void IFullTextSearchTests_IsAttributedWithRequiresCapability()
    {
        var attribute = typeof(IFullTextSearchTests).GetCustomAttribute<RequiresCapabilityAttribute>();

        Assert.NotNull(attribute);
        Assert.Equal(GraphCapability.FullTextSearch, attribute.Capability);
    }
}
