// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Core.Tests;

using System.Reflection;

[Trait("Area", "SchemaRegistry")]
[Collection("SchemaRegistry")]
public sealed class RuntimeKeySchemaValidationTests
{
    [Theory]
    [InlineData("string?")]
    [InlineData("int?")]
    public void CreateEntitySchemaInfo_NullableKey_Throws(string propertyType)
    {
        var source = $$"""
            #nullable enable
            using Cvoya.Graph;

            [Node("RuntimeNullableKeyNode")]
            public sealed record RuntimeNullableKeyNode : Node
            {
                [Property(IsKey = true)]
                public {{propertyType}} DomainKey { get; init; }
            }
            """;

        AssertInvalidSchema(source, "RuntimeNullableKeyNode", "non-nullable");
    }

    [Theory]
    [InlineData("string[]", " = [];")]
    [InlineData("object", " = new();")]
    [InlineData("ComplexKeyValue", " = new();")]
    public void CreateEntitySchemaInfo_NonScalarKey_Throws(string propertyType, string initializer)
    {
        var source = $$"""
            #nullable enable
            using Cvoya.Graph;

            public sealed record ComplexKeyValue;

            [Node("RuntimeNonScalarKeyNode")]
            public sealed record RuntimeNonScalarKeyNode : Node
            {
                [Property(IsKey = true)]
                public {{propertyType}} DomainKey { get; init; }{{initializer}}
            }
            """;

        AssertInvalidSchema(source, "RuntimeNonScalarKeyNode", "graph-storable scalar");
    }

    [Theory]
    [InlineData("IntPtr")]
    [InlineData("UIntPtr")]
    [InlineData("IntPtr?")]
    [InlineData("UIntPtr?")]
    [InlineData("nint[]")]
    [InlineData("List<nuint?>")]
    public void CreateEntitySchemaInfo_NativeSizedIntegerProperty_Throws(string propertyType)
    {
        var source = $$"""
            #nullable enable
            using System;
            using System.Collections.Generic;
            using Cvoya.Graph;

            [Node("RuntimeNativeIntegerNode")]
            public sealed record RuntimeNativeIntegerNode : Node
            {
                public {{propertyType}} DomainKey { get; init; }
            }
            """;

        AssertInvalidSchema(source, "RuntimeNativeIntegerNode", "native-sized integer", "IntPtr", "UIntPtr");
    }

    [Theory]
    [InlineData("nint")]
    [InlineData("nuint")]
    public void CreateEntitySchemaInfo_NativeSizedIntegerKey_Throws(string propertyType)
    {
        var source = $$"""
            using Cvoya.Graph;

            [Node("RuntimeNativeIntegerKeyNode")]
            public sealed record RuntimeNativeIntegerKeyNode : Node
            {
                [Property(IsKey = true)]
                public {{propertyType}} DomainKey { get; init; }
            }
            """;

        AssertInvalidSchema(source, "RuntimeNativeIntegerKeyNode", "native-sized integer", "IntPtr", "UIntPtr");
    }

    [Fact]
    public void CreateEntitySchemaInfo_ComplexPropertyContainingNativeSizedInteger_Throws()
    {
        const string source = """
            using System;
            using Cvoya.Graph;

            public sealed class NativeHandleHolder
            {
                public IntPtr Handle { get; set; }
            }

            [Node("RuntimeNestedNativeIntegerNode")]
            public sealed record RuntimeNestedNativeIntegerNode : Node
            {
                public NativeHandleHolder DomainKey { get; init; } = new();
            }
            """;

        AssertInvalidSchema(source, "RuntimeNestedNativeIntegerNode", "native-sized integer", "IntPtr");
    }

    [Fact]
    public void CreateEntitySchemaInfo_ComplexCollectionElementContainingNativeSizedInteger_Throws()
    {
        const string source = """
            using System;
            using System.Collections.Generic;
            using Cvoya.Graph;

            public sealed class PointerHolder
            {
                public UIntPtr Pointer { get; set; }
            }

            [Node("RuntimeNestedNativeIntegerCollectionNode")]
            public sealed record RuntimeNestedNativeIntegerCollectionNode : Node
            {
                public List<PointerHolder> DomainKey { get; init; } = new();
            }
            """;

        AssertInvalidSchema(source, "RuntimeNestedNativeIntegerCollectionNode", "native-sized integer", "UIntPtr");
    }

