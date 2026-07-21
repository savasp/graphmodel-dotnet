// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Age.Tests.GraphTests;

using Cvoya.Graph.Age.Core;
using Cvoya.Graph.CompatibilityTests;

/// <summary>AGE-specific coverage for the provider's no-managed-index contract.</summary>
public sealed class AgeManagedIndexMaintenanceTests(AgeHarness harness)
    : AgeTest(harness, StoreIsolation.FreshStore)
{
    [Fact]
    public async Task RecreateManagedIndexesAsync_PerformsNoDdlOrSchemaInitialization()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var before = await ReadSchemaArtifactsAsync(cancellationToken);
        Assert.False(Graph.SchemaRegistry.IsInitialized);

        await Graph.RecreateManagedIndexesAsync(cancellationToken);

        Assert.False(Graph.SchemaRegistry.IsInitialized);
        Assert.Equal(before, await ReadSchemaArtifactsAsync(cancellationToken));
    }

    private async Task<string[]> ReadSchemaArtifactsAsync(CancellationToken cancellationToken)
    {
        await using var transaction = await Graph.GetTransactionAsync(cancellationToken);
        var runner = ((AgeGraphTransaction)transaction).Runner;
        var values = await runner.QueryScalarStringsAsync(
            """
            SELECT artifact
            FROM (
                SELECT 'index:' || indexname || ':' || indexdef AS artifact
                FROM pg_indexes
                WHERE schemaname = @query
                UNION ALL
                SELECT 'function:' || routine_name || ':' || coalesce(routine_definition, '') AS artifact
                FROM information_schema.routines
                WHERE routine_schema = @query
            ) AS artifacts
            ORDER BY artifact
            """,
            runner.GraphName,
            cancellationToken);
        await transaction.CommitAsync();
        return [.. values];
    }
}
