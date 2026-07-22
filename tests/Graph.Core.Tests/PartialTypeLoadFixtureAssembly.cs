// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Core.Tests;

using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

/// <summary>
/// Loads a graph-entity fixture whose base-type dependency can be withheld and supplied later.
/// </summary>
internal static class PartialTypeLoadFixtureAssembly
{
    private static readonly Lazy<MetadataReference[]> References = new(BuildReferences);

    public static void Run(
        string loadableNodeLabel,
        string loadableRelationshipType,
        string deferredNodeLabel,
        Action<Fixture> action)
    {
        var contextReference = LoadAndRun(
            loadableNodeLabel,
            loadableRelationshipType,
            deferredNodeLabel,
            action);

        for (var i = 0; contextReference.IsAlive && i < 10; i++)
        {
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);
            GC.WaitForPendingFinalizers();
        }

        if (contextReference.IsAlive)
        {
            throw new InvalidOperationException("Partial-load fixture assembly context was not reclaimed.");
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference LoadAndRun(
        string loadableNodeLabel,
        string loadableRelationshipType,
        string deferredNodeLabel,
        Action<Fixture> action)
    {
        var suffix = Guid.NewGuid().ToString("N");
        var dependencyName = $"PartialTypeLoadDependency_{suffix}";
        var dependencyImage = Compile(
            dependencyName,
            """
            using Cvoya.Graph;

            public abstract record DeferredNodeBase : Node;
            """,
            References.Value);

        using var dependencyReferenceStream = new MemoryStream(dependencyImage);
        var fixtureReferences = References.Value.Append(
            MetadataReference.CreateFromStream(dependencyReferenceStream));
        var fixtureImage = Compile(
            $"PartialTypeLoadFixture_{suffix}",
            $$"""
            using Cvoya.Graph;

            [Node("{{loadableNodeLabel}}")] public sealed record LoadableNode : Node;

            [Relationship("{{loadableRelationshipType}}")] public sealed record LoadableRelationship : Relationship;

            [Node("{{deferredNodeLabel}}")] public sealed record DeferredNode : DeferredNodeBase;
            """,
            fixtureReferences);

        var context = new FixtureLoadContext(dependencyName, dependencyImage);
        try
        {
            using var fixtureStream = new MemoryStream(fixtureImage);
            var assembly = context.LoadFromStream(fixtureStream);
            action(new Fixture(assembly, dependencyName, context.LoadDependency));
        }
        finally
        {
            Labels.ClearCachesForTesting();
            context.PrepareForUnload();
            context.Unload();
        }

        return new WeakReference(context);
    }

    private static byte[] Compile(
        string assemblyName,
        string source,
        IEnumerable<MetadataReference> references)
    {
        var compilation = CSharpCompilation.Create(
            assemblyName,
            [CSharpSyntaxTree.ParseText(source)],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        using var peStream = new MemoryStream();
        var emitResult = compilation.Emit(peStream);
        if (!emitResult.Success)
        {
            var diagnostics = string.Join('\n', emitResult.Diagnostics.Select(diagnostic => diagnostic.ToString()));
            throw new InvalidOperationException($"Partial-load fixture source failed to compile:\n{diagnostics}");
        }

        return peStream.ToArray();
    }

    private static MetadataReference[] BuildReferences()
    {
        var trustedAssemblies = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator);

        var references = trustedAssemblies
            .Where(path => Path.GetFileName(path) is
                "System.Private.CoreLib.dll" or "System.Runtime.dll" or "netstandard.dll" or
                "System.Net.Primitives.dll")
            .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))
            .ToList();

        references.Add(MetadataReference.CreateFromFile(typeof(INode).Assembly.Location));
        return [.. references];
    }

    internal sealed record Fixture(
        Assembly Assembly,
        string DependencyName,
        Func<Assembly> LoadDependency);

    private sealed class FixtureLoadContext(string dependencyName, byte[] dependencyImage)
        : AssemblyLoadContext(name: null, isCollectible: true)
    {
        private Assembly? _dependency;

        protected override Assembly? Load(AssemblyName assemblyName)
            => assemblyName.Name == dependencyName ? _dependency : null;

        public Assembly LoadDependency()
        {
            if (_dependency is not null)
            {
                return _dependency;
            }

            using var stream = new MemoryStream(dependencyImage);
            _dependency = LoadFromStream(stream);
            return _dependency;
        }

        public void PrepareForUnload() => _dependency = null;
    }
}
