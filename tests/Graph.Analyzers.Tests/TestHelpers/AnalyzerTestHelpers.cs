// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Analyzers.Tests.TestHelpers;

using Cvoya.Graph;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;


/// <summary>
/// Helper methods and extensions for analyzer tests
/// </summary>
public static class AnalyzerTestHelpers
{
    /// <summary>
    /// Creates a diagnostic result for the specified diagnostic ID
    /// </summary>
    public static DiagnosticResult Diagnostic(string diagnosticId)
        => new DiagnosticResult(diagnosticId, DiagnosticSeverity.Error);

    /// <summary>
    /// Verifies that an analyzer produces the expected diagnostics for the given source code
    /// </summary>
    public static async Task VerifyAnalyzerAsync(string source, params DiagnosticResult[] expected)
    {
        var test = new FilteringAnalyzerTest<GraphModelAnalyzer>
        {
            TestState =
            {
                Sources = { source },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            },
        };

        // Add reference to Graph
        test.TestState.AdditionalReferences.Add(typeof(INode).Assembly);

        test.ExpectedDiagnostics.AddRange(expected);
        await test.RunAsync();
    }
}