// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Serialization.CodeGen.Tests;

using Cvoya.Graph.Serialization;

public sealed class CollectionConstraintSchemaValidationTests
{
    [Fact]
    public void GeneratedPropertySchemas_RejectEverySimpleCollectionConstraint()
    {
        const string source = """
            #nullable enable
            using System.Collections.Generic;
            using Cvoya.Graph;

            namespace GeneratedCollectionConstraints;

            [Node("KeyStrings")]
            public sealed record KeyStrings : Node
            {
                [Property(IsKey = true)]
                public List<string> Value { get; init; } = [];
            }

            [Node("UniqueStrings")]
            public sealed record UniqueStrings : Node
            {
                [Property(IsUnique = true)]
                public List<string> Value { get; init; } = [];
            }

            [Node("KeyNullableStrings")]
            public sealed record KeyNullableStrings : Node
            {
                [Property(IsKey = true)]
                public List<string?> Value { get; init; } = [];
            }

            [Node("UniqueNullableStrings")]
            public sealed record UniqueNullableStrings : Node
            {
                [Property(IsUnique = true)]
                public List<string?> Value { get; init; } = [];
            }

            [Node("KeyStringArray")]
            public sealed record KeyStringArray : Node
            {
                [Property(IsKey = true)]
                public string[] Value { get; init; } = [];
            }

            [Node("UniqueStringArray")]
            public sealed record UniqueStringArray : Node
            {
                [Property(IsUnique = true)]
                public string[] Value { get; init; } = [];
            }

            [Node("KeyNullableValues")]
            public sealed record KeyNullableValues : Node
            {
                [Property(IsKey = true)]
                public List<int?> Value { get; init; } = [];
            }

            [Node("UniqueNullableValues")]
            public sealed record UniqueNullableValues : Node
            {
                [Property(IsUnique = true)]
                public List<int?> Value { get; init; } = [];
            }
            """;
        var assembly = GeneratorTestHelpers.CompileAndLoadGeneratedAssembly(source);

        foreach (var (typeName, constraint) in new[]
        {
            ("KeyStrings", "IsKey"),
            ("UniqueStrings", "IsUnique"),
            ("KeyNullableStrings", "IsKey"),
            ("UniqueNullableStrings", "IsUnique"),
            ("KeyStringArray", "IsKey"),
            ("UniqueStringArray", "IsUnique"),
            ("KeyNullableValues", "IsKey"),
            ("UniqueNullableValues", "IsUnique"),
        })
        {
            var serializerType = assembly.GetType(
                $"GeneratedCollectionConstraints.Generated.{typeName}Serializer",
                throwOnError: true)!;
            var serializer = Assert.IsAssignableFrom<IEntitySerializer>(Activator.CreateInstance(serializerType));

            var exception = Assert.ThrowsAny<GraphException>(() => serializer.GetSchema());

            Assert.Equal(
                $"Property 'GeneratedCollectionConstraints.{typeName}.Value' cannot declare {constraint} " +
                "because simple collections cannot be key or unique values.",
                exception.Message);
        }
    }

    [Fact]
    public void GeneratedPropertySchema_AllowsScalarConstraintsAndOrdinaryCollection()
    {
        const string source = """
            #nullable enable
            using System.Collections.Generic;
            using Cvoya.Graph;

            namespace GeneratedValidConstraints;

            [Node("ValidConstraints")]
            public sealed record ValidConstraints : Node
            {
                [Property(IsKey = true)]
                public string DomainKey { get; init; } = string.Empty;

                [Property(IsUnique = true)]
                public int Sequence { get; init; }

                public List<string?> Aliases { get; init; } = [];
            }
            """;
        var assembly = GeneratorTestHelpers.CompileAndLoadGeneratedAssembly(source);
        var serializerType = assembly.GetType(
            "GeneratedValidConstraints.Generated.ValidConstraintsSerializer",
            throwOnError: true)!;
        var serializer = Assert.IsAssignableFrom<IEntitySerializer>(Activator.CreateInstance(serializerType));

        var schema = serializer.GetSchema();

        Assert.Equal(PropertyType.Simple, schema.SimpleProperties["DomainKey"].PropertyType);
        Assert.Equal(PropertyType.Simple, schema.SimpleProperties["Sequence"].PropertyType);
        Assert.Equal(PropertyType.SimpleCollection, schema.SimpleProperties["Aliases"].PropertyType);
    }
}
