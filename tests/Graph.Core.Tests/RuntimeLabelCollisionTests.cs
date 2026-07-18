// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Core.Tests;

using System.Reflection;

/// <summary>
/// Runtime counterpart to the CG007/CG008/CG009 analyzer rules (#155): mirrors their exact allow/deny
/// matrix against <see cref="SchemaRegistry"/> and <see cref="Labels"/>, the two places duplicate labels
/// were previously silently swallowed (last-registered/last-resolved type wins).
/// </summary>
/// <remarks>
/// The <c>SchemaRegistry</c>-focused tests below compile their (intentionally colliding) fixture types
/// into an isolated, collectible <see cref="System.Runtime.Loader.AssemblyLoadContext"/> via
/// <see cref="RuntimeLabelCollisionFixtureAssembly.Run"/> rather than declaring them as ordinary members
/// of this test assembly - see that type's remarks for why. This class shares an explicit xunit
/// collection with <c>SchemaRegistryTests</c> so the two never run concurrently, closing the residual
/// window between a fixture assembly's load and unload.
/// </remarks>
[Trait("Area", "RuntimeLabelCollision")]
[Collection("SchemaRegistry")]
public class RuntimeLabelCollisionTests : IDisposable
{
    public void Dispose()
    {
        Labels.ClearCachesForTesting();
        GC.SuppressFinalize(this);
    }

    // ===== SchemaRegistry: CG009 mirror (duplicate node labels) =====

    [Fact]
    public void RegisterNodeTypes_DistinctTypesSameExplicitLabel_Collides()
    {
        const string source = """
            using Cvoya.Graph;

            [Node("RLC_ExplicitA")]
            public sealed record ExplicitLabelNodeA : Node;

            [Node("RLC_ExplicitA")]
            public sealed record ExplicitLabelNodeADuplicate : Node;
            """;

        RuntimeLabelCollisionFixtureAssembly.Run(
            source,
            ["ExplicitLabelNodeA", "ExplicitLabelNodeADuplicate"],
            types =>
            {
                var collisions = RegisterNodes(types);

                var message = Assert.Single(collisions);
                Assert.Contains("RLC_ExplicitA", message, StringComparison.Ordinal);
                Assert.Contains(types[0].FullName!, message, StringComparison.Ordinal);
                Assert.Contains(types[1].FullName!, message, StringComparison.Ordinal);
            });
    }

    [Fact]
    public void RegisterNodeTypes_DistinctTypesSameFallbackLabelAcrossNamespaces_Throws()
    {
        // The runtime hole is strictly larger than "analyzer disabled": CG009 only ever compares types
        // that carry a [Node] attribute, so two unrelated types with no attribute at all - sharing a bare
        // CLR type name across namespaces - are structurally invisible to it. The runtime must still
        // catch this because SchemaRegistry.GetLabelFromType falls back to Labels.GetLabelFromType for
        // the type name, and Labels' own (static, process-wide) collision check fires there - so this
        // surfaces as a thrown GraphException rather than an entry in SchemaRegistry's own collisions
        // list, but it is caught either way.
        const string source = """
            using Cvoya.Graph;

            namespace NamespaceA { [Node] public sealed record FallbackNameCollisionNode : Node; }
            namespace NamespaceB { [Node] public sealed record FallbackNameCollisionNode : Node; }
            """;

        RuntimeLabelCollisionFixtureAssembly.Run(
            source,
            ["NamespaceA.FallbackNameCollisionNode", "NamespaceB.FallbackNameCollisionNode"],
            types =>
            {
                var exception = Assert.Throws<GraphException>(() => RegisterNodes(types));

                Assert.Contains("FallbackNameCollisionNode", exception.Message, StringComparison.Ordinal);
                Assert.Contains(types[0].FullName!, exception.Message, StringComparison.Ordinal);
                Assert.Contains(types[1].FullName!, exception.Message, StringComparison.Ordinal);
            });
    }

    // Label uniqueness is case-insensitive (one label per type): SchemaRegistry's
    // _nodeSchemas/_relationshipSchemas use StringComparer.OrdinalIgnoreCase, matching the resolver's
    // case-insensitive label comparison, so "Person" and "person" collide rather than registering as two
    // types the reader could not tell apart. See RegisterNodeTypes_DistinctTypesSameLabelDifferentCasing_Collides.

    [Fact]
    public void RegisterNodeTypes_UniqueLabels_NoCollision()
    {
        const string source = """
            using Cvoya.Graph;

            [Node("RLC_Unique1")]
            public sealed record UniqueNodeOne : Node;

            [Node("RLC_Unique2")]
            public sealed record UniqueNodeTwo : Node;
            """;

        RuntimeLabelCollisionFixtureAssembly.Run(
            source,
            ["UniqueNodeOne", "UniqueNodeTwo"],
            types => Assert.Empty(RegisterNodes(types)));
    }

