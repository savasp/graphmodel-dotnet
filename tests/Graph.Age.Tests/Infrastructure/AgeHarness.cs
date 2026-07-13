// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Age.Tests;

using Cvoya.Graph.Age.Core;
using Cvoya.Graph.Age.Querying.Cypher;
using Cvoya.Graph.Age.Querying.Cypher.Execution;
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
        catch (Exception exception) when (exception is not OperationCanceledException)
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
        var registered = false;
        try
        {
            await store.CreateGraphIfNotExistsAsync(cancellationToken);
            stores.Add(store);
            registered = true;
            return store.Graph;
        }
        catch (NpgsqlException exception)
        {
            throw new GraphProviderUnavailableException(
                "Apache AGE is unavailable. Set AGE_CONNECTION_STRING or run the repository AGE container.",
                exception);
        }
        finally
        {
            // Dispose on every failure path (including cancellation, which the narrowed catch
            // above deliberately doesn't wrap); a successfully registered store is cleaned up by
            // DisposeAsync instead.
            if (!registered)
            {
                await store.DisposeAsync();
            }
        }
    }

    public async ValueTask<int> CountNodesByPropertyAsync(
        IGraph graph,
        string label,
        string propertyName,
        IReadOnlyCollection<string> values,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(values);
        var escapedLabel = CypherIdentifier.EscapeIfNeeded(label, "node label");
        var escapedProperty = CypherIdentifier.EscapeIfNeeded(propertyName, "property name");

        await using var transaction = await graph.GetTransactionAsync(cancellationToken);
        var ageTransaction = transaction as AgeGraphTransaction
            ?? throw new ArgumentException("The graph was not created by the AGE harness.", nameof(graph));
        var result = await ageTransaction.Runner.RunAsync(
            $"""
            MATCH (n:{escapedLabel})
            WHERE n.{escapedProperty} IN $values
            RETURN count(n) AS node_count
            """,
            new { values = values.ToArray() },
            cancellationToken);
        await using var resultLease = result.ConfigureAwait(false);
        var record = await result.SingleAsync(cancellationToken);
        await transaction.CommitAsync();
        return record["node_count"].As<int>();
    }

    public bool IsExpectedConcurrentUpdateException(Exception exception) =>
        exception.GetBaseException() is PostgresException { SqlState: "40001" or "40P01" };

    private static string NewGraphName() => $"cvoya_tck_{Guid.NewGuid():N}";
}
