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

    /// <inheritdoc/>
    public ValueTask InitializeAsync() => fixture.InitializeAsync();

    /// <inheritdoc/>
    public ValueTask DisposeAsync() => fixture.DisposeAsync();

    /// <inheritdoc/>
    public async ValueTask<IGraph> GetGraphAsync(StoreIsolation isolation, CancellationToken cancellationToken)
    {
        try
        {
            return await fixture.GetGraph(getNewDatabase: isolation == StoreIsolation.FreshStore);
        }
        catch (TestInfrastructureFixture.Neo4jTestInfrastructureUnavailableException ex)
        {
            throw new GraphProviderUnavailableException(ex.Message, ex);
        }
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
