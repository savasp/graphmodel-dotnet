// Copyright CVOYA. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.
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

namespace Cvoya.Graph.Analyzers.Tests;

using Microsoft.CodeAnalysis;
using Xunit;


/// <summary>
/// Mirrors <c>Cvoya.Graph.Core.Tests.GraphDataModelTypeClassificationTests</c> (the runtime,
/// <see cref="Type"/>-based truth table) with the same shapes resolved as <see cref="ITypeSymbol"/>s,
/// asserted against <see cref="AnalyzerHelper"/>. The two classification implementations are
/// independent (the analyzer targets netstandard2.0 and works on <see cref="ITypeSymbol"/>, so it
/// cannot call the runtime <c>GraphDataModel</c> directly) - this test class exists so they cannot
/// silently drift apart.
/// </summary>
public class AnalyzerHelperTypeClassificationTests
{
    private const string TestSource = """
        using System;
        using System.Collections;
        using System.Collections.Generic;
        using System.Collections.ObjectModel;

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

        public sealed class RecursiveValueObject
        {
            public RecursiveValueObject? Next { get; set; }
        }
        """;

    private static readonly Compilation Compilation = CreateCompilation();

    private static readonly AnalyzerHelper Helper = new(Compilation);

    private static Compilation CreateCompilation()
    {
        var trustedAssemblies = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator);

