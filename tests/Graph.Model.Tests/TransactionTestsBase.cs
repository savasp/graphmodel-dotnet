// Copyright 2025 Savas Parastatidis
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//      https://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

namespace Cvoya.Graph.Model.Tests;

public abstract class TransactionTestsBase : ITestBase
{
    public abstract IGraph Graph { get; }

    [Fact]
    public async Task TransactionCommit_PersistsChanges()
    {
        var person = new Person { FirstName = "TransactionTest", LastName = "Commit" };

        await using var transaction = await Graph.GetTransactionAsync();
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

        await using var transaction = await Graph.GetTransactionAsync();
        await Graph.CreateNodeAsync(person, transaction, TestContext.Current.CancellationToken);
        await transaction.Rollback();

        await Assert.ThrowsAsync<GraphException>(
            () => Graph.GetNodeAsync<Person>(person.Id, null, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task TransactionDisposeWithoutCommit_RollsBack()
    {
        var person = new Person { FirstName = "TransactionTest", LastName = "AutoRollback" };

        await using (var transaction = await Graph.GetTransactionAsync())
        {
            await Graph.CreateNodeAsync(person, transaction, TestContext.Current.CancellationToken);
            // Dispose without commit should rollback
        }

        await Assert.ThrowsAsync<GraphException>(
            () => Graph.GetNodeAsync<Person>(person.Id, null, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task MultipleOperationsInTransaction_AllCommittedTogether()
    {
        var person1 = new Person { FirstName = "Transaction", LastName = "Person1" };
        var person2 = new Person { FirstName = "Transaction", LastName = "Person2" };
        var relationship = new Friend(person1.Id, person2.Id) { Since = DateTime.UtcNow };

        await using var transaction = await Graph.GetTransactionAsync();

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

        await using var transaction = await Graph.GetTransactionAsync();
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

        await using var transaction = await Graph.GetTransactionAsync();
        await Graph.DeleteNodeAsync(person.Id, false, transaction, TestContext.Current.CancellationToken);
        await transaction.CommitAsync();

        await Assert.ThrowsAsync<GraphException>(
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

        await using var transaction = await Graph.GetTransactionAsync();
        await Graph.DeleteNodeAsync(person1.Id, true, transaction, TestContext.Current.CancellationToken);
        await transaction.CommitAsync();

        await Assert.ThrowsAsync<GraphException>(
            () => Graph.GetNodeAsync<Person>(person1.Id, null, TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<GraphException>(
            () => Graph.GetRelationshipAsync<Friend>(relationship.Id, null, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task TransactionPartialFailure_RollsBackAll()
    {
        var person1 = new Person { FirstName = "Valid", LastName = "Person" };
        var person2 = new Person { FirstName = "Another", LastName = "Person" };

        await using var transaction = await Graph.GetTransactionAsync();

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
            await transaction.Rollback();
        }

        // Neither person should exist
        await Assert.ThrowsAsync<GraphException>(
            () => Graph.GetNodeAsync<Person>(person1.Id, null, TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<GraphException>(
            () => Graph.GetNodeAsync<Person>(person2.Id, null, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task MultipleQueriesInTransaction_UseCorrectContext()
    {
        var person1 = new Person { FirstName = "Query", LastName = "Person1" };
        var person2 = new Person { FirstName = "Query", LastName = "Person2" };

        await using var transaction = await Graph.GetTransactionAsync();

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

        await using var transaction = await Graph.GetTransactionAsync();
        await Graph.CreateNodeAsync(person, transaction, TestContext.Current.CancellationToken);

        // Outside the transaction, the node should not be visible
        await Assert.ThrowsAsync<GraphException>(
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
        var transaction = await Graph.GetTransactionAsync();
        var person = new Person { FirstName = "DoubleCommit", LastName = "Test" };

        await Graph.CreateNodeAsync(person, transaction, TestContext.Current.CancellationToken);
        await transaction.CommitAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => transaction.CommitAsync());

        await transaction.DisposeAsync();
    }

    [Fact]
    public async Task CommitAfterRollback_ThrowsException()
    {
        var transaction = await Graph.GetTransactionAsync();
        var person = new Person { FirstName = "CommitAfterRollback", LastName = "Test" };

        await Graph.CreateNodeAsync(person, transaction, TestContext.Current.CancellationToken);
        await transaction.Rollback();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => transaction.CommitAsync());

        await transaction.DisposeAsync();
    }

    [Fact]
    public async Task RollbackAfterCommit_ThrowsException()
    {
        var transaction = await Graph.GetTransactionAsync();
        var person = new Person { FirstName = "RollbackAfterCommit", LastName = "Test" };

        await Graph.CreateNodeAsync(person, transaction, TestContext.Current.CancellationToken);
        await transaction.CommitAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => transaction.Rollback());

        await transaction.DisposeAsync();
    }
}