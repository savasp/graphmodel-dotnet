// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Serialization.CodeGen.Tests;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

/// <summary>
/// Truth table for the source generator's <see cref="GraphDataModel"/> type classification,
/// mirroring <c>Cvoya.Graph.Core.Tests.GraphDataModelTypeClassificationTests</c> (the runtime,
/// <see cref="Type"/>-based implementation) and
/// <c>Cvoya.Graph.Analyzers.Tests.AnalyzerHelperTypeClassificationTests</c> (the analyzer) over the
/// same shapes. The three implementations are independent by design (the generator and analyzer
/// target netstandard2.0 and work on <see cref="ITypeSymbol"/>s), so this table is what keeps the
/// named simple types from drifting apart: in particular, <c>Cvoya.Graph.Point</c> is the only
/// supported spatial simple type - <c>System.Drawing.Point</c> must classify as not-simple in every
/// layer (#387).
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
            "decimal" => Special(SpecialType.System_Decimal),
            "string" => String(),
            "object" => Special(SpecialType.System_Object),
            "TestEnum" => Named("TestTypes.TestEnum"),
            "Point" => Point(),
            "Point?" => Nullable(Point()),
            "System.Drawing.Point" => DrawingPoint(),
            "System.Drawing.Point?" => Nullable(DrawingPoint()),
            "DateTime" => Named("System.DateTime"),
            "DateTimeOffset" => Named("System.DateTimeOffset"),
            "TimeSpan" => Named("System.TimeSpan"),
            "TimeOnly" => Named("System.TimeOnly"),
            "DateOnly" => Named("System.DateOnly"),
            "Guid" => Named("System.Guid"),
            "Guid?" => Nullable(Named("System.Guid")),
            "Uri" => Named("System.Uri"),
            "byte[]" => Array(Special(SpecialType.System_Byte)),
            "int[]" => Array(Int32()),
            "string[]" => Array(String()),
            "FlatValueObject" => FlatValueObject(),
            "SimpleStruct" => Named("TestTypes.SimpleStruct"),
            "List<int>" => Named("System.Collections.Generic.List`1", Int32()),
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
        { "Guid?", true },
        { "byte[]", true },
        { "Uri", true },
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
        { "string[]", true },
        { "List<int>", true },
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
}
