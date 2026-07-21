// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.InMemory.Tests.GraphTests;

using Cvoya.Graph.CompatibilityTests;

public class TransactionTests(InMemoryHarness harness) : InMemoryTest(harness), ITransactionTests
{
    [Fact]
    public async Task TransactionPartialFailure_RollsBackAll()
    {
        var first = new Person { FirstName = "Valid" };
        var second = new Person { FirstName = "Also valid" };
        await using var transaction = await Graph.GetTransactionAsync(TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(first, transaction, TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(second, transaction, TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<ArgumentException>(() => Graph.CreateNodeAsync(
            new Person { Id = string.Empty, FirstName = "Invalid" },
            transaction,
            TestContext.Current.CancellationToken));
        await transaction.RollbackAsync();

        await Assert.ThrowsAsync<EntityNotFoundException>(() => Graph.GetNodeAsync<Person>(
            first.Id,
            cancellationToken: TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<EntityNotFoundException>(() => Graph.GetNodeAsync<Person>(
            second.Id,
            cancellationToken: TestContext.Current.CancellationToken));
    }
}
