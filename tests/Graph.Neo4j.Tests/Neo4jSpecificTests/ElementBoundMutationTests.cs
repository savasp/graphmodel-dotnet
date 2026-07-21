// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Neo4j.Tests;

using Cvoya.Graph.CompatibilityTests;
using Cvoya.Graph.Neo4j.Core;
using Cvoya.Graph.Neo4j.Entities;
using Cvoya.Graph.Serialization;
using global::Neo4j.Driver;

/// <summary>
/// Exercises the element-bound (Neo4j elementId-addressed) update helpers added for #469:
/// <see cref="Neo4jNodeManager.UpdateByElementIdAsync"/>, <see cref="Neo4jRelationshipManager.UpdateByElementIdAsync"/>,
/// and the element-bound overloads on <see cref="ComplexPropertyManager"/>. These are persistence-prep
/// groundwork - not yet wired into <c>CypherMutationPlanner</c>'s live command path - so they are called
/// directly against a live transaction here rather than through the public LINQ command surface.
/// </summary>
public sealed class ElementBoundMutationTests(Neo4jHarness harness) : Neo4jTest(harness)
{
    [Fact]
    public async Task NodeManager_UpdateByElementIdAsync_UpdatesSimplePropertiesForTypedNode()
    {
        var person = new Person { FirstName = "before-element-bound" };
        await Graph.CreateNodeAsync(person, cancellationToken: TestContext.Current.CancellationToken);
        var neo4jGraph = Assert.IsType<Neo4jGraph>(Graph);

        await using var transaction = await Graph.GetTransactionAsync(TestContext.Current.CancellationToken);
        var neo4jTransaction = Assert.IsType<GraphTransaction>(transaction);
        var elementId = await GetElementIdAsync(
            neo4jTransaction,
            "MATCH (n {Id: $id}) RETURN elementId(n) AS elementId",
            new { id = person.Id });

        var updated = person with { FirstName = "after-element-bound" };
        var wasUpdated = await neo4jGraph.Context.NodeManager.UpdateByElementIdAsync(
            updated,
            elementId,
            neo4jTransaction,
            TestContext.Current.CancellationToken);

        Assert.True(wasUpdated);
        var record = await QuerySingleAsync(
            neo4jTransaction,
            "MATCH (n) WHERE elementId(n) = $elementId RETURN n.FirstName AS firstName",
            new { elementId });
        Assert.Equal("after-element-bound", record["firstName"].As<string>());

        await transaction.CommitAsync();
    }

    [Fact]
    public async Task NodeManager_UpdateByElementIdAsync_ReplacesLabelsAndPropertiesForDynamicNode()
    {
        var originalLabels = new[] { $"ElementBoundA{Guid.NewGuid():N}", $"ElementBoundB{Guid.NewGuid():N}", $"ElementBoundC{Guid.NewGuid():N}" };
        var marker = $"element-bound-{Guid.NewGuid():N}";
        var node = new DynamicNode(originalLabels, new Dictionary<string, object?> { ["marker"] = marker, ["rank"] = "before" });
        await Graph.CreateNodeAsync(node, cancellationToken: TestContext.Current.CancellationToken);
        var neo4jGraph = Assert.IsType<Neo4jGraph>(Graph);

        await using var transaction = await Graph.GetTransactionAsync(TestContext.Current.CancellationToken);
        var neo4jTransaction = Assert.IsType<GraphTransaction>(transaction);
        var elementId = await GetElementIdAsync(
            neo4jTransaction,
            $"MATCH (n:{originalLabels[0]}) WHERE n.marker = $marker RETURN elementId(n) AS elementId",
            new { marker });

        // A different, non-overlapping pair of labels - a regression of the REMOVE-clause
        // separator bug (#469 F2) would still remove exactly these two labels for ordinary label
        // names, but this pins the intended end state precisely rather than relying on that.
        var newLabels = new[] { $"ElementBoundD{Guid.NewGuid():N}", $"ElementBoundE{Guid.NewGuid():N}" };
        var updated = new DynamicNode(node.Id, newLabels, new Dictionary<string, object?> { ["marker"] = marker, ["rank"] = "after" });
        var wasUpdated = await neo4jGraph.Context.NodeManager.UpdateByElementIdAsync(
            updated,
            elementId,
            neo4jTransaction,
            TestContext.Current.CancellationToken);

        Assert.True(wasUpdated);
        var record = await QuerySingleAsync(
            neo4jTransaction,
            "MATCH (n) WHERE elementId(n) = $elementId RETURN labels(n) AS labels, n.rank AS rank",
            new { elementId });
        var labels = record["labels"].As<List<string>>();
        Assert.Equal(newLabels.OrderBy(label => label, StringComparer.Ordinal), labels.OrderBy(label => label, StringComparer.Ordinal));
        Assert.DoesNotContain(originalLabels[0], labels);
        Assert.DoesNotContain(originalLabels[1], labels);
        Assert.DoesNotContain(originalLabels[2], labels);
        Assert.Equal("after", record["rank"].As<string>());

        await transaction.CommitAsync();
    }

