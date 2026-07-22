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
    private static readonly ReferenceAssemblies CurrentReferenceAssemblies = new("net10.0");

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
        var test = new FilteringAnalyzerTest<GraphAnalyzer>
        {
            TestState =
            {
                Sources = { source },
                ReferenceAssemblies = CurrentReferenceAssemblies,
            },
        };

        // The analyzer-testing package does not yet ship a .NET 10 reference-assembly preset.
        // Use the current test host's trusted platform assemblies so AttributeData binds against
        // the same framework identity as the repository's net10.0 Graph reference.
        var trustedAssemblies = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator);
        foreach (var path in trustedAssemblies.Distinct(StringComparer.Ordinal))
        {
            test.TestState.AdditionalReferences.Add(MetadataReference.CreateFromFile(path));
        }

        var graphAssemblyPath = typeof(INode).Assembly.Location;
        if (!trustedAssemblies.Contains(graphAssemblyPath, StringComparer.Ordinal))
        {
            test.TestState.AdditionalReferences.Add(MetadataReference.CreateFromFile(graphAssemblyPath));
        }

        test.ExpectedDiagnostics.AddRange(expected);
        await test.RunAsync();
    }
}
