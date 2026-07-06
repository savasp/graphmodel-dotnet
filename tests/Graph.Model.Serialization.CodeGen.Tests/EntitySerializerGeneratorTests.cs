// Copyright 2025 Savas Parastatidis
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//      https://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

namespace Cvoya.Graph.Model.Serialization.CodeGen.Tests;

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
            using Cvoya.Graph.Model;

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
    public Task Relationship()
    {
        const string source = """
            using Cvoya.Graph.Model;

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
            using Cvoya.Graph.Model;

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
            using Cvoya.Graph.Model;

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
            using Cvoya.Graph.Model;

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
    public void EntityTypeDiscovery_CachesOnUnchangedSecondRun()
    {
        const string source = """
            using Cvoya.Graph.Model;

            namespace TestNamespace;

            [Node("Person")]
            public record Person : Node
            {
                public string FirstName { get; set; } = string.Empty;
            }
            """;

        var reasons = GeneratorTestHelpers.GetSecondRunReasons(source, "GraphModel.EntityTypes");

        Assert.NotEmpty(reasons);
        Assert.DoesNotContain(reasons, reason =>
            reason is IncrementalStepRunReason.New or IncrementalStepRunReason.Modified);
        Assert.Contains(reasons, reason =>
            reason is IncrementalStepRunReason.Cached or IncrementalStepRunReason.Unchanged);
    }

    /// <summary>
    /// The scenario #83/#134 flagged: a whitespace-only keystroke in a file unrelated to any
    /// entity type must not re-run the referenced-assembly type walk - the expensive step that
    /// used to be backed by a process-wide static cache instead of proper incremental modeling.
    /// <c>GraphModel.MetadataReferences</c> and <c>GraphModel.ReferencedEntityTypes</c> are keyed
    /// off <see cref="Microsoft.CodeAnalysis.IncrementalGeneratorInitializationContext.MetadataReferencesProvider"/>,
    /// which is untouched by source-only edits, so both must report Cached/Unchanged, never
    /// New/Modified.
    /// </summary>
    /// <remarks>
    /// This intentionally does not assert Cached/Unchanged for the attribute-based
    /// (<c>GraphModel.NodeAttributeEntityTypes</c> etc.) or base-list-based steps, nor for the
    /// final <c>SourceOutput</c> step: those collect <see cref="INamedTypeSymbol"/> values, and
    /// Roslyn's incremental engine always reports <see cref="IncrementalStepRunReason.Modified"/>
    /// for a symbol-collecting step the first time it re-runs after ANY compilation change -
    /// including edits to unrelated files - because symbols from different
    /// <see cref="Microsoft.CodeAnalysis.Compilation"/> instances are never reference-equal,
    /// regardless of what changed. That's a pre-existing Roslyn-level characteristic of
    /// <c>ForAttributeWithMetadataName</c>/<c>CreateSyntaxProvider</c> pipelines collecting
    /// symbols, not something #134 introduced or is scoped to fix. What #134 guarantees is that
    /// the actual generated output is unaffected: see
    /// <see cref="EntityTypeDiscovery_GeneratedSourceIdenticalAfterUnrelatedEdit"/>.
    /// </remarks>
    [Theory]
    [InlineData("GraphModel.MetadataReferences")]
    [InlineData("GraphModel.ReferencedEntityTypes")]
    public void EntityTypeDiscovery_CachesAfterUnrelatedEdit(string trackingName)
    {
        const string source = """
            using Cvoya.Graph.Model;

            namespace TestNamespace;

            [Node("Person")]
            public record Person : Node
            {
                public string FirstName { get; set; } = string.Empty;
            }
            """;

        var reasons = GeneratorTestHelpers.GetUnrelatedEditReasons(source, trackingName);

        Assert.NotEmpty(reasons);
        Assert.DoesNotContain(reasons, reason =>
            reason is IncrementalStepRunReason.New or IncrementalStepRunReason.Modified);
        Assert.Contains(reasons, reason =>
            reason is IncrementalStepRunReason.Cached or IncrementalStepRunReason.Unchanged);
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
            using Cvoya.Graph.Model;

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
}
