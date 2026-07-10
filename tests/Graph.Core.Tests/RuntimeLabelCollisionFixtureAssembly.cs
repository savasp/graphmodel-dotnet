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

namespace Cvoya.Graph.Core.Tests;

using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

/// <summary>
/// Compiles small C# snippets into an isolated, collectible <see cref="AssemblyLoadContext"/> for
/// <see cref="RuntimeLabelCollisionTests"/>.
/// </summary>
/// <remarks>
/// <see cref="SchemaRegistry.InitializeAsync"/> discovers node/relationship types by scanning every
/// assembly in <see cref="AppDomain.CurrentDomain"/>, with no attribute-based opt-out. Fixture types that
/// are meant to collide therefore cannot be declared as ordinary members of this test assembly - every
/// other test in this project that calls <c>InitializeAsync</c> (see <c>SchemaRegistryTests</c>) would
/// throw the moment any such collision exists anywhere in the assembly, since a collision aborts the
/// entire aggregated scan. Loading fixtures into their own collectible <see cref="AssemblyLoadContext"/>,
/// used only within a single test, disconnected from <see cref="Labels"/>' static caches, and unloaded
/// (with a forced GC pass to actually reclaim it) before the test returns, keeps them invisible to
/// <c>AppDomain.CurrentDomain.GetAssemblies()</c> everywhere else. <see cref="RuntimeLabelCollisionTests"/>
/// and <c>SchemaRegistryTests</c> additionally share an explicit xunit collection so they never run
/// concurrently, closing the residual window between load and unload.
/// </remarks>
internal static class RuntimeLabelCollisionFixtureAssembly
{
    private static readonly Lazy<MetadataReference[]> References = new(BuildReferences);

    /// <summary>
    /// Compiles <paramref name="source"/> (which may reference <c>Cvoya.Graph</c> types such as
    /// <see cref="Node"/>, <see cref="Relationship"/>, <see cref="NodeAttribute"/>, etc.) into an isolated,
    /// collectible <see cref="AssemblyLoadContext"/>, resolves <paramref name="typeNames"/> against the
    /// loaded assembly, runs <paramref name="action"/> against the resolved types, then unloads the context
    /// and forces a full GC pass so the fixture types never leak into later, unrelated tests. This requires
    /// fixture actions to leave no static cache references behind; the helper clears <see cref="Labels"/>
    /// caches before unloading the context.
    /// </summary>
    public static void Run(string source, string[] typeNames, Action<Type[]> action)
    {
        var contextReference = LoadAndRun(source, typeNames, action);

        // Force the collectible context to actually be reclaimed before returning, so the fixture
        // types are gone from AppDomain.CurrentDomain.GetAssemblies() by the time this method
        // returns, not just eventually.
        for (var i = 0; contextReference.IsAlive && i < 10; i++)
        {
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);
            GC.WaitForPendingFinalizers();
        }

        if (contextReference.IsAlive)
        {
            throw new InvalidOperationException("Fixture assembly load context was not reclaimed.");
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference LoadAndRun(string source, string[] typeNames, Action<Type[]> action)
    {
        var compilation = CSharpCompilation.Create(
            assemblyName: $"RuntimeLabelCollisionFixture_{Guid.NewGuid():N}",
            syntaxTrees: [CSharpSyntaxTree.ParseText(source)],
            references: References.Value,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        using var peStream = new MemoryStream();
        var emitResult = compilation.Emit(peStream);
        if (!emitResult.Success)
        {
            var diagnostics = string.Join('\n', emitResult.Diagnostics.Select(d => d.ToString()));
            throw new InvalidOperationException($"Fixture source failed to compile:\n{diagnostics}");
        }

        peStream.Seek(0, SeekOrigin.Begin);

        var context = new AssemblyLoadContext(name: null, isCollectible: true);
        try
        {
            var assembly = context.LoadFromStream(peStream);
            var types = typeNames
                .Select(name => assembly.GetType(name) ?? throw new InvalidOperationException($"Type '{name}' was not found in the compiled fixture."))
                .ToArray();

            action(types);
        }
        finally
        {
            Labels.ClearCachesForTesting();
            context.Unload();
        }

        return new WeakReference(context);
    }

    private static MetadataReference[] BuildReferences()
    {
        var trustedAssemblies = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator);

        var references = trustedAssemblies
            .Where(path => Path.GetFileName(path) is
                "System.Private.CoreLib.dll" or "System.Runtime.dll" or "netstandard.dll")
            .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))
            .ToList();

        references.Add(MetadataReference.CreateFromFile(typeof(INode).Assembly.Location));

        return [.. references];
    }
}
