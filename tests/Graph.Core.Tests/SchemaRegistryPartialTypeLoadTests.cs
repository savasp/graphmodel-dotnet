// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Core.Tests;

using System.Reflection;
using System.Runtime.ExceptionServices;

[Trait("Area", "SchemaRegistry")]
[Collection("SchemaRegistry")]
public class SchemaRegistryPartialTypeLoadTests
{
    [Fact]
    public void Initialize_RegistersLoadableTypesAndRetriesPartialAssembly()
    {
        const string loadableNodeLabel = "PartialLoad_LoadableNode";
        const string loadableRelationshipType = "PARTIAL_LOAD_LOADABLE_REL";
        const string deferredNodeLabel = "PartialLoad_DeferredNode";

        PartialTypeLoadFixtureAssembly.Run(
            loadableNodeLabel,
            loadableRelationshipType,
            deferredNodeLabel,
            fixture =>
            {
                var diagnostics = new List<(Assembly Assembly, IReadOnlyList<Exception> LoaderExceptions)>();
                using var registry = new SchemaRegistry(
                    (assembly, loaderExceptions) => diagnostics.Add((assembly, loaderExceptions)));

                try
                {
                    registry.InitializeAsync(TestContext.Current.CancellationToken).GetAwaiter().GetResult();

                    var loadableNode = registry.GetNodeSchema(loadableNodeLabel);
                    var loadableRelationship = registry.GetRelationshipSchema(loadableRelationshipType);

                    Assert.NotNull(loadableNode);
                    Assert.NotNull(loadableRelationship);
                    Assert.Equal("LoadableNode", loadableNode.Type.Name);
                    Assert.Equal("LoadableRelationship", loadableRelationship.Type.Name);

                    var diagnostic = Assert.Single(diagnostics, item => item.Assembly == fixture.Assembly);
                    Assert.NotEmpty(diagnostic.LoaderExceptions);
                    Assert.Contains(
                        diagnostic.LoaderExceptions,
                        exception => exception.Message.Contains(fixture.DependencyName, StringComparison.Ordinal));

                    fixture.LoadDependency();

                    var deferredNode = registry.GetNodeSchema(deferredNodeLabel);

                    Assert.NotNull(deferredNode);
                    Assert.Equal("DeferredNode", deferredNode.Type.Name);
                    Assert.Single(diagnostics, item => item.Assembly == fixture.Assembly);

                    // A complete retry marks the assembly scanned, so later misses do not rediscover it.
                    Assert.Null(registry.GetNodeSchema("PartialLoad_UnknownNode"));
                    Assert.Single(diagnostics, item => item.Assembly == fixture.Assembly);
                }
                finally
                {
                    registry.ClearAsync(TestContext.Current.CancellationToken).GetAwaiter().GetResult();
                    diagnostics.Clear();
                }
            });
    }

    [Fact]
    public void Retry_IsIdempotentForKnownTypeAndPreservesRealCollisionDetection()
    {
        const string collidingNodeLabel = "PartialLoad_Collision";

        PartialTypeLoadFixtureAssembly.Run(
            collidingNodeLabel,
            "PARTIAL_LOAD_COLLISION_REL",
            collidingNodeLabel,
            fixture =>
            {
                using var registry = new SchemaRegistry((_, _) => { });

                try
                {
                    registry.InitializeAsync(TestContext.Current.CancellationToken).GetAwaiter().GetResult();
                    Assert.Equal("LoadableNode", registry.GetNodeSchema(collidingNodeLabel)!.Type.Name);

                    fixture.LoadDependency();

                    var exception = Assert.Throws<GraphException>(
                        () => registry.GetNodeSchema("PartialLoad_ForceCollisionRescan"));

                    Assert.Contains(collidingNodeLabel, exception.Message, StringComparison.Ordinal);
                    Assert.Contains("LoadableNode", exception.Message, StringComparison.Ordinal);
                    Assert.Contains("DeferredNode", exception.Message, StringComparison.Ordinal);
                }
                finally
                {
                    registry.ClearAsync(TestContext.Current.CancellationToken).GetAwaiter().GetResult();
                }
            });
    }

    [Fact]
    public void Initialize_PublishesDiagnosticsAfterRegistrationAndIgnoresSinkFailures()
    {
        const string loadableNodeLabel = "PartialLoad_DiagnosticFailureNode";

        PartialTypeLoadFixtureAssembly.Run(
            loadableNodeLabel,
            "PARTIAL_LOAD_DIAGNOSTIC_FAILURE_REL",
            "PartialLoad_DiagnosticFailureDeferredNode",
            fixture =>
            {
                EntitySchemaInfo? schemaObservedByDiagnostic = null;
                SchemaRegistry? callbackRegistry = null;
                using var registry = new SchemaRegistry(
                    (_, _) =>
                    {
                        schemaObservedByDiagnostic = callbackRegistry!.GetNodeSchema(loadableNodeLabel);
                        throw new InvalidOperationException("Diagnostic sink failure.");
                    });
                callbackRegistry = registry;

                try
                {
                    var exception = Record.Exception(
                        () => registry.InitializeAsync(TestContext.Current.CancellationToken).GetAwaiter().GetResult());

                    Assert.Null(exception);
                    Assert.NotNull(schemaObservedByDiagnostic);
                    Assert.Equal("LoadableNode", schemaObservedByDiagnostic.Type.Name);
                    Assert.Equal(fixture.Assembly, schemaObservedByDiagnostic.Type.Assembly);
                }
                finally
                {
                    registry.ClearAsync(TestContext.Current.CancellationToken).GetAwaiter().GetResult();
                }
            });
    }

    [Fact]
    public void RegisterGraphEntityTypes_UnexpectedGetTypesFailure_Propagates()
    {
        using var registry = new SchemaRegistry();
        var failure = new InvalidOperationException("Unexpected GetTypes failure.");
        var assembly = new ThrowingAssembly(failure);

        var exception = Assert.Throws<InvalidOperationException>(
            () => InvokeRegisterGraphEntityTypes(registry, [assembly]));

        Assert.Same(failure, exception);
    }

    private static void InvokeRegisterGraphEntityTypes(SchemaRegistry registry, IEnumerable<Assembly> assemblies)
    {
        var method = typeof(SchemaRegistry).GetMethod(
            "RegisterGraphEntityTypes",
            BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("SchemaRegistry.RegisterGraphEntityTypes was not found.");

        try
        {
            method.Invoke(registry, [assemblies]);
        }
        catch (TargetInvocationException exception) when (exception.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
        }
    }

    private sealed class ThrowingAssembly(Exception failure) : Assembly
    {
        public override Type[] GetTypes() => throw failure;
    }
}
