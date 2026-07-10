// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.CompatibilityTests;

public interface ITransactionTests : IGraphModelTest
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
}