    [Fact]
    public async Task NodeManager_UpdateByElementIdAsync_NoMatchingElement_ReturnsFalseWithoutThrowing()
    {
        var neo4jGraph = Assert.IsType<Neo4jGraph>(Graph);
        // Direct manager access bypasses the CreateNodeAsync/CreateRelationshipAsync entry points
        // that normally trigger lazy schema initialization first.
        await neo4jGraph.Context.SchemaManager.InitializeSchemaAsync(TestContext.Current.CancellationToken);
        await using var transaction = await Graph.GetTransactionAsync(TestContext.Current.CancellationToken);
        var neo4jTransaction = Assert.IsType<GraphTransaction>(transaction);

        var wasUpdated = await neo4jGraph.Context.NodeManager.UpdateByElementIdAsync(
            new Person { FirstName = "never-persisted" },
            "4:00000000-0000-0000-0000-000000000000:9999999",
            neo4jTransaction,
            TestContext.Current.CancellationToken);

        Assert.False(wasUpdated);
        await transaction.RollbackAsync();
    }

    [Fact]
    public async Task RelationshipManager_UpdateByElementIdAsync_UpdatesPropertiesByElementId()
    {
        var first = new Person { FirstName = "relationship-source" };
        var second = new Person { FirstName = "relationship-target" };
        var relationship = new Knows(first, second) { Since = DateTime.UnixEpoch };
        await Graph.CreateNodeAsync(first, cancellationToken: TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(second, cancellationToken: TestContext.Current.CancellationToken);
        await Graph.CreateRelationshipAsync(relationship, cancellationToken: TestContext.Current.CancellationToken);
        var neo4jGraph = Assert.IsType<Neo4jGraph>(Graph);

        await using var transaction = await Graph.GetTransactionAsync(TestContext.Current.CancellationToken);
        var neo4jTransaction = Assert.IsType<GraphTransaction>(transaction);
        var elementId = await GetElementIdAsync(
            neo4jTransaction,
            "MATCH ()-[r {Id: $id}]->() RETURN elementId(r) AS elementId",
            new { id = relationship.Id });

        var replacement = DateTime.UnixEpoch.AddDays(1);
        var updated = relationship with { Since = replacement };
        var wasUpdated = await neo4jGraph.Context.RelationshipManager.UpdateByElementIdAsync(
            updated,
            elementId,
            neo4jTransaction,
            TestContext.Current.CancellationToken);

        Assert.True(wasUpdated);
        var record = await QuerySingleAsync(
            neo4jTransaction,
            "MATCH ()-[r]->() WHERE elementId(r) = $elementId RETURN r.Since AS since",
            new { elementId });
        var storedSince = record["since"].As<ZonedDateTime>().ToDateTimeOffset().UtcDateTime;
        Assert.Equal(replacement, storedSince);

        await transaction.CommitAsync();
    }

    [Fact]
    public async Task RelationshipManager_UpdateByElementIdAsync_NoMatchingElement_ReturnsFalseWithoutThrowing()
    {
        var neo4jGraph = Assert.IsType<Neo4jGraph>(Graph);
        // Direct manager access bypasses the CreateNodeAsync/CreateRelationshipAsync entry points
        // that normally trigger lazy schema initialization first.
        await neo4jGraph.Context.SchemaManager.InitializeSchemaAsync(TestContext.Current.CancellationToken);
        await using var transaction = await Graph.GetTransactionAsync(TestContext.Current.CancellationToken);
        var neo4jTransaction = Assert.IsType<GraphTransaction>(transaction);

        var wasUpdated = await neo4jGraph.Context.RelationshipManager.UpdateByElementIdAsync(
            new Knows("missing-source", "missing-target"),
            "5:00000000-0000-0000-0000-000000000000:9999999",
            neo4jTransaction,
            TestContext.Current.CancellationToken);

        Assert.False(wasUpdated);
        await transaction.RollbackAsync();
    }

    [Fact]
    public async Task ComplexPropertyManager_CreateElementBoundComplexPropertiesAsync_CreatesValueNodeUnderParent()
    {
        var neo4jGraph = Assert.IsType<Neo4jGraph>(Graph);
        await using var transaction = await Graph.GetTransactionAsync(TestContext.Current.CancellationToken);
        var neo4jTransaction = Assert.IsType<GraphTransaction>(transaction);

        var marker = $"complex-create-{Guid.NewGuid():N}";
        var parentElementId = await GetElementIdAsync(
            neo4jTransaction,
            "CREATE (n:ElementBoundParent {marker: $marker}) RETURN elementId(n) AS elementId",
            new { marker });

        var seed = new PersonWithComplexProperty
        {
            FirstName = "complex-owner",
            Address = new AddressValue { Street = "First Street", City = "First City" }
        };
        var entity = new EntityFactory().Serialize(seed);
        var complexPropertyManager = new ComplexPropertyManager(neo4jGraph.Context);

        await complexPropertyManager.CreateElementBoundComplexPropertiesAsync(
            neo4jTransaction.Transaction,
            parentElementId,
            entity,
            TestContext.Current.CancellationToken);

        var record = await QuerySingleAsync(
            neo4jTransaction,
            $"""
            MATCH (parent)-[r]->(child)
            WHERE elementId(parent) = $parentElementId AND r.{ComplexPropertyStorage.RelationshipMarkerProperty} = true
            RETURN count(r) AS relCount, child.Street AS street, child.City AS city
            """,
            new { parentElementId });

        Assert.Equal(1, record["relCount"].As<int>());
        Assert.Equal("First Street", record["street"].As<string>());
        Assert.Equal("First City", record["city"].As<string>());

        await transaction.RollbackAsync();
    }

    [Fact]
    public async Task ComplexPropertyManager_UpdateElementBoundComplexPropertiesAsync_ReplacesExistingValueNode()
    {
        var neo4jGraph = Assert.IsType<Neo4jGraph>(Graph);
        await using var transaction = await Graph.GetTransactionAsync(TestContext.Current.CancellationToken);
        var neo4jTransaction = Assert.IsType<GraphTransaction>(transaction);
        var complexPropertyManager = new ComplexPropertyManager(neo4jGraph.Context);

        var marker = $"complex-update-{Guid.NewGuid():N}";
        var parentElementId = await GetElementIdAsync(
            neo4jTransaction,
            "CREATE (n:ElementBoundParent {marker: $marker}) RETURN elementId(n) AS elementId",
            new { marker });
        var original = new PersonWithComplexProperty
        {
            FirstName = "complex-owner",
            Address = new AddressValue { Street = "First Street", City = "First City" }
        };
        await complexPropertyManager.CreateElementBoundComplexPropertiesAsync(
            neo4jTransaction.Transaction,
            parentElementId,
            new EntityFactory().Serialize(original),
            TestContext.Current.CancellationToken);

        var replacement = new PersonWithComplexProperty
        {
            FirstName = "complex-owner",
            Address = new AddressValue { Street = "Second Street", City = "Second City" }
        };
        await complexPropertyManager.UpdateElementBoundComplexPropertiesAsync(
            neo4jTransaction.Transaction,
            parentElementId,
            new EntityFactory().Serialize(replacement),
            TestContext.Current.CancellationToken);

        var record = await QuerySingleAsync(
            neo4jTransaction,
            $"""
            MATCH (parent)-[r]->(child)
            WHERE elementId(parent) = $parentElementId AND r.{ComplexPropertyStorage.RelationshipMarkerProperty} = true
            RETURN count(r) AS relCount, child.Street AS street, child.City AS city
            """,
            new { parentElementId });

        Assert.Equal(1, record["relCount"].As<int>());
        Assert.Equal("Second Street", record["street"].As<string>());
        Assert.Equal("Second City", record["city"].As<string>());

        await transaction.RollbackAsync();
    }

    private static async Task<string> GetElementIdAsync(GraphTransaction transaction, string cypher, object parameters)
    {
        var record = await QuerySingleAsync(transaction, cypher, parameters);
        return record["elementId"].As<string>();
    }

    private static async Task<IRecord> QuerySingleAsync(GraphTransaction transaction, string cypher, object? parameters)
    {
        var result = parameters is null
            ? await transaction.Transaction.RunAsync(cypher)
            : await transaction.Transaction.RunAsync(cypher, parameters);
        return await result.SingleAsync(TestContext.Current.CancellationToken);
    }
}
