// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Serialization.CodeGen.Tests;

using Cvoya.Graph.Serialization;

/// <summary>
/// The generator must never emit invalid C# for open generic graph entities (see #373). Their
/// serializer would be a non-generic class referencing unbound type parameters, so open generic
/// roots are excluded from generation entirely (the actionable CG016 diagnostic is asserted
/// separately in the analyzer tests). A non-generic entity built from a closed construction such as
/// <c>StringNode : GenericNode&lt;string&gt;</c> still generates and round-trips.
/// </summary>
public class OpenGenericEntityTests
{
    [Fact]
    public void OpenGenericNode_IsExcludedWithoutInvalidCode()
    {
        const string source = """
            using Cvoya.Graph;

            namespace OpenGeneric;

            public record GenericNode<T> : Node
            {
                public string Name { get; set; } = string.Empty;
            }
            """;

        var generated = GeneratorTestHelpers.RunGenerator(source);

        Assert.Contains("== No generated sources ==", generated, StringComparison.Ordinal);
        Assert.DoesNotContain("GenericNodeSerializer", generated, StringComparison.Ordinal);
        Assert.DoesNotContain("== Compile errors in output compilation ==", generated, StringComparison.Ordinal);
    }

    [Fact]
    public void EntityNestedInOpenGenericType_IsExcludedWithoutInvalidCode()
    {
        // A non-generic entity nested in an open generic still captures the containing type's unbound
        // parameter, so it is rejected too.
        const string source = """
            using Cvoya.Graph;

            namespace NestedOpenGeneric;

            public class Container<T>
            {
                public record Inner : Node
                {
                    public string Name { get; set; } = string.Empty;
                }
            }
            """;

        var generated = GeneratorTestHelpers.RunGenerator(source);

        Assert.Contains("== No generated sources ==", generated, StringComparison.Ordinal);
        Assert.DoesNotContain("InnerSerializer", generated, StringComparison.Ordinal);
        Assert.DoesNotContain("== Compile errors in output compilation ==", generated, StringComparison.Ordinal);
    }

    [Fact]
    public void NonGenericEntityFromClosedConstruction_GeneratesAndRoundTrips()
    {
        // The closed base substitutes T=string, so `Value` is an ordinary simple property on the
        // concrete non-generic entity.
        const string source = """
            using Cvoya.Graph;

            namespace ClosedConstruction;

            public abstract record GenericNode<T> : Node
            {
                public T Value { get; set; } = default!;
            }

            [Node("StringNode")]
            public record StringNode : GenericNode<string>;
            """;
        var assembly = GeneratorTestHelpers.CompileAndLoadGeneratedAssembly(source);
        // The abstract open generic base is never generated; the closed concrete subtype is.
        Assert.Null(assembly.GetType("ClosedConstruction.Generated.GenericNodeSerializer", throwOnError: false));
        var nodeType = assembly.GetType("ClosedConstruction.StringNode", throwOnError: true)!;
        var serializer = CreateSerializer(assembly, "ClosedConstruction.Generated.StringNodeSerializer");

        var node = Activator.CreateInstance(nodeType)!;
        nodeType.GetProperty("Value")!.SetValue(node, "hello");

        var roundTripped = serializer.Deserialize(serializer.Serialize(node));

        Assert.Equal("hello", nodeType.GetProperty("Value")!.GetValue(roundTripped));
    }

    [Fact]
    public void MultipleClosedConstructionSubtypes_GenerateWithoutCollision()
    {
        // Two closed subtypes of the same open generic base must produce distinct serializers and
        // registrations - a clean compile/load proves no hint-name or registration collision.
        const string source = """
            using Cvoya.Graph;

            namespace ClosedConstructions;

            public abstract record GenericNode<T> : Node
            {
                public T Value { get; set; } = default!;
            }

            [Node("StringNode")]
            public record StringNode : GenericNode<string>;

            [Node("IntNode")]
            public record IntNode : GenericNode<int>;
            """;
        var assembly = GeneratorTestHelpers.CompileAndLoadGeneratedAssembly(source);

        Assert.NotNull(assembly.GetType("ClosedConstructions.Generated.StringNodeSerializer", throwOnError: true));
        Assert.NotNull(assembly.GetType("ClosedConstructions.Generated.IntNodeSerializer", throwOnError: true));
    }

    private static IEntitySerializer CreateSerializer(System.Reflection.Assembly assembly, string serializerTypeName)
    {
        var serializerType = assembly.GetType(serializerTypeName, throwOnError: true)!;
        return Assert.IsAssignableFrom<IEntitySerializer>(Activator.CreateInstance(serializerType));
    }
}
