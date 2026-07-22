// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Neo4j.Tests;

using Cvoya.Graph.CompatibilityTests;
using Cvoya.Graph.Neo4j.Core;
using global::Neo4j.Driver;

public sealed class NativeGraphCommandTests(Neo4jHarness harness) : Neo4jTest(harness)
{
    [Fact]
    public async Task ByteArrayScalar_DoesNotWriteSimpleCollectionCompanions()
    {
        var marker = $"native-binary-{Guid.NewGuid():N}";
        var expected = new byte[] { 0, 1, 127, 128, 255 };
        await Graph.CreateNodeAsync(
            new BinaryPropertyNode { TestKey = marker, Data = expected },
            cancellationToken: TestContext.Current.CancellationToken);

        var record = await QueryRawSingleAsync(
            "MATCH (node:BinaryPropertyNode {TestKey: $marker}) RETURN keys(node) AS keys, node.Data AS data",
            new { marker });
        var keys = record["keys"].As<List<string>>();

        Assert.Contains(nameof(BinaryPropertyNode.Data), keys);
        Assert.DoesNotContain(keys, key => key.StartsWith("__cvoya_sc:v1:", StringComparison.Ordinal));
        Assert.Equal(expected, record["data"].As<byte[]>());
    }

    [Fact]
    public async Task UpdateAsync_FreezesDynamicTargetBeforeChangingEveryDomainProperty()
    {
        var label = $"NativeCommand{Guid.NewGuid():N}";
        var marker = $"before-{Guid.NewGuid():N}";
        var node = new DynamicNode(
            [label],
            new Dictionary<string, object?>
            {
                ["selector"] = marker,
                ["rank"] = "before",
                ["score"] = 1
            });
        await Graph.CreateNodeAsync(node, cancellationToken: TestContext.Current.CancellationToken);

        var affected = await GraphCommandExtensions.UpdateAsync(
            Graph.DynamicNodes().Where(candidate =>
                candidate.HasLabel(label) && candidate.GetProperty<string>("selector") == marker),
            setters => setters
                .SetProperty(candidate => candidate.Properties["selector"], "after")
                .SetProperty(candidate => candidate.Properties["rank"], "after")
                .SetProperty(candidate => candidate.Properties["score"], 2),
            TestContext.Current.CancellationToken);

        Assert.Equal(1, affected);
        var updated = await Graph.DynamicNodes()
            .Where(candidate => candidate.HasLabel(label))
            .SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal("after", updated.GetProperty<string>("selector"));
        Assert.Equal("after", updated.GetProperty<string>("rank"));
        Assert.Equal(2L, updated.GetProperty<long>("score"));
        Assert.DoesNotContain("__nativeId", updated.Properties.Keys);
    }

    [Fact]
    public async Task RawElementsWithoutCvoyaIdentity_AreMutableByNativeSelection()
    {
        var label = $"RawCommandNode{Guid.NewGuid():N}";
        var relationshipType = $"RAW_COMMAND_{Guid.NewGuid():N}";
        await ExecuteRawAsync(
            $"CREATE (source:{label} {{name: $sourceName, score: 1}}), " +
            $"(target:{label} {{name: $targetName}}), " +
            $"(source)-[:{relationshipType} {{weight: 1}}]->(target)",
            new { sourceName = "source", targetName = "target" });

        var updatedNodeCount = await GraphCommandExtensions.UpdateAsync(
            Graph.DynamicNodes().Where(node =>
                node.HasLabel(label) && node.GetProperty<string>("name") == "source"),
            setters => setters.SetProperty(node => node.Properties["score"], 2),
            TestContext.Current.CancellationToken);
        var updatedRelationshipCount = await GraphCommandExtensions.UpdateAsync(
            Graph.DynamicRelationships().Where(relationship => relationship.HasType(relationshipType)),
            setters => setters.SetProperty(relationship => relationship.Properties["weight"], 2),
            TestContext.Current.CancellationToken);

        Assert.Equal(1, updatedNodeCount);
        Assert.Equal(1, updatedRelationshipCount);
        var dynamicNode = await Graph.DynamicNodes()
            .Where(node => node.HasLabel(label) && node.GetProperty<string>("name") == "source")
            .SingleAsync(TestContext.Current.CancellationToken);
        var dynamicRelationship = await Graph.DynamicRelationships()
            .Where(relationship => relationship.HasType(relationshipType))
            .SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(2L, dynamicNode.GetProperty<long>("score"));
        Assert.Equal(2L, dynamicRelationship.GetProperty<long>("weight"));
        Assert.DoesNotContain("Id", dynamicNode.Properties.Keys);
        Assert.DoesNotContain("Id", dynamicRelationship.Properties.Keys);

        var deletedRelationshipCount = await GraphCommandExtensions.DeleteAsync(
            Graph.DynamicRelationships().Where(relationship => relationship.HasType(relationshipType)),
            TestContext.Current.CancellationToken);
        var deletedNodeCount = await GraphCommandExtensions.DeleteAsync(
            Graph.DynamicNodes().Where(node => node.HasLabel(label)),
            cascadeDelete: false,
            TestContext.Current.CancellationToken);

        Assert.Equal(1, deletedRelationshipCount);
        Assert.Equal(2, deletedNodeCount);
    }

