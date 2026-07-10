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
    /// Compiles <paramref name="source"/> with the given <paramref name="warningsAsErrors"/>
    /// setting (mirroring the repository's <c>WarningsAsErrors</c> build setting, which is what
    /// turns the <c>[Obsolete]</c> free-floating <c>WithDepth</c>/<c>Direction</c> modifiers into a
    /// hard compile failure for the "misuse that must not compile" fixtures) and returns the
    /// resulting diagnostics.
    /// </summary>
    public static CompilationFixtureResult Compile(string source, bool warningsAsErrors = false)
    {
        var options = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
            .WithNullableContextOptions(NullableContextOptions.Enable);

        if (warningsAsErrors)
        {
            options = options.WithGeneralDiagnosticOption(ReportDiagnostic.Error);
        }

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