    [Fact]
    public void RegisterNodeTypes_DerivedWithoutOwnAttributeInheritingBaseLabel_NoCollision()
    {
        // Mirrors CG009's NodeInheritingLabelFromParent_NoDiagnostic: a derived type that does not
        // declare its own [Node] attribute silently inherits the base's label (via reflection's
        // GetCustomAttribute(inherit: true) inside SchemaRegistry.GetLabelFromType) - this is legitimate,
        // not a collision, because there is only one "owner" of the label (the base type).
        const string source = """
            using Cvoya.Graph;

            [Node("RLC_InheritingBase")]
            public record InheritingBaseNode : Node;

            public sealed record InheritingDerivedNode : InheritingBaseNode;
            """;

        RuntimeLabelCollisionFixtureAssembly.Run(
            source,
            ["InheritingBaseNode", "InheritingDerivedNode"],
            types => Assert.Empty(RegisterNodes(types)));
    }

    [Fact]
    public void RegisterNodeTypes_DerivedWithOwnAttributeMatchingBaseLabel_Collides()
    {
        // Mirrors CG009's InheritedNodeWithDuplicateLabel_ProducesDiagnostic: unlike the previous case,
        // here the derived type declares its own [Node] attribute with the same label as its base -
        // both are now distinct "owners" of the same label, which is a genuine collision.
        const string source = """
            using Cvoya.Graph;

            [Node("RLC_OwnLabelBase")]
            public record OwnLabelBaseNode : Node;

            [Node("RLC_OwnLabelBase")]
            public sealed record OwnLabelDerivedNodeSameLabel : OwnLabelBaseNode;
            """;

        RuntimeLabelCollisionFixtureAssembly.Run(
            source,
            ["OwnLabelBaseNode", "OwnLabelDerivedNodeSameLabel"],
            types =>
            {
                var collisions = RegisterNodes(types);

                var message = Assert.Single(collisions);
                Assert.Contains(types[0].FullName!, message, StringComparison.Ordinal);
                Assert.Contains(types[1].FullName!, message, StringComparison.Ordinal);
            });
    }

    [Fact]
    public void RegisterNodeTypes_SameTypeRegisteredTwice_NoCollision()
    {
        const string source = """
            using Cvoya.Graph;

            [Node("RLC_SameTypeTwice")]
            public sealed record SameTypeTwiceNode : Node;
            """;

        RuntimeLabelCollisionFixtureAssembly.Run(
            source,
            ["SameTypeTwiceNode"],
            types =>
            {
                var registry = new SchemaRegistry();
                var collisions = new List<string>();

                InvokeRegisterNode(registry, types[0], collisions);
                InvokeRegisterNode(registry, types[0], collisions);

                Assert.Empty(collisions);
            });
    }

    [Fact]
    public void RegisterNodeTypes_ClosedGenericsOfSameDefinition_NoCollision()
    {
        // Both GenericNoCollision<int> and GenericNoCollision<string> are distinct Type objects, but both
        // resolve to the same fallback label (backticks stripped) - the brief's closed-generic
        // normalization rule treats them as the same type identity.
        //
        // Uses a name distinct from RegisterNodeTypes_GenuinelyDifferentTypeSharingGenericFallbackLabel's
        // fixture below so any failure points at this test's intentional pair instead of a cross-test
        // label reuse.
        const string source = """
            using Cvoya.Graph;

            public sealed record GenericNoCollision<T> : Node;
            """;

        RuntimeLabelCollisionFixtureAssembly.Run(
            source,
            ["GenericNoCollision`1"],
            types =>
            {
                var openGeneric = types[0];
                var closedInt = openGeneric.MakeGenericType(typeof(int));
                var closedString = openGeneric.MakeGenericType(typeof(string));

                Assert.Empty(RegisterNodes([closedInt, closedString]));
            });
    }

    [Fact]
    public void RegisterNodeTypes_GenuinelyDifferentTypeSharingGenericFallbackLabel_Throws()
    {
        const string source = """
            using Cvoya.Graph;

            public sealed record GenericDoesCollide<T> : Node;

            public sealed record GenericDoesCollide1 : Node;
            """;

        RuntimeLabelCollisionFixtureAssembly.Run(
            source,
            ["GenericDoesCollide`1", "GenericDoesCollide1"],
            types =>
            {
                var closedGeneric = types[0].MakeGenericType(typeof(int));

                var exception = Assert.Throws<GraphException>(() => RegisterNodes([closedGeneric, types[1]]));
                Assert.Contains("GenericDoesCollide1", exception.Message, StringComparison.Ordinal);
            });
    }

    // ===== SchemaRegistry: CG008 mirror (duplicate relationship labels) =====

