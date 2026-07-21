// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Age.Tests.GraphTests;

using Cvoya.Graph.CompatibilityTests;

public class TransactionTests(AgeHarness harness) : AgeTest(harness), ITransactionTests
{
    async Task ITransactionTests.TransactionPartialFailure_RollsBackAll()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var first = new Person { FirstName = "Valid" };
        var second = new Person { FirstName = "Also valid" };

        await using var transaction = await Graph.GetTransactionAsync(cancellationToken);
        await Graph.CreateNodeAsync(first, transaction, cancellationToken);
        await Graph.CreateNodeAsync(second, transaction, cancellationToken);
        try
        {
            await Graph.CreateNodeAsync(
                new DynamicNode(["CvoyaNode"], new Dictionary<string, object?>()),
                transaction,
                cancellationToken);
            await transaction.CommitAsync();
        }
        catch (GraphException)
        {
            await transaction.RollbackAsync();
        }

        await Assert.ThrowsAsync<EntityNotFoundException>(() =>
            Graph.GetNodeAsync<Person>(first.Id, cancellationToken: cancellationToken));
        await Assert.ThrowsAsync<EntityNotFoundException>(() =>
            Graph.GetNodeAsync<Person>(second.Id, cancellationToken: cancellationToken));
    }
}
