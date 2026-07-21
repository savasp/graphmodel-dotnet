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

        var retrieved = await Graph.FindNodeByTestKeyAsync<Person>(person.TestKey, null, TestContext.Current.CancellationToken);
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

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => Graph.FindNodeByTestKeyAsync<Person>(person.TestKey, null, TestContext.Current.CancellationToken));
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

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => Graph.FindNodeByTestKeyAsync<Person>(person.TestKey, null, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task MultipleOperationsInTransaction_AllCommittedTogether()
    {
        var person1 = new Person { FirstName = "Transaction", LastName = "Person1" };
        var person2 = new Person { FirstName = "Transaction", LastName = "Person2" };
        var relationship = new Friend { Since = DateTime.UtcNow };

        await using var transaction = await Graph.GetTransactionAsync(TestContext.Current.CancellationToken);

        await Graph.CreateNodeAsync(person1, transaction, TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(person2, transaction, TestContext.Current.CancellationToken);
        await Graph.ConnectAsync(
            person1,
            relationship,
            person2,
            transaction: transaction,
            cancellationToken: TestContext.Current.CancellationToken);

        await transaction.CommitAsync();

        var retrievedPerson1 = await Graph.FindNodeByTestKeyAsync<Person>(person1.TestKey, null, TestContext.Current.CancellationToken);
        var retrievedPerson2 = await Graph.FindNodeByTestKeyAsync<Person>(person2.TestKey, null, TestContext.Current.CancellationToken);
        var retrievedRelationship = await Graph.FindRelationshipByTestKeyAsync<Friend>(relationship.TestKey, null, TestContext.Current.CancellationToken);

        Assert.Equal("Person1", retrievedPerson1.LastName);
        Assert.Equal("Person2", retrievedPerson2.LastName);
        Assert.Equal(relationship.TestKey, retrievedRelationship.TestKey);
        var segment = await Graph.SelectNode(person1)
            .PathSegments<Person, Friend, Person>()
            .SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(person2.TestKey, segment.EndNode.TestKey);
        Assert.Equal(RelationshipDirection.Outgoing, segment.Direction);
    }

    [Fact]
    public async Task TransactionUpdate_CommitsChanges()
    {
        var person = new Person { FirstName = "Original", LastName = "Name" };
        await Graph.CreateNodeAsync(person, null, TestContext.Current.CancellationToken);

        await using var transaction = await Graph.GetTransactionAsync(TestContext.Current.CancellationToken);
        await Graph.SelectNode(person, transaction).UpdateAsync(
            setters => setters
                .SetProperty(candidate => candidate.FirstName, "Updated")
                .SetProperty(candidate => candidate.LastName, "InTransaction"),
            TestContext.Current.CancellationToken);
        await transaction.CommitAsync();

        var retrieved = await Graph.FindNodeByTestKeyAsync<Person>(person.TestKey, null, TestContext.Current.CancellationToken);
        Assert.Equal("Updated", retrieved.FirstName);
        Assert.Equal("InTransaction", retrieved.LastName);
    }

    [Fact]
    public async Task RelationshipMetadataUpdate_DoesNotCorruptStagedUpdateAndRollbackRestoresOriginal()
    {
        var p1 = new Person { FirstName = "A" };
        var p2 = new Person { FirstName = "B" };
        await Graph.CreateNodeAsync(p1, null, TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(p2, null, TestContext.Current.CancellationToken);

        var originalSince = DateTime.UtcNow;
        var knows = new Knows { Since = originalSince };
        await Graph.ConnectAsync(p1, knows, p2, cancellationToken: TestContext.Current.CancellationToken);

        var stagedSince = originalSince.AddDays(1);
        await using var transaction = await Graph.GetTransactionAsync(TestContext.Current.CancellationToken);
        var selected = Graph.SelectRelationship(knows, transaction);
        await selected.UpdateAsync(
            setters => setters.SetProperty(candidate => candidate.Since, stagedSince),
            TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<GraphQueryTranslationException>(() => selected.UpdateAsync(
            setters => setters.SetProperty(candidate => candidate.Type, "FRIENDOF"),
            TestContext.Current.CancellationToken));

        var fetchedInTransaction = await Graph.FindRelationshipByTestKeyAsync<Knows>(
            knows.TestKey,
            transaction,
            TestContext.Current.CancellationToken);
        Assert.Equal(Labels.GetLabelFromType(typeof(Knows)), fetchedInTransaction.Type);
        Assert.Equal(stagedSince, fetchedInTransaction.Since);

        await transaction.RollbackAsync();

        var fetchedAfterRollback = await Graph.FindRelationshipByTestKeyAsync<Knows>(
            knows.TestKey,
            null,
            TestContext.Current.CancellationToken);
        Assert.Equal(Labels.GetLabelFromType(typeof(Knows)), fetchedAfterRollback.Type);
        Assert.Equal(originalSince, fetchedAfterRollback.Since);
    }

    [Fact]
    public async Task TransactionDelete_CommitsChanges()
    {
        var person = new Person { FirstName = "ToDelete", LastName = "InTransaction" };
        await Graph.CreateNodeAsync(person, null, TestContext.Current.CancellationToken);

        await using var transaction = await Graph.GetTransactionAsync(TestContext.Current.CancellationToken);
        await Graph.SelectNode(person, transaction)
            .DeleteAsync(cancellationToken: TestContext.Current.CancellationToken);
        await transaction.CommitAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => Graph.FindNodeByTestKeyAsync<Person>(person.TestKey, null, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task TransactionCascadeDelete_CommitsChanges()
    {
        var person1 = new Person { FirstName = "Person", LastName = "One" };
        var person2 = new Person { FirstName = "Person", LastName = "Two" };
        await Graph.CreateNodeAsync(person1, null, TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(person2, null, TestContext.Current.CancellationToken);

        var relationship = new Friend();
        await Graph.ConnectAsync(person1, relationship, person2, cancellationToken: TestContext.Current.CancellationToken);

        await using var transaction = await Graph.GetTransactionAsync(TestContext.Current.CancellationToken);
        await Graph.SelectNode(person1, transaction)
            .DeleteAsync(cascadeDelete: true, cancellationToken: TestContext.Current.CancellationToken);
        await transaction.CommitAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => Graph.FindNodeByTestKeyAsync<Person>(person1.TestKey, null, TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => Graph.FindRelationshipByTestKeyAsync<Friend>(relationship.TestKey, null, TestContext.Current.CancellationToken));

        var remainingPerson = await Graph.FindNodeByTestKeyAsync<Person>(person2.TestKey, null, TestContext.Current.CancellationToken);
        Assert.Equal(person2.TestKey, remainingPerson.TestKey);
    }

    [Fact]
    public async Task TransactionPartialFailure_RollsBackAll()
    {
        var person1 = new Person { FirstName = "Valid", LastName = "Person" };
        var person2 = new Person { FirstName = "Another", LastName = "Person" };

        await using var transaction = await Graph.GetTransactionAsync(TestContext.Current.CancellationToken);

        await Graph.CreateNodeAsync(person1, transaction, TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(person2, transaction, TestContext.Current.CancellationToken);

        // Trigger model validation and roll the complete transaction back.
        try
        {
            await Graph.CreateNodeAsync(
                new IAttributeValidationTests.PersonWithValidationProperties(),
                transaction,
                TestContext.Current.CancellationToken);
            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
        }

        // Neither person should exist
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => Graph.FindNodeByTestKeyAsync<Person>(person1.TestKey, null, TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => Graph.FindNodeByTestKeyAsync<Person>(person2.TestKey, null, TestContext.Current.CancellationToken));
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
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => Graph.FindNodeByTestKeyAsync<Person>(person.TestKey, null, TestContext.Current.CancellationToken));

        // But inside the transaction, it should be visible
        var retrievedInTransaction = await Graph.FindNodeByTestKeyAsync<Person>(person.TestKey, transaction, TestContext.Current.CancellationToken);
        Assert.Equal("Isolation", retrievedInTransaction.FirstName);

        await transaction.CommitAsync();

        // After commit, it should be visible outside
        var retrievedAfterCommit = await Graph.FindNodeByTestKeyAsync<Person>(person.TestKey, null, TestContext.Current.CancellationToken);
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

        var relationship = new Knows();
        await Graph.ConnectAsync(person, relationship, related, cancellationToken: TestContext.Current.CancellationToken);

        var otherGraph = await Harness.GetGraphAsync(StoreIsolation.IndependentStore, TestContext.Current.CancellationToken);

        await using var transaction = await Graph.GetTransactionAsync(TestContext.Current.CancellationToken);

        await AssertForeignTransactionRejectedAsync(
            () => otherGraph.FindNodeByTestKeyAsync<Person>(person.TestKey, transaction, TestContext.Current.CancellationToken));
        await AssertForeignTransactionRejectedAsync(
            () => otherGraph.FindRelationshipByTestKeyAsync<Knows>(relationship.TestKey, transaction, TestContext.Current.CancellationToken));

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
        var readable = await Graph.FindNodeByTestKeyAsync<Person>(person.TestKey, transaction, TestContext.Current.CancellationToken);
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
        var existingRelationship = new Knows { Since = originalSince };
        await Graph.ConnectAsync(
            relationshipSource,
            existingRelationship,
            relationshipTarget,
            cancellationToken: TestContext.Current.CancellationToken);

        var otherGraph = await Harness.GetGraphAsync(StoreIsolation.IndependentStore, TestContext.Current.CancellationToken);

        await using var transaction = await Graph.GetTransactionAsync(TestContext.Current.CancellationToken);

        var intruder = new Person { FirstName = "CrossStore", LastName = "Intruder" };
        var foreignRelationship = new Friend();
        var subgraphSource = new Person { FirstName = "CrossStore", LastName = "SubgraphSource" };
        var subgraphTarget = new Person { FirstName = "CrossStore", LastName = "SubgraphTarget" };

        await AssertForeignTransactionRejectedAsync(
            () => otherGraph.CreateNodeAsync(intruder, transaction, TestContext.Current.CancellationToken));
        var selectionOwnershipException = await Assert.ThrowsAsync<GraphException>(
            () => otherGraph.CreateRelationshipAsync(
                Graph.SelectNode(relationshipSource, transaction),
                foreignRelationship,
                Graph.SelectNode(relationshipTarget, transaction),
                cancellationToken: TestContext.Current.CancellationToken));
        Assert.Contains("graph instance", selectionOwnershipException.Message, StringComparison.OrdinalIgnoreCase);
        await AssertForeignTransactionRejectedAsync(
            () => otherGraph.CreateAsync(
                subgraphSource,
                new Knows(),
                subgraphTarget,
                RelationshipDirection.Outgoing,
                transaction,
                TestContext.Current.CancellationToken));

        // Nothing executed against the transaction's own store: attempted entities are absent even
        // when reading through the transaction, and pre-existing entities are intact.
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => Graph.FindNodeByTestKeyAsync<Person>(intruder.TestKey, transaction, TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => Graph.FindRelationshipByTestKeyAsync<Friend>(foreignRelationship.TestKey, transaction, TestContext.Current.CancellationToken));
        var unchanged = await Graph.FindNodeByTestKeyAsync<Person>(existing.TestKey, transaction, TestContext.Current.CancellationToken);
        Assert.Equal("Baseline", unchanged.LastName);
        var unchangedRelationship = await Graph.FindRelationshipByTestKeyAsync<Knows>(
            existingRelationship.TestKey,
            transaction,
            TestContext.Current.CancellationToken);
        Assert.Equal(originalSince, unchangedRelationship.Since);

        // Nothing landed in the other graph either. A harness may back both graphs with the same
        // database - this asserts the rejected write reached no store at all, not that the two are
        // isolated from each other.
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => otherGraph.FindNodeByTestKeyAsync<Person>(intruder.TestKey, null, TestContext.Current.CancellationToken));

        // The rejected caller-owned transaction remains fully usable with its owner graph: it
        // still accepts a write and commits it.
        var lateArrival = new Person { FirstName = "CrossStore", LastName = "Committed" };
        await Graph.CreateNodeAsync(lateArrival, transaction, TestContext.Current.CancellationToken);
        await transaction.CommitAsync();

        var persisted = await Graph.FindNodeByTestKeyAsync<Person>(lateArrival.TestKey, null, TestContext.Current.CancellationToken);
        Assert.Equal("Committed", persisted.LastName);
        var baseline = await Graph.FindNodeByTestKeyAsync<Person>(existing.TestKey, null, TestContext.Current.CancellationToken);
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
