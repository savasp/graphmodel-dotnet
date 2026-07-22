// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Serialization.CodeGen.Tests;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

/// <summary>
/// Truth table for the source generator's <see cref="GraphDataModel"/> type classification. Its
/// named-simple-type slice matches
/// <c>Cvoya.Graph.Core.Tests.GraphDataModelTypeClassificationTests</c> (the runtime,
/// <see cref="Type"/>-based implementation) and
/// <c>Cvoya.Graph.Analyzers.Tests.AnalyzerHelperTypeClassificationTests</c> (the analyzer). The three
/// implementations are independent by design (the generator and analyzer target netstandard2.0 and
/// work on <see cref="ITypeSymbol"/>s), so these matching cases keep the named simple types from
/// drifting apart: in particular, <c>Cvoya.Graph.Point</c> is the only supported spatial simple type
/// - <c>System.Drawing.Point</c> must classify as not-simple in every layer (#387).
/// </summary>
public class GraphDataModelTypeClassificationTests
{
    private const string TestSource = """
        namespace TestTypes;

        public enum TestEnum { One }

        public readonly struct SimpleStruct
        {
            public int Value { get; init; }
        }

        public sealed class FlatValueObject
        {
            public string Street { get; init; } = string.Empty;
            public int Unit { get; init; }
        }
        """;

    private static readonly Compilation Compilation = CreateCompilation();

    private static CSharpCompilation CreateCompilation()
    {
        var trustedAssemblies = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator);

