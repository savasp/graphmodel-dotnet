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
