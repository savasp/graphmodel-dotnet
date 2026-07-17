// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Serialization.CodeGen.Tests;

using System.Collections.Immutable;
using System.Globalization;
using System.Runtime.CompilerServices;
using Cvoya.Graph.Serialization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

/// <summary>
/// Drives <c>EntitySerializerGenerator</c> directly via <see cref="CSharpGeneratorDriver"/>
/// against a compilation of the given source plus a reference to <c>Cvoya.Graph</c>
/// (needed for <c>INode</c>/<c>IRelationship</c>/attributes), and renders the generated files
/// (sorted by hint name, with diagnostics) as a single snapshot-friendly string.
/// </summary>
internal static class GeneratorTestHelpers
{
    private static readonly Lazy<ImmutableArray<MetadataReference>> References = new(BuildReferences);

    public static string RunGenerator(string source, [CallerMemberName] string testName = "")
    {
        var compilation = CreateCompilation(source, testName);

        var generator = new EntitySerializerGenerator();
        var driver = CSharpGeneratorDriver.Create(generator.AsSourceGenerator());

        driver = (CSharpGeneratorDriver)driver.RunGeneratorsAndUpdateCompilation(
            compilation,
            out var outputCompilation,
            out var generatorDiagnostics);

        var runResult = driver.GetRunResult();

        return Render(runResult, generatorDiagnostics, outputCompilation);
    }

    public static System.Reflection.Assembly CompileAndLoadGeneratedAssembly(
        string source,
        [CallerMemberName] string testName = "")
    {
        var compilation = CreateCompilation(source, testName);
        var driver = CSharpGeneratorDriver.Create(new EntitySerializerGenerator().AsSourceGenerator());
        driver.RunGeneratorsAndUpdateCompilation(
            compilation,
            out var outputCompilation,
            out var generatorDiagnostics);

        var errors = generatorDiagnostics
            .Concat(outputCompilation.GetDiagnostics())
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ToArray();
        if (errors.Length > 0)
        {
            throw new InvalidOperationException(string.Join("\n", errors.Select(error => error.ToString())));
        }

        using var assemblyStream = new MemoryStream();
        var emitResult = outputCompilation.Emit(assemblyStream);
        if (!emitResult.Success)
        {
            throw new InvalidOperationException(string.Join(
                "\n",
                emitResult.Diagnostics.Select(diagnostic => diagnostic.ToString())));
        }

        assemblyStream.Position = 0;
        return System.Runtime.Loader.AssemblyLoadContext.Default.LoadFromStream(assemblyStream);
    }

    public static IReadOnlyCollection<IncrementalStepRunReason> GetSecondRunReasons(
        string source,
        string trackingName,
        [CallerMemberName] string testName = "")
    {
        var compilation = CreateCompilation(source, testName);

        var driver = CreateTrackingDriver();

        driver = driver.RunGenerators(compilation);
        driver = driver.RunGenerators(compilation);

        return GetStepReasons(driver, trackingName);
    }

    public static IReadOnlyDictionary<string, IReadOnlyCollection<IncrementalStepRunReason>> GetSecondRunReasonsByTrackingName(
        string source,
        [CallerMemberName] string testName = "")
    {
        var compilation = CreateCompilation(source, testName);

        var driver = CreateTrackingDriver();

        driver = driver.RunGenerators(compilation);
        driver = driver.RunGenerators(compilation);

        return GetAllStepReasons(driver);
    }

    /// <summary>
    /// Runs the generator once against <paramref name="source"/> plus an unrelated,
    /// non-entity source file, then re-runs it after applying a whitespace-only edit to that
    /// unrelated file (leaving <paramref name="source"/> untouched). Returns the step outputs
    /// for <paramref name="trackingName"/> from the second run, so callers can assert that an
    /// edit to unrelated source doesn't invalidate the tracked step (i.e. it reports
    /// <see cref="IncrementalStepRunReason.Cached"/> or <see cref="IncrementalStepRunReason.Unchanged"/>,
    /// never <see cref="IncrementalStepRunReason.New"/> or <see cref="IncrementalStepRunReason.Modified"/>).
    /// </summary>
    public static IReadOnlyCollection<IncrementalStepRunReason> GetUnrelatedEditReasons(
        string source,
        string trackingName,
        [CallerMemberName] string testName = "")
    {
        var (_, driver) = RunBeforeAndAfterUnrelatedEdit(source, testName);

        return GetStepReasons(driver, trackingName);
    }

    public static IReadOnlyDictionary<string, IReadOnlyCollection<IncrementalStepRunReason>> GetUnrelatedEditReasonsByTrackingName(
        string source,
        [CallerMemberName] string testName = "")
    {
        var (_, driver) = RunBeforeAndAfterUnrelatedEdit(source, testName);

        return GetAllStepReasons(driver);
    }