    [Fact]
    public void RegisterRelationshipTypes_DistinctTypesSameExplicitLabel_Collides()
    {
        const string source = """
            using Cvoya.Graph;

            [Relationship("RLC_EXPLICIT_A")]
            public sealed record ExplicitLabelRelA(string StartNodeId, string EndNodeId) : Relationship(StartNodeId, EndNodeId);

            [Relationship("RLC_EXPLICIT_A")]
            public sealed record ExplicitLabelRelADuplicate(string StartNodeId, string EndNodeId) : Relationship(StartNodeId, EndNodeId);
            """;

        RuntimeLabelCollisionFixtureAssembly.Run(
            source,
            ["ExplicitLabelRelA", "ExplicitLabelRelADuplicate"],
            types =>
            {
                var collisions = RegisterRelationships(types);

                var message = Assert.Single(collisions);
                Assert.Contains("RLC_EXPLICIT_A", message, StringComparison.Ordinal);
                Assert.Contains(types[0].FullName!, message, StringComparison.Ordinal);
                Assert.Contains(types[1].FullName!, message, StringComparison.Ordinal);
            });
    }

    [Fact]
    public void RegisterRelationshipTypes_UniqueLabels_NoCollision()
    {
        const string source = """
            using Cvoya.Graph;

            [Relationship("RLC_UNIQUE_1")]
            public sealed record UniqueRelOne(string StartNodeId, string EndNodeId) : Relationship(StartNodeId, EndNodeId);

            [Relationship("RLC_UNIQUE_2")]
            public sealed record UniqueRelTwo(string StartNodeId, string EndNodeId) : Relationship(StartNodeId, EndNodeId);
            """;

        RuntimeLabelCollisionFixtureAssembly.Run(
            source,
            ["UniqueRelOne", "UniqueRelTwo"],
            types => Assert.Empty(RegisterRelationships(types)));
    }

    [Fact]
    public void RegisterRelationshipTypes_DerivedWithoutOwnAttributeInheritingBaseLabel_NoCollision()
    {
        const string source = """
            using Cvoya.Graph;

            [Relationship("RLC_INHERITING_BASE")]
            public record InheritingBaseRel(string StartNodeId, string EndNodeId) : Relationship(StartNodeId, EndNodeId);

            public sealed record InheritingDerivedRel(string StartNodeId, string EndNodeId) : InheritingBaseRel(StartNodeId, EndNodeId);
            """;

        RuntimeLabelCollisionFixtureAssembly.Run(
            source,
            ["InheritingBaseRel", "InheritingDerivedRel"],
            types => Assert.Empty(RegisterRelationships(types)));
    }

    [Fact]
    public void RegisterRelationshipTypes_DerivedWithOwnAttributeMatchingBaseLabel_Collides()
    {
        const string source = """
            using Cvoya.Graph;

            [Relationship("RLC_OWN_LABEL_BASE")]
            public record OwnLabelBaseRel(string StartNodeId, string EndNodeId) : Relationship(StartNodeId, EndNodeId);

            [Relationship("RLC_OWN_LABEL_BASE")]
            public sealed record OwnLabelDerivedRelSameLabel(string StartNodeId, string EndNodeId) : OwnLabelBaseRel(StartNodeId, EndNodeId);
            """;

        RuntimeLabelCollisionFixtureAssembly.Run(
            source,
            ["OwnLabelBaseRel", "OwnLabelDerivedRelSameLabel"],
            types => Assert.Single(RegisterRelationships(types)));
    }

    // ===== Node label vs. relationship type: explicitly NOT a collision =====

    [Fact]
    public void NodeLabelAndRelationshipType_SameName_NoCollision()
    {
        const string source = """
            using Cvoya.Graph;

            [Node("RLC_SharedName")]
            public sealed record SharedNameEntity : Node;

            [Relationship("RLC_SharedName")]
            public sealed record SharedNameRelationship(string StartNodeId, string EndNodeId) : Relationship(StartNodeId, EndNodeId);
            """;

        RuntimeLabelCollisionFixtureAssembly.Run(
            source,
            ["SharedNameEntity", "SharedNameRelationship"],
            types =>
            {
                var registry = new SchemaRegistry();
                var nodeCollisions = new List<string>();
                var relationshipCollisions = new List<string>();

                InvokeRegisterNode(registry, types[0], nodeCollisions);
                InvokeRegisterRelationship(registry, types[1], relationshipCollisions);
                MarkInitialized(registry);

                Assert.Empty(nodeCollisions);
                Assert.Empty(relationshipCollisions);

                var nodeSchema = registry.GetNodeSchema("RLC_SharedName");
                var relSchema = registry.GetRelationshipSchema("RLC_SharedName");

                Assert.NotNull(nodeSchema);
                Assert.NotNull(relSchema);
                Assert.Equal(types[0], nodeSchema.Type);
                Assert.Equal(types[1], relSchema.Type);
            });
    }

    // ===== SchemaRegistry: CG007 mirror (duplicate property labels within a type hierarchy) =====

