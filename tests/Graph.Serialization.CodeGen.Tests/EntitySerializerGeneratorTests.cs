// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Serialization.CodeGen.Tests;

using Cvoya.Graph.Serialization;
using Microsoft.CodeAnalysis;

/// <summary>
/// Snapshot tests for <c>EntitySerializerGenerator</c> covering a simple node, a relationship,
/// a node with a nested complex property, and a node with a collection of complex properties.
/// </summary>
public class EntitySerializerGeneratorTests
{
    [Fact]
    public Task SimpleNode()
    {
        const string source = """
            using Cvoya.Graph;

            namespace TestNamespace;

            [Node("Person")]
            public record Person : Node
            {
                public string FirstName { get; set; } = string.Empty;
                public string LastName { get; set; } = string.Empty;
                public int Age { get; set; }
            }
            """;

        return Verifier.Verify(GeneratorTestHelpers.RunGenerator(source));
    }

    [Fact]
    public async Task GeneratedSchemaInitialization_IsThreadSafe()
    {
        var generatedProperties = string.Join(
            "\n",
            Enumerable.Range(0, 512).Select(index => $"public int Value{index} {{ get; set; }}"));
        var source = $$"""
            using Cvoya.Graph;

            namespace ThreadSafeSchema;

            [Node("ConcurrentNode")]
            public record ConcurrentNode : Node
            {
                {{generatedProperties}}
            }
            """;
        var assembly = GeneratorTestHelpers.CompileAndLoadGeneratedAssembly(source);
        var serializer = assembly.GetType("ThreadSafeSchema.Generated.ConcurrentNodeSerializer", throwOnError: true)!;
        var getSchema = serializer.GetMethod("GetSchemaStatic")!;

        const int workerCount = 16;
        var cancellationToken = TestContext.Current.CancellationToken;
        using var ready = new CountdownEvent(workerCount);
        using var start = new ManualResetEventSlim();
        var tasks = Enumerable.Range(0, workerCount)
            .Select(_ => Task.Factory.StartNew(
                () =>
                {
                    ready.Signal();
                    start.Wait(cancellationToken);
                    return (EntitySchema)getSchema.Invoke(null, null)!;
                },
                cancellationToken,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default))
            .ToArray();

        Assert.True(ready.Wait(TimeSpan.FromSeconds(10), cancellationToken));
        start.Set();
        var schemas = await Task.WhenAll(tasks);

        Assert.All(schemas, schema => Assert.Contains("Value511", schema.SimpleProperties.Keys));
    }

    [Fact]
    public Task NodeWithCustomPropertyLabels()
    {
        const string source = """
            using Cvoya.Graph;

            namespace TestNamespace;

            [Node("Person")]
            public record Person(
                [property: Property(Label = "last_name")] string LastName) : Node
            {
                [Property(Label = "first_name")]
                public string FirstName { get; init; } = string.Empty;
            }
            """;

        return Verifier.Verify(GeneratorTestHelpers.RunGenerator(source));
    }

    [Fact]
    public Task NodeWithGuidSimpleValues()
    {
        const string source = """
            using System;
            using System.Collections.Generic;
            using Cvoya.Graph;

            namespace TestNamespace;

            [Node("Tracked")]
            public record TrackedNode : Node
            {
                public Guid TrackingId { get; set; }
                public List<Guid> RelatedIds { get; set; } = new();
            }
            """;

        return Verifier.Verify(GeneratorTestHelpers.RunGenerator(source));
    }

    [Fact]
    public Task Relationship()
    {
        const string source = """
            using Cvoya.Graph;

            namespace TestNamespace;

            [Relationship("KNOWS")]
            public record Knows(string StartNodeId, string EndNodeId) : Relationship(StartNodeId, EndNodeId)
            {
                public int Since { get; set; }
            }
            """;

        return Verifier.Verify(GeneratorTestHelpers.RunGenerator(source));
    }

    [Fact]
    public Task NodeWithNestedComplexProperty()
    {
        const string source = """
            using Cvoya.Graph;

            namespace TestNamespace;

            public record Address
            {
                public string Street { get; set; } = string.Empty;
                public string City { get; set; } = string.Empty;
            }

            [Node("Person")]
            public record Person : Node
            {
                public string FirstName { get; set; } = string.Empty;

                [ComplexProperty(RelationshipType = "LIVES_AT")]
                public Address? HomeAddress { get; set; }
            }
            """;

        return Verifier.Verify(GeneratorTestHelpers.RunGenerator(source));
    }

