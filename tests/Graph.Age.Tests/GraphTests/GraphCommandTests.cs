// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Age.Tests.GraphTests;

using Cvoya.Graph.Age.Core;
using Cvoya.Graph.CompatibilityTests;
using Cvoya.Graph.Querying.Commands;

public sealed class GraphCommandTests(AgeHarness harness) : AgeTest(harness), IGraphCommandTests
{
    private static readonly TimeSpan LockWaitTimeout = TimeSpan.FromSeconds(30);

    [Fact]
    public async Task ConcurrentComplexOnlyUpdates_SerializeOnTheFrozenRoot()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var marker = $"command-complex-concurrent-{Guid.NewGuid():N}";
        var winnerName = marker + "-winner";
        var loserName = marker + "-loser";
        var node = new ComplexCommandNode { Group = marker };
        await Graph.CreateNodeAsync(node, cancellationToken: cancellationToken);

        await using var winner = await Graph.GetTransactionAsync(cancellationToken);
        var winnerAffected = await GraphCommandExtensions.UpdateAsync(
            Graph.Nodes<ComplexCommandNode>(winner).Where(candidate => candidate.Group == marker),
            setters => setters.SetProperty(
                candidate => candidate.Contact,
                new CommandContactValue { Name = winnerName }),
            cancellationToken);
        Assert.Equal(1, winnerAffected);

        await using var loser = await Graph.GetTransactionAsync(cancellationToken);
        var loserBackendPid = Assert.Single(await ((AgeGraphTransaction)loser).Runner.QueryScalarStringsAsync(
            "SELECT pg_backend_pid()::text",
            queryParameter: null,
            cancellationToken));
        var loserWrite = Task.Run(
            () => GraphCommandExtensions.UpdateAsync(
                Graph.Nodes<ComplexCommandNode>(loser).Where(candidate => candidate.Group == marker),
                setters => setters.SetProperty(
                    candidate => candidate.Contact,
                    new CommandContactValue { Name = loserName }),
                cancellationToken),
            cancellationToken);

        await WaitUntilBackendWaitsForLockAsync(loserBackendPid, loserWrite, cancellationToken);
        await winner.CommitAsync();
        Assert.Equal(1, await loserWrite);
        await loser.CommitAsync();

        var stored = await Graph.Nodes<ComplexCommandNode>()
            .Where(candidate => candidate.Group == marker)
            .SingleAsync(cancellationToken);
        Assert.Equal(loserName, stored.Contact?.Name);
        Assert.Equal(
            0,
            await Harness.CountNodesByPropertyAsync(
                Graph,
                Labels.GetLabelFromType(typeof(CommandContactValue)),
                nameof(CommandContactValue.Name),
                [winnerName],
                cancellationToken));
        Assert.Equal(
            1,
            await Harness.CountNodesByPropertyAsync(
                Graph,
                Labels.GetLabelFromType(typeof(CommandContactValue)),
                nameof(CommandContactValue.Name),
                [loserName],
                cancellationToken));
    }

    [Fact]
    public async Task FailedComplexUpdate_RestoresCallerSavepointAndLeavesTransactionUsable()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var label = $"CommandComplexFailure{Guid.NewGuid():N}";
        var node = new DynamicNode(
            [label],
            new Dictionary<string, object?>
            {
                ["profile"] = new Dictionary<string, object?> { ["name"] = "before" },
                ["status"] = "before",
            });
        await Graph.CreateNodeAsync(node, cancellationToken: cancellationToken);

        await using (var transaction = await Graph.GetTransactionAsync(cancellationToken))
        {
            var invalidReplacement = new Dictionary<string, object?>
            {
                ["name"] = "after",
                ["invalid\nproperty"] = "forces an AGE identifier failure after target mutation starts",
            };

            await Assert.ThrowsAsync<GraphException>(() => GraphCommandExtensions.UpdateAsync(
                Graph.DynamicNodes(transaction).OfLabel(label),
                setters => setters
                    .SetProperty(candidate => candidate.Properties["status"], "after")
                    .SetProperty(candidate => candidate.Properties["profile"], invalidReplacement),
                cancellationToken));

            var restored = await Graph.DynamicNodes(transaction)
                .OfLabel(label)
                .SingleAsync(cancellationToken);
            Assert.Equal("before", restored.Properties["status"]);
            var restoredProfile = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(
                restored.Properties["profile"]);
            Assert.Equal("before", restoredProfile["name"]);

            var affected = await GraphCommandExtensions.UpdateAsync(
                Graph.DynamicNodes(transaction).OfLabel(label),
                setters => setters.SetProperty(candidate => candidate.Properties["status"], "survived"),
                cancellationToken);
            Assert.Equal(1, affected);
            await transaction.CommitAsync();
        }

        var committed = await Graph.DynamicNodes()
            .OfLabel(label)
            .SingleAsync(cancellationToken);
        Assert.Equal("survived", committed.Properties["status"]);
        var committedProfile = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(
            committed.Properties["profile"]);
        Assert.Equal("before", committedProfile["name"]);
    }

    private async Task WaitUntilBackendWaitsForLockAsync(
        string backendPid,
        Task competingWrite,
        CancellationToken cancellationToken)
    {
        var deadline = DateTimeOffset.UtcNow + LockWaitTimeout;
        while (DateTimeOffset.UtcNow < deadline)
        {
            await using var observer = await Graph.GetTransactionAsync(cancellationToken);
            var waitEventTypes = await ((AgeGraphTransaction)observer).Runner.QueryScalarStringsAsync(
                "SELECT COALESCE(wait_event_type, '') FROM pg_stat_activity WHERE pid = @query::integer",
                backendPid,
                cancellationToken);
            await observer.RollbackAsync();

            if (waitEventTypes.Contains("Lock", StringComparer.Ordinal))
            {
                return;
            }

            if (competingWrite.IsCompleted)
            {
                await competingWrite;
                Assert.Fail(
                    "The competing complex-only update completed before the first transaction " +
                    "released its frozen-root lock.");
            }

            await Task.Delay(TimeSpan.FromMilliseconds(25), cancellationToken);
        }

        Assert.Fail(
            "The competing complex-only update never waited on the frozen root row lock.");
    }
}
