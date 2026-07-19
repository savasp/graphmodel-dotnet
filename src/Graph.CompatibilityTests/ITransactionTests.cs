// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.CompatibilityTests;

// Every test here drives an explicit, provider-managed transaction (IGraphTransaction), so the
// whole interface is gated on the Transactions capability - a provider that does not declare it
// skips all of them.
[RequiresCapability(GraphCapability.Transactions)]
public interface ITransactionTests : IGraphTest
{
    [Fact]
    public async Task TransactionCommit_PersistsChanges()
    {
        var person = new Person { FirstName = "TransactionTest", LastName = "Commit" };

        await using var transaction = await Graph.GetTransactionAsync(TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(person, transaction, TestContext.Current.CancellationToken);
        await transaction.CommitAsync();

        var retrieved = await Graph.GetNodeAsync<Person>(person.Id, null, TestContext.Current.CancellationToken);
        Assert.Equal("TransactionTest", retrieved.FirstName);
        Assert.Equal("Commit", retrieved.LastName);
    }

    [Fact]
    public async Task TransactionRollback_DiscardsChanges()
    {
        var person = new Person { FirstName = "TransactionTest", LastName = "Rollback" };

        await using var transaction = await Graph.GetTransactionAsync(TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(person, transaction, TestContext.Current.CancellationToken);
        await transaction.RollbackAsync();

        await Assert.ThrowsAsync<EntityNotFoundException>(
            () => Graph.GetNodeAsync<Person>(person.Id, null, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task TransactionDisposeWithoutCommit_RollsBack()
    {
        var person = new Person { FirstName = "TransactionTest", LastName = "AutoRollback" };

        await using (var transaction = await Graph.GetTransactionAsync(TestContext.Current.CancellationToken))
        {
            await Graph.CreateNodeAsync(person, transaction, TestContext.Current.CancellationToken);
            // Dispose without commit should rollback
        }

        await Assert.ThrowsAsync<EntityNotFoundException>(
            () => Graph.GetNodeAsync<Person>(person.Id, null, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task MultipleOperationsInTransaction_AllCommittedTogether()
    {
        var person1 = new Person { FirstName = "Transaction", LastName = "Person1" };
        var person2 = new Person { FirstName = "Transaction", LastName = "Person2" };
        var relationship = new Friend(person1.Id, person2.Id) { Since = DateTime.UtcNow };

        await using var transaction = await Graph.GetTransactionAsync(TestContext.Current.CancellationToken);

        await Graph.CreateNodeAsync(person1, transaction, TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(person2, transaction, TestContext.Current.CancellationToken);
        await Graph.CreateRelationshipAsync(relationship, transaction, TestContext.Current.CancellationToken);

        await transaction.CommitAsync();

        var retrievedPerson1 = await Graph.GetNodeAsync<Person>(person1.Id, null, TestContext.Current.CancellationToken);
        var retrievedPerson2 = await Graph.GetNodeAsync<Person>(person2.Id, null, TestContext.Current.CancellationToken);
        var retrievedRelationship = await Graph.GetRelationshipAsync<Friend>(relationship.Id, null, TestContext.Current.CancellationToken);

        Assert.Equal("Person1", retrievedPerson1.LastName);
        Assert.Equal("Person2", retrievedPerson2.LastName);
        Assert.Equal(person1.Id, retrievedRelationship.StartNodeId);
        Assert.Equal(person2.Id, retrievedRelationship.EndNodeId);
    }

    [Fact]
    public async Task TransactionUpdate_CommitsChanges()
    {
        var person = new Person { FirstName = "Original", LastName = "Name" };
        await Graph.CreateNodeAsync(person, null, TestContext.Current.CancellationToken);

        await using var transaction = await Graph.GetTransactionAsync(TestContext.Current.CancellationToken);
        person.FirstName = "Updated";
        person.LastName = "InTransaction";
        await Graph.UpdateNodeAsync(person, transaction, TestContext.Current.CancellationToken);
        await transaction.CommitAsync();

        var retrieved = await Graph.GetNodeAsync<Person>(person.Id, null, TestContext.Current.CancellationToken);
        Assert.Equal("Updated", retrieved.FirstName);
        Assert.Equal("InTransaction", retrieved.LastName);
    }

    [Fact]
    public async Task RelationshipTypeChange_DoesNotCorruptStagedUpdateAndRollbackRestoresOriginal()
    {
        var p1 = new Person { FirstName = "A" };
        var p2 = new Person { FirstName = "B" };
        await Graph.CreateNodeAsync(p1, null, TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(p2, null, TestContext.Current.CancellationToken);

        var originalSince = DateTime.UtcNow;
        var knows = new Knows(p1.Id, p2.Id) { Since = originalSince };
        await Graph.CreateRelationshipAsync(knows, null, TestContext.Current.CancellationToken);

        var stagedSince = originalSince.AddDays(1);
        await using var transaction = await Graph.GetTransactionAsync(TestContext.Current.CancellationToken);
        var stagedKnows = knows with { Since = stagedSince };
        await Graph.UpdateRelationshipAsync(stagedKnows, transaction, TestContext.Current.CancellationToken);

        var friend = new Friend(p1.Id, p2.Id)
        {
            Id = knows.Id,
            Direction = RelationshipDirection.Outgoing,
            Since = originalSince.AddDays(2)
        };
        var exception = await Assert.ThrowsAsync<GraphException>(() =>
            Graph.UpdateRelationshipAsync(friend, transaction, TestContext.Current.CancellationToken));

        Assert.StartsWith(
            "Relationship type or concrete CLR type cannot be changed on update; delete and recreate the relationship.",
            exception.Message);

        var fetchedInTransaction = await Graph.GetRelationshipAsync<Knows>(
            knows.Id,
            transaction,
            TestContext.Current.CancellationToken);
        Assert.Equal(Labels.GetLabelFromType(typeof(Knows)), fetchedInTransaction.Type);
        Assert.Equal(p1.Id, fetchedInTransaction.StartNodeId);
        Assert.Equal(p2.Id, fetchedInTransaction.EndNodeId);
        Assert.Equal(RelationshipDirection.Outgoing, fetchedInTransaction.Direction);
        Assert.Equal(stagedSince, fetchedInTransaction.Since);

        await transaction.RollbackAsync();

        var fetchedAfterRollback = await Graph.GetRelationshipAsync<Knows>(
            knows.Id,
            null,
            TestContext.Current.CancellationToken);
        Assert.Equal(Labels.GetLabelFromType(typeof(Knows)), fetchedAfterRollback.Type);
        Assert.Equal(p1.Id, fetchedAfterRollback.StartNodeId);
        Assert.Equal(p2.Id, fetchedAfterRollback.EndNodeId);
        Assert.Equal(RelationshipDirection.Outgoing, fetchedAfterRollback.Direction);
        Assert.Equal(originalSince, fetchedAfterRollback.Since);
    }

    [Fact]
    public async Task TransactionDelete_CommitsChanges()
    {
        var person = new Person { FirstName = "ToDelete", LastName = "InTransaction" };
        await Graph.CreateNodeAsync(person, null, TestContext.Current.CancellationToken);

        await using var transaction = await Graph.GetTransactionAsync(TestContext.Current.CancellationToken);
        await Graph.DeleteNodeAsync(person.Id, false, transaction, TestContext.Current.CancellationToken);
        await transaction.CommitAsync();

        await Assert.ThrowsAsync<EntityNotFoundException>(
            () => Graph.GetNodeAsync<Person>(person.Id, null, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task TransactionCascadeDelete_CommitsChanges()
    {
        var person1 = new Person { FirstName = "Person", LastName = "One" };
        var person2 = new Person { FirstName = "Person", LastName = "Two" };
        await Graph.CreateNodeAsync(person1, null, TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(person2, null, TestContext.Current.CancellationToken);

        var relationship = new Friend(person1.Id, person2.Id);
        await Graph.CreateRelationshipAsync(relationship, null, TestContext.Current.CancellationToken);

        await using var transaction = await Graph.GetTransactionAsync(TestContext.Current.CancellationToken);
        await Graph.DeleteNodeAsync(person1.Id, true, transaction, TestContext.Current.CancellationToken);
        await transaction.CommitAsync();

        await Assert.ThrowsAsync<EntityNotFoundException>(
            () => Graph.GetNodeAsync<Person>(person1.Id, null, TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<EntityNotFoundException>(
            () => Graph.GetRelationshipAsync<Friend>(relationship.Id, null, TestContext.Current.CancellationToken));

        var remainingPerson = await Graph.GetNodeAsync<Person>(person2.Id, null, TestContext.Current.CancellationToken);
        Assert.Equal(person2.Id, remainingPerson.Id);
    }

    [Fact]
    public async Task TransactionPartialFailure_RollsBackAll()
    {
        var person1 = new Person { FirstName = "Valid", LastName = "Person" };
        var person2 = new Person { FirstName = "Another", LastName = "Person" };

        await using var transaction = await Graph.GetTransactionAsync(TestContext.Current.CancellationToken);

        await Graph.CreateNodeAsync(person1, transaction, TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(person2, transaction, TestContext.Current.CancellationToken);

        // Try to create a duplicate (this should cause the transaction to fail)
        try
        {
            await Graph.CreateNodeAsync(person1, transaction, TestContext.Current.CancellationToken);
            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
        }

        // Neither person should exist
        await Assert.ThrowsAsync<EntityNotFoundException>(
            () => Graph.GetNodeAsync<Person>(person1.Id, null, TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<EntityNotFoundException>(
            () => Graph.GetNodeAsync<Person>(person2.Id, null, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task MultipleQueriesInTransaction_UseCorrectContext()
    {
        var person1 = new Person { FirstName = "Query", LastName = "Person1" };
        var person2 = new Person { FirstName = "Query", LastName = "Person2" };

        await using var transaction = await Graph.GetTransactionAsync(TestContext.Current.CancellationToken);

        await Graph.CreateNodeAsync(person1, transaction, TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(person2, transaction, TestContext.Current.CancellationToken);

        var queryResults = await Graph.Nodes<Person>(transaction)
            .Where(p => p.FirstName == "Query")
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, queryResults.Count);
        Assert.Contains(queryResults, p => p.LastName == "Person1");
        Assert.Contains(queryResults, p => p.LastName == "Person2");

        await transaction.CommitAsync();
    }

    [Fact]
    public async Task TransactionIsolation_UncommittedChangesNotVisibleOutside()
    {
        var person = new Person { FirstName = "Isolation", LastName = "Test" };

        await using var transaction = await Graph.GetTransactionAsync(TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(person, transaction, TestContext.Current.CancellationToken);

        // Outside the transaction, the node should not be visible
        await Assert.ThrowsAsync<EntityNotFoundException>(
            () => Graph.GetNodeAsync<Person>(person.Id, null, TestContext.Current.CancellationToken));

        // But inside the transaction, it should be visible
        var retrievedInTransaction = await Graph.GetNodeAsync<Person>(person.Id, transaction, TestContext.Current.CancellationToken);
        Assert.Equal("Isolation", retrievedInTransaction.FirstName);

        await transaction.CommitAsync();

        // After commit, it should be visible outside
        var retrievedAfterCommit = await Graph.GetNodeAsync<Person>(person.Id, null, TestContext.Current.CancellationToken);
        Assert.Equal("Isolation", retrievedAfterCommit.FirstName);
    }

    [Fact]
    public async Task DoubleCommit_ThrowsException()
    {
        await using var transaction = await Graph.GetTransactionAsync(TestContext.Current.CancellationToken);
        var person = new Person { FirstName = "DoubleCommit", LastName = "Test" };

        await Graph.CreateNodeAsync(person, transaction, TestContext.Current.CancellationToken);
        await transaction.CommitAsync();

        await Assert.ThrowsAsync<GraphException>(
            () => transaction.CommitAsync());
    }

    [Fact]
    public async Task CommitAfterRollback_ThrowsException()
    {
        await using var transaction = await Graph.GetTransactionAsync(TestContext.Current.CancellationToken);
        var person = new Person { FirstName = "CommitAfterRollback", LastName = "Test" };

        await Graph.CreateNodeAsync(person, transaction, TestContext.Current.CancellationToken);
        await transaction.RollbackAsync();

        await Assert.ThrowsAsync<GraphException>(
            () => transaction.CommitAsync());
    }

    [Fact]
    public async Task RollbackAfterCommit_ThrowsException()
    {
        await using var transaction = await Graph.GetTransactionAsync(TestContext.Current.CancellationToken);
        var person = new Person { FirstName = "RollbackAfterCommit", LastName = "Test" };

        await Graph.CreateNodeAsync(person, transaction, TestContext.Current.CancellationToken);
        await transaction.CommitAsync();

        await Assert.ThrowsAsync<GraphException>(
            () => transaction.RollbackAsync());
    }

    // A transaction is owned by the graph/store that created it. Passing it to another store of
    // the SAME provider must be rejected by reference identity - before any schema work, query
    // execution, or mutation - and must leave the caller-owned transaction untouched (#366).

    [Fact]
    public async Task TransactionFromAnotherStoreOfSameProvider_IsRejectedByReadsAndQueries()
    {
        var person = new Person { FirstName = "CrossStore", LastName = "Read" };
        var related = new Person { FirstName = "CrossStore", LastName = "Related" };
        await Graph.CreateNodeAsync(person, null, TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(related, null, TestContext.Current.CancellationToken);

        var relationship = new Knows(person.Id, related.Id);
        await Graph.CreateRelationshipAsync(relationship, null, TestContext.Current.CancellationToken);

        var otherGraph = await Harness.GetGraphAsync(StoreIsolation.IndependentStore, TestContext.Current.CancellationToken);

        await using var transaction = await Graph.GetTransactionAsync(TestContext.Current.CancellationToken);

        await AssertForeignTransactionRejectedAsync(
            () => otherGraph.GetNodeAsync<Person>(person.Id, transaction, TestContext.Current.CancellationToken));
        await AssertForeignTransactionRejectedAsync(
            () => otherGraph.GetRelationshipAsync<Knows>(relationship.Id, transaction, TestContext.Current.CancellationToken));

        // Query roots reject at construction, before a provider can perform schema work or execute
        // the query. Cover every distinct public root; generic/non-generic search overloads share
        // implementation today but remain separate contract surfaces.
        AssertForeignTransactionRejected(() => otherGraph.Nodes<Person>(transaction));
        AssertForeignTransactionRejected(() => otherGraph.Relationships<Knows>(transaction));
        AssertForeignTransactionRejected(() => otherGraph.DynamicNodes(transaction));
        AssertForeignTransactionRejected(() => otherGraph.DynamicRelationships(transaction));
        AssertForeignTransactionRejected(() => otherGraph.Search("CrossStore", transaction));
        AssertForeignTransactionRejected(() => otherGraph.SearchNodes("CrossStore", transaction));
        AssertForeignTransactionRejected(() => otherGraph.SearchRelationships("CrossStore", transaction));
        AssertForeignTransactionRejected(() => otherGraph.SearchNodes<Person>("CrossStore", transaction));
        AssertForeignTransactionRejected(() => otherGraph.SearchRelationships<Knows>("CrossStore", transaction));

        // The rejected caller-owned transaction is untouched: still active and usable for reads
        // on the graph that created it, all the way through commit. Activity is asserted by using
        // the transaction - the public IGraphTransaction surface has no state to inspect, and a
        // rolled-back or disposed transaction would fail these calls.
        var readable = await Graph.GetNodeAsync<Person>(person.Id, transaction, TestContext.Current.CancellationToken);
        Assert.Equal("Read", readable.LastName);
        await transaction.CommitAsync();
    }

    [Fact]
    public async Task TransactionFromAnotherStoreOfSameProvider_IsRejectedByEveryCrudOperation()
    {
        var existing = new Person { FirstName = "CrossStore", LastName = "Baseline" };
        var relationshipSource = new Person { FirstName = "CrossStore", LastName = "RelationshipSource" };
        var relationshipTarget = new Person { FirstName = "CrossStore", LastName = "RelationshipTarget" };
        await Graph.CreateNodeAsync(existing, null, TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(relationshipSource, null, TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(relationshipTarget, null, TestContext.Current.CancellationToken);

        var originalSince = new DateTime(2025, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        var existingRelationship = new Knows(relationshipSource.Id, relationshipTarget.Id) { Since = originalSince };
        await Graph.CreateRelationshipAsync(existingRelationship, null, TestContext.Current.CancellationToken);

        var otherGraph = await Harness.GetGraphAsync(StoreIsolation.IndependentStore, TestContext.Current.CancellationToken);

        await using var transaction = await Graph.GetTransactionAsync(TestContext.Current.CancellationToken);

        var intruder = new Person { FirstName = "CrossStore", LastName = "Intruder" };
        var foreignRelationship = new Friend(relationshipSource.Id, relationshipTarget.Id);
        var subgraphSource = new Person { FirstName = "CrossStore", LastName = "SubgraphSource" };
        var subgraphTarget = new Person { FirstName = "CrossStore", LastName = "SubgraphTarget" };

        await AssertForeignTransactionRejectedAsync(
            () => otherGraph.CreateNodeAsync(intruder, transaction, TestContext.Current.CancellationToken));
        await AssertForeignTransactionRejectedAsync(
            () => otherGraph.CreateRelationshipAsync(foreignRelationship, transaction, TestContext.Current.CancellationToken));
        await AssertForeignTransactionRejectedAsync(
            () => otherGraph.CreateAsync(
                subgraphSource,
                new Knows(subgraphSource.Id, subgraphTarget.Id),
                subgraphTarget,
                null,
                transaction,
                TestContext.Current.CancellationToken));
        await AssertForeignTransactionRejectedAsync(
            () => otherGraph.UpdateNodeAsync(existing with { LastName = "Mutated" }, transaction, TestContext.Current.CancellationToken));
        await AssertForeignTransactionRejectedAsync(
            () => otherGraph.UpdateRelationshipAsync(
                existingRelationship with { Since = originalSince.AddDays(1) },
                transaction,
                TestContext.Current.CancellationToken));
        await AssertForeignTransactionRejectedAsync(
            () => otherGraph.DeleteNodeAsync(existing.Id, false, transaction, TestContext.Current.CancellationToken));
        await AssertForeignTransactionRejectedAsync(
            () => otherGraph.DeleteRelationshipAsync(existingRelationship.Id, transaction, TestContext.Current.CancellationToken));

        // Nothing executed against the transaction's own store: attempted entities are absent even
        // when reading through the transaction, and pre-existing entities are intact.
        await Assert.ThrowsAsync<EntityNotFoundException>(
            () => Graph.GetNodeAsync<Person>(intruder.Id, transaction, TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<EntityNotFoundException>(
            () => Graph.GetRelationshipAsync<Friend>(foreignRelationship.Id, transaction, TestContext.Current.CancellationToken));
        var unchanged = await Graph.GetNodeAsync<Person>(existing.Id, transaction, TestContext.Current.CancellationToken);
        Assert.Equal("Baseline", unchanged.LastName);
        var unchangedRelationship = await Graph.GetRelationshipAsync<Knows>(
            existingRelationship.Id,
            transaction,
            TestContext.Current.CancellationToken);
        Assert.Equal(originalSince, unchangedRelationship.Since);

        // Nothing landed in the other graph either. A harness may back both graphs with the same
        // database - this asserts the rejected write reached no store at all, not that the two are
        // isolated from each other.
        await Assert.ThrowsAsync<EntityNotFoundException>(
            () => otherGraph.GetNodeAsync<Person>(intruder.Id, null, TestContext.Current.CancellationToken));

        // The rejected caller-owned transaction remains fully usable with its owner graph: it
        // still accepts a write and commits it.
        var lateArrival = new Person { FirstName = "CrossStore", LastName = "Committed" };
        await Graph.CreateNodeAsync(lateArrival, transaction, TestContext.Current.CancellationToken);
        await transaction.CommitAsync();

        var persisted = await Graph.GetNodeAsync<Person>(lateArrival.Id, null, TestContext.Current.CancellationToken);
        Assert.Equal("Committed", persisted.LastName);
        var baseline = await Graph.GetNodeAsync<Person>(existing.Id, null, TestContext.Current.CancellationToken);
        Assert.Equal("Baseline", baseline.LastName);
    }

    private static void AssertForeignTransactionRejected(Action operation)
    {
        var exception = Assert.Throws<GraphException>(operation);
        AssertOwnershipMessage(exception);
    }

    private static async Task AssertForeignTransactionRejectedAsync(Func<Task> operation)
    {
        var exception = await Assert.ThrowsAsync<GraphException>(operation);
        AssertOwnershipMessage(exception);
    }

    private static void AssertOwnershipMessage(GraphException exception)
    {
        Assert.Contains("transaction", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("store", exception.Message, StringComparison.OrdinalIgnoreCase);
    }
}