    [Fact]
    public void CreateEntitySchemaInfo_SelfReferentialComplexTypeWithNativeSizedInteger_Throws()
    {
        const string source = """
            #nullable enable
            using System;
            using Cvoya.Graph;

            public sealed class RecursiveHandleHolder
            {
                public RecursiveHandleHolder? Next { get; set; }
                public IntPtr Handle { get; set; }
            }

            [Node("RuntimeRecursiveNativeIntegerNode")]
            public sealed record RuntimeRecursiveNativeIntegerNode : Node
            {
                public RecursiveHandleHolder DomainKey { get; init; } = new();
            }
            """;

        AssertInvalidSchema(source, "RuntimeRecursiveNativeIntegerNode", "native-sized integer", "IntPtr");
    }

    [Fact]
    public void CreateEntitySchemaInfo_SelfReferentialComplexTypeWithoutNativeSizedInteger_Succeeds()
    {
        const string source = """
            #nullable enable
            using Cvoya.Graph;

            public sealed class RecursiveHolder
            {
                public RecursiveHolder? Next { get; set; }
                public string Name { get; set; } = string.Empty;
            }

            [Node("RuntimeRecursiveCleanNode")]
            public sealed record RuntimeRecursiveCleanNode : Node
            {
                public RecursiveHolder DomainKey { get; init; } = new();
            }
            """;

        AssertValidSchema(source, "RuntimeRecursiveCleanNode");
    }

    [Fact]
    public void CreateEntitySchemaInfo_ComplexPropertyWithIgnoredNativeSizedInteger_Succeeds()
    {
        const string source = """
            using System;
            using Cvoya.Graph;

            public sealed class IgnoredHandleHolder
            {
                [Property(Ignore = true)]
                public IntPtr Handle { get; set; }

                public string Name { get; set; } = string.Empty;
            }

            [Node("RuntimeIgnoredNestedNativeIntegerNode")]
            public sealed record RuntimeIgnoredNestedNativeIntegerNode : Node
            {
                public IgnoredHandleHolder DomainKey { get; init; } = new();
            }
            """;

        AssertValidSchema(source, "RuntimeIgnoredNestedNativeIntegerNode");
    }

    [Theory]
    [InlineData("IsKey = true", "IsKey")]
    [InlineData("IsUnique = true", "IsUnique")]
    [InlineData("IsIndexed = true", "IsIndexed")]
    [InlineData("IsRequired = true", "IsRequired")]
    public void CreateEntitySchemaInfo_IgnoredPropertyWithSchemaBehavior_Throws(
        string declaration,
        string expectedFlag)
    {
        var source = $$"""
            using Cvoya.Graph;

            [Node("RuntimeIgnoredSchemaFlagNode")]
            public sealed record RuntimeIgnoredSchemaFlagNode : Node
            {
                [Property(Ignore = true, {{declaration}})]
                public string DomainKey { get; init; } = string.Empty;
            }
            """;

        AssertInvalidSchema(source, "RuntimeIgnoredSchemaFlagNode", "Ignore", expectedFlag);
    }

    private static void AssertValidSchema(string source, string typeName)
    {
        RuntimeLabelCollisionFixtureAssembly.Run(
            source,
            [typeName],
            types => Assert.NotNull(CreateEntitySchemaInfo(types[0])));
    }

    private static void AssertInvalidSchema(
        string source,
        string typeName,
        params string[] expectedMessageParts)
    {
        RuntimeLabelCollisionFixtureAssembly.Run(
            source,
            [typeName],
            types =>
            {
                var exception = Assert.Throws<GraphException>(() => CreateEntitySchemaInfo(types[0]));
                Assert.Contains("DomainKey", exception.Message, StringComparison.OrdinalIgnoreCase);
                foreach (var expected in expectedMessageParts)
                {
                    Assert.Contains(expected, exception.Message, StringComparison.Ordinal);
                }
            });
    }

    private static EntitySchemaInfo CreateEntitySchemaInfo(Type entityType)
    {
        var method = typeof(SchemaRegistry).GetMethod(
            "CreateEntitySchemaInfo",
            BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("SchemaRegistry.CreateEntitySchemaInfo was not found.");

        try
        {
            return (EntitySchemaInfo)method.Invoke(null, [entityType, "RuntimeKeySchema", new List<string>()])!;
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            throw;
        }
    }
}