    [Fact]
    public Task NodeWithCollectionOfComplexProperties()
    {
        const string source = """
            using System.Collections.Generic;
            using Cvoya.Graph;

            namespace TestNamespace;

            public record PhoneNumber
            {
                public string CountryCode { get; set; } = string.Empty;
                public string Number { get; set; } = string.Empty;
            }

            [Node("Person")]
            public record Person : Node
            {
                public string FirstName { get; set; } = string.Empty;
                public List<PhoneNumber> PhoneNumbers { get; set; } = new();
            }
            """;

        return Verifier.Verify(GeneratorTestHelpers.RunGenerator(source));
    }

    /// <summary>
    /// A collection declared with a base complex-property type (<c>List&lt;AnimalDescription&gt;</c>)
    /// can hold mixed derived instances at runtime. <c>DogDescription</c>/<c>PoliceDogDescription</c>
    /// are never themselves a declared property type anywhere - the generator must still discover
    /// them (by scanning the compilation for subtypes of the complex types it already generates
    /// serializers for, recursively) and give each its own generated serializer and registry
    /// entry, or a derived instance silently serializes/deserializes as its base type instead
    /// (see #146). The snapshot also proves two things the round-trip read path depends on:
    /// (1) a nested complex property that exists only on the most-derived type
    /// (<c>PoliceDogDescription.Handler</c>) produces its own <c>HandlerDescriptionSerializer</c>,
    /// and (2) <c>PoliceDogDescriptionSerializer</c>'s <c>Serialize</c> and <c>GetSchema</c> both
    /// include <c>Handler</c> in their complex properties - which is what lets the reader resolve
    /// the derived-only complex property by the discovered concrete type's schema.
    /// </summary>
    [Fact]
    public Task NodeWithCollectionOfComplexProperties_MixedDerivedInstances()
    {
        const string source = """
            using System.Collections.Generic;
            using Cvoya.Graph;

            namespace TestNamespace;

            public class HandlerDescription
            {
                public string Name { get; set; } = string.Empty;
            }

            public class AnimalDescription
            {
                public string Name { get; set; } = string.Empty;
            }

            public class DogDescription : AnimalDescription
            {
                public string Breed { get; set; } = string.Empty;
            }

            public class PoliceDogDescription : DogDescription
            {
                public string Badge { get; set; } = string.Empty;
                public HandlerDescription? Handler { get; set; }
            }

            [Node("Kennel")]
            public record Kennel : Node
            {
                public List<AnimalDescription> Animals { get; set; } = new();
            }
            """;

        return Verifier.Verify(GeneratorTestHelpers.RunGenerator(source));
    }

    [Fact]
    public void EntityTypeDiscovery_CachesAllTrackedGraphModelStepsOnUnchangedSecondRun()
    {
        const string source = """
            using Cvoya.Graph;

            namespace TestNamespace;

            [Node("Person")]
            public record Person : Node
            {
                public string FirstName { get; set; } = string.Empty;
            }
            """;

        var reasonsByStep = GeneratorTestHelpers.GetSecondRunReasonsByTrackingName(source);

        AssertAllTrackedStepsCachedOrUnchanged(reasonsByStep);
    }

    /// <summary>
    /// A whitespace-only keystroke in a file unrelated to any entity type must leave the tracked
    /// GraphModel pipeline cached/unchanged. The important bit for #148 is that the source and
    /// attribute/base-list discovery steps now return equatable value models instead of symbols,
    /// so the final generation input is unchanged too.
    /// </summary>
    [Fact]
    public void EntityTypeDiscovery_CachesAllTrackedGraphModelStepsAfterUnrelatedEdit()
    {
        const string source = """
            using Cvoya.Graph;

            namespace TestNamespace;

            [Node("Person")]
            public record Person : Node
            {
                public string FirstName { get; set; } = string.Empty;
            }
            """;

        var reasonsByStep = GeneratorTestHelpers.GetUnrelatedEditReasonsByTrackingName(source);

        AssertAllTrackedStepsCachedOrUnchanged(reasonsByStep);
    }

