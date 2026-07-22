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

        await Assert.ThrowsAsync<GraphException>(() => Graph.CreateNodeAsync(
            new IAttributeValidationTests.PersonWithValidationProperties(),
            transaction,
            TestContext.Current.CancellationToken));
        await transaction.RollbackAsync();

        Assert.Null(await Graph.Nodes<Person>().Where(person => person.TestKey == first.TestKey)
            .SingleOrDefaultAsync(TestContext.Current.CancellationToken));
        Assert.Null(await Graph.Nodes<Person>().Where(person => person.TestKey == second.TestKey)
            .SingleOrDefaultAsync(TestContext.Current.CancellationToken));
    }
}
