// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Analyzers.Tests;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using Xunit;


using static Cvoya.Graph.Analyzers.Tests.TestHelpers.AnalyzerTestHelpers;

public class NamedSimpleTypeIdentityAnalyzerTests
{
    [Fact]
    public async Task SourceDefinedNamedSimpleLookalikes_UseUnsupportedPropertyDiagnostics()
    {
        const string source = """
            namespace System
            {
                public readonly struct Guid { }
            }

            namespace Cvoya.Graph
            {
                public readonly struct Point { }
            }

            namespace Consumer
            {
                [Cvoya.Graph.Node("InvalidNode")]
                public sealed record InvalidNode : Cvoya.Graph.Node
                {
                    public System.Guid {|#0:TrackingId|} { get; set; }
                    public Cvoya.Graph.Point {|#1:Location|} { get; set; }
                }

                [Cvoya.Graph.Relationship("INVALID_RELATIONSHIP")]
                public sealed record InvalidRelationship : Cvoya.Graph.Relationship
                {
                    public System.Guid {|#2:TrackingId|} { get; set; }
                    public Cvoya.Graph.Point {|#3:Location|} { get; set; }
                }
            }
            """;
        var expected = new[]
        {
            VerifyCG004.Diagnostic("CG004").WithLocation(0)
                .WithArguments("TrackingId", "InvalidNode", "Guid"),
            VerifyCG004.Diagnostic("CG004").WithLocation(1)
                .WithArguments("Location", "InvalidNode", "Point"),
            VerifyCG005.Diagnostic("CG005").WithLocation(2)
                .WithArguments("TrackingId", "InvalidRelationship", "Guid"),
            VerifyCG005.Diagnostic("CG005").WithLocation(3)
                .WithArguments("Location", "InvalidRelationship", "Point"),
        };

        await VerifyAnalyzerAsync(source, expected);
    }
}

file static class VerifyCG004
{
    public static DiagnosticResult Diagnostic(string diagnosticId)
        => new(diagnosticId, DiagnosticSeverity.Error);
}

file static class VerifyCG005
{
    public static DiagnosticResult Diagnostic(string diagnosticId)
        => new(diagnosticId, DiagnosticSeverity.Error);
}