    public static IReadOnlyDictionary<string, IReadOnlyCollection<IncrementalStepRunReason>> GetUnrelatedNonEntityTypeAdditionReasonsByTrackingName(
        string source,
        [CallerMemberName] string testName = "")
    {
        var (_, driver) = RunBeforeAndAfterAddingUnrelatedNonEntityType(source, testName);

        return GetAllStepReasons(driver);
    }

    /// <summary>
    /// Runs the generator against <paramref name="source"/> plus an unrelated, non-entity source
    /// file, then re-runs it after applying a whitespace-only edit to that unrelated file, and
    /// returns the rendered generated-source text (see <see cref="Render"/>) from both runs so
    /// callers can assert they are byte-identical.
    /// </summary>
    public static (string Before, string After) GetGeneratedSourceBeforeAndAfterUnrelatedEdit(
        string source,
        [CallerMemberName] string testName = "")
    {
        var (beforeResult, driver) = RunBeforeAndAfterUnrelatedEdit(source, testName);
        var afterResult = driver.GetRunResult();

        var before = RenderGeneratedSourcesOnly(beforeResult);
        var after = RenderGeneratedSourcesOnly(afterResult);

        return (before, after);
    }

    public static IReadOnlyDictionary<string, IReadOnlyCollection<IncrementalStepRunReason>> GetRelevantEditReasonsByTrackingName(
        string source,
        string editedSource,
        [CallerMemberName] string testName = "")
    {
        var (_, driver) = RunBeforeAndAfterRelevantEdit(source, editedSource, testName);

        return GetAllStepReasons(driver);
    }

    public static (string Before, string After) GetGeneratedSourceBeforeAndAfterRelevantEdit(
        string source,
        string editedSource,
        [CallerMemberName] string testName = "")
    {
        var (beforeResult, driver) = RunBeforeAndAfterRelevantEdit(source, editedSource, testName);
        var afterResult = driver.GetRunResult();

        var before = RenderGeneratedSourcesOnly(beforeResult);
        var after = RenderGeneratedSourcesOnly(afterResult);

        return (before, after);
    }

    private static (GeneratorDriverRunResult FirstRunResult, GeneratorDriver Driver) RunBeforeAndAfterUnrelatedEdit(
        string source,
        string testName)
    {
        const string unrelatedSource = """
            namespace TestNamespace;

            public static class Unrelated
            {
                public static int Value => 42;
            }
            """;

        var unrelatedTree = CSharpSyntaxTree.ParseText(unrelatedSource, path: "Unrelated.cs");
        var compilation = CreateCompilation(source, testName).AddSyntaxTrees(unrelatedTree);

        var driver = CreateTrackingDriver();
        driver = driver.RunGenerators(compilation);
        var firstRunResult = driver.GetRunResult();

        // Apply a whitespace-only edit to the unrelated file - the entity source is untouched.
        var editedUnrelatedTree = CSharpSyntaxTree.ParseText(unrelatedSource + "\n", path: "Unrelated.cs");
        var editedCompilation = compilation.ReplaceSyntaxTree(unrelatedTree, editedUnrelatedTree);

        driver = driver.RunGenerators(editedCompilation);

        return (firstRunResult, driver);
    }

    private static (GeneratorDriverRunResult FirstRunResult, GeneratorDriver Driver) RunBeforeAndAfterAddingUnrelatedNonEntityType(
        string source,
        string testName)
    {
        const string unrelatedSource = """
            namespace TestNamespace;

            public class NonEntityDescription
            {
                public string Name { get; set; } = string.Empty;
            }
            """;

        var compilation = CreateCompilation(source, testName);

        var driver = CreateTrackingDriver();
        driver = driver.RunGenerators(compilation);
        var firstRunResult = driver.GetRunResult();

        var unrelatedTree = CSharpSyntaxTree.ParseText(unrelatedSource, path: "NonEntityDescription.cs");
        var editedCompilation = compilation.AddSyntaxTrees(unrelatedTree);

        driver = driver.RunGenerators(editedCompilation);

        return (firstRunResult, driver);
    }

    private static (GeneratorDriverRunResult FirstRunResult, GeneratorDriver Driver) RunBeforeAndAfterRelevantEdit(
        string source,
        string editedSource,
        string testName)
    {
        var compilation = CreateCompilation(source, testName);
        var inputTree = compilation.SyntaxTrees.Single(tree => tree.FilePath == "Input.cs");

        var driver = CreateTrackingDriver();
        driver = driver.RunGenerators(compilation);
        var firstRunResult = driver.GetRunResult();

        var editedInputTree = CSharpSyntaxTree.ParseText(editedSource, path: "Input.cs");
        var editedCompilation = compilation.ReplaceSyntaxTree(inputTree, editedInputTree);

        driver = driver.RunGenerators(editedCompilation);

        return (firstRunResult, driver);
    }

