// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Age.Tests.GraphTests;

using Cvoya.Graph.Age.Core;
using Cvoya.Graph.Age.Querying.Cypher.Execution;
using Cvoya.Graph.CompatibilityTests;
using Cvoya.Graph.Querying;
using Cvoya.Graph.Querying.Commands;

/// <summary>Native AGE shape and migration-free legacy interop coverage for issues #470-#472.</summary>
public sealed class AgeNativeInteropTests(AgeHarness harness)
    : AgeTest(harness, StoreIsolation.FreshStore)
{
    [Node("Mapped Person")]
    public sealed record MappedPerson : Node
    {
        public string Name { get; set; } = string.Empty;
    }

    [Relationship("MAPPED KNOWS")]
    public sealed record MappedKnows : Relationship
    {
        public MappedKnows() : base(string.Empty, string.Empty) { }

        public MappedKnows(INode source, INode target) : base(source.Id, target.Id) { }
    }

    [Fact]
    public async Task RawNativeRowsWithoutCvoyaMetadata_AreTypedDynamicAndTraversable()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var marker = $"raw-{Guid.NewGuid():N}";
        await RunRawAsync(
            """
            CREATE (source:Person)
            SET source.FirstName = $sourceName, source.LastName = $marker, source.Age = 31
            CREATE (target:Person)
            SET target.FirstName = $targetName, target.LastName = $marker, target.Age = 32
            CREATE (source)-[relationship:KNOWS]->(target)
            SET relationship.Since = $since
            RETURN true AS created
            """,
            new
            {
                sourceName = "Raw source",
                targetName = "Raw target",
                marker,
                since = DateTime.UnixEpoch.ToString("O", System.Globalization.CultureInfo.InvariantCulture),
            },
            cancellationToken);

        var typed = await Graph.Nodes<Person>()
            .Where(person => person.LastName == marker)
            .OrderBy(person => person.FirstName)
            .ToListAsync(cancellationToken);
        var dynamicNodes = await Graph.DynamicNodes().ToListAsync(cancellationToken);
        var relationships = await Graph.Relationships<Knows>().ToListAsync(cancellationToken);
        var traversed = await Graph.Nodes<Person>()
            .Where(person => person.FirstName == "Raw source")
            .Traverse<Knows, Person>()
            .ToListAsync(cancellationToken);

        Assert.Equal(["Raw source", "Raw target"], typed.Select(person => person.FirstName));
        Assert.Equal(2, dynamicNodes.Count);
        Assert.All(dynamicNodes, node => Assert.Equal(["Person"], node.Labels));
        Assert.DoesNotContain(dynamicNodes, node => node.Labels.Contains("CvoyaNode", StringComparer.Ordinal));
        Assert.Single(relationships);
        Assert.Equal("KNOWS", relationships[0].Type);
        Assert.Equal("Raw target", Assert.Single(traversed).FirstName);
    }

    [Fact]
    public async Task DynamicRootQueriesExcludeProviderOwnedComplexValueNodes()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var person = new PersonWithComplexProperty
        {
            FirstName = "Root",
            Address = new AddressValue { Street = "Value node", City = "Nowhere" },
        };
        await Graph.CreateNodeAsync(person, cancellationToken: cancellationToken);

        var dynamicNodes = await Graph.DynamicNodes().ToListAsync(cancellationToken);

        var root = Assert.Single(dynamicNodes);
        Assert.Equal(person.Id, root.Id);
        Assert.DoesNotContain("CvoyaNode", root.Labels);
    }

    [Fact]
    public async Task DirectIdMutationExcludesComplexValueNodeWithCollidingId()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var person = new PersonWithComplexProperty
        {
            FirstName = "Root",
            Address = new AddressValue { Street = "Value node", City = "Nowhere" },
        };
        await Graph.CreateNodeAsync(person, cancellationToken: cancellationToken);
        await RunRawAsync(
            """
            MATCH (owner {Id: $id})-[relationship]->(value)
            WHERE relationship.__graphModelComplexProperty = true
            SET value.Id = $id
            RETURN true AS updated
            """,
            new { id = person.Id },
            cancellationToken);

        await Graph.DeleteNodeAsync(
            person.Id,
            cascadeDelete: true,
            cancellationToken: cancellationToken);

        var remaining = await RunRawForRecordAsync(
            "MATCH (node) RETURN count(node) AS nodeCount",
            new { },
            cancellationToken);
        Assert.Equal(0, remaining["nodeCount"].As<int>());
    }

    [Fact]
    public async Task SubgraphWritesUseLegacyTablesForNonSymbolicMappedNames()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var source = new MappedPerson { Name = "Source" };
        var target = new MappedPerson { Name = "Target" };
        var relationship = new MappedKnows(source, target);

        await Graph.CreateAsync(source, relationship, target, null, null, cancellationToken);

        var shape = await RunRawForRecordAsync(
            """
            MATCH (source {Id: $sourceId})-[relationship {Id: $relationshipId}]->(target {Id: $targetId})
            RETURN head(labels(source)) AS sourceLabel,
                   type(relationship) AS relationshipType,
                   head(source.inheritance_labels) AS logicalNodeLabel,
                   relationship.Type AS logicalRelationshipType
            """,
            new
            {
                sourceId = source.Id,
                relationshipId = relationship.Id,
                targetId = target.Id,
            },
            cancellationToken);

        Assert.Equal("CvoyaNode", shape["sourceLabel"].As<string>());
        Assert.Equal("CvoyaRelationship", shape["relationshipType"].As<string>());
        Assert.Equal("Mapped Person", shape["logicalNodeLabel"].As<string>());
        Assert.Equal("MAPPED KNOWS", shape["logicalRelationshipType"].As<string>());
        Assert.Equal("Source", (await Graph.GetNodeAsync<MappedPerson>(
            source.Id, cancellationToken: cancellationToken)).Name);
        Assert.Equal(relationship.Id, (await Graph.GetRelationshipAsync<MappedKnows>(
            relationship.Id, cancellationToken: cancellationToken)).Id);
    }

    [Fact]
    public async Task NewWritesUseLogicalNativeStorage_AndPlainIdsAreNotImplicitlyUnique()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var duplicateId = $"duplicate-{Guid.NewGuid():N}";
        var source = new Person { Id = duplicateId, FirstName = "First" };
        var duplicate = new Person { Id = duplicateId, FirstName = "Second" };
        var target = new Person { FirstName = "Target" };
        await Graph.CreateNodeAsync(source, cancellationToken: cancellationToken);
        await Graph.CreateNodeAsync(target, cancellationToken: cancellationToken);
        var relationship = new Knows(source, target) { Since = DateTime.UnixEpoch };
        await Graph.CreateRelationshipAsync(relationship, cancellationToken: cancellationToken);
        await Graph.CreateRelationshipAsync(
            relationship with { },
            cancellationToken: cancellationToken);
        await Graph.CreateNodeAsync(duplicate, cancellationToken: cancellationToken);

        var shape = await RunRawForRecordAsync(
            """
            MATCH (source {Id: $sourceId})-[relationship {Id: $relationshipId}]->(target {Id: $targetId})
            RETURN head(labels(source)) AS sourceLabel,
                   type(relationship) AS relationshipType,
                   source.inheritance_labels IS NULL AS noNodeHierarchy,
                   source.__graphModelEntityKind__ IS NULL AS noEntityKind,
                   relationship.Type IS NULL AS noStoredType,
                   relationship.inheritance_labels IS NULL AS noRelationshipHierarchy,
                   count(relationship) AS relationshipCount
            """,
            new
            {
                sourceId = source.Id,
                relationshipId = relationship.Id,
                targetId = target.Id,
            },
            cancellationToken);

        Assert.Equal("Person", shape["sourceLabel"].As<string>());
        Assert.Equal("KNOWS", shape["relationshipType"].As<string>());
        Assert.True(shape["noNodeHierarchy"].As<bool>());
        Assert.True(shape["noEntityKind"].As<bool>());
        Assert.True(shape["noStoredType"].As<bool>());
        Assert.True(shape["noRelationshipHierarchy"].As<bool>());
        Assert.Equal(2, shape["relationshipCount"].As<int>());
        Assert.Equal(2, await Graph.Nodes<Person>()
            .Where(person => person.Id == duplicateId)
            .CountAsync(cancellationToken));
        await Assert.ThrowsAsync<GraphException>(() =>
            Graph.UpdateNodeAsync(source with { FirstName = "Ambiguous" }, cancellationToken: cancellationToken));
    }

    [Fact]
    public async Task SetBasedMutationKeepsFrozenNativeIdentityWhenIdPropertyChanges()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var person = new Person { FirstName = "Before" };
        await Graph.CreateNodeAsync(person, cancellationToken: cancellationToken);
        var oldId = person.Id;
        var newId = $"changed-{Guid.NewGuid():N}";

        var affected = await GraphCommandExtensions.UpdateAsync(
            Graph.Nodes<Person>().Where(candidate => candidate.Id == oldId),
            setters => setters
                .SetProperty(candidate => candidate.Id, newId)
                .SetProperty(candidate => candidate.FirstName, "After"),
            cancellationToken);

        Assert.Equal(1, affected);
        Assert.Equal("After", (await Graph.GetNodeAsync<Person>(
            newId, cancellationToken: cancellationToken)).FirstName);
        await Assert.ThrowsAsync<EntityNotFoundException>(() =>
            Graph.GetNodeAsync<Person>(oldId, cancellationToken: cancellationToken));
    }

    [Fact]
    public async Task RelationshipCommandsSupportEveryEndpointIntentAndExplicitSelfLoop()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var selectedSource = new Person { FirstName = "Selected source" };
        var selectedTarget = new Person { FirstName = "Selected target" };
        await Graph.CreateNodeAsync(selectedSource, cancellationToken: cancellationToken);
        await Graph.CreateNodeAsync(selectedTarget, cancellationToken: cancellationToken);
        var sourceQuery = Graph.Nodes<Person>().Where(node => node.Id == selectedSource.Id);
        var targetQuery = Graph.Nodes<Person>().Where(node => node.Id == selectedTarget.Id);

        await CreateWithSelectionsAsync(
            sourceQuery,
            new Knows(string.Empty, string.Empty),
            targetQuery,
            RelationshipDirection.Outgoing,
            cancellationToken);
        await CreateHybridAsync(
            sourceQuery,
            new Knows(string.Empty, string.Empty),
            new Person { FirstName = "New target" },
            newEndpointIsTarget: true,
            cancellationToken);
        await CreateHybridAsync(
            targetQuery,
            new Knows(string.Empty, string.Empty),
            new Person { FirstName = "New source" },
            newEndpointIsTarget: false,
            cancellationToken);

        var allNewSource = new Person { FirstName = "All-new source" };
        var allNewTarget = new Person { FirstName = "All-new target" };
        await CreateAllNewAsync(
            allNewSource,
            new Knows(string.Empty, string.Empty),
            allNewTarget,
            selfLoop: false,
            cancellationToken);
        var selfLoop = new Person { FirstName = "Self loop" };
        await CreateAllNewAsync(
            selfLoop,
            new Knows(string.Empty, string.Empty),
            selfLoop,
            selfLoop: true,
            cancellationToken);

        var equalSource = new Person { Id = $"equal-{Guid.NewGuid():N}", FirstName = "Equal" };
        var equalTarget = equalSource with { };
        Assert.Equal(equalSource, equalTarget);
        Assert.NotSame(equalSource, equalTarget);
        await CreateAllNewAsync(
            equalSource,
            new Knows(string.Empty, string.Empty),
            equalTarget,
            selfLoop: false,
            cancellationToken);

        Assert.Equal(6, await Graph.Relationships<Knows>().CountAsync(cancellationToken));
        Assert.Equal(2, await Graph.Nodes<Person>()
            .Where(node => node.Id == equalSource.Id)
            .CountAsync(cancellationToken));
        var loopCount = await RunRawForRecordAsync(
            """
            MATCH (node {Id: $id})-[relationship:KNOWS]->(node)
            RETURN count(relationship) AS loopCount
            """,
            new { id = selfLoop.Id },
            cancellationToken);
        Assert.Equal(1, loopCount["loopCount"].As<int>());
        var syntheticDirectionCount = await RunRawForRecordAsync(
            "MATCH ()-[relationship:KNOWS]->() WHERE relationship.Direction IS NOT NULL " +
            "RETURN count(relationship) AS relationshipCount",
            new { },
            cancellationToken);
        Assert.Equal(0, syntheticDirectionCount["relationshipCount"].As<int>());
    }

    [Fact]
    public async Task FailedRelationshipCommandRollsBackItsCallerTransactionSavepoint()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var createdBeforeFailure = new Person { FirstName = "Must roll back" };
        var invalidTarget = new DynamicNode(
            ["CvoyaNode"],
            new Dictionary<string, object?> { ["name"] = "reserved" });
        var survivor = new Person { FirstName = "Caller can continue" };

        await using (var transaction = await Graph.GetTransactionAsync(cancellationToken))
        {
            var provider = Assert.IsAssignableFrom<IGraphCommandProvider>(
                Graph.Nodes<Person>(transaction).Provider);
            await Assert.ThrowsAsync<GraphException>(() => provider.InWriteTransactionAsync(
                async (context, token) =>
                {
                    await context.CreateRelationshipAsync(
                        new NewGraphCommandEndpoint(createdBeforeFailure),
                        new Knows(string.Empty, string.Empty),
                        new NewGraphCommandEndpoint(invalidTarget),
                        RelationshipDirection.Outgoing,
                        GraphRelationshipCreationMode.Standard,
                        token).ConfigureAwait(false);
                    return true;
                },
                cancellationToken));

            await Graph.CreateNodeAsync(survivor, transaction, cancellationToken);
            await transaction.CommitAsync();
        }

        await Assert.ThrowsAsync<EntityNotFoundException>(() =>
            Graph.GetNodeAsync<Person>(createdBeforeFailure.Id, cancellationToken: cancellationToken));
        Assert.Equal(
            survivor.Id,
            (await Graph.GetNodeAsync<Person>(survivor.Id, cancellationToken: cancellationToken)).Id);
    }

    private async Task RunRawAsync(string cypher, object parameters, CancellationToken cancellationToken)
    {
        await using var transaction = await Graph.GetTransactionAsync(cancellationToken);
        await using var result = await ((AgeGraphTransaction)transaction).Runner
            .RunAsync(cypher, parameters, cancellationToken);
        _ = await result.SingleAsync(cancellationToken);
        await transaction.CommitAsync();
    }

    private async Task<AgeRecord> RunRawForRecordAsync(
        string cypher,
        object parameters,
        CancellationToken cancellationToken)
    {
        await using var transaction = await Graph.GetTransactionAsync(cancellationToken);
        await using var result = await ((AgeGraphTransaction)transaction).Runner
            .RunAsync(cypher, parameters, cancellationToken);
        var record = await result.SingleAsync(cancellationToken);
        await transaction.CommitAsync();
        return record;
    }

    private static async Task CreateWithSelectionsAsync(
        IGraphQueryable<Person> source,
        IRelationship relationship,
        IGraphQueryable<Person> target,
        RelationshipDirection direction,
        CancellationToken cancellationToken)
    {
        var provider = Assert.IsAssignableFrom<IGraphCommandProvider>(source.Provider);
        await provider.InWriteTransactionAsync(
            async (context, token) =>
            {
                var selectedSource = await SelectAsync(
                    context, source, GraphEndpointRole.Source, token).ConfigureAwait(false);
                var selectedTarget = await SelectAsync(
                    context, target, GraphEndpointRole.Target, token).ConfigureAwait(false);
                await context.CreateRelationshipAsync(
                    new SelectedGraphCommandEndpoint(selectedSource),
                    relationship,
                    new SelectedGraphCommandEndpoint(selectedTarget),
                    direction,
                    GraphRelationshipCreationMode.Standard,
                    token).ConfigureAwait(false);
                return true;
            },
            cancellationToken);
    }

    private static async Task CreateHybridAsync(
        IGraphQueryable<Person> selected,
        IRelationship relationship,
        INode newEndpoint,
        bool newEndpointIsTarget,
        CancellationToken cancellationToken)
    {
        var provider = Assert.IsAssignableFrom<IGraphCommandProvider>(selected.Provider);
        await provider.InWriteTransactionAsync(
            async (context, token) =>
            {
                var role = newEndpointIsTarget ? GraphEndpointRole.Source : GraphEndpointRole.Target;
                var selectedElement = await SelectAsync(context, selected, role, token).ConfigureAwait(false);
                GraphCommandEndpoint source = newEndpointIsTarget
                    ? new SelectedGraphCommandEndpoint(selectedElement)
                    : new NewGraphCommandEndpoint(newEndpoint);
                GraphCommandEndpoint target = newEndpointIsTarget
                    ? new NewGraphCommandEndpoint(newEndpoint)
                    : new SelectedGraphCommandEndpoint(selectedElement);
                await context.CreateRelationshipAsync(
                        source,
                        relationship,
                        target,
                        RelationshipDirection.Outgoing,
                        GraphRelationshipCreationMode.Standard,
                        token)
                    .ConfigureAwait(false);
                return true;
            },
            cancellationToken);
    }

    private async Task CreateAllNewAsync(
        INode source,
        IRelationship relationship,
        INode target,
        bool selfLoop,
        CancellationToken cancellationToken)
    {
        var provider = Assert.IsAssignableFrom<IGraphCommandProvider>(Graph.Nodes<Person>().Provider);
        await provider.InWriteTransactionAsync(
            async (context, token) =>
            {
                await context.CreateRelationshipAsync(
                    new NewGraphCommandEndpoint(source),
                    relationship,
                    new NewGraphCommandEndpoint(target),
                    RelationshipDirection.Outgoing,
                    selfLoop
                        ? GraphRelationshipCreationMode.SelfLoop
                        : GraphRelationshipCreationMode.Standard,
                    token).ConfigureAwait(false);
                return true;
            },
            cancellationToken);
    }

    private static Task<SelectedGraphElement> SelectAsync(
        IGraphCommandExecutionContext context,
        IGraphQueryable<Person> query,
        GraphEndpointRole role,
        CancellationToken cancellationToken) => GraphCommandSelection.SelectExactOneAsync(
            context,
            new GraphElementSelectionModel(
                GraphQueryModelBuilder.Build(query.Expression),
                GraphElementSelectionMode.ExactOne),
            query.Expression,
            role,
            cancellationToken);
}
