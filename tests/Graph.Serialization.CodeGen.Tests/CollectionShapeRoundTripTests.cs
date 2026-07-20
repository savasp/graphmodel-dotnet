// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Serialization.CodeGen.Tests;

using Cvoya.Graph.Serialization;

/// <summary>
/// Compiles and executes generated serializers for every collection shape CG004/CG005 accepts,
/// proving the generated deserialization assignment is assignable to the declared type and preserves
/// the collection's semantics (see #362). <see cref="GeneratorTestHelpers.CompileAndLoadGeneratedAssembly"/>
/// throws on any output-compilation error, so a shape that generated non-assignable source fails here.
/// </summary>
public class CollectionShapeRoundTripTests
{
    private static readonly string[] ExpectedCategories = ["c1", "c2"];

    [Fact]
    public void SetShapes_RoundTripAsSets()
    {
        const string source = """
            using System.Collections.Generic;
            using Cvoya.Graph;

            namespace SetShapes;

            [Node("Tagged")]
            public record TaggedNode : Node
            {
                public HashSet<int> Numbers { get; set; } = new();
                public ISet<string> Names { get; set; } = new HashSet<string>();
                public IReadOnlySet<int> Readonly { get; set; } = new HashSet<int>();
            }
            """;
        var assembly = GeneratorTestHelpers.CompileAndLoadGeneratedAssembly(source);
        var nodeType = assembly.GetType("SetShapes.TaggedNode", throwOnError: true)!;
        var serializer = CreateSerializer(assembly, "SetShapes.Generated.TaggedNodeSerializer");

        var node = Activator.CreateInstance(nodeType)!;
        nodeType.GetProperty("Numbers")!.SetValue(node, new HashSet<int> { 1, 2, 3 });
        nodeType.GetProperty("Names")!.SetValue(node, new HashSet<string> { "a", "b" });
        nodeType.GetProperty("Readonly")!.SetValue(node, new HashSet<int> { 7, 8 });

        var roundTripped = serializer.Deserialize(serializer.Serialize(node));

        var numbers = nodeType.GetProperty("Numbers")!.GetValue(roundTripped);
        Assert.IsType<HashSet<int>>(numbers);
        Assert.Equal(new HashSet<int> { 1, 2, 3 }, (HashSet<int>)numbers!);

        var names = nodeType.GetProperty("Names")!.GetValue(roundTripped);
        Assert.IsType<HashSet<string>>(names);
        Assert.Equal(new HashSet<string> { "a", "b" }, (HashSet<string>)names!);

        var readonlyNumbers = nodeType.GetProperty("Readonly")!.GetValue(roundTripped);
        Assert.IsType<HashSet<int>>(readonlyNumbers);
        Assert.Equal(new HashSet<int> { 7, 8 }, (HashSet<int>)readonlyNumbers!);
    }

    [Fact]
    public void ListArrayAndEnumerableShapes_RoundTrip()
    {
        const string source = """
            using System.Collections.Generic;
            using Cvoya.Graph;

            namespace ListShapes;

            [Node("Tagged")]
            public record TaggedNode : Node
            {
                public List<string> Tags { get; set; } = new();
                public string[] Categories { get; set; } = System.Array.Empty<string>();
                public IEnumerable<bool> Flags { get; set; } = System.Linq.Enumerable.Empty<bool>();
                public IList<int> Positions { get; set; } = new List<int>();
                public IReadOnlyCollection<string> Codes { get; set; } = new List<string>();
                public IReadOnlyList<int> Ranks { get; set; } = new List<int>();
                public ICollection<long> Ids { get; set; } = new List<long>();
            }
            """;
        var assembly = GeneratorTestHelpers.CompileAndLoadGeneratedAssembly(source);
        var nodeType = assembly.GetType("ListShapes.TaggedNode", throwOnError: true)!;
        var serializer = CreateSerializer(assembly, "ListShapes.Generated.TaggedNodeSerializer");

        var node = Activator.CreateInstance(nodeType)!;
        nodeType.GetProperty("Tags")!.SetValue(node, new List<string> { "x", "y" });
        nodeType.GetProperty("Categories")!.SetValue(node, ExpectedCategories);
        nodeType.GetProperty("Flags")!.SetValue(node, new List<bool> { true, false });
        nodeType.GetProperty("Positions")!.SetValue(node, new List<int> { 3, 4 });
        nodeType.GetProperty("Codes")!.SetValue(node, new List<string> { "a1", "b2" });
        nodeType.GetProperty("Ranks")!.SetValue(node, new List<int> { 10, 20 });
        nodeType.GetProperty("Ids")!.SetValue(node, new List<long> { 100L, 200L });

        var roundTripped = serializer.Deserialize(serializer.Serialize(node));

        Assert.Equal(new List<string> { "x", "y" }, Assert.IsType<List<string>>(nodeType.GetProperty("Tags")!.GetValue(roundTripped)));
        Assert.Equal(ExpectedCategories, Assert.IsType<string[]>(nodeType.GetProperty("Categories")!.GetValue(roundTripped)));
        Assert.Equal(new List<bool> { true, false }, Assert.IsType<List<bool>>(nodeType.GetProperty("Flags")!.GetValue(roundTripped)));
        Assert.Equal(new List<int> { 3, 4 }, Assert.IsType<List<int>>(nodeType.GetProperty("Positions")!.GetValue(roundTripped)));
        Assert.Equal(new List<string> { "a1", "b2" }, Assert.IsType<List<string>>(nodeType.GetProperty("Codes")!.GetValue(roundTripped)));
        Assert.Equal(new List<int> { 10, 20 }, Assert.IsType<List<int>>(nodeType.GetProperty("Ranks")!.GetValue(roundTripped)));
        Assert.Equal(new List<long> { 100L, 200L }, Assert.IsType<List<long>>(nodeType.GetProperty("Ids")!.GetValue(roundTripped)));
    }

