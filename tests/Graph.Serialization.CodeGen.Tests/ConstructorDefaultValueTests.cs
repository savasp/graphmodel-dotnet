// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Serialization.CodeGen.Tests;

using System.Collections.Generic;
using System.Globalization;
using Cvoya.Graph.Serialization;

/// <summary>
/// Exercises the optional-constructor-parameter default matrix end to end (see #372): every legal
/// default must emit valid, culture-invariant C# and, when the serialized data omits the argument,
/// deserialize to exactly the declared default. Before the fix, defaults were formatted with
/// <c>object.ToString()</c> and ad-hoc quoting, which broke escaped strings/chars, enum members,
/// and suffixed/culture-sensitive numerics.
/// </summary>
public class ConstructorDefaultValueTests
{
    // One record whose primary constructor spans the supported default matrix: escaped string, a
    // quote and a backslash char, signed/unsigned/wide integrals, float/double/decimal, a named enum
    // member, a cast-only (unnamed) enum value, bool, and a nullable reference default.
    private const string MatrixSource = """
        using Cvoya.Graph;

        namespace Defaults;

        public enum Priority { Low, Normal, High }
        public enum @class { @default = 3 }

        [Node("Widget")]
        public record Widget(
            string Text = "a\"b\\c",
            char Quote = '\'',
            char Backslash = '\\',
            int Count = -7,
            short SmallSigned = -12,
            ushort SmallUnsigned = 60000,
            byte Tiny = 200,
            sbyte TinySigned = -100,
            long Big = 5000000000,
            uint UnsignedCount = 42,
            ulong HugeCount = 18000000000000000000,
            float Ratio = 1.5f,
            double Precise = -0.25,
            decimal Money = 3.5m,
            Priority Level = Priority.High,
            Priority Combined = (Priority)7,
            @class KeywordEnum = @class.@default,
            bool Enabled = true,
            string? Optional = null) : Node;
        """;

    [Fact]
    public void OptionalDefaultMatrix_GeneratesCompilableCode()
    {
        // CompileAndLoadGeneratedAssembly throws on any generated-code compile error, so a clean load
        // is the acceptance criterion "generated consumer compilation reports zero errors".
        var assembly = GeneratorTestHelpers.CompileAndLoadGeneratedAssembly(MatrixSource);
        Assert.NotNull(assembly.GetType("Defaults.Generated.WidgetSerializer", throwOnError: true));
    }

    [Fact]
    public void OmittedOptionalArguments_DeserializeToDeclaredDefaults()
    {
        var assembly = GeneratorTestHelpers.CompileAndLoadGeneratedAssembly(MatrixSource);
        var widgetType = assembly.GetType("Defaults.Widget", throwOnError: true)!;
        var serializer = CreateSerializer(assembly, "Defaults.Generated.WidgetSerializer");

        // An entity with no stored properties forces every constructor argument down the
        // missing-value path, i.e. onto its formatted default expression.
        var entity = new EntityInfo(
            widgetType,
            "Widget",
            new List<string> { "Widget" },
            new Dictionary<string, Property>(),
            new Dictionary<string, Property>());

        var widget = serializer.Deserialize(entity);

        Assert.Equal("a\"b\\c", widgetType.GetProperty("Text")!.GetValue(widget));
        Assert.Equal('\'', widgetType.GetProperty("Quote")!.GetValue(widget));
        Assert.Equal('\\', widgetType.GetProperty("Backslash")!.GetValue(widget));
        Assert.Equal(-7, (int)widgetType.GetProperty("Count")!.GetValue(widget)!);
        Assert.Equal((short)-12, (short)widgetType.GetProperty("SmallSigned")!.GetValue(widget)!);
        Assert.Equal((ushort)60000, (ushort)widgetType.GetProperty("SmallUnsigned")!.GetValue(widget)!);
        Assert.Equal((byte)200, (byte)widgetType.GetProperty("Tiny")!.GetValue(widget)!);
        Assert.Equal((sbyte)-100, (sbyte)widgetType.GetProperty("TinySigned")!.GetValue(widget)!);
        Assert.Equal(5000000000L, (long)widgetType.GetProperty("Big")!.GetValue(widget)!);
        Assert.Equal(42u, (uint)widgetType.GetProperty("UnsignedCount")!.GetValue(widget)!);
        Assert.Equal(18000000000000000000UL, (ulong)widgetType.GetProperty("HugeCount")!.GetValue(widget)!);
        Assert.Equal(1.5f, (float)widgetType.GetProperty("Ratio")!.GetValue(widget)!);
        Assert.Equal(-0.25d, (double)widgetType.GetProperty("Precise")!.GetValue(widget)!);
        Assert.Equal(3.5m, (decimal)widgetType.GetProperty("Money")!.GetValue(widget)!);
        Assert.Equal(2, Convert.ToInt32(widgetType.GetProperty("Level")!.GetValue(widget), CultureInfo.InvariantCulture));
        Assert.Equal(7, Convert.ToInt32(widgetType.GetProperty("Combined")!.GetValue(widget), CultureInfo.InvariantCulture));
        Assert.Equal(3, Convert.ToInt32(widgetType.GetProperty("KeywordEnum")!.GetValue(widget), CultureInfo.InvariantCulture));
        Assert.True((bool)widgetType.GetProperty("Enabled")!.GetValue(widget)!);
        Assert.Null(widgetType.GetProperty("Optional")!.GetValue(widget));
    }

    private static IEntitySerializer CreateSerializer(System.Reflection.Assembly assembly, string serializerTypeName)
    {
        var serializerType = assembly.GetType(serializerTypeName, throwOnError: true)!;
        return Assert.IsAssignableFrom<IEntitySerializer>(Activator.CreateInstance(serializerType));
    }
}
