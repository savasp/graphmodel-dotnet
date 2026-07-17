// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Serialization.CodeGen.Tests;

using System.Globalization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

/// <summary>
/// Unit-level coverage for <see cref="DefaultValueFormatter"/> (see #372): the literal emitter must
/// qualify enum members/casts, escape strings/chars, keep integral width and floating suffixes, and
/// stay culture-invariant regardless of the process's current culture.
/// </summary>
public class DefaultValueFormatterTests
{
    private const string Snippet = """
        public enum Priority { Low, Normal, High }
        public enum @class { @default = 1 }

        public class Holder
        {
            public string Str = "";
            public char Ch;
            public int I;
            public long L;
            public uint U;
            public ulong UL;
            public short S;
            public ushort US;
            public byte B;
            public sbyte SB;
            public float F;
            public double D;
            public decimal M;
            public bool Bo;
            public Priority P;
            public @class KeywordEnum;
            public int? NullableInt;
        }
        """;

    [Fact]
    public void IntegralDefaults_KeepWidthAndSignViaSuffixes()
    {
        var type = CompileFieldTypes();

        Assert.Equal("-7", DefaultValueFormatter.Format(-7, type("I")));
        Assert.Equal("5000000000L", DefaultValueFormatter.Format(5000000000L, type("L")));
        Assert.Equal("42U", DefaultValueFormatter.Format(42u, type("U")));
        Assert.Equal("18000000000000000000UL", DefaultValueFormatter.Format(18000000000000000000UL, type("UL")));
        // Narrow integrals are cast so the deserializer's conditional stays their exact type.
        Assert.Equal("(short)5", DefaultValueFormatter.Format((short)5, type("S")));
        Assert.Equal("(ushort)9", DefaultValueFormatter.Format((ushort)9, type("US")));
        Assert.Equal("(byte)3", DefaultValueFormatter.Format((byte)3, type("B")));
        Assert.Equal("(sbyte)-4", DefaultValueFormatter.Format((sbyte)-4, type("SB")));
    }

    [Fact]
    public void StringAndCharDefaults_AreEscaped()
    {
        var type = CompileFieldTypes();

        Assert.Equal("\"a\\\"b\\\\c\"", DefaultValueFormatter.Format("a\"b\\c", type("Str")));
        Assert.Equal("'\\''", DefaultValueFormatter.Format('\'', type("Ch")));
        Assert.Equal("'\\\\'", DefaultValueFormatter.Format('\\', type("Ch")));
    }

    [Fact]
    public void BoolAndNullDefaults_UseLiterals()
    {
        var type = CompileFieldTypes();

        Assert.Equal("true", DefaultValueFormatter.Format(true, type("Bo")));
        Assert.Equal("false", DefaultValueFormatter.Format(false, type("Bo")));
        Assert.Equal("null", DefaultValueFormatter.Format(null, type("Str")));
        // A value-type `= default` (null ExplicitDefaultValue) resolves via a fully qualified
        // default(T); the fully qualified format keeps the C# shorthand for a nullable value type.
        Assert.Equal("default(int?)", DefaultValueFormatter.Format(null, type("NullableInt")));
    }

    [Fact]
    public void EnumDefaults_QualifyNamedMembersAndCastUnnamedValues()
    {
        var type = CompileFieldTypes();

        // High = 2 names a member; 7 maps to no single member and must be an explicit cast.
        Assert.Equal("global::Priority.High", DefaultValueFormatter.Format(2, type("P")));
        Assert.Equal("(global::Priority)(7)", DefaultValueFormatter.Format(7, type("P")));
        Assert.Equal("global::@class.@default", DefaultValueFormatter.Format(1, type("KeywordEnum")));
    }

    [Fact]
    public void FloatingPointDefaults_AreCultureInvariant()
    {
        var type = CompileFieldTypes();
        var originalCulture = CultureInfo.CurrentCulture;
        try
        {
            // A culture whose decimal separator is ',' would corrupt a naive ToString().
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");

            Assert.Equal("1.5F", DefaultValueFormatter.Format(1.5f, type("F")));
            Assert.Equal("-0.25D", DefaultValueFormatter.Format(-0.25d, type("D")));
            Assert.Equal("3.5M", DefaultValueFormatter.Format(3.5m, type("M")));
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    private static Func<string, ITypeSymbol> CompileFieldTypes()
    {
        var compilation = CSharpCompilation.Create(
            assemblyName: "DefaultValueFormatterTests.Fixture",
            syntaxTrees: [CSharpSyntaxTree.ParseText(Snippet)],
            references: [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)],
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var holder = compilation.GetTypeByMetadataName("Holder")
            ?? throw new InvalidOperationException("Holder type not found in fixture compilation.");

        return fieldName => holder.GetMembers(fieldName).OfType<IFieldSymbol>().First().Type;
    }
}