    [Fact]
    public void EnumElementSetAndListShapes_RoundTrip()
    {
        const string source = """
            using System.Collections.Generic;
            using Cvoya.Graph;

            namespace EnumCollectionShapes;

            public enum Priority { Low, High }

            [Node("Tagged")]
            public record TaggedNode : Node
            {
                public HashSet<Priority> Priorities { get; set; } = new();
                public List<Priority> Ordered { get; set; } = new();
            }
            """;
        var assembly = GeneratorTestHelpers.CompileAndLoadGeneratedAssembly(source);
        var nodeType = assembly.GetType("EnumCollectionShapes.TaggedNode", throwOnError: true)!;
        var enumType = assembly.GetType("EnumCollectionShapes.Priority", throwOnError: true)!;
        var high = Enum.Parse(enumType, "High");
        var low = Enum.Parse(enumType, "Low");
        var serializer = CreateSerializer(assembly, "EnumCollectionShapes.Generated.TaggedNodeSerializer");

        var node = Activator.CreateInstance(nodeType)!;
        var prioritySet = (System.Collections.IEnumerable)typeof(HashSet<>).MakeGenericType(enumType)
            .GetConstructor(Type.EmptyTypes)!.Invoke(null)!;
        typeof(HashSet<>).MakeGenericType(enumType).GetMethod("Add")!.Invoke(prioritySet, [high]);
        nodeType.GetProperty("Priorities")!.SetValue(node, prioritySet);
        var orderedList = (System.Collections.IList)typeof(List<>).MakeGenericType(enumType)
            .GetConstructor(Type.EmptyTypes)!.Invoke(null)!;
        orderedList.Add(low);
        orderedList.Add(high);
        nodeType.GetProperty("Ordered")!.SetValue(node, orderedList);

        var roundTripped = serializer.Deserialize(serializer.Serialize(node));

        var priorities = (System.Collections.IEnumerable)nodeType.GetProperty("Priorities")!.GetValue(roundTripped)!;
        Assert.Equal(typeof(HashSet<>).MakeGenericType(enumType), priorities.GetType());
        Assert.Equal([high], priorities.Cast<object>());
        var ordered = (System.Collections.IEnumerable)nodeType.GetProperty("Ordered")!.GetValue(roundTripped)!;
        Assert.Equal(typeof(List<>).MakeGenericType(enumType), ordered.GetType());
        Assert.Equal([low, high], ordered.Cast<object>());
    }

    [Fact]
    public void ConstructorBoundAndInitOnlyCollections_RoundTripAndDefaultToAssignableValues()
    {
        const string source = """
            using System.Collections.Generic;
            using Cvoya.Graph;

            namespace ConstructorShapes;

            [Node("Tagged")]
            public sealed record TaggedNode(HashSet<int> Numbers) : Node
            {
                public ISet<string> Names { get; init; } = new HashSet<string>();
            }
            """;
        var assembly = GeneratorTestHelpers.CompileAndLoadGeneratedAssembly(source);
        var nodeType = assembly.GetType("ConstructorShapes.TaggedNode", throwOnError: true)!;
        var serializer = CreateSerializer(assembly, "ConstructorShapes.Generated.TaggedNodeSerializer");
        var node = Activator.CreateInstance(nodeType, [new HashSet<int> { 1, 2 }])!;
        nodeType.GetProperty("Names")!.SetValue(node, new HashSet<string> { "alpha", "beta" });

        var serialized = serializer.Serialize(node);
        var roundTripped = serializer.Deserialize(serialized);

        Assert.Equal(new HashSet<int> { 1, 2 }, Assert.IsType<HashSet<int>>(nodeType.GetProperty("Numbers")!.GetValue(roundTripped)));
        Assert.Equal(new HashSet<string> { "alpha", "beta" }, Assert.IsType<HashSet<string>>(nodeType.GetProperty("Names")!.GetValue(roundTripped)));

        serialized.SimpleProperties.Remove("Numbers");
        var missingValueRoundTrip = serializer.Deserialize(serialized);
        Assert.Empty(Assert.IsType<HashSet<int>>(nodeType.GetProperty("Numbers")!.GetValue(missingValueRoundTrip)));
    }