    [Fact]
    public void CreateEntitySchemaInfo_TwoPropertiesSameExplicitLabel_Collides()
    {
        const string source = """
            using Cvoya.Graph;

            public sealed record SamePropertyLabelNode : Node
            {
                [Property(Label = "dup_prop")]
                public string First { get; init; } = string.Empty;

                [Property(Label = "dup_prop")]
                public string Second { get; init; } = string.Empty;
            }
            """;

        RuntimeLabelCollisionFixtureAssembly.Run(
            source,
            ["SamePropertyLabelNode"],
            types =>
            {
                var registry = new SchemaRegistry();
                var collisions = new List<string>();

                InvokeRegisterNode(registry, types[0], collisions);

                var message = Assert.Single(collisions);
                Assert.Contains("dup_prop", message, StringComparison.Ordinal);
                Assert.Contains("First", message, StringComparison.Ordinal);
                Assert.Contains("Second", message, StringComparison.Ordinal);
            });
    }

    [Fact]
    public void CreateEntitySchemaInfo_InheritedPropertyCollidesWithDerivedProperty_Collides()
    {
        // Mirrors CG007's InheritedNodeWithDuplicatePropertyLabels_ProducesDiagnostic: SchemaRegistry
        // flattens the full inheritance chain via Type.GetProperties(), so this is visible even though
        // Labels.GetLabelFromProperty (keyed by PropertyInfo.DeclaringType) cannot see it - see the
        // Labels-layer tests below for that narrower, complementary check.
        const string source = """
            using Cvoya.Graph;

            public record PropertyCollisionBaseNode : Node
            {
                [Property(Label = "shared_prop")]
                public string BaseValue { get; init; } = string.Empty;
            }

            public sealed record PropertyCollisionDerivedNode : PropertyCollisionBaseNode
            {
                [Property(Label = "shared_prop")]
                public string DerivedValue { get; init; } = string.Empty;
            }
            """;

        RuntimeLabelCollisionFixtureAssembly.Run(
            source,
            ["PropertyCollisionDerivedNode"],
            types =>
            {
                var registry = new SchemaRegistry();
                var collisions = new List<string>();

                InvokeRegisterNode(registry, types[0], collisions);

                var message = Assert.Single(collisions);
                Assert.Contains("shared_prop", message, StringComparison.Ordinal);
            });
    }

    [Fact]
    public void CreateEntitySchemaInfo_UniquePropertyLabels_NoCollision()
    {
        const string source = """
            using Cvoya.Graph;

            public sealed record UniquePropertyLabelsNode : Node
            {
                [Property(Label = "a")]
                public string A { get; init; } = string.Empty;

                [Property(Label = "b")]
                public string B { get; init; } = string.Empty;
            }
            """;

        RuntimeLabelCollisionFixtureAssembly.Run(
            source,
            ["UniquePropertyLabelsNode"],
            types =>
            {
                var registry = new SchemaRegistry();
                var collisions = new List<string>();

                InvokeRegisterNode(registry, types[0], collisions);

                Assert.Empty(collisions);
            });
    }

    // ===== Aggregated exception (RegisterGraphEntityTypes) =====

    [Fact]
    public void RegisterGraphEntityTypes_MultipleCollisions_ThrowsSingleAggregatedExceptionListingAll()
    {
        const string source = """
            using Cvoya.Graph;

            [Node("RLC_AggregateA")]
            public sealed record AggregateNodeA1 : Node;

            [Node("RLC_AggregateA")]
            public sealed record AggregateNodeA2 : Node;

            [Node("RLC_AggregateB")]
            public sealed record AggregateNodeB1 : Node;

            [Node("RLC_AggregateB")]
            public sealed record AggregateNodeB2 : Node;
            """;

        RuntimeLabelCollisionFixtureAssembly.Run(
            source,
            ["AggregateNodeA1", "AggregateNodeA2", "AggregateNodeB1", "AggregateNodeB2"],
            types =>
            {
                var registry = new SchemaRegistry();

                var exception = Assert.Throws<GraphException>(() =>
                    InvokeRegisterGraphEntityTypes(registry, types, []));

                Assert.Contains("RLC_AggregateA", exception.Message, StringComparison.Ordinal);
                Assert.Contains("RLC_AggregateB", exception.Message, StringComparison.Ordinal);
                foreach (var type in types)
                {
                    Assert.Contains(type.FullName!, exception.Message, StringComparison.Ordinal);
                }
            });
    }

    // ===== SchemaRegistry: case-insensitive label uniqueness (one label per type) =====