    private static string RenderGeneratedSourcesOnly(GeneratorDriverRunResult runResult)
    {
        var sb = new System.Text.StringBuilder();

        var generatedTrees = runResult.Results
            .SelectMany(r => r.GeneratedSources)
            .OrderBy(s => s.HintName, StringComparer.Ordinal)
            .ToList();

        foreach (var generated in generatedTrees)
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"== {generated.HintName} ==");
            sb.AppendLine(generated.SourceText.ToString().TrimEnd());
            sb.AppendLine();
        }

        return sb.ToString();
    }

    // GeneratorDriver update methods return the base type, and callers repeatedly reassign them.
#pragma warning disable CA1859
    private static GeneratorDriver CreateTrackingDriver()
    {
        var generator = new EntitySerializerGenerator();
        return CSharpGeneratorDriver.Create(
            generators: [generator.AsSourceGenerator()],
            additionalTexts: null,
            parseOptions: null,
            optionsProvider: null,
            driverOptions: new GeneratorDriverOptions(
                disabledOutputs: IncrementalGeneratorOutputKind.None,
                trackIncrementalGeneratorSteps: true,
                baseDirectory: null));
    }
#pragma warning restore CA1859

    private static IncrementalStepRunReason[] GetStepReasons(
        GeneratorDriver driver,
        string trackingName)
    {
        var generatorResult = driver.GetRunResult().Results.Single();

        return generatorResult.TrackedSteps.TryGetValue(trackingName, out var steps)
            ? steps.SelectMany(step => step.Outputs).Select(output => output.Reason).ToArray()
            : [];
    }

    private static Dictionary<string, IReadOnlyCollection<IncrementalStepRunReason>> GetAllStepReasons(
        GeneratorDriver driver)
    {
        var generatorResult = driver.GetRunResult().Results.Single();

        return generatorResult.TrackedSteps
            .OrderBy(step => step.Key, StringComparer.Ordinal)
            .ToDictionary(
                step => step.Key,
                step => (IReadOnlyCollection<IncrementalStepRunReason>)step.Value
                    .SelectMany(value => value.Outputs)
                    .Select(output => output.Reason)
                    .ToArray(),
                StringComparer.Ordinal);
    }

    private static string Render(
        GeneratorDriverRunResult runResult,
        ImmutableArray<Diagnostic> generatorDiagnostics,
        Compilation outputCompilation)
    {
        var sb = new System.Text.StringBuilder();

        if (generatorDiagnostics.Length > 0)
        {
            sb.AppendLine("== Generator diagnostics ==");
            foreach (var diagnostic in generatorDiagnostics.OrderBy(d => d.Id, StringComparer.Ordinal))
            {
                sb.AppendLine(diagnostic.ToString());
            }

            sb.AppendLine();
        }

        var generatedTrees = runResult.Results
            .SelectMany(r => r.GeneratedSources)
            .OrderBy(s => s.HintName, StringComparer.Ordinal)
            .ToList();

        if (generatedTrees.Count == 0)
        {
            sb.AppendLine("== No generated sources ==");
            return NormalizeSnapshotText(sb);
        }

        foreach (var generated in generatedTrees)
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"== {generated.HintName} ==");
            sb.AppendLine(generated.SourceText.ToString().TrimEnd());
            sb.AppendLine();
        }

        // Verify the generated code actually compiles cleanly alongside the input - this is
        // part of the characterization: if generated code has compile errors, that's current
        // behavior worth capturing rather than hiding.
        var compileErrors = outputCompilation.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .OrderBy(d => d.Id, StringComparer.Ordinal)
            .ToList();

        if (compileErrors.Count > 0)
        {
            sb.AppendLine("== Compile errors in output compilation ==");
            foreach (var error in compileErrors)
            {
                sb.AppendLine(error.ToString());
            }
        }

        return NormalizeSnapshotText(sb);
    }

    private static string NormalizeSnapshotText(System.Text.StringBuilder sb)
    {
        return sb.ToString().TrimEnd() + "\n";
    }

    private static ImmutableArray<MetadataReference> BuildReferences()
    {
        var trustedAssemblies = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator);

        var references = trustedAssemblies
            .Where(path => Path.GetFileName(path) is "System.Private.CoreLib.dll" or "System.Runtime.dll" or "netstandard.dll" or "System.Collections.dll" or "System.Collections.Concurrent.dll" or "System.Linq.dll" or "System.Linq.Expressions.dll" or "System.ComponentModel.Primitives.dll")
            .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))
            .ToList();

        references.Add(MetadataReference.CreateFromFile(typeof(INode).Assembly.Location));
        // The generator emits IEntitySerializer implementations referencing runtime
        // serialization types (EntityInfo, SimpleValue, EntitySchema, PropertySchema, ...) from
        // Cvoya.Graph.Serialization - required for the output compilation to succeed.
        references.Add(MetadataReference.CreateFromFile(typeof(EntitySerializerRegistry).Assembly.Location));

        return [.. references];
    }

    private static CSharpCompilation CreateCompilation(string source, string testName)
    {
        return CSharpCompilation.Create(
            assemblyName: $"CodeGenTests.{testName}",
            syntaxTrees: [CSharpSyntaxTree.ParseText(source, path: "Input.cs")],
            references: References.Value,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
    }
}
