// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Analyzers.Tests;

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;
using Xunit;


using static Cvoya.Graph.Analyzers.Tests.TestHelpers.AnalyzerTestHelpers;

public class GraphAttributeIdentityTests
{
    [Fact]
    public async Task ForeignLookalikeAttributes_ProduceNoGraphAttributeDiagnostics()
    {
        const string source = """
            namespace Foreign
            {
                public sealed class NodeAttribute : System.Attribute
                {
                    public NodeAttribute(string label) { }
                }

                public sealed class RelationshipAttribute : System.Attribute
                {
                    public RelationshipAttribute(string label) { }
                }

                public sealed class PropertyAttribute : System.Attribute
                {
                    public string? Label { get; set; }
                }

                public sealed class ComplexPropertyAttribute : System.Attribute { }
            }

            [Foreign.Node("ForeignNode")]
            [Foreign.Relationship("FOREIGN_RELATIONSHIP")]
            public sealed class PlainType { }

            public sealed record ActualNode : Cvoya.Graph.Node
            {
                [Foreign.Property(Label = "same")]
                public string First { get; set; } = string.Empty;

                [Foreign.Property(Label = "same")]
                [Foreign.ComplexProperty]
                public string Second { get; set; } = string.Empty;
            }
            """;

        await VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task AliasedQualifiedReorderedAndMultilineAttributes_UseSemanticLabels()
    {
        const string source = """
            using NodeAlias = Cvoya.Graph.NodeAttribute;
            using RelationshipAlias = Cvoya.Graph.RelationshipAttribute;
            using PropertyAlias = Cvoya.Graph.PropertyAttribute;

            public sealed record Details
            {
                public string Name { get; set; } = string.Empty;
            }

            [NodeAlias(label: "Person")]
            public sealed record FirstNode : Cvoya.Graph.Node
            {
                [PropertyAlias(IsRequired = true, Label = "name")]
                public string FirstName { get; set; } = string.Empty;

                [Cvoya.Graph.PropertyAttribute(
                    Label = "name",
                    IsRequired = true)]
                public string {|#0:LastName|} { get; set; } = string.Empty;

                [{|#3:Cvoya.Graph.ComplexPropertyAttribute(
                    RelationshipType = "   ")|}]
                public Details Metadata { get; set; } = new();
            }

            [Cvoya.Graph.NodeAttribute(
                Label = "Person")]
            public sealed record {|#1:SecondNode|} : Cvoya.Graph.Node;

            [RelationshipAlias(label: "KNOWS")]
            public sealed record FirstRelationship : Cvoya.Graph.Relationship;

            [Cvoya.Graph.RelationshipAttribute(
                Label = "KNOWS")]
            public sealed record {|#2:SecondRelationship|} : Cvoya.Graph.Relationship;
            """;
        var expected = new[]
        {
            VerifyCG007.Diagnostic("CG007").WithLocation(0)
                .WithArguments("LastName", "FirstNode", "name", "FirstName", "FirstNode"),
            VerifyCG009.Diagnostic("CG009").WithLocation(1)
                .WithArguments("SecondNode", "Person", "FirstNode"),
            VerifyCG008.Diagnostic("CG008").WithLocation(2)
                .WithArguments("SecondRelationship", "KNOWS", "FirstRelationship"),
            VerifyCG015.Diagnostic("CG015").WithLocation(3)
                .WithArguments(
                    "Metadata",
                    "RelationshipType is null, empty, or whitespace, so convention-based naming is used"),
        };

        await VerifyAnalyzerAsync(source, expected);
    }

    [Fact]
    public async Task DerivedGraphAttributes_RetainGraphAttributeBehavior()
    {
        const string source = """
            public sealed class DerivedNodeAttribute(string label) : Cvoya.Graph.NodeAttribute(label);
            public sealed class DerivedRelationshipAttribute(string label) : Cvoya.Graph.RelationshipAttribute(label);
            public sealed class DerivedPropertyAttribute : Cvoya.Graph.PropertyAttribute;

            [DerivedNode("Person")]
            public sealed record FirstNode : Cvoya.Graph.Node
            {
                [DerivedProperty(Label = "name")]
                public string FirstName { get; set; } = string.Empty;

                [DerivedProperty(Label = "name")]
                public string {|#0:LastName|} { get; set; } = string.Empty;
            }

            [DerivedNode("Person")]
            public sealed record {|#1:SecondNode|} : Cvoya.Graph.Node;

            [DerivedRelationship("KNOWS")]
            public sealed record FirstRelationship : Cvoya.Graph.Relationship;

            [DerivedRelationship("KNOWS")]
            public sealed record {|#2:SecondRelationship|} : Cvoya.Graph.Relationship;
            """;
        var expected = new[]
        {
            VerifyCG007.Diagnostic("CG007").WithLocation(0)
                .WithArguments("LastName", "FirstNode", "name", "FirstName", "FirstNode"),
            VerifyCG009.Diagnostic("CG009").WithLocation(1)
                .WithArguments("SecondNode", "Person", "FirstNode"),
            VerifyCG008.Diagnostic("CG008").WithLocation(2)
                .WithArguments("SecondRelationship", "KNOWS", "FirstRelationship"),
        };

        await VerifyAnalyzerAsync(source, expected);
    }

    [Fact]
    public async Task ConcurrentAnalysisWithoutGraphReference_DoesNotThrow()
    {
        const string source = """
            public sealed class PlainType
            {
                public string Name { get; set; } = string.Empty;
            }
            """;
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path));
        var cancellationToken = TestContext.Current.CancellationToken;
        var compilation = CSharpCompilation.Create(
            assemblyName: "Consumer.WithoutGraphReference",
            syntaxTrees: [CSharpSyntaxTree.ParseText(source, cancellationToken: cancellationToken)],
            references: references,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var analyzerOptions = new AnalyzerOptions(ImmutableArray<AdditionalText>.Empty);
        var executionOptions = new CompilationWithAnalyzersOptions(
            analyzerOptions,
            onAnalyzerException: null,
            concurrentAnalysis: true,
            logAnalyzerExecutionTime: false);

        var diagnostics = await compilation
            .WithAnalyzers([new GraphAnalyzer()], executionOptions)
            .GetAnalyzerDiagnosticsAsync(cancellationToken);

        if (!diagnostics.IsEmpty)
        {
            throw new InvalidOperationException(string.Join("\n", diagnostics));
        }
    }
}

file static class VerifyCG007
{
    public static DiagnosticResult Diagnostic(string diagnosticId)
        => new(diagnosticId, DiagnosticSeverity.Error);
}

file static class VerifyCG008
{
    public static DiagnosticResult Diagnostic(string diagnosticId)
        => new(diagnosticId, DiagnosticSeverity.Error);
}

file static class VerifyCG009
{
    public static DiagnosticResult Diagnostic(string diagnosticId)
        => new(diagnosticId, DiagnosticSeverity.Error);
}

file static class VerifyCG015
{
    public static DiagnosticResult Diagnostic(string diagnosticId)
        => new(diagnosticId, DiagnosticSeverity.Warning);
}