    [Fact]
    public void RegisterNodeTypes_DistinctTypesSameLabelDifferentCasing_Collides()
    {
        // A node type maps to exactly one label, unique case-insensitively across loaded types: "Person"
        // and "person" cannot both be registered, because the resolver compares stored labels
        // case-insensitively and could not tell them apart on read.
        const string source = """
            using Cvoya.Graph;

            [Node("RLC_CasePerson")]
            public sealed record CasePersonUpper : Node;

            [Node("rlc_caseperson")]
            public sealed record CasePersonLower : Node;
            """;

        RuntimeLabelCollisionFixtureAssembly.Run(
            source,
            ["CasePersonUpper", "CasePersonLower"],
            types =>
            {
                var collisions = RegisterNodes(types);

                var message = Assert.Single(collisions);
                Assert.Contains(types[0].FullName!, message, StringComparison.Ordinal);
                Assert.Contains(types[1].FullName!, message, StringComparison.Ordinal);
            });
    }

    // ===== Reflection helpers driving SchemaRegistry's private registration methods directly =====

    private static List<string> RegisterNodes(Type[] types)
    {
        var registry = new SchemaRegistry();
        var collisions = new List<string>();

        foreach (var type in types)
        {
            InvokeRegisterNode(registry, type, collisions);
        }

        return collisions;
    }

    private static List<string> RegisterRelationships(Type[] types)
    {
        var registry = new SchemaRegistry();
        var collisions = new List<string>();

        foreach (var type in types)
        {
            InvokeRegisterRelationship(registry, type, collisions);
        }

        return collisions;
    }