        var references = trustedAssemblies
            .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))
            .ToList();

        return Microsoft.CodeAnalysis.CSharp.CSharpCompilation.Create(
            assemblyName: "AnalyzerHelperTypeClassificationTests",
            syntaxTrees: [Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree.ParseText(TestSource)],
            references: references,
            options: new Microsoft.CodeAnalysis.CSharp.CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    /// <summary>
    /// Resolves an <see cref="ITypeSymbol"/> for one of the truth-table shapes below by a short
    /// symbolic name. Kept as a switch (rather than reflection-driven Type-to-symbol mapping) so
    /// each case is explicit about how the symbol is constructed (arrays, generics, nullable value
    /// types all need different Roslyn APIs than their runtime <see cref="Type"/> equivalents).
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

        ITypeSymbol Nullable(ITypeSymbol valueType) =>
            Named("System.Nullable`1", valueType);

        ITypeSymbol String() => Special(SpecialType.System_String);
        ITypeSymbol Int32() => Special(SpecialType.System_Int32);
        ITypeSymbol Object() => Special(SpecialType.System_Object);
        ITypeSymbol TestEnum() => Named("TestTypes.TestEnum");
        ITypeSymbol Point() => Named("Cvoya.Graph.Point");
        ITypeSymbol FlatValueObject() => Named("TestTypes.FlatValueObject");
        ITypeSymbol RecursiveValueObject() => Named("TestTypes.RecursiveValueObject");
        ITypeSymbol SimpleStruct() => Named("TestTypes.SimpleStruct");

        return shape switch
        {
            "bool" => Special(SpecialType.System_Boolean),
            "int" => Int32(),
            "int?" => Nullable(Int32()),
            "decimal" => Special(SpecialType.System_Decimal),
            "string" => String(),
            "object" => Object(),
            "Guid" => Named("System.Guid"),
            "DateTime" => Named("System.DateTime"),
            "Uri" => Named("System.Uri"),
            "Point" => Point(),
            "byte[]" => Array(Special(SpecialType.System_Byte)),
            "int[]" => Array(Int32()),
            "string[]" => Array(String()),
            "object[]" => Array(Object()),
            "TestEnum" => TestEnum(),
            "FlatValueObject" => FlatValueObject(),
            "FlatValueObject[]" => Array(FlatValueObject()),
            "RecursiveValueObject" => RecursiveValueObject(),
            "SimpleStruct" => SimpleStruct(),
            "List<int>" => Named("System.Collections.Generic.List`1", Int32()),
            "List<string>" => Named("System.Collections.Generic.List`1", String()),
            "List<object>" => Named("System.Collections.Generic.List`1", Object()),
            "List<FlatValueObject>" => Named("System.Collections.Generic.List`1", FlatValueObject()),
            "List<RecursiveValueObject>" => Named("System.Collections.Generic.List`1", RecursiveValueObject()),
            "List<SimpleStruct>" => Named("System.Collections.Generic.List`1", SimpleStruct()),
            "List<List<int>>" => Named("System.Collections.Generic.List`1", Named("System.Collections.Generic.List`1", Int32())),
            "List<List<FlatValueObject>>" => Named("System.Collections.Generic.List`1", Named("System.Collections.Generic.List`1", FlatValueObject())),
            "IReadOnlyList<FlatValueObject>" => Named("System.Collections.Generic.IReadOnlyList`1", FlatValueObject()),
            "ArrayList" => Named("System.Collections.ArrayList"),
            "Dictionary<string,int>" => Named("System.Collections.Generic.Dictionary`2", String(), Int32()),
            "Dictionary<string,FlatValueObject>" => Named("System.Collections.Generic.Dictionary`2", String(), FlatValueObject()),
            "IDictionary<string,int>" => Named("System.Collections.Generic.IDictionary`2", String(), Int32()),
            "IDictionary<string,FlatValueObject>" => Named("System.Collections.Generic.IDictionary`2", String(), FlatValueObject()),
            "IReadOnlyDictionary<string,int>" => Named("System.Collections.Generic.IReadOnlyDictionary`2", String(), Int32()),
            "IReadOnlyDictionary<string,FlatValueObject>" => Named("System.Collections.Generic.IReadOnlyDictionary`2", String(), FlatValueObject()),
            "ReadOnlyDictionary<string,int>" => Named("System.Collections.ObjectModel.ReadOnlyDictionary`2", String(), Int32()),
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
        { "DateTime", true },
        { "Guid", true },
        { "byte[]", true },
        { "Uri", true },
        { "object", false },
        { "FlatValueObject", false },
        { "RecursiveValueObject", false },
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
        { "List<string>", true },
        { "ArrayList", false },
        { "List<object>", false },
        { "List<List<int>>", false },
        { "List<FlatValueObject>", false },
        { "Dictionary<string,int>", false },
        { "IDictionary<string,int>", false },
        { "IReadOnlyDictionary<string,int>", false },
        { "Dictionary<string,FlatValueObject>", false },
    };

    public static TheoryData<string, bool> CollectionOfComplexCases => new()
    {
        { "string", false },
        { "List<int>", false },
        { "List<string>", false },
        { "int[]", false },
        { "List<object>", false },
        { "object[]", false },
        { "FlatValueObject[]", true },
        { "List<FlatValueObject>", true },
        { "IReadOnlyList<FlatValueObject>", true },
        { "List<RecursiveValueObject>", true },
        { "List<SimpleStruct>", true },
        { "List<List<FlatValueObject>>", false },
        { "Dictionary<string,FlatValueObject>", false },
        { "IDictionary<string,FlatValueObject>", false },
        { "IReadOnlyDictionary<string,FlatValueObject>", false },
    };

    public static TheoryData<string, bool> ComplexCases => new()
    {
        { "string", false },
        { "int", false },
        { "int?", false },
        { "Point", false },
        { "Uri", false },
        { "byte[]", false },
        { "List<int>", false },
        { "List<FlatValueObject>", false },
        { "Dictionary<string,int>", false },
        { "IDictionary<string,int>", false },
        { "IReadOnlyDictionary<string,int>", false },
        { "object", false },
        { "TestEnum", false },
        { "FlatValueObject", true },
        { "RecursiveValueObject", true },
        { "SimpleStruct", true },
    };

    public static TheoryData<string, bool> DictionaryCases => new()
    {
        { "Dictionary<string,int>", true },
        { "IDictionary<string,int>", true },
        { "IReadOnlyDictionary<string,int>", true },
        { "Dictionary<string,FlatValueObject>", true },
        { "ReadOnlyDictionary<string,int>", true },
        { "List<int>", false },
        { "int[]", false },
        { "string", false },
        { "FlatValueObject", false },
    };

    [Theory]
    [MemberData(nameof(SimpleTypeCases))]
    public void IsSimpleType_ReturnsExpectedResult(string shape, bool expected)
    {
        AssertEqual(expected, Helper.IsSimpleType(Resolve(shape)), shape);
    }

    [Theory]
    [MemberData(nameof(CollectionOfSimpleCases))]
    public void IsCollectionOfSimpleTypes_ReturnsExpectedResult(string shape, bool expected)
    {
        AssertEqual(expected, Helper.IsCollectionOfSimpleTypes(Resolve(shape)), shape);
    }

    [Theory]
    [MemberData(nameof(CollectionOfComplexCases))]
    public void IsCollectionOfComplexTypes_ReturnsExpectedResult(string shape, bool expected)
    {
        AssertEqual(expected, Helper.IsCollectionOfComplexTypes(Resolve(shape)), shape);
    }

    [Theory]
    [MemberData(nameof(ComplexCases))]
    public void IsComplexType_ReturnsExpectedResult(string shape, bool expected)
    {
        AssertEqual(expected, Helper.IsComplexType(Resolve(shape)), shape);
    }

    [Theory]
    [MemberData(nameof(DictionaryCases))]
    public void IsDictionaryType_ReturnsExpectedResult(string shape, bool expected)
    {
        AssertEqual(expected, Helper.IsDictionaryType(Resolve(shape)), shape);
    }

    /// <summary>
    /// The test project references both xunit.v3 and (transitively, via
    /// Microsoft.CodeAnalysis.CSharp.Analyzer.Testing.XUnit) xunit v2, which both define
    /// <c>Xunit.Assert</c> - an unaliasable ambiguous-type collision (CS0433). Every other test
    /// class in this project sidesteps it by only ever calling into
    /// <c>Microsoft.CodeAnalysis.Testing</c> APIs; this is the first class needing plain value
    /// assertions, so it avoids the collision with its own minimal helper instead.
    /// </summary>
    private static void AssertEqual(bool expected, bool actual, string shape)
    {
        if (expected != actual)
        {
            throw new InvalidOperationException($"Expected '{shape}' to classify as {expected} but got {actual}.");
        }
    }
}
