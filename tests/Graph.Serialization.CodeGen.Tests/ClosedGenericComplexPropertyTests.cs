// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Serialization.CodeGen.Tests;

using Cvoya.Graph.Serialization;

/// <summary>
/// Verifies distinct closed constructions of one generic complex-property type receive distinct
/// generated serializer names and registrations (see #415).
/// </summary>
public class ClosedGenericComplexPropertyTests
{
    [Fact]
    public void DistinctClosedConstructions_GenerateAndRoundTrip()
    {
        const string source = """
            using Cvoya.Graph;

            namespace ClosedGenericComplex;

            public record Box<T>
            {
                public T Value { get; set; } = default!;
            }

            [Node("Container")]
            public record Container : Node
            {
                public Box<int> Number { get; set; } = new();
                public Box<string> Text { get; set; } = new();
            }
            """;
        var assembly = GeneratorTestHelpers.CompileAndLoadGeneratedAssembly(source);
        var genericBoxType = assembly.GetType("ClosedGenericComplex.Box`1", throwOnError: true)!;
        var numberBoxType = genericBoxType.MakeGenericType(typeof(int));
        var textBoxType = genericBoxType.MakeGenericType(typeof(string));
        var numberBox = Activator.CreateInstance(numberBoxType)!;
        var textBox = Activator.CreateInstance(textBoxType)!;
        numberBoxType.GetProperty("Value")!.SetValue(numberBox, 42);
        textBoxType.GetProperty("Value")!.SetValue(textBox, "hello");

        var containerType = assembly.GetType("ClosedGenericComplex.Container", throwOnError: true)!;
        var container = Activator.CreateInstance(containerType)!;
        containerType.GetProperty("Number")!.SetValue(container, numberBox);
        containerType.GetProperty("Text")!.SetValue(container, textBox);
        var serializer = CreateSerializer(assembly, "ClosedGenericComplex.Generated.ContainerSerializer");

        var roundTripped = serializer.Deserialize(serializer.Serialize(container));

        var roundTrippedNumber = containerType.GetProperty("Number")!.GetValue(roundTripped)!;
        var roundTrippedText = containerType.GetProperty("Text")!.GetValue(roundTripped)!;
        Assert.Equal(42, numberBoxType.GetProperty("Value")!.GetValue(roundTrippedNumber));
        Assert.Equal("hello", textBoxType.GetProperty("Value")!.GetValue(roundTrippedText));
    }

    private static IEntitySerializer CreateSerializer(System.Reflection.Assembly assembly, string serializerTypeName)
    {
        var serializerType = assembly.GetType(serializerTypeName, throwOnError: true)!;
        return Assert.IsAssignableFrom<IEntitySerializer>(Activator.CreateInstance(serializerType));
    }
}
