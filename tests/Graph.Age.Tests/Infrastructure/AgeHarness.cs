// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Age.Tests;

using Cvoya.Graph.Age.Querying.Cypher;
using Cvoya.Graph.CompatibilityTests;
using Npgsql;
using Npgsql.Age;

/// <summary>The compatibility-suite harness for Apache AGE.</summary>
public sealed class AgeHarness : IGraphProviderTestHarness
{
    private readonly List<AgeGraphStore> stores = [];
    private readonly string connectionString = Environment.GetEnvironmentVariable("AGE_CONNECTION_STRING")
        ?? "Host=localhost;Port=5455;Username=postgres;Password=postgres;Database=postgres";
    private NpgsqlDataSource? dataSource;

    public string ProviderName => "Cvoya.Graph.Age";

    public CapabilitySet Capabilities => AgeDialect.Instance.Capabilities;

    public async ValueTask InitializeAsync()
    {
        try
        {
            var builder = new NpgsqlDataSourceBuilder(connectionString);
            builder.UseAge();
            dataSource = builder.Build();
            await using var store = new AgeGraphStore(dataSource, NewGraphName());
            await store.CreateGraphIfNotExistsAsync(TestContext.Current.CancellationToken);
        }
        catch (Exception exception)
        {
            throw new GraphProviderUnavailableException(
                "Apache AGE is unavailable. Set AGE_CONNECTION_STRING or run the repository AGE container.",
                exception);
        }
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var store in stores)
        {
            await store.DisposeAsync();
        }

        if (dataSource is null)
        {
            return;
        }

        await dataSource.DisposeAsync();
        dataSource = null;
    }

    public async ValueTask<IGraph> GetGraphAsync(
        StoreIsolation isolation,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var graphName = NewGraphName();
        var store = new AgeGraphStore(
            dataSource ?? throw new GraphProviderUnavailableException("The AGE test data source was not initialized."),
            graphName);
        try
        {
            await store.CreateGraphIfNotExistsAsync(cancellationToken);
            stores.Add(store);
            return store.Graph;
        }
        catch (Exception exception)
        {
            await store.DisposeAsync();
            throw new GraphProviderUnavailableException(
                "Apache AGE is unavailable. Set AGE_CONNECTION_STRING or run the repository AGE container.",
                exception);
        }
    }

    private static string NewGraphName() => $"cvoya_tck_{Guid.NewGuid():N}";
}
