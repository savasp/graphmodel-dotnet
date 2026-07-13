// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Neo4j.Tests;

using Cvoya.Graph.CompatibilityTests;
using global::Neo4j.Driver;

/// <summary>
/// Neo4j-specific proof that <see cref="IGraph.CreateAsync{TSource, TRelationship, TTarget}"/>
/// composes the whole subgraph — both endpoint nodes, their complex-property value-node subtrees,
/// and the connecting edge — into a single statement, so it costs exactly one <c>RunAsync</c>
/// round-trip to the database.
/// </summary>
public sealed class SubgraphRoundTripTests
{
    [Fact]
    public async Task CreateAsync_WithComplexProperties_IssuesExactlyOneRunAsync()
    {
        var infrastructure = new Neo4jTestInfrastructureWithDbInstance();

        var counter = new RunAsyncCounter();
        IDriver innerDriver = GraphDatabase.Driver(
            infrastructure.ConnectionString,
            AuthTokens.Basic(infrastructure.Username, infrastructure.Password));

        try
        {
            if (!await innerDriver.TryVerifyConnectivityAsync())
            {
                Assert.Skip($"Neo4j is not reachable at {infrastructure.ConnectionString}.");
                return;
            }
        }
        catch (Exception ex) when (ex is Neo4jException or System.Net.Sockets.SocketException)
        {
            await innerDriver.DisposeAsync();
            Assert.Skip($"Neo4j is not reachable at {infrastructure.ConnectionString}: {ex.Message}");
            return;
        }

        await using var _ = innerDriver.ConfigureAwait(false);
        var countingDriver = new CountingDriver(innerDriver, counter);
        await using var store = new Neo4jGraphStore(countingDriver, databaseName: "neo4j");
        var graph = store.Graph;
        var cancellationToken = TestContext.Current.CancellationToken;

        // Warm up: the first create initializes the schema (constraints/indexes), which issues its
        // own statements. Those must not be counted against the measured operation.
        await CreateSubgraphAsync(graph, cancellationToken);

        counter.Reset();

        // Measured operation: a subgraph whose endpoints both carry a complex property, so a single
        // RunAsync here proves the endpoints, the edge, AND the complex-property value-node subtrees
        // are all in one statement.
        await CreateSubgraphAsync(graph, cancellationToken);

        Assert.Equal(1, counter.Count);
    }

    private static async Task CreateSubgraphAsync(IGraph graph, CancellationToken cancellationToken)
    {
        var source = new PersonWithComplexProperty
        {
            FirstName = "Source",
            Address = new AddressValue { Street = "1 Source St", City = "Sourceville" }
        };
        var target = new PersonWithComplexProperty
        {
            FirstName = "Target",
            Address = new AddressValue { Street = "2 Target Ave", City = "Targettown" }
        };
        var knows = new Knows { StartNodeId = source.Id, EndNodeId = target.Id, Since = DateTime.UtcNow };

        await graph.CreateAsync(source, knows, target, null, null, cancellationToken);
    }
}
