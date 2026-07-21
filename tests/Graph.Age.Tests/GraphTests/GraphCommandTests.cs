// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Age.Tests.GraphTests;

using Cvoya.Graph.CompatibilityTests;
using Cvoya.Graph.Querying.Commands;

public sealed class GraphCommandTests(AgeHarness harness) : AgeTest(harness), IGraphCommandTests
{
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
                Graph.DynamicNodes(transaction).Where(candidate => candidate.Id == node.Id),
                setters => setters
                    .SetProperty(candidate => candidate.Properties["status"], "after")
                    .SetProperty(candidate => candidate.Properties["profile"], invalidReplacement),
                cancellationToken));

            var restored = await Graph.GetDynamicNodeAsync(node.Id, transaction, cancellationToken);
            Assert.Equal("before", restored.Properties["status"]);
            var restoredProfile = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(
                restored.Properties["profile"]);
            Assert.Equal("before", restoredProfile["name"]);

            var affected = await GraphCommandExtensions.UpdateAsync(
                Graph.DynamicNodes(transaction).Where(candidate => candidate.Id == node.Id),
                setters => setters.SetProperty(candidate => candidate.Properties["status"], "survived"),
                cancellationToken);
            Assert.Equal(1, affected);
            await transaction.CommitAsync();
        }

        var committed = await Graph.GetDynamicNodeAsync(node.Id, cancellationToken: cancellationToken);
        Assert.Equal("survived", committed.Properties["status"]);
        var committedProfile = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(
            committed.Properties["profile"]);
        Assert.Equal("before", committedProfile["name"]);
    }
}
