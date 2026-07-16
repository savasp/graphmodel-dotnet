// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.QuerySurface.CompilationTests;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

/// <summary>
/// Compiles a snippet of C# source against the real <c>Cvoya.Graph</c> assembly (plus the
/// BCL) using a plain <see cref="CSharpCompilation"/> - the "Roslyn CSharpCompilation" technique
/// named in issue #94's testing requirements for "chains that MUST compile" /
/// "misuse that MUST NOT compile" fixtures.
/// </summary>
internal static class CompilationFixture
{
    private static readonly Lazy<MetadataReference[]> References = new(BuildReferences);

    /// <summary>
    /// Compiles <paramref name="source"/> and returns the resulting diagnostics.
    /// </summary>
    public static CompilationFixtureResult Compile(string source)
    {
        var options = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
            .WithNullableContextOptions(NullableContextOptions.Enable);

        var syntaxTree = CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Latest));

        var compilation = CSharpCompilation.Create(
            assemblyName: $"CompilationFixture_{Guid.NewGuid():N}",
            syntaxTrees: [syntaxTree],
            references: References.Value,
            options: options);

        var diagnostics = compilation.GetDiagnostics();
        return new CompilationFixtureResult(diagnostics);
    }

    private static MetadataReference[] BuildReferences()
    {
        // Reference the BCL via the currently-running runtime's trusted platform assemblies
        // (avoids taking a dependency on a reference-assembly package purely for this).
        var tpaPaths = (AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string)
            ?.Split(Path.PathSeparator)
            ?? throw new InvalidOperationException("TRUSTED_PLATFORM_ASSEMBLIES was not available.");

        var bclReferences = tpaPaths
            .Where(File.Exists)
            .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path));

        // Reference the real Cvoya.Graph assembly under test.
        var graphModelReference = MetadataReference.CreateFromFile(typeof(IGraph).Assembly.Location);

        return [.. bclReferences, graphModelReference];
    }
}

/// <summary>
/// Thin wrapper over a diagnostics collection with the assertions the fixtures need.
/// </summary>
internal sealed class CompilationFixtureResult(IEnumerable<Diagnostic> diagnostics)
{
    private readonly List<Diagnostic> _diagnostics = [.. diagnostics];

    public bool HasErrors => _diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error);

    public IReadOnlyList<Diagnostic> Errors => _diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();

    public string DescribeErrors() => Errors.Count == 0
        ? "(no errors)"
        : string.Join(Environment.NewLine, Errors.Select(e => e.ToString()));
}
