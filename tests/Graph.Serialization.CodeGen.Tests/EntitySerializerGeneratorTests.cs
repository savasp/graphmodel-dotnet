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
    public Task NodeWithNullableSimpleCollections()
    {
        const string source = """
            using System;
            using System.Collections.Generic;
            using Cvoya.Graph;

            namespace NullableCollections;

            [Node("Tracked")]
            public record TrackedNode : Node
            {
                public List<int?> Scores { get; set; } = new();
                public List<Guid?> RelatedIds { get; set; } = new();
                public List<string?> Aliases { get; set; } = new();
            }
            """;

        return Verifier.Verify(GeneratorTestHelpers.RunGenerator(source));
    }

    [Fact]
    public void NodeWithNullableSimpleCollections_RoundTripsNullElements()
    {
        const string source = """
            using System;
            using System.Collections.Generic;
            using Cvoya.Graph;

            namespace NullableCollections;

            [Node("Tracked")]
            public record TrackedNode : Node
            {
                public List<int?> Scores { get; set; } = new();
                public List<Guid?> RelatedIds { get; set; } = new();
                public List<string?> Aliases { get; set; } = new();
            }
            """;
        var firstId = Guid.Parse("7a2ef43f-dadf-4c88-a2f6-af730f87a963");
        var assembly = GeneratorTestHelpers.CompileAndLoadGeneratedAssembly(source);
        var nodeType = assembly.GetType("NullableCollections.TrackedNode", throwOnError: true)!;
        var serializerType = assembly.GetType(
            "NullableCollections.Generated.TrackedNodeSerializer",
            throwOnError: true)!;
        var node = Activator.CreateInstance(nodeType)!;
        nodeType.GetProperty("Scores")!.SetValue(node, new List<int?> { 1, null, 3 });
        nodeType.GetProperty("RelatedIds")!.SetValue(node, new List<Guid?> { firstId, null });
        nodeType.GetProperty("Aliases")!.SetValue(node, new List<string?> { "first", null, "third" });
        var serializer = Assert.IsAssignableFrom<IEntitySerializer>(Activator.CreateInstance(serializerType));

        var entity = serializer.Serialize(node);
        var roundTripped = serializer.Deserialize(entity);

        Assert.Equal(
            new int?[] { 1, null, 3 },
            Assert.IsType<List<int?>>(nodeType.GetProperty("Scores")!.GetValue(roundTripped)));
        Assert.Equal(
            new Guid?[] { firstId, null },
            Assert.IsType<List<Guid?>>(nodeType.GetProperty("RelatedIds")!.GetValue(roundTripped)));
        Assert.Equal(
            new string?[] { "first", null, "third" },
            Assert.IsType<List<string?>>(nodeType.GetProperty("Aliases")!.GetValue(roundTripped)));
    }

    [Fact]
    public void NodeWithNonNullableSimpleCollections_RejectsNullElementsWithIndexedDiagnostics()
    {
        const string source = """
            using System.Collections.Generic;
            using Cvoya.Graph;

            namespace NonNullableCollections;

            [Node("Tracked")]
            public record TrackedNode : Node
            {
                public List<int> Scores { get; set; } = new();

                [Property(Label = "stored_names")]
                public List<string> Names { get; set; } = new();

                public List<string?> NullableNames { get; set; } = new();
            }
            """;
        var assembly = GeneratorTestHelpers.CompileAndLoadGeneratedAssembly(source);
        var nodeType = assembly.GetType("NonNullableCollections.TrackedNode", throwOnError: true)!;
        var serializerType = assembly.GetType(
            "NonNullableCollections.Generated.TrackedNodeSerializer",
            throwOnError: true)!;
        var serializer = Assert.IsAssignableFrom<IEntitySerializer>(Activator.CreateInstance(serializerType));
        var schema = serializer.GetSchema();

        Assert.False(schema.SimpleProperties["Scores"].IsElementNullable);
        Assert.False(schema.SimpleProperties["stored_names"].IsElementNullable);
        Assert.True(schema.SimpleProperties["NullableNames"].IsElementNullable);

        foreach (var (clrPropertyName, storedPropertyName, elementType, values) in new[]
        {
            (
                "Scores",
                "Scores",
                typeof(int),
                new[] { new SimpleValue(1, typeof(int)), new SimpleValue(null!, typeof(int)), new SimpleValue(3, typeof(int)) }),
            (
                "Names",
                "stored_names",
                typeof(string),
                new[] { new SimpleValue("first", typeof(string)), new SimpleValue(null!, typeof(string)), new SimpleValue("third", typeof(string)) }),
        })
        {
            var propertyInfo = nodeType.GetProperty(clrPropertyName)!;
            var entity = new EntityInfo(
                nodeType,
                "Tracked",
                ["Tracked"],
                new Dictionary<string, Property>
                {
                    [storedPropertyName] = new Property(
                        propertyInfo,
                        storedPropertyName,
                        Value: new SimpleCollection(values, elementType)),
                },
                new Dictionary<string, Property>());

            var exception = Assert.Throws<GraphException>(() => serializer.Deserialize(entity));

            Assert.Equal(
                $"Collection property '{storedPropertyName}' contains a null element at index 1, " +
                $"but its target element type '{elementType}' is non-nullable.",
                exception.Message);
            Assert.IsNotType<NullReferenceException>(exception.InnerException);
        }
    }

    [Fact]
    public void NodeWithBracedPropertyLabel_EscapesLabelInNullElementDiagnostic()
    {
        // Braces are legal in stored labels but would open interpolation holes in the generated
        // null-element diagnostic; quote/backslash labels are covered by the systemic sweep (#422).
        const string source = """
            using System.Collections.Generic;
            using Cvoya.Graph;

            namespace BracedLabels;

            [Node("Tracked")]
            public record TrackedNode : Node
            {
                [Property(Label = "stored{names}")]
                public List<string> Names { get; set; } = new();
            }
            """;
        var assembly = GeneratorTestHelpers.CompileAndLoadGeneratedAssembly(source);
        var nodeType = assembly.GetType("BracedLabels.TrackedNode", throwOnError: true)!;
        var serializerType = assembly.GetType(
            "BracedLabels.Generated.TrackedNodeSerializer",
            throwOnError: true)!;
        var serializer = Assert.IsAssignableFrom<IEntitySerializer>(Activator.CreateInstance(serializerType));

        var entity = new EntityInfo(
            nodeType,
            "Tracked",
            ["Tracked"],
            new Dictionary<string, Property>
            {
                ["stored{names}"] = new Property(
                    nodeType.GetProperty("Names")!,
                    "stored{names}",
                    Value: new SimpleCollection(
                        new[]
                        {
                            new SimpleValue("first", typeof(string)),
                            new SimpleValue(null!, typeof(string)),
                        },
                        typeof(string))),
            },
            new Dictionary<string, Property>());

        var exception = Assert.Throws<GraphException>(() => serializer.Deserialize(entity));

        Assert.Equal(
            "Collection property 'stored{names}' contains a null element at index 1, " +
            "but its target element type 'System.String' is non-nullable.",
            exception.Message);
    }

    /// <summary>
    /// Source shared by the complex-collection null/mistyped-element tests: a node with a
    /// <c>List&lt;Address&gt;</c> plus a second, unrelated complex type so a wrongly-typed element
    /// can be constructed from a serializer that is genuinely registered.
    /// </summary>
    private const string ComplexCollectionSource = """
        using System.Collections.Generic;
        using Cvoya.Graph;

        namespace ComplexCollections;

        public record Address
        {
            public string Street { get; set; } = string.Empty;
        }

        public record Tag
        {
            public string Name { get; set; } = string.Empty;
        }

        [Node("Person")]
        public record Person : Node
        {
            [Property(Label = "stored{addresses}")]
            public List<Address> Addresses { get; set; } = new();

            public Tag PrimaryTag { get; set; } = new();
        }
        """;

    private const string NullableComplexCollectionSource = """
        #nullable enable
        using System.Collections.Generic;
        using Cvoya.Graph;

        namespace NullableComplexCollections;

        public record Address
        {
            public string Street { get; set; } = string.Empty;
        }

        [Node("Person")]
        public record Person : Node
        {
            [Property(Label = "stored{addresses}")]
            public List<Address?> Addresses { get; set; } = new();
        }
        """;

    [Fact]
    public void NodeWithNullableComplexCollection_PreservesNullSlotsAndSchema()
    {
        var assembly = GeneratorTestHelpers.CompileAndLoadGeneratedAssembly(NullableComplexCollectionSource);
        var nodeType = assembly.GetType("NullableComplexCollections.Person", throwOnError: true)!;
        var addressType = assembly.GetType("NullableComplexCollections.Address", throwOnError: true)!;
        var serializerType = assembly.GetType(
            "NullableComplexCollections.Generated.PersonSerializer",
            throwOnError: true)!;
        var serializer = Assert.IsAssignableFrom<IEntitySerializer>(Activator.CreateInstance(serializerType));

        var addresses = (System.Collections.IList)Activator.CreateInstance(
            typeof(List<>).MakeGenericType(addressType))!;
        addresses.Add(Activator.CreateInstance(addressType));
        addresses.Add(null);
        addresses.Add(Activator.CreateInstance(addressType));
        var node = Activator.CreateInstance(nodeType)!;
        nodeType.GetProperty("Addresses")!.SetValue(node, addresses);

        var serialized = serializer.Serialize(node);
        var collection = Assert.IsType<EntityCollection>(serialized.ComplexProperties["stored{addresses}"].Value);
        Assert.Collection(collection.Entities, Assert.NotNull, Assert.Null, Assert.NotNull);
        Assert.True(serializer.GetSchema().ComplexProperties["stored{addresses}"].IsElementNullable);

        var roundTripped = serializer.Deserialize(serialized);
        var actual = (System.Collections.IList)nodeType.GetProperty("Addresses")!.GetValue(roundTripped)!;
        Assert.Collection(actual.Cast<object?>(), Assert.NotNull, Assert.Null, Assert.NotNull);
    }

    [Fact]
    public void NodeWithComplexCollection_RejectsNullElementOnWriteWithIndexedDiagnostic()
    {
        var assembly = GeneratorTestHelpers.CompileAndLoadGeneratedAssembly(ComplexCollectionSource);
        var nodeType = assembly.GetType("ComplexCollections.Person", throwOnError: true)!;
        var addressType = assembly.GetType("ComplexCollections.Address", throwOnError: true)!;
        var serializerType = assembly.GetType(
            "ComplexCollections.Generated.PersonSerializer",
            throwOnError: true)!;
        var serializer = Assert.IsAssignableFrom<IEntitySerializer>(Activator.CreateInstance(serializerType));

        var addresses = (System.Collections.IList)Activator.CreateInstance(
            typeof(List<>).MakeGenericType(addressType))!;
        addresses.Add(Activator.CreateInstance(addressType));
        addresses.Add(null);
        addresses.Add(Activator.CreateInstance(addressType));

        var node = Activator.CreateInstance(nodeType)!;
        nodeType.GetProperty("Addresses")!.SetValue(node, addresses);

        var exception = Assert.Throws<GraphException>(() => serializer.Serialize(node));

        Assert.Equal(
            "Complex collection property 'stored{addresses}' contains a null element at index 1, " +
            $"but its target element type '{addressType}' does not allow null elements.",
            exception.Message);
    }

    [Fact]
    public void NodeWithComplexCollection_RejectsMistypedElementOnReadWithIndexedDiagnostic()
    {
        var assembly = GeneratorTestHelpers.CompileAndLoadGeneratedAssembly(ComplexCollectionSource);
        var nodeType = assembly.GetType("ComplexCollections.Person", throwOnError: true)!;
        var addressType = assembly.GetType("ComplexCollections.Address", throwOnError: true)!;
        var tagType = assembly.GetType("ComplexCollections.Tag", throwOnError: true)!;
        var serializerType = assembly.GetType(
            "ComplexCollections.Generated.PersonSerializer",
            throwOnError: true)!;
        var serializer = Assert.IsAssignableFrom<IEntitySerializer>(Activator.CreateInstance(serializerType));

        // The second element carries Tag's ActualType, so it resolves to a real registered
        // serializer and materializes as a Tag - the exact case .OfType<Address>() used to drop.
        var entity = new EntityInfo(
            nodeType,
            "Person",
            ["Person"],
            new Dictionary<string, Property>(),
            new Dictionary<string, Property>
            {
                ["stored{addresses}"] = new Property(
                    nodeType.GetProperty("Addresses")!,
                    "stored{addresses}",
                    Value: new EntityCollection(
                        addressType,
                        [
                            NewComplexEntity(addressType, "Address", "Street"),
                            NewComplexEntity(tagType, "Tag", "Name"),
                        ])),
            });

        var exception = Assert.Throws<GraphException>(() => serializer.Deserialize(entity));

        Assert.Equal(
            $"Complex collection property 'stored{{addresses}}' contains an element of type '{tagType}' at index 1, " +
            $"which is not assignable to its target element type '{addressType}'.",
            exception.Message);
    }

    [Fact]
    public void NodeWithComplexCollection_RejectsNullDeserializerResultWithIndexedDiagnostic()
    {
        var assembly = GeneratorTestHelpers.CompileAndLoadGeneratedAssembly(ComplexCollectionSource);
        var nodeType = assembly.GetType("ComplexCollections.Person", throwOnError: true)!;
        var addressType = assembly.GetType("ComplexCollections.Address", throwOnError: true)!;
        var tagType = assembly.GetType("ComplexCollections.Tag", throwOnError: true)!;
        var serializerType = assembly.GetType(
            "ComplexCollections.Generated.PersonSerializer",
            throwOnError: true)!;
        var serializer = Assert.IsAssignableFrom<IEntitySerializer>(Activator.CreateInstance(serializerType));
        EntitySerializerRegistry.Instance.Register(tagType, new NullDeserializer(tagType));

        var entity = new EntityInfo(
            nodeType,
            "Person",
            ["Person"],
            new Dictionary<string, Property>(),
            new Dictionary<string, Property>
            {
                ["stored{addresses}"] = new Property(
                    nodeType.GetProperty("Addresses")!,
                    "stored{addresses}",
                    Value: new EntityCollection(
                        addressType,
                        [NewComplexEntity(tagType, "Tag", "Name")])),
            });

        var exception = Assert.Throws<GraphException>(() => serializer.Deserialize(entity));

        Assert.Equal(
            "Complex collection property 'stored{addresses}' contains a null element at index 0, " +
            $"but its target element type '{addressType}' does not allow null elements.",
            exception.Message);
    }

    [Fact]
    public void NodeWithComplexCollection_PreservesCountAndOrderForValidElements()
    {
        var assembly = GeneratorTestHelpers.CompileAndLoadGeneratedAssembly(ComplexCollectionSource);
        var nodeType = assembly.GetType("ComplexCollections.Person", throwOnError: true)!;
        var addressType = assembly.GetType("ComplexCollections.Address", throwOnError: true)!;
        var serializerType = assembly.GetType(
            "ComplexCollections.Generated.PersonSerializer",
            throwOnError: true)!;
        var serializer = Assert.IsAssignableFrom<IEntitySerializer>(Activator.CreateInstance(serializerType));

        var streetProperty = addressType.GetProperty("Street")!;
        var addresses = (System.Collections.IList)Activator.CreateInstance(
            typeof(List<>).MakeGenericType(addressType))!;
        foreach (var street in new[] { "First", "Second", "Third" })
        {
            var address = Activator.CreateInstance(addressType)!;
            streetProperty.SetValue(address, street);
            addresses.Add(address);
        }

        var node = Activator.CreateInstance(nodeType)!;
        nodeType.GetProperty("Addresses")!.SetValue(node, addresses);

        var roundTripped = serializer.Deserialize(serializer.Serialize(node));

        var actual = (System.Collections.IList)nodeType.GetProperty("Addresses")!.GetValue(roundTripped)!;
        Assert.Collection(
            actual.Cast<object>(),
            address => Assert.Equal("First", streetProperty.GetValue(address)),
            address => Assert.Equal("Second", streetProperty.GetValue(address)),
            address => Assert.Equal("Third", streetProperty.GetValue(address)));
    }

    [Fact]
    public void NodeWithEmptyComplexCollection_RoundTripsAsEmpty()
    {
        var assembly = GeneratorTestHelpers.CompileAndLoadGeneratedAssembly(ComplexCollectionSource);
        var nodeType = assembly.GetType("ComplexCollections.Person", throwOnError: true)!;
        var serializerType = assembly.GetType(
            "ComplexCollections.Generated.PersonSerializer",
            throwOnError: true)!;
        var serializer = Assert.IsAssignableFrom<IEntitySerializer>(Activator.CreateInstance(serializerType));

        Assert.False(serializer.GetSchema().ComplexProperties["stored{addresses}"].IsElementNullable);

        var roundTripped = serializer.Deserialize(serializer.Serialize(Activator.CreateInstance(nodeType)!));

        var actual = (System.Collections.IList)nodeType.GetProperty("Addresses")!.GetValue(roundTripped)!;
        Assert.Empty(actual);
    }

    private static EntityInfo NewComplexEntity(Type type, string label, string propertyName) =>
        new(
            type,
            label,
            [],
            new Dictionary<string, Property>
            {
                [propertyName] = new Property(
                    type.GetProperty(propertyName)!,
                    propertyName,
                    Value: new SimpleValue("value", typeof(string))),
            },
            new Dictionary<string, Property>());

    private sealed class NullDeserializer(Type entityType) : IEntitySerializer
    {
        public Type EntityType => entityType;

        public EntityInfo Serialize(object obj) => throw new NotSupportedException();

        public object Deserialize(EntityInfo entity) => null!;

        public EntitySchema GetSchema() => throw new NotSupportedException();
    }

    [Fact]
    public void NodeWithNullableObliviousSimpleCollection_TreatsElementsAsNonNullable()
    {
        const string source = """
            #nullable disable
            using System.Collections.Generic;
            using Cvoya.Graph;

            namespace ObliviousCollections;

            [Node("Tracked")]
            public record TrackedNode : Node
            {
                public List<string> Names { get; set; } = new();
            }
            """;
        var assembly = GeneratorTestHelpers.CompileAndLoadGeneratedAssembly(source);
        var nodeType = assembly.GetType("ObliviousCollections.TrackedNode", throwOnError: true)!;
        var serializerType = assembly.GetType(
            "ObliviousCollections.Generated.TrackedNodeSerializer",
            throwOnError: true)!;
        var serializer = Assert.IsAssignableFrom<IEntitySerializer>(Activator.CreateInstance(serializerType));

        Assert.False(serializer.GetSchema().SimpleProperties["Names"].IsElementNullable);

        var entity = new EntityInfo(
            nodeType,
            "Tracked",
            ["Tracked"],
            new Dictionary<string, Property>
            {
                ["Names"] = new Property(
                    nodeType.GetProperty("Names")!,
                    "Names",
                    Value: new SimpleCollection(
                        new[]
                        {
                            new SimpleValue("first", typeof(string)),
                            new SimpleValue(null!, typeof(string)),
                        },
                        typeof(string))),
            },
            new Dictionary<string, Property>());

        var exception = Assert.Throws<GraphException>(() => serializer.Deserialize(entity));

        Assert.Equal(
            "Collection property 'Names' contains a null element at index 1, " +
            "but its target element type 'System.String' is non-nullable.",
            exception.Message);
    }

    /// <summary>
    /// Pins that set-shaped collections deserialize via <c>.ToHashSet()</c> (never <c>.ToList()</c>,
    /// which is not assignable to a <c>HashSet&lt;T&gt;</c>/<c>ISet&lt;T&gt;</c>), and that a missing
    /// set property defaults to an empty <c>HashSet&lt;T&gt;</c> (see #362).
    /// </summary>
    [Fact]
    public Task NodeWithSetCollections()
    {
        const string source = """
            using System.Collections.Generic;
            using Cvoya.Graph;

            namespace TestNamespace;

            [Node("Tagged")]
            public record Tagged : Node
            {
                public HashSet<int> Numbers { get; set; } = new();
                public ISet<string> Names { get; set; } = new HashSet<string>();
            }
            """;

        return Verifier.Verify(GeneratorTestHelpers.RunGenerator(source));
    }

    /// <summary>
    /// Pins that a struct used as a complex property gets its own generated serializer, is registered,
    /// serializes without a <c>value is null</c> test (a compile error on a non-nullable value type),
    /// and is referenced by the owning type's schema (see #363).
    /// </summary>
    [Fact]
    public Task NodeWithStructComplexProperty()
    {
        const string source = """
            using Cvoya.Graph;

            namespace TestNamespace;

            public struct Address
            {
                public string Street { get; set; }
                public int Unit { get; set; }
            }

            [Node("Person")]
            public record Person : Node
            {
                public string FirstName { get; set; } = string.Empty;
                public Address HomeAddress { get; set; }
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
            public record Knows : Relationship
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