        var references = trustedAssemblies
            .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))
            .ToList();

        return CSharpCompilation.Create(
            assemblyName: "CodeGenGraphDataModelTypeClassificationTests",
            syntaxTrees: [CSharpSyntaxTree.ParseText(TestSource)],
            references: references,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    private static CSharpCompilation CreateLookalikeCompilation()
    {
        const string lookalikeSource = """
            namespace System
            {
                public readonly struct DateTime { }
                public readonly struct DateTimeOffset { }
                public readonly struct TimeSpan { }
                public readonly struct TimeOnly { }
                public readonly struct DateOnly { }
                public readonly struct Guid { }
                public sealed class Uri { }
            }

            namespace Cvoya.Graph
            {
                public readonly struct Point { }
            }
            """;
        var trustedAssemblies = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator);
        var references = trustedAssemblies
            .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path));

        return CSharpCompilation.Create(
            assemblyName: "Consumer.NamedSimpleLookalikes",
            syntaxTrees: [CSharpSyntaxTree.ParseText(lookalikeSource)],
            references: references,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    /// <summary>
    /// Resolves an <see cref="ITypeSymbol"/> for one of the truth-table shapes by the same short
    /// symbolic names used in the runtime and analyzer mirrors of this table.
    /// </summary>
    private static ITypeSymbol Resolve(string shape)
    {
        var compilation = Compilation;

        ITypeSymbol Named(string metadataName, params ITypeSymbol[] typeArguments)
        {
            var type = compilation.GetTypeByMetadataName(metadataName)
                ?? throw new InvalidOperationException($"Could not resolve type '{metadataName}'.");
            return typeArguments.Length == 0 ? type : type.Construct(typeArguments);
        }

        ITypeSymbol Special(SpecialType specialType) => compilation.GetSpecialType(specialType);

        ITypeSymbol Array(ITypeSymbol element) => compilation.CreateArrayTypeSymbol(element);

        ITypeSymbol Nullable(ITypeSymbol valueType) => Named("System.Nullable`1", valueType);

        ITypeSymbol String() => Special(SpecialType.System_String);
        ITypeSymbol Int32() => Special(SpecialType.System_Int32);
        ITypeSymbol Point() => Named("Cvoya.Graph.Point");
        ITypeSymbol DrawingPoint() => Named("System.Drawing.Point");
        ITypeSymbol FlatValueObject() => Named("TestTypes.FlatValueObject");

        return shape switch
        {
            "bool" => Special(SpecialType.System_Boolean),
            "int" => Int32(),
            "int?" => Nullable(Int32()),
            "nint" => Named("System.IntPtr"),
            "nuint" => Named("System.UIntPtr"),
            "nint?" => Nullable(Named("System.IntPtr")),
            "nuint?" => Nullable(Named("System.UIntPtr")),
            "decimal" => Special(SpecialType.System_Decimal),
            "string" => String(),
            "object" => Special(SpecialType.System_Object),
            "TestEnum" => Named("TestTypes.TestEnum"),
            "Point" => Point(),
            "Point?" => Nullable(Point()),
            "System.Drawing.Point" => DrawingPoint(),
            "System.Drawing.Point?" => Nullable(DrawingPoint()),
            "DateTime" => Named("System.DateTime"),
            "DateTime?" => Nullable(Named("System.DateTime")),
            "DateTimeOffset" => Named("System.DateTimeOffset"),
            "DateTimeOffset?" => Nullable(Named("System.DateTimeOffset")),
            "TimeSpan" => Named("System.TimeSpan"),
            "TimeSpan?" => Nullable(Named("System.TimeSpan")),
            "TimeOnly" => Named("System.TimeOnly"),
            "TimeOnly?" => Nullable(Named("System.TimeOnly")),
            "DateOnly" => Named("System.DateOnly"),
            "DateOnly?" => Nullable(Named("System.DateOnly")),
            "Guid" => Named("System.Guid"),
            "Guid?" => Nullable(Named("System.Guid")),
            "Uri" => Named("System.Uri"),
            "byte[]" => Array(Special(SpecialType.System_Byte)),
            "int[]" => Array(Int32()),
            "nint[]" => Array(Named("System.IntPtr")),
            "string[]" => Array(String()),
            "FlatValueObject" => FlatValueObject(),
            "SimpleStruct" => Named("TestTypes.SimpleStruct"),
            "List<int>" => Named("System.Collections.Generic.List`1", Int32()),
            "List<nuint?>" => Named("System.Collections.Generic.List`1", Nullable(Named("System.UIntPtr"))),
            "List<Point>" => Named("System.Collections.Generic.List`1", Point()),
            "List<System.Drawing.Point>" => Named("System.Collections.Generic.List`1", DrawingPoint()),
            "List<FlatValueObject>" => Named("System.Collections.Generic.List`1", FlatValueObject()),
            "Dictionary<string,int>" => Named("System.Collections.Generic.Dictionary`2", String(), Int32()),
            _ => throw new ArgumentOutOfRangeException(nameof(shape), shape, "Unknown test shape."),
        };
    }

    public static TheoryData<string, bool> SimpleTypeCases => new()
    {
        { "bool", true },
        { "int", true },
        { "int?", true },
        { "nint", false },
        { "nuint", false },
        { "nint?", false },
        { "nuint?", false },
        { "decimal", true },
        { "TestEnum", true },
        { "string", true },
        { "Point", true },
        { "Point?", true },
        { "System.Drawing.Point", false },
        { "System.Drawing.Point?", false },
        { "DateTime", true },
        { "DateTimeOffset", true },
        { "TimeSpan", true },
        { "TimeOnly", true },
        { "DateOnly", true },
        { "Guid", true },
        { "Uri", true },
        { "DateTime?", true },
        { "DateTimeOffset?", true },
        { "TimeSpan?", true },
        { "TimeOnly?", true },
        { "DateOnly?", true },
        { "Guid?", true },
        { "byte[]", true },
        { "object", false },
        { "FlatValueObject", false },
        { "SimpleStruct", false },
        { "int[]", false },
        { "string[]", false },
        { "List<int>", false },
        { "List<FlatValueObject>", false },
        { "Dictionary<string,int>", false },
    };

    public static TheoryData<string, bool> CollectionOfSimpleCases => new()
    {
        { "string", false },
        { "byte[]", true },
        { "int[]", true },
        { "nint[]", false },
        { "string[]", true },
        { "List<int>", true },
        { "List<nuint?>", false },
        { "List<Point>", true },
        { "List<System.Drawing.Point>", false },
        { "List<FlatValueObject>", false },
    };

    [Theory]
    [MemberData(nameof(SimpleTypeCases))]
    public void IsSimple_ReturnsExpectedResult(string shape, bool expected)
    {
        Assert.Equal(expected, GraphDataModel.IsSimple(Resolve(shape)));
    }

    [Theory]
    [MemberData(nameof(CollectionOfSimpleCases))]
    public void IsCollectionOfSimple_ReturnsExpectedResult(string shape, bool expected)
    {
        Assert.Equal(expected, GraphDataModel.IsCollectionOfSimple(Resolve(shape)));
    }

    [Theory]
    [InlineData("System.DateTime")]
    [InlineData("System.DateTimeOffset")]
    [InlineData("System.TimeSpan")]
    [InlineData("System.TimeOnly")]
    [InlineData("System.DateOnly")]
    [InlineData("System.Guid")]
    [InlineData("System.Uri")]
    [InlineData("Cvoya.Graph.Point")]
    public void SourceDefinedNamedSimpleLookalike_IsNotSimple(string metadataName)
    {
        var compilation = CreateLookalikeCompilation();
        var type = compilation.GetTypeByMetadataName(metadataName)
            ?? throw new InvalidOperationException($"Could not resolve source type '{metadataName}'.");

        Assert.False(GraphDataModel.IsSimple(type));
        Assert.True(GraphDataModel.IsUnsupportedFrameworkType(type));
    }
}
