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
    /// rather than silently drifting. Statically-skipped facts (<c>[Fact(Skip = ...)]</c>) are
    /// excluded: they can never execute on any provider, so they play no part in the strict-mode
    /// execution floor.
    /// </summary>
    // +9 for #120: eight of the nine pattern-comprehension tests were un-skipped once correlated
    // collection projections landed — Basic/Filtered/TimeBased/Ordered/Aggregated/Grouped pattern
    // comprehensions and TraversePathAndGroupBy (gated on CallSubqueries), plus the ungated
    // CanCombineNodeAndRelationshipQueries — and one cross-provider edge-case contract was added.
    // CanProjectRelationshipCounts stays skipped pending a node relationship-count (degree)
    // projection surface (#300).
    // +3 for #294: capability-certifying tests in IAdvancedQueryTests -
    // CanQueryPolymorphicBaseTypeAcrossSubtypeLabels (MultiLabelMatch), CanOrderByBareEntity
    // (OrderByEntity), and CanProjectComplexCollectionSize (PatternSizeProjection). OptionalTraversal
    // uses the two pre-existing null-propagating navigation contracts in IQueryTests.
    // +10 for ISubgraphCreationTests (atomic node–relationship–node subgraph create, #45).
    // +4 for #288: four new IFullTextSearchTests methods (multi-term AND, whole-token vs sub-token,
    // metacharacter robustness, search-as-source rejection).
    // +10 net for #306 scalar-key GroupBy: +11 IGroupByTests methods (gated on GroupByAggregation),
    // -1 for the removed IAdvancedQueryTests.GroupByThrowsNotSupportedUntilIssue100 (now supported).
    // +1 for #307: IAdvancedQueryTests.UnsupportedGroupedProjectionShapeIsRejectedConsistently -
    // pins that an unsupported correlated grouped-projection member (Take over the group) is rejected
    // with the same GraphQueryTranslationException on every provider.
    // +5 for #318: three correlated-composition cases and two scalar-projection parity cases.
    private const int ExpectedTotalTestMethods = 401;

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
            .Count(m => m.GetCustomAttributes(inherit: false).Any(a => a is FactAttribute { Skip: null }));

        Assert.Equal(expected, ComplianceInventory.TotalTestMethods);
    }

    [Fact]
    public void EveryCapabilityHasRunnableCoverageOrAnExplicitNoSurfaceRecord()
    {
        GraphCapability[] recordOnly =
        [
            GraphCapability.NestedTransactions,
            GraphCapability.ShortestPath,
        ];

        var covered = typeof(IGraphTest).Assembly.GetTypes()
            .Where(type => type.IsInterface && type.IsPublic && typeof(IGraphTest).IsAssignableFrom(type))
            .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(method => method.GetCustomAttribute<FactAttribute>(inherit: false) is { Skip: null })
                .SelectMany(method => method.GetCustomAttributes<RequiresCapabilityAttribute>(inherit: false)
                    .Concat(type.GetCustomAttributes<RequiresCapabilityAttribute>(inherit: false))))
            .Select(attribute => attribute.Capability)
            .Distinct()
            .Order()
            .ToArray();
        var expected = Enum.GetValues<GraphCapability>()
            .Except(recordOnly)
            .Order()
            .ToArray();

        Assert.Equal(expected, covered);
    }

    [Fact]
    public void MinimumExecuted_All_EqualsTotalTestMethods()
    {
        Assert.Equal(ComplianceInventory.TotalTestMethods, ComplianceInventory.MinimumExecuted(CapabilitySet.All));
    }

    [Fact]
    public void MinimumExecuted_AllExceptFullTextSearch_ExcludesFullTextSearchGatedMethods()
    {
        // Ground truth derived independently: every runnable test method gated on FullTextSearch,
        // whether by an interface-level attribute (IFullTextSearchTests) or a method-level one
        // (full-text tests living in otherwise ungated interfaces).
        var gatedMethodCount = typeof(IGraphTest).Assembly.GetTypes()
            .Where(t => t.IsInterface && t.IsPublic && typeof(IGraphTest).IsAssignableFrom(t))
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(m => m.GetCustomAttributes(inherit: false).Any(a => a is FactAttribute { Skip: null }))
                .Select(m => (Interface: t, Method: m)))
            .Count(pair =>
                pair.Method.GetCustomAttributes<RequiresCapabilityAttribute>(inherit: false)
                    .Concat(pair.Interface.GetCustomAttributes<RequiresCapabilityAttribute>(inherit: false))
                    .Any(a => a.Capability == GraphCapability.FullTextSearch));

        var declared = CapabilitySet.All.Except(GraphCapability.FullTextSearch);

        Assert.True(gatedMethodCount > 0);
        Assert.Equal(
            ComplianceInventory.TotalTestMethods - gatedMethodCount,
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
