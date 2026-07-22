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

    public static TheoryData<string, string> SimpleCollectionConstraints => new()
    {
        { "IsKey", "List<string>" },
        { "IsUnique", "List<string>" },
        { "IsKey", "List<string?>" },
        { "IsUnique", "List<string?>" },
        { "IsKey", "string[]" },
        { "IsUnique", "string[]" },
        { "IsKey", "List<int?>" },
        { "IsUnique", "List<int?>" },
    };

    [Theory]
    [MemberData(nameof(SimpleCollectionConstraints))]
    public void CreateEntitySchemaInfo_SimpleCollectionConstraint_ThrowsDeterministicError(
        string constraint,
        string propertyType)
    {
        var source = $$"""
            #nullable enable
            using System.Collections.Generic;
            using Cvoya.Graph;

            [Node("RuntimeCollectionConstraintNode")]
            public sealed record RuntimeCollectionConstraintNode : Node
            {
                [Property({{constraint}} = true)]
                public {{propertyType}} DomainKey { get; init; } = [];
            }
            """;

        AssertInvalidSchemaMessage(
            source,
            "RuntimeCollectionConstraintNode",
            $"Property 'RuntimeCollectionConstraintNode.DomainKey' cannot declare {constraint} " +
            "because simple collections cannot be key or unique values.");
    }

    [Theory]
    [InlineData("IsKey", "string[]")]
    [InlineData("IsUnique", "List<int?>")]
    public void InitializeAsync_SimpleCollectionConstraint_FailsBeforeProviderSetup(
        string constraint,
        string propertyType)
    {
        var source = $$"""
            #nullable enable
            using System.Collections.Generic;
            using Cvoya.Graph;

            [Node("RuntimeProviderSetupConstraintNode")]
            public sealed record RuntimeProviderSetupConstraintNode : Node
            {
                [Property({{constraint}} = true)]
                public {{propertyType}} DomainKey { get; init; } = [];
            }
            """;

        RuntimeLabelCollisionFixtureAssembly.Run(
            source,
            ["RuntimeProviderSetupConstraintNode"],
            _ =>
            {
                using var registry = new SchemaRegistry();
                var exception = Assert.ThrowsAny<GraphException>(
                    () => registry.InitializeAsync(TestContext.Current.CancellationToken).GetAwaiter().GetResult());

                Assert.Equal(
                    $"Property 'RuntimeProviderSetupConstraintNode.DomainKey' cannot declare {constraint} " +
                    "because simple collections cannot be key or unique values.",
                    exception.Message);
            });
    }

    [Fact]
    public void CreateEntitySchemaInfo_ScalarConstraintsAndOrdinaryCollection_Succeed()
    {
        const string source = """
            using System.Collections.Generic;
            using Cvoya.Graph;

            [Node("RuntimeValidConstraintNode")]
            public sealed record RuntimeValidConstraintNode : Node
            {
                [Property(IsKey = true)]
                public string DomainKey { get; init; } = string.Empty;

                [Property(IsUnique = true)]
                public int Sequence { get; init; }

                public List<string?> Aliases { get; init; } = [];
            }
            """;

        RuntimeLabelCollisionFixtureAssembly.Run(
            source,
            ["RuntimeValidConstraintNode"],
            types =>
            {
                var schema = CreateEntitySchemaInfo(types[0]);
                Assert.True(schema.Properties["DomainKey"].IsKey);
                Assert.True(schema.Properties["Sequence"].IsUnique);
                Assert.False(schema.Properties["Aliases"].IsKey);
                Assert.False(schema.Properties["Aliases"].IsUnique);
            });
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

    [Theory]
    [InlineData("System.Threading.Tasks.Task", "framework type")]
    [InlineData("System.Threading.Tasks.ValueTask", "framework type")]
    [InlineData("System.Action", "delegate type")]
    [InlineData("System.Collections.Generic.Dictionary<string, string>", "dictionary type")]
    [InlineData("System.Collections.IDictionary", "dictionary type")]
    [InlineData("System.Collections.Generic.List<System.Collections.Generic.Dictionary<string, string>>", "dictionary type")]
    [InlineData("System.Collections.Generic.List<System.Collections.Generic.List<System.Threading.Tasks.Task>>", "framework type")]
    [InlineData("System.IO.Stream", "framework type")]
    [InlineData("System.Net.IPAddress", "framework type")]
    [InlineData("System.Reflection.MemberInfo", "framework type")]
    [InlineData("System.Runtime.InteropServices.GCHandle", "framework type")]
    public void CreateEntitySchemaInfo_ComplexPropertyContainingUnsupportedShape_Throws(
        string propertyType,
        string expectedReason)
    {
        var source = $$"""
            using Cvoya.Graph;

            public sealed class UnsupportedHolder
            {
                public {{propertyType}} Unsupported { get; set; }
            }

            [Node("RuntimeNestedUnsupportedNode")]
            public sealed record RuntimeNestedUnsupportedNode : Node
            {
                public UnsupportedHolder DomainKey { get; init; } = new();
            }
            """;

        AssertInvalidSchema(
            source,
            "RuntimeNestedUnsupportedNode",
            "DomainKey.Unsupported",
            expectedReason);
    }

    [Fact]
    public void CreateEntitySchemaInfo_ComplexPropertyWithInheritedUnsupportedMember_Throws()
    {
        const string source = """
            using System.Threading.Tasks;
            using Cvoya.Graph;

            public abstract class UnsupportedHolderBase
            {
                public Task Unsupported { get; set; } = null!;
            }

            public sealed class DerivedUnsupportedHolder : UnsupportedHolderBase
            {
                public string Name { get; set; } = string.Empty;
            }

            [Node("RuntimeInheritedUnsupportedNode")]
            public sealed record RuntimeInheritedUnsupportedNode : Node
            {
                public DerivedUnsupportedHolder DomainKey { get; init; } = new();
            }
            """;

        AssertInvalidSchema(
            source,
            "RuntimeInheritedUnsupportedNode",
            "DomainKey.Unsupported",
            "framework type");
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

    [Fact]
    public void CreateEntitySchemaInfo_RecursiveComplexPropertyWithExcludedUnsupportedMembers_Succeeds()
    {
        const string source = """
            #nullable enable
            using System.Threading.Tasks;
            using Cvoya.Graph;

            public abstract class SupportedHolderBase
            {
                public string Name { get; set; } = string.Empty;
                public Task Hidden { get; set; } = null!;
            }

            public sealed class RecursiveSupportedHolder : SupportedHolderBase
            {
                public RecursiveSupportedHolder? Next { get; set; }

                [Property(Ignore = true)]
                public Task Ignored { get; set; } = null!;

                [Property(Ignore = true)]
                public new Task Hidden { get; set; } = null!;

                public static Task Static { get; } = Task.CompletedTask;

                public Task this[int index] => Task.CompletedTask;
            }

            [Node("RuntimeRecursiveFilteredNode")]
            public sealed record RuntimeRecursiveFilteredNode : Node
            {
                public RecursiveSupportedHolder DomainKey { get; init; } = new();
            }
            """;

        AssertValidSchema(source, "RuntimeRecursiveFilteredNode");
    }

    [Fact]
    public void CreateEntitySchemaInfo_NonSerializedUnsupportedEntityProperties_AreExcluded()
    {
        // The exclusions applied to nested complex members apply to the entity's own declarations
        // too: an unsupported type on an ignored property, a static property, or an indexer is not
        // a schema error, and only the ignored declaration stays in the schema (flagged as ignored).
        const string source = """
            using System.Threading.Tasks;
            using Cvoya.Graph;

            [Node("RuntimeNonSerializedUnsupportedNode")]
            public sealed record RuntimeNonSerializedUnsupportedNode : Node
            {
                public string DomainKey { get; init; } = string.Empty;

                [Property(Ignore = true)]
                public Task Ignored { get; init; } = null!;

                public static Task Shared { get; set; } = Task.CompletedTask;

                public Task this[int index] => Task.CompletedTask;
            }
            """;

        RuntimeLabelCollisionFixtureAssembly.Run(
            source,
            ["RuntimeNonSerializedUnsupportedNode"],
            types =>
            {
                var schema = CreateEntitySchemaInfo(types[0]);

                Assert.Contains("DomainKey", schema.Properties.Keys);
                Assert.True(schema.Properties["Ignored"].Ignore);
                Assert.DoesNotContain("Shared", schema.Properties.Keys);
                Assert.DoesNotContain("Item", schema.Properties.Keys);
            });
    }

    [Fact]
    public void CreateEntitySchemaInfo_EffectiveSerializedMemberParityFixture_Succeeds()
    {
        const string source = """
            using Cvoya.Graph;

            public sealed class EffectiveSerializedMemberParityDetails
            {
                public string Name { get; set; } = string.Empty;

                [Property(Ignore = true)]
                public INode IgnoredNode { get; set; } = null!;

                public static IRelationship SharedRelationship { get; set; } = null!;

                public INode this[int index]
                {
                    get => null!;
                    set { }
                }

                private IRelationship HiddenRelationship { get; set; } = null!;
            }

            [Node("EffectiveSerializedMemberParity")]
            public sealed record EffectiveSerializedMemberParityNode : Node
            {
                public EffectiveSerializedMemberParityDetails Details { get; init; } = new();

                [Property(Ignore = true)]
                public IRelationship IgnoredRelationship { get; init; } = null!;

                public static INode SharedNode { get; set; } = null!;

                public IRelationship this[int index]
                {
                    get => null!;
                    set { }
                }

                private INode HiddenNode { get; set; } = null!;
            }
            """;

        AssertValidSchema(source, "EffectiveSerializedMemberParityNode");
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

    private static void AssertInvalidSchemaMessage(
        string source,
        string typeName,
        string expectedMessage)
    {
        RuntimeLabelCollisionFixtureAssembly.Run(
            source,
            [typeName],
            types =>
            {
                var exception = Assert.ThrowsAny<GraphException>(() => CreateEntitySchemaInfo(types[0]));
                Assert.Equal(expectedMessage, exception.Message);
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