    [Fact]
    public async Task RelationshipCreation_SupportsEveryEndpointIntentAndExplicitSelfLoop()
    {
        var selectedSource = new Person { FirstName = "selected-source" };
        var selectedTarget = new Person { FirstName = "selected-target" };
        await Graph.CreateNodeAsync(selectedSource, cancellationToken: TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(selectedTarget, cancellationToken: TestContext.Current.CancellationToken);
        var sourceQuery = Graph.Nodes<Person>().Where(person => person.FirstName == selectedSource.FirstName);
        var targetQuery = Graph.Nodes<Person>().Where(person => person.FirstName == selectedTarget.FirstName);

        await GraphCommandExtensions.CreateRelationshipAsync(
            Graph,
            sourceQuery,
            new Knows(),
            targetQuery,
            RelationshipDirection.Incoming,
            TestContext.Current.CancellationToken);
        await GraphCommandExtensions.CreateAsync(
            Graph,
            sourceQuery,
            new Knows(),
            new Person { FirstName = "new-target" },
            RelationshipDirection.Outgoing,
            TestContext.Current.CancellationToken);
        await GraphCommandExtensions.CreateAsync(
            Graph,
            new Person { FirstName = "new-source" },
            new Knows(),
            targetQuery,
            RelationshipDirection.Incoming,
            TestContext.Current.CancellationToken);

        var equalEndpoint = new Person { FirstName = "equal-endpoint" };
        await Graph.CreateAsync(
            equalEndpoint,
            new Knows(),
            equalEndpoint,
            RelationshipDirection.Outgoing,
            cancellationToken: TestContext.Current.CancellationToken);
        await GraphCommandExtensions.CreateSelfLoopAsync(
            Graph,
            new Person { FirstName = "new-self-loop" },
            new Knows(),
            cancellationToken: TestContext.Current.CancellationToken);
        await GraphCommandExtensions.CreateRelationshipAsync(
            Graph,
            sourceQuery,
            new Knows(),
            sourceQuery,
            RelationshipDirection.Outgoing,
            TestContext.Current.CancellationToken);

        var parallelRelationship = new Knows();
        await GraphCommandExtensions.CreateRelationshipAsync(
            Graph,
            sourceQuery,
            parallelRelationship,
            targetQuery,
            RelationshipDirection.Outgoing,
            TestContext.Current.CancellationToken);
        await GraphCommandExtensions.CreateRelationshipAsync(
            Graph,
            sourceQuery,
            parallelRelationship,
            targetQuery,
            RelationshipDirection.Outgoing,
            TestContext.Current.CancellationToken);

        var record = await QueryRawSingleAsync(
            """
            MATCH (node:Person)
            WITH collect(node.FirstName) AS names
            MATCH ()-[relationship:KNOWS]->()
            RETURN names AS names,
                   count(relationship) AS relationshipCount,
                   count(CASE WHEN relationship.Id IS NULL AND relationship.Direction IS NULL THEN 1 END) AS identityNeutralCount,
                   count(CASE WHEN startNode(relationship).FirstName = 'selected-target' AND endNode(relationship).FirstName = 'selected-source' THEN 1 END) AS incomingQueryCount,
                   count(CASE WHEN startNode(relationship).FirstName = 'selected-source' AND endNode(relationship).FirstName = 'new-target' THEN 1 END) AS queryNewCount,
                   count(CASE WHEN startNode(relationship).FirstName = 'selected-target' AND endNode(relationship).FirstName = 'new-source' THEN 1 END) AS newQueryCount,
                   count(CASE WHEN startNode(relationship).FirstName = 'new-self-loop' AND endNode(relationship).FirstName = 'new-self-loop' THEN 1 END) AS newSelfLoopCount,
                   count(CASE WHEN startNode(relationship).FirstName = 'selected-source' AND endNode(relationship).FirstName = 'selected-source' THEN 1 END) AS selectedSelfLoopCount
            """);
        var names = record["names"].As<List<string>>();
        Assert.Equal(2, names.Count(name => name == "equal-endpoint"));
        Assert.Equal(8, record["relationshipCount"].As<int>());
        Assert.Equal(8, record["identityNeutralCount"].As<int>());
        Assert.Equal(1, record["incomingQueryCount"].As<int>());
        Assert.Equal(1, record["queryNewCount"].As<int>());
        Assert.Equal(1, record["newQueryCount"].As<int>());
        Assert.Equal(1, record["newSelfLoopCount"].As<int>());
        Assert.Equal(1, record["selectedSelfLoopCount"].As<int>());
    }

    [Fact]
    public async Task RelationshipCreation_CardinalityFailureCreatesNothing()
    {
        var marker = $"cardinality-{Guid.NewGuid():N}";
        var source = new Person { FirstName = "source", LastName = marker };
        await Graph.CreateNodeAsync(source, cancellationToken: TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(
            new Person { FirstName = "target-one", LastName = marker },
            cancellationToken: TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(
            new Person { FirstName = "target-two", LastName = marker },
            cancellationToken: TestContext.Current.CancellationToken);
        var sourceQuery = Graph.Nodes<Person>().Where(person => person.FirstName == source.FirstName);

        var empty = await Assert.ThrowsAsync<GraphCardinalityException>(() =>
            GraphCommandExtensions.CreateRelationshipAsync(
                Graph,
                sourceQuery,
                new Knows(),
                Graph.Nodes<Person>().Where(person => person.FirstName == "missing"),
                RelationshipDirection.Outgoing,
                TestContext.Current.CancellationToken));
        var multiple = await Assert.ThrowsAsync<GraphCardinalityException>(() =>
            GraphCommandExtensions.CreateRelationshipAsync(
                Graph,
                sourceQuery,
                new Knows(),
                Graph.Nodes<Person>().Where(person => person.LastName == marker && person.FirstName != "source"),
                RelationshipDirection.Outgoing,
                TestContext.Current.CancellationToken));

        Assert.Equal(GraphEndpointRole.Target, empty.Role);
        Assert.Equal(GraphCardinalityFailure.Empty, empty.Failure);
        Assert.Equal(GraphEndpointRole.Target, multiple.Role);
        Assert.Equal(GraphCardinalityFailure.Multiple, multiple.Failure);
        Assert.Equal(0, await Graph.Relationships<Knows>().CountAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task RelationshipCreation_ReusesBoundTransactionAndRejectsScopeMismatch()
    {
        var source = new Person { FirstName = "transaction-source" };
        var target = new Person { FirstName = "transaction-target" };
        await Graph.CreateNodeAsync(source, cancellationToken: TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(target, cancellationToken: TestContext.Current.CancellationToken);
        await using var transaction = await Graph.GetTransactionAsync(TestContext.Current.CancellationToken);
        var sourceInTransaction = Graph.Nodes<Person>(transaction).Where(person => person.FirstName == source.FirstName);
        var targetInTransaction = Graph.Nodes<Person>(transaction).Where(person => person.FirstName == target.FirstName);

        await GraphCommandExtensions.CreateRelationshipAsync(
            Graph,
            sourceInTransaction,
            new Knows(),
            targetInTransaction,
            RelationshipDirection.Outgoing,
            TestContext.Current.CancellationToken);
        Assert.Equal(
            1,
            await Graph.Relationships<Knows>(transaction).CountAsync(TestContext.Current.CancellationToken));

        var mismatch = await Assert.ThrowsAsync<GraphException>(() =>
            GraphCommandExtensions.CreateRelationshipAsync(
                Graph,
                sourceInTransaction,
                new Knows(),
                Graph.Nodes<Person>().Where(person => person.FirstName == target.FirstName),
                RelationshipDirection.Outgoing,
                TestContext.Current.CancellationToken));
        Assert.Contains("same transaction object", mismatch.Message, StringComparison.Ordinal);

        await transaction.RollbackAsync();
        Assert.Equal(0, await Graph.Relationships<Knows>().CountAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Mutation_WithExplicitReadOnlyTransaction_ThrowsBeforeAnyWrite()
    {
        var person = new Person { FirstName = "read-only-guard" };
        await Graph.CreateNodeAsync(person, cancellationToken: TestContext.Current.CancellationToken);
        var neo4jGraph = Assert.IsType<Neo4jGraph>(Graph);

        await using var readOnlyTransaction = new GraphTransaction(neo4jGraph.Context, isReadOnly: true);
        await readOnlyTransaction.BeginTransactionAsync(TestContext.Current.CancellationToken);

        var exception = await Assert.ThrowsAsync<GraphException>(() =>
            GraphCommandExtensions.UpdateAsync(
                Graph.Nodes<Person>(readOnlyTransaction).Where(candidate => candidate.TestKey == person.TestKey),
                setters => setters.SetProperty(candidate => candidate.FirstName, "after"),
                TestContext.Current.CancellationToken));
        Assert.Equal(
            "A graph command cannot use a transaction opened with read-only access.",
            exception.Message);

        await readOnlyTransaction.RollbackAsync();
        Assert.Equal(
            "read-only-guard",
            (await Graph.Nodes<Person>()
                .Where(candidate => candidate.TestKey == person.TestKey)
                .SingleAsync(TestContext.Current.CancellationToken)).FirstName);
    }

    [Fact]
    public async Task CancelledRelationshipCreation_CreatesNothing()
    {
        var source = new Person { FirstName = "cancel-relationship-source" };
        var target = new Person { FirstName = "cancel-relationship-target" };
        await Graph.CreateNodeAsync(source, cancellationToken: TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(target, cancellationToken: TestContext.Current.CancellationToken);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => GraphCommandExtensions.CreateRelationshipAsync(
            Graph,
            Graph.Nodes<Person>().Where(person => person.FirstName == source.FirstName),
            new Knows(),
            Graph.Nodes<Person>().Where(person => person.FirstName == target.FirstName),
            RelationshipDirection.Outgoing,
            cancellation.Token));

        Assert.Equal(0, await Graph.Relationships<Knows>().CountAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CancelledDelete_LeavesSelectedEntityUnchanged()
    {
        var person = new Person { FirstName = "cancel-delete" };
        await Graph.CreateNodeAsync(person, cancellationToken: TestContext.Current.CancellationToken);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => GraphCommandExtensions.DeleteAsync(
            Graph.Nodes<Person>().Where(candidate => candidate.TestKey == person.TestKey),
            cascadeDelete: false,
            cancellation.Token));

        _ = await Graph.Nodes<Person>()
            .Where(candidate => candidate.TestKey == person.TestKey)
            .SingleAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task RelationshipCreation_DynamicBagEntriesNamedLikeStructuralProperties_ArePersistedAsUserData()
    {
        var label = $"DynamicBagId{Guid.NewGuid():N}";
        var relationshipType = $"DYNAMIC_BAG_{Guid.NewGuid():N}";
        var source = new DynamicNode(
            [label],
            new Dictionary<string, object?> { ["Id"] = "user-supplied-node-id", ["name"] = "source" });
        var relationship = new DynamicRelationship(
            relationshipType,
            new Dictionary<string, object?> { ["Id"] = "user-supplied-relationship-id" });
        var target = new DynamicNode([label], new Dictionary<string, object?> { ["name"] = "target" });

        await Graph.CreateAsync(
            source,
            relationship,
            target,
            RelationshipDirection.Outgoing,
            cancellationToken: TestContext.Current.CancellationToken);

        // A dynamic property-bag entry named "Id" is user data, not framework identity: it must be
        // stored verbatim, while the target without one still gets no synthetic identity.
        var record = await QueryRawSingleAsync(
            $"MATCH (source:{label} {{name: 'source'}})-[r:{relationshipType}]->(target:{label} {{name: 'target'}}) " +
            "RETURN source.Id AS sourceId, r.Id AS relationshipId, target.Id AS targetId");
        Assert.Equal("user-supplied-node-id", record["sourceId"].As<string>());
        Assert.Equal("user-supplied-relationship-id", record["relationshipId"].As<string>());
        Assert.Null(record["targetId"].As<string?>());
    }

    [Fact]
    public async Task NewNativeSubgraph_DoesNotPersistLegacyOrReservedIdentityProtocol()
    {
        var marker = $"native-subgraph-{Guid.NewGuid():N}";
        await Graph.CreateAsync(
            new PersonWithComplexProperty
            {
                FirstName = marker,
                Address = new AddressValue { Street = "source-street", City = "source-city" }
            },
            new Knows(),
            new PersonWithComplexProperty
            {
                FirstName = "target",
                Address = new AddressValue { Street = "target-street", City = "target-city" }
            },
            RelationshipDirection.Outgoing,
            cancellationToken: TestContext.Current.CancellationToken);

        var record = await QueryRawSingleAsync(
            """
            MATCH (source:PersonWithComplexProperty {FirstName: $marker})-[relationship:KNOWS]->(target:PersonWithComplexProperty)
            MATCH (source)-[propertyRelationship]->(valueNode)
            WHERE propertyRelationship.__graphModelComplexProperty = true
            RETURN source.Id IS NULL AS sourceIdMissing,
                   target.Id IS NULL AS targetIdMissing,
                   source.Labels IS NULL AS sourceLabelsMissing,
                   target.Labels IS NULL AS targetLabelsMissing,
                   relationship.Id IS NULL AS relationshipIdMissing,
                   relationship.Direction IS NULL AS directionMissing,
                   relationship.Type IS NULL AS relationshipTypeMissing,
                   propertyRelationship.Id IS NULL AS propertyRelationshipIdMissing,
                   valueNode.Id IS NULL AS valueNodeIdMissing,
                   '__CvoyaRootNode' IN labels(source) AS hasReservedRootLabel
            """,
            new { marker });

        Assert.True(record["sourceIdMissing"].As<bool>());
        Assert.True(record["targetIdMissing"].As<bool>());
        Assert.True(record["sourceLabelsMissing"].As<bool>());
        Assert.True(record["targetLabelsMissing"].As<bool>());
        Assert.True(record["relationshipIdMissing"].As<bool>());
        Assert.True(record["directionMissing"].As<bool>());
        Assert.True(record["relationshipTypeMissing"].As<bool>());
        Assert.True(record["propertyRelationshipIdMissing"].As<bool>());
        Assert.True(record["valueNodeIdMissing"].As<bool>());
        Assert.False(record["hasReservedRootLabel"].As<bool>());
    }

    private async Task ExecuteRawAsync(string cypher, object? parameters = null)
    {
        await using var transaction = await Graph.GetTransactionAsync(TestContext.Current.CancellationToken);
        var neo4jTransaction = Assert.IsType<GraphTransaction>(transaction);
        var result = parameters is null
            ? await neo4jTransaction.Transaction.RunAsync(cypher)
            : await neo4jTransaction.Transaction.RunAsync(cypher, parameters);
        await result.ConsumeAsync();
        await transaction.CommitAsync();
    }

    private async Task<IRecord> QueryRawSingleAsync(string cypher, object? parameters = null)
    {
        await using var transaction = await Graph.GetTransactionAsync(TestContext.Current.CancellationToken);
        var neo4jTransaction = Assert.IsType<GraphTransaction>(transaction);
        var result = parameters is null
            ? await neo4jTransaction.Transaction.RunAsync(cypher)
            : await neo4jTransaction.Transaction.RunAsync(cypher, parameters);
        var record = await result.SingleAsync(TestContext.Current.CancellationToken);
        await transaction.CommitAsync();
        return record;
    }
}