    /// <summary>
    /// Adding a new plain non-entity type exercises the false side of the syntax discovery
    /// predicates: the new type declaration is visible to the driver, but it has no graph
    /// attribute and no base list, so no GraphModel step should produce a new value.
    /// </summary>
    [Fact]
    public void EntityTypeDiscovery_CachesAllTrackedGraphModelStepsAfterAddingUnrelatedNonEntityType()
    {
        const string source = """
            using Cvoya.Graph;

            namespace TestNamespace;

            [Node("Person")]
            public record Person : Node
            {
                public string FirstName { get; set; } = string.Empty;
            }
            """;

        var reasonsByStep = GeneratorTestHelpers.GetUnrelatedNonEntityTypeAdditionReasonsByTrackingName(source);

        AssertAllTrackedStepsCachedOrUnchanged(reasonsByStep);
    }

    /// <summary>
    /// End-to-end proof that an unrelated whitespace edit produces byte-identical generated
    /// source: even though the driver re-executes <c>RegisterSourceOutput</c> for the reasons
    /// documented on <see cref="EntityTypeDiscovery_CachesAfterUnrelatedEdit"/>, the resulting
    /// generated files must not differ from the pre-edit run.
    /// </summary>
    [Fact]
    public void EntityTypeDiscovery_GeneratedSourceIdenticalAfterUnrelatedEdit()
    {
        const string source = """
            using Cvoya.Graph;

            namespace TestNamespace;

            [Node("Person")]
            public record Person : Node
            {
                public string FirstName { get; set; } = string.Empty;
            }
            """;

        var (before, after) = GeneratorTestHelpers.GetGeneratedSourceBeforeAndAfterUnrelatedEdit(source);

        Assert.Equal(before, after);
    }

    [Fact]
    public void EntityTypeDiscovery_RegeneratesAfterRelevantEntityEdit()
    {
        const string source = """
            using Cvoya.Graph;

            namespace TestNamespace;

            [Node("Person")]
            public record Person : Node
            {
                public string FirstName { get; set; } = string.Empty;
            }
            """;

        const string editedSource = """
            using Cvoya.Graph;

            namespace TestNamespace;

            [Node("Person")]
            public record Person : Node
            {
                public string FirstName { get; set; } = string.Empty;
                public string LastName { get; set; } = string.Empty;
            }
            """;

        var reasonsByStep = GeneratorTestHelpers.GetRelevantEditReasonsByTrackingName(source, editedSource);

        Assert.Contains("GraphModel.SerializerGenerationInput", reasonsByStep.Keys);
        Assert.Contains(reasonsByStep["GraphModel.SerializerGenerationInput"], IsInvalidatedReason);

        var (before, after) = GeneratorTestHelpers.GetGeneratedSourceBeforeAndAfterRelevantEdit(source, editedSource);

        Assert.NotEqual(before, after);
        Assert.Contains("LastName", after, StringComparison.Ordinal);
    }

    private static void AssertAllTrackedStepsCachedOrUnchanged(
        IReadOnlyDictionary<string, IReadOnlyCollection<IncrementalStepRunReason>> reasonsByStep)
    {
        string[] expectedTrackingNames =
        [
            "GraphModel.AllConcreteDeclaredTypes",
            "GraphModel.AttributedEntityTypes",
            "GraphModel.BaseListEntityTypes",
            "GraphModel.EntityTypes",
            "GraphModel.MetadataReferences",
            "GraphModel.NodeAttributeEntityTypes",
            "GraphModel.ReferencedEntityTypes",
            "GraphModel.RelationshipAttributeEntityTypes",
            "GraphModel.SerializerGenerationInput",
        ];

        foreach (var trackingName in expectedTrackingNames)
        {
            Assert.Contains(trackingName, reasonsByStep.Keys);
        }

        foreach (var (trackingName, reasons) in reasonsByStep
            .Where(step => step.Key.StartsWith("GraphModel.", StringComparison.Ordinal)))
        {
            Assert.NotEmpty(reasons);
            var invalidatedReasons = reasons.Where(IsInvalidatedReason).ToArray();
            Assert.True(
                invalidatedReasons.Length == 0,
                $"{trackingName} invalidated with: {string.Join(", ", invalidatedReasons)}");
            Assert.Contains(reasons, IsCachedReason);
        }
    }

    private static bool IsInvalidatedReason(IncrementalStepRunReason reason)
    {
        return reason is IncrementalStepRunReason.New or IncrementalStepRunReason.Modified;
    }

    private static bool IsCachedReason(IncrementalStepRunReason reason)
    {
        return reason is IncrementalStepRunReason.Cached or IncrementalStepRunReason.Unchanged;
    }
}
