// Copyright CVOYA. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Serialization.CodeGen.Tests;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

public class PropertyLabelResolutionTests
{
    public static TheoryData<string, string> LabelCases => new()
    {
        { nameof(RuntimeLabelCases.Default), string.Empty },
        { nameof(RuntimeLabelCases.AttributeWithoutLabel), "[Property]" },
        { nameof(RuntimeLabelCases.Empty), "[Property(Label = \"\")]" },
        { nameof(RuntimeLabelCases.Whitespace), "[Property(Label = \" \" )]" },
        { nameof(RuntimeLabelCases.Custom), "[Property(Label = \"physical_name\")]" },
    };

    [Theory]
    [MemberData(nameof(LabelCases))]
    public void GeneratorPropertyLabelRule_MatchesRuntime(string propertyName, string attribute)
    {
        var trustedAssemblies = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator);
        var source = $$"""
            using Cvoya.Graph;

            public sealed class Subject
            {
                {{attribute}}
                public string {{propertyName}} { get; set; } = string.Empty;
            }
            """;

        var compilation = CSharpCompilation.Create(
            "PropertyLabelResolution",
            [CSharpSyntaxTree.ParseText(source, cancellationToken: TestContext.Current.CancellationToken)],
            [
                .. trustedAssemblies.Select(path =>
                    (MetadataReference)MetadataReference.CreateFromFile(path)),
                MetadataReference.CreateFromFile(typeof(PropertyAttribute).Assembly.Location),
            ],
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var property = Assert.IsAssignableFrom<IPropertySymbol>(
            compilation.GetTypeByMetadataName("Subject")!.GetMembers(propertyName).Single());
        var runtimeProperty = typeof(RuntimeLabelCases).GetProperty(propertyName)!;

        Assert.Equal(Labels.GetLabelFromProperty(runtimeProperty), Utils.GetPropertyName(property));
    }

    private sealed class RuntimeLabelCases
    {
        public string Default { get; set; } = string.Empty;

        [Property]
        public string AttributeWithoutLabel { get; set; } = string.Empty;

        [Property(Label = "")]
        public string Empty { get; set; } = string.Empty;

        [Property(Label = " ")]
        public string Whitespace { get; set; } = string.Empty;

        [Property(Label = "physical_name")]
        public string Custom { get; set; } = string.Empty;
    }
}