    /// <summary>
    /// Registers <paramref name="nodeTypes"/> into a fresh registry, then runs the real
    /// <c>SchemaRegistry.DetectAdditionalLabelCollisions</c> pass over the populated schemas - mirroring
    /// <c>RegisterGraphEntityTypes</c>'s register-then-reconcile sequence for the secondary-label rule
    /// without going through full-<see cref="AppDomain"/> assembly discovery. The fixtures used here carry
    /// no primary/property collisions, so the returned list isolates the additional-label collisions.
    /// </summary>
    private static List<string> RegisterNodesThenDetectAdditional(Type[] nodeTypes)
    {
        var registry = new SchemaRegistry();
        var collisions = new List<string>();

        foreach (var type in nodeTypes)
        {
            InvokeRegisterNode(registry, type, collisions);
        }

        var method = typeof(SchemaRegistry).GetMethod("DetectAdditionalLabelCollisions", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("SchemaRegistry.DetectAdditionalLabelCollisions was not found.");

        InvokeUnwrapped(method, registry, [collisions]);

        return collisions;
    }

    private static void InvokeRegisterNode(SchemaRegistry registry, Type nodeType, List<string> collisions)
    {
        var method = typeof(SchemaRegistry).GetMethod("RegisterNodeType", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("SchemaRegistry.RegisterNodeType was not found.");

        InvokeUnwrapped(method, registry, [nodeType, collisions]);
    }

    private static void InvokeRegisterRelationship(SchemaRegistry registry, Type relationshipType, List<string> collisions)
    {
        var method = typeof(SchemaRegistry).GetMethod("RegisterRelationshipType", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("SchemaRegistry.RegisterRelationshipType was not found.");

        InvokeUnwrapped(method, registry, [relationshipType, collisions]);
    }

    private static void MarkInitialized(SchemaRegistry registry)
    {
        var field = typeof(SchemaRegistry).GetField("_isInitialized", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("SchemaRegistry._isInitialized field was not found.");

        field.SetValue(registry, true);
    }

    /// <summary>
    /// Invokes <paramref name="method"/> via reflection and re-throws the original exception (rather than
    /// the <see cref="TargetInvocationException"/> wrapper reflection adds) so tests can assert on the
    /// real <see cref="GraphException"/> the production code throws.
    /// </summary>
    private static void InvokeUnwrapped(MethodInfo method, object target, object?[] args)
    {
        try
        {
            method.Invoke(target, args);
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
        }
    }

    /// <summary>
    /// Reproduces <c>SchemaRegistry.RegisterGraphEntityTypes</c>'s aggregation-then-throw behavior (collect
    /// every collision across all given types, then throw one <see cref="GraphException"/> listing all of
    /// them) by driving the same private per-type registration methods it calls internally - without going
    /// through full-<see cref="AppDomain"/> assembly discovery.
    /// </summary>
    private static void InvokeRegisterGraphEntityTypes(SchemaRegistry registry, Type[] nodeTypes, Type[] relationshipTypes)
    {
        var collisions = new List<string>();

        foreach (var nodeType in nodeTypes)
        {
            InvokeRegisterNode(registry, nodeType, collisions);
        }

        foreach (var relationshipType in relationshipTypes)
        {
            InvokeRegisterRelationship(registry, relationshipType, collisions);
        }

        if (collisions.Count > 0)
        {
            throw new GraphException(
                "Duplicate label(s) detected while registering graph entity types:\n" +
                string.Join('\n', collisions.Select(c => $"  - {c}")));
        }
    }

    // ===== Labels: reverse type lookup (cold-cache scan path) =====

    [Fact]
    public void Labels_GetTypeFromLabel_ColdCacheDistinctNodeTypesSameExplicitLabel_ThrowsWithAllCandidates()
    {
        const string source = """
            using Cvoya.Graph;

            [Node("Labels_RLC_ReverseExplicitNode")]
            public sealed record ReverseExplicitNodeA : Node;

            [Node("Labels_RLC_ReverseExplicitNode")]
            public sealed record ReverseExplicitNodeB : Node;
            """;

        RuntimeLabelCollisionFixtureAssembly.Run(
            source,
            ["ReverseExplicitNodeA", "ReverseExplicitNodeB"],
            types =>
            {
                Labels.ClearCachesForTesting();

                var exception = Assert.Throws<GraphException>(() =>
                    Labels.GetTypeFromLabel("Labels_RLC_ReverseExplicitNode"));

                Assert.Contains("Labels_RLC_ReverseExplicitNode", exception.Message, StringComparison.Ordinal);
                Assert.Contains(types[0].FullName!, exception.Message, StringComparison.Ordinal);
                Assert.Contains(types[1].FullName!, exception.Message, StringComparison.Ordinal);
            });
    }

    [Fact]
    public void Labels_GetTypeFromLabel_ColdCacheDistinctRelationshipTypesSameExplicitLabel_ThrowsWithAllCandidates()
    {
        const string source = """
            using Cvoya.Graph;

            [Relationship("Labels_RLC_REVERSE_EXPLICIT_REL")]
            public sealed record ReverseExplicitRelA(string StartNodeId, string EndNodeId) : Relationship(StartNodeId, EndNodeId);

            [Relationship("Labels_RLC_REVERSE_EXPLICIT_REL")]
            public sealed record ReverseExplicitRelB(string StartNodeId, string EndNodeId) : Relationship(StartNodeId, EndNodeId);
            """;

        RuntimeLabelCollisionFixtureAssembly.Run(
            source,
            ["ReverseExplicitRelA", "ReverseExplicitRelB"],
            types =>
            {
                Labels.ClearCachesForTesting();

                var exception = Assert.Throws<GraphException>(() =>
                    Labels.GetTypeFromLabel("Labels_RLC_REVERSE_EXPLICIT_REL"));

                Assert.Contains("Labels_RLC_REVERSE_EXPLICIT_REL", exception.Message, StringComparison.Ordinal);
                Assert.Contains(types[0].FullName!, exception.Message, StringComparison.Ordinal);
                Assert.Contains(types[1].FullName!, exception.Message, StringComparison.Ordinal);
            });
    }

    [Fact]
    public void Labels_GetTypeFromLabel_ColdCacheFindsRelationshipAndFallbackLabels()
    {
        const string source = """
            using Cvoya.Graph;

            [Relationship("Labels_RLC_REVERSE_REL")]
            public sealed record ReverseLookupRelationship(string StartNodeId, string EndNodeId) : Relationship(StartNodeId, EndNodeId);

            public sealed record ReverseLookupFallbackNode : Node;

            public sealed record ReverseLookupFallbackRelationship(string StartNodeId, string EndNodeId) : Relationship(StartNodeId, EndNodeId);
            """;

        RuntimeLabelCollisionFixtureAssembly.Run(
            source,
            ["ReverseLookupRelationship", "ReverseLookupFallbackNode", "ReverseLookupFallbackRelationship"],
            types =>
            {
                Labels.ClearCachesForTesting();
                Assert.Equal(types[0], Labels.GetTypeFromLabel("Labels_RLC_REVERSE_REL"));

                Labels.ClearCachesForTesting();
                Assert.Equal(types[1], Labels.GetTypeFromLabel("ReverseLookupFallbackNode"));

                Labels.ClearCachesForTesting();
                Assert.Equal(types[2], Labels.GetTypeFromLabel("ReverseLookupFallbackRelationship"));
            });
    }

    [Fact]
    public void Labels_GetTypeFromLabel_ColdCacheNodeLabelAndRelationshipTypeSameName_NoThrow()
    {
        const string source = """
            using Cvoya.Graph;

            [Node("Labels_RLC_ReverseSharedName")]
            public sealed record ReverseSharedNameNode : Node;

            [Relationship("Labels_RLC_ReverseSharedName")]
            public sealed record ReverseSharedNameRelationship(string StartNodeId, string EndNodeId) : Relationship(StartNodeId, EndNodeId);
            """;

        RuntimeLabelCollisionFixtureAssembly.Run(
            source,
            ["ReverseSharedNameNode", "ReverseSharedNameRelationship"],
            types =>
            {
                Labels.ClearCachesForTesting();

                var resolved = Labels.GetTypeFromLabel("Labels_RLC_ReverseSharedName");

                Assert.Equal(types[0], resolved);
            });
    }

    [Fact]
    public void Labels_GetTypeFromLabel_ColdCacheMatchesLabelCaseInsensitively()
    {
        // Labels are unique case-insensitively, and the cache is OrdinalIgnoreCase; the cold-cache scan
        // must agree, so a lookup whose casing differs from the declared label still resolves the type.
        const string source = """
            using Cvoya.Graph;

            [Node("Labels_RLC_CaseInsensitiveNode")]
            public sealed record CaseInsensitiveReverseNode : Node;
            """;

        RuntimeLabelCollisionFixtureAssembly.Run(
            source,
            ["CaseInsensitiveReverseNode"],
            types =>
            {
                Labels.ClearCachesForTesting();

                Assert.Equal(types[0], Labels.GetTypeFromLabel("labels_rlc_caseinsensitivenode"));
            });
    }

    // ===== Labels: reverse property lookup (cold-cache scan path) =====

    [Fact]
    public void Labels_GetPropertyFromLabel_ColdCacheHonorsEnclosingType()
    {
        const string source = """
            using Cvoya.Graph;

            public sealed record ReversePropertyOwnerA : Node
            {
                [Property(Label = "labels_reverse_shared_prop")]
                public string First { get; init; } = string.Empty;
            }

            public sealed record ReversePropertyOwnerB : Node
            {
                [Property(Label = "labels_reverse_shared_prop")]
                public string Second { get; init; } = string.Empty;
            }
            """;

        RuntimeLabelCollisionFixtureAssembly.Run(
            source,
            ["ReversePropertyOwnerA", "ReversePropertyOwnerB"],
            types =>
            {
                Labels.ClearCachesForTesting();

                var first = Labels.GetPropertyFromLabel("labels_reverse_shared_prop", types[0]);
                var second = Labels.GetPropertyFromLabel("labels_reverse_shared_prop", types[1]);

                Assert.Equal("First", first.Name);
                Assert.Equal(types[0], first.DeclaringType);
                Assert.Equal("Second", second.Name);
                Assert.Equal(types[1], second.DeclaringType);
            });
    }

    [Fact]
    public void Labels_GetPropertyFromLabel_ColdCacheSameTypeDifferentPropertiesSameLabel_ThrowsWithAllCandidates()
    {
        const string source = """
            using Cvoya.Graph;

            public sealed record ReversePropertyCollisionNode : Node
            {
                [Property(Label = "labels_reverse_dup_prop")]
                public string First { get; init; } = string.Empty;

                [Property(Label = "labels_reverse_dup_prop")]
                public string Second { get; init; } = string.Empty;
            }
            """;

        RuntimeLabelCollisionFixtureAssembly.Run(
            source,
            ["ReversePropertyCollisionNode"],
            types =>
            {
                Labels.ClearCachesForTesting();

                var exception = Assert.Throws<GraphException>(() =>
                    Labels.GetPropertyFromLabel("labels_reverse_dup_prop", types[0]));

                Assert.Contains("labels_reverse_dup_prop", exception.Message, StringComparison.Ordinal);
                Assert.Contains("First", exception.Message, StringComparison.Ordinal);
                Assert.Contains("Second", exception.Message, StringComparison.Ordinal);
            });
    }

    [Fact]
    public void Labels_ClearCachesForTesting_AllowsDifferentFixtureTypeWithSameLabel()
    {
        const string source = """
            using Cvoya.Graph;

            [Node("Labels_RLC_ClearCacheReuse")]
            public sealed record ClearCacheReuseNodeA : Node;

            [Node("Labels_RLC_ClearCacheReuse")]
            public sealed record ClearCacheReuseNodeB : Node;
            """;

        RuntimeLabelCollisionFixtureAssembly.Run(
            source,
            ["ClearCacheReuseNodeA", "ClearCacheReuseNodeB"],
            types =>
            {
                Labels.ClearCachesForTesting();
                Assert.Equal("Labels_RLC_ClearCacheReuse", Labels.GetLabelFromType(types[0]));

                Labels.ClearCachesForTesting();

                var exception = Record.Exception(() =>
                    Assert.Equal("Labels_RLC_ClearCacheReuse", Labels.GetLabelFromType(types[1])));

                Assert.Null(exception);
            });
    }

    // ===== Fixtures: Labels layer (non-colliding cases only - safe as ordinary static members since a
    // non-colliding registration never breaks SchemaRegistryTests' InitializeAsync-based tests) =====

    private sealed record LabelsRepeatedResolutionNode : Node
    {
        public string Value { get; init; } = string.Empty;
    }

    [Node("Labels_RLC_SharedName")]
    private sealed record LabelsSharedNameNode : Node;

    [Relationship("Labels_RLC_SharedName")]
    private sealed record LabelsSharedNameRelationship(string Start, string End) : Relationship(Start, End);

    private sealed record LabelsGenericCollisionNode<T> : Node;

    // ===== Labels: same-declaring-type property collision (complementary, narrower than CG007) =====
    //
    // These fixtures are intentionally colliding, so - like the SchemaRegistry fixtures above - they are
    // compiled into an isolated assembly rather than declared as ordinary members of this class.

    [Fact]
    public void Labels_GetLabelFromProperty_SameDeclaringTypeDifferentPropertySameLabel_Throws()
    {
        const string source = """
            using Cvoya.Graph;

            public sealed record LabelsPropertyCollisionNode : Node
            {
                [Property(Label = "labels_dup_prop")]
                public string First { get; init; } = string.Empty;

                [Property(Label = "labels_dup_prop")]
                public string Second { get; init; } = string.Empty;
            }
            """;

        RuntimeLabelCollisionFixtureAssembly.Run(
            source,
            ["LabelsPropertyCollisionNode"],
            types =>
            {
                var first = types[0].GetProperty("First")!;
                var second = types[0].GetProperty("Second")!;

                Labels.GetLabelFromProperty(first);

                var exception = Assert.Throws<GraphException>(() => Labels.GetLabelFromProperty(second));

                Assert.Contains("labels_dup_prop", exception.Message, StringComparison.Ordinal);
                Assert.Contains("First", exception.Message, StringComparison.Ordinal);
                Assert.Contains("Second", exception.Message, StringComparison.Ordinal);
            });
    }

    [Fact]
    public void ToDynamicNode_PropertyLabelCollision_Throws()
    {
        const string source = """
            using Cvoya.Graph;

            public sealed record DynamicPropertyCollisionNode : Node
            {
                [Property(Label = "dynamic_dup_prop")]
                public string First { get; init; } = string.Empty;

                [Property(Label = "dynamic_dup_prop")]
                public string Second { get; init; } = string.Empty;
            }
            """;

        RuntimeLabelCollisionFixtureAssembly.Run(
            source,
            ["DynamicPropertyCollisionNode"],
            types =>
            {
                var node = Assert.IsAssignableFrom<INode>(Activator.CreateInstance(types[0]));

                var exception = Assert.Throws<GraphException>(() => node.ToDynamic());

                Assert.Contains("dynamic_dup_prop", exception.Message, StringComparison.Ordinal);
                Assert.Contains("First", exception.Message, StringComparison.Ordinal);
                Assert.Contains("Second", exception.Message, StringComparison.Ordinal);
            });
    }

    [Fact]
    public void Labels_GetLabelFromProperty_SamePropertyResolvedTwice_NoThrow()
    {
        var property = typeof(LabelsRepeatedResolutionNode).GetProperty(nameof(LabelsRepeatedResolutionNode.Value))!;

        var first = Labels.GetLabelFromProperty(property);
        var second = Labels.GetLabelFromProperty(property);

        Assert.Equal(first, second);
    }

    // ===== Labels: node/relationship type collision (kind-aware, generic-normalized) =====

    [Fact]
    public void Labels_GetLabelFromType_DistinctNodeTypesSameExplicitLabel_Throws()
    {
        const string source = """
            using Cvoya.Graph;

            [Node("Labels_RLC_ExplicitA")]
            public sealed record LabelsExplicitNodeA : Node;

            [Node("Labels_RLC_ExplicitA")]
            public sealed record LabelsExplicitNodeADuplicate : Node;
            """;

        RuntimeLabelCollisionFixtureAssembly.Run(
            source,
            ["LabelsExplicitNodeA", "LabelsExplicitNodeADuplicate"],
            types =>
            {
                Labels.GetLabelFromType(types[0]);

                var exception = Assert.Throws<GraphException>(() => Labels.GetLabelFromType(types[1]));

                Assert.Contains("Labels_RLC_ExplicitA", exception.Message, StringComparison.Ordinal);
                Assert.Contains(types[0].FullName!, exception.Message, StringComparison.Ordinal);
                Assert.Contains(types[1].FullName!, exception.Message, StringComparison.Ordinal);
            });
    }

    [Fact]
    public void Labels_GetLabelFromType_SameTypeResolvedTwice_NoThrow()
    {
        var first = Labels.GetLabelFromType(typeof(LabelsRepeatedResolutionNode));
        var second = Labels.GetLabelFromType(typeof(LabelsRepeatedResolutionNode));

        Assert.Equal(first, second);
    }

    [Fact]
    public void Labels_GetLabelFromType_NodeLabelAndRelationshipTypeSameName_NoThrow()
    {
        // NOT a collision: node labels and relationship types are different namespaces in the graph model.
        Labels.GetLabelFromType(typeof(LabelsSharedNameNode));

        var exception = Record.Exception(() => Labels.GetLabelFromType(typeof(LabelsSharedNameRelationship)));

        Assert.Null(exception);
    }

    [Fact]
    public void Labels_GetLabelFromType_ClosedGenericsOfSameDefinition_NoThrow()
    {
        Labels.GetLabelFromType(typeof(LabelsGenericCollisionNode<int>));

        var exception = Record.Exception(() => Labels.GetLabelFromType(typeof(LabelsGenericCollisionNode<string>)));

        Assert.Null(exception);
    }
}