    [Fact]
    public void RelationshipCollectionShapes_RoundTrip()
    {
        const string source = """
            using System.Collections.Generic;
            using Cvoya.Graph;

            namespace RelationshipShapes;

            [Relationship("TAGGED")]
            public sealed record TaggedRelationship(string StartNodeId, string EndNodeId)
                : Relationship(StartNodeId, EndNodeId)
            {
                public HashSet<int> Scores { get; set; } = new();
                public IEnumerable<string> Tags { get; set; } = new List<string>();
            }
            """;
        var assembly = GeneratorTestHelpers.CompileAndLoadGeneratedAssembly(source);
        var relationshipType = assembly.GetType("RelationshipShapes.TaggedRelationship", throwOnError: true)!;
        var serializer = CreateSerializer(assembly, "RelationshipShapes.Generated.TaggedRelationshipSerializer");
        var relationship = Activator.CreateInstance(relationshipType, ["start", "end"])!;
        relationshipType.GetProperty("Scores")!.SetValue(relationship, new HashSet<int> { 4, 8 });
        relationshipType.GetProperty("Tags")!.SetValue(relationship, new List<string> { "one", "two" });

        var roundTripped = serializer.Deserialize(serializer.Serialize(relationship));

        Assert.Equal(new HashSet<int> { 4, 8 }, Assert.IsType<HashSet<int>>(relationshipType.GetProperty("Scores")!.GetValue(roundTripped)));
        Assert.Equal(new List<string> { "one", "two" }, Assert.IsType<List<string>>(relationshipType.GetProperty("Tags")!.GetValue(roundTripped)));
    }

    [Fact]
    public void UnsupportedCollectionShapes_DoNotEmitSerializers()
    {
        const string directUnsupported = """
            using System.Collections.Generic;
            using Cvoya.Graph;

            [Node("Invalid")]
            public sealed record InvalidNode : Node
            {
                public Queue<int> Values { get; set; } = new();
            }
            """;
        const string nestedUnsupported = """
            using System.Collections.Generic;
            using Cvoya.Graph;

            public sealed class Details
            {
                public Queue<int> Values { get; set; } = new();
            }

            [Node("Invalid")]
            public sealed record InvalidNode : Node
            {
                public Details Value { get; set; } = new();
            }
            """;
        const string multidimensionalArray = """
            using Cvoya.Graph;

            [Node("Invalid")]
            public sealed record InvalidNode : Node
            {
                public int[,] Values { get; set; } = new int[0, 0];
            }
            """;
        const string frameworkLookalike = """
            using Cvoya.Graph;

            namespace System.Collections.Generic
            {
                public interface ISet<TItem, TTag> : global::System.Collections.Generic.IEnumerable<TItem>
                {
                }
            }

            [Node("Invalid")]
            public sealed record InvalidNode : Node
            {
                public System.Collections.Generic.ISet<int, string> Values { get; set; } = null!;
            }
            """;

        AssertGeneratorProducesNoSources(directUnsupported);
        AssertGeneratorProducesNoSources(nestedUnsupported);
        AssertGeneratorProducesNoSources(multidimensionalArray);
        AssertGeneratorProducesNoSources(frameworkLookalike);
    }

    [Fact]
    public void NativeSizedIntegerProperties_DoNotEmitSerializers()
    {
        const string source = """
            using System;
            using System.Collections.Generic;
            using Cvoya.Graph;

            [Node("Invalid")]
            public sealed record InvalidNode : Node
            {
                public IntPtr Pointer { get; set; }
                public UIntPtr? OptionalPointer { get; set; }
                public List<nint> Pointers { get; set; } = new();
            }
            """;

        AssertGeneratorProducesNoSources(source);
    }

    private static IEntitySerializer CreateSerializer(System.Reflection.Assembly assembly, string serializerTypeName)
    {
        var serializerType = assembly.GetType(serializerTypeName, throwOnError: true)!;
        return Assert.IsAssignableFrom<IEntitySerializer>(Activator.CreateInstance(serializerType));
    }

    private static void AssertGeneratorProducesNoSources(string source)
    {
        var generated = GeneratorTestHelpers.RunGenerator(source);
        Assert.Contains("== No generated sources ==", generated, StringComparison.Ordinal);
        Assert.DoesNotContain("Serializer.g.cs", generated, StringComparison.Ordinal);
    }
}
