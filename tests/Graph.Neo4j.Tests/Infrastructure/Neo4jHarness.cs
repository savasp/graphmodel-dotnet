// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Neo4j.Tests;

using Cvoya.Graph.CompatibilityTests;
using Cvoya.Graph.Neo4j.Core;
using Cvoya.Graph.Neo4j.Querying.Cypher;
using global::Neo4j.Driver;
using Microsoft.Extensions.Logging;

/// <summary>
/// The Neo4j binding of <see cref="IGraphProviderTestHarness"/>. Delegates to the existing
/// <see cref="TestInfrastructureFixture"/>/<see cref="DatabasePoolManager"/> machinery rather than
/// duplicating it: this class is the reference implementation of the compatibility suite's SPI.
/// </summary>
public sealed class Neo4jHarness : IGraphProviderTestHarness
{
    private readonly TestInfrastructureFixture fixture = new();

    /// <inheritdoc/>
    public string ProviderName => "Cvoya.Graph.Neo4j";

    /// <inheritdoc/>
    public CapabilitySet Capabilities => Neo4jDialect.Instance.Capabilities;

    /// <summary>
    /// Gets the logger factory shared with the underlying <see cref="TestInfrastructureFixture"/>,
    /// so <c>Neo4jTest</c> can keep logging/correlation behavior consistent with test infrastructure.
    /// </summary>
    public static ILoggerFactory LoggerFactory => TestInfrastructureFixture.LoggerFactory;

    internal string CurrentDatabaseName => fixture.CurrentDatabaseName;

    internal static IDriver CreateIndependentDriver() => TestInfrastructureFixture.CreateIndependentDriver();

    /// <inheritdoc/>
    public ValueTask InitializeAsync() => fixture.InitializeAsync();

    /// <inheritdoc/>
    public ValueTask DisposeAsync() => fixture.DisposeAsync();

    /// <inheritdoc/>
    public async ValueTask<IGraph> GetGraphAsync(StoreIsolation isolation, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            return isolation == StoreIsolation.IndependentStore
                ? fixture.GetIndependentGraph()
                : await fixture.GetGraph(getNewDatabase: isolation == StoreIsolation.FreshStore);
        }
        catch (TestInfrastructureFixture.Neo4jTestInfrastructureUnavailableException ex)
        {
            throw new GraphProviderUnavailableException(ex.Message, ex);
        }
    }

    /// <inheritdoc/>
    public async ValueTask SeedExternalGraphAsync(
        IGraph graph,
        string marker,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentException.ThrowIfNullOrWhiteSpace(marker);
        await using var transaction = await graph.GetTransactionAsync(cancellationToken);
        var native = transaction as GraphTransaction
            ?? throw new ArgumentException("The graph was not created by the Neo4j harness.", nameof(graph));
        var result = await native.Transaction.RunAsync(
            """
            CREATE (source:ContractExternalNode {Marker: $marker, Role: 'source'})
            CREATE (target:ContractExternalNode {Marker: $marker, Role: 'target'})
            CREATE (source)-[:CONTRACT_EXTERNAL_RELATIONSHIP {Marker: $marker}]->(target)
            """,
            new { marker });
        await result.ConsumeAsync();
        await transaction.CommitAsync();
    }

    /// <inheritdoc/>
    public async ValueTask<IReadOnlyCollection<string>> GetStoreArtifactsAsync(
        IGraph graph,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(graph);
        await using var transaction = await graph.GetTransactionAsync(cancellationToken);
        var native = transaction as GraphTransaction
            ?? throw new ArgumentException("The graph was not created by the Neo4j harness.", nameof(graph));
        var indexes = await (await native.Transaction.RunAsync(
                "SHOW INDEXES YIELD name RETURN name ORDER BY name"))
            .ToListAsync(cancellationToken);
        var constraints = await (await native.Transaction.RunAsync(
                "SHOW CONSTRAINTS YIELD name RETURN name ORDER BY name"))
            .ToListAsync(cancellationToken);
        var labels = await (await native.Transaction.RunAsync(
                "CALL db.labels() YIELD label RETURN label ORDER BY label"))
            .ToListAsync(cancellationToken);
        var relationshipTypes = await (await native.Transaction.RunAsync(
                "CALL db.relationshipTypes() YIELD relationshipType RETURN relationshipType ORDER BY relationshipType"))
            .ToListAsync(cancellationToken);
        var propertyKeys = await (await native.Transaction.RunAsync(
                "CALL db.propertyKeys() YIELD propertyKey RETURN propertyKey ORDER BY propertyKey"))
            .ToListAsync(cancellationToken);
        await transaction.CommitAsync();
        return indexes.Select(record => $"index:{record["name"].As<string>()}")
            .Concat(constraints.Select(record => $"constraint:{record["name"].As<string>()}"))
            .Concat(labels.Select(record => $"label:{record["label"].As<string>()}"))
            .Concat(relationshipTypes.Select(
                record => $"relationship-type:{record["relationshipType"].As<string>()}"))
            .Concat(propertyKeys.Select(record => $"property-key:{record["propertyKey"].As<string>()}"))
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    /// <inheritdoc/>
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
        var neo4jTransaction = transaction as GraphTransaction
            ?? throw new ArgumentException("The graph was not created by the Neo4j harness.", nameof(graph));
        var result = await neo4jTransaction.Transaction.RunAsync(
            $"MATCH (n:{escapedLabel}) WHERE n.{escapedProperty} IN $values RETURN count(n) AS count",
            new { values = values.ToArray() });
        var record = await result.SingleAsync(cancellationToken);
        await transaction.CommitAsync();
        return record["count"].As<int>();
    }

    /// <inheritdoc/>
    public bool IsExpectedConcurrentUpdateException(Exception exception) =>
        exception is GraphException { InnerException: Neo4jException { IsRetriable: true } } or
            Neo4jException { IsRetriable: true };
}
