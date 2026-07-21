// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.InMemory.Tests.GraphTests;

using Cvoya.Graph.CompatibilityTests;

public class SubgraphCreationTests(InMemoryHarness harness) : InMemoryTest(harness), ISubgraphCreationTests
{
    [Fact]
    public async Task CreateSubgraph_LateConstraintFailureDoesNotMutateCallerTransaction()
    {
        var existingSource = new Person { FirstName = "Existing source" };
        var existingTarget = new Person { FirstName = "Existing target" };
        await Graph.CreateNodeAsync(existingSource, null, TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(existingTarget, null, TestContext.Current.CancellationToken);

        var existingRelationship = new UniqueSubgraphRelationship
        {
            StartNodeId = existingSource.Id,
            EndNodeId = existingTarget.Id,
            Code = "duplicate",
        };
        await Graph.CreateRelationshipAsync(existingRelationship, null, TestContext.Current.CancellationToken);

        var source = new Person { FirstName = "New source" };
        var target = new Person { FirstName = "New target" };
        var duplicateRelationship = new UniqueSubgraphRelationship
        {
            StartNodeId = source.Id,
            EndNodeId = target.Id,
            Code = existingRelationship.Code,
        };

        await using var transaction = await Graph.GetTransactionAsync(TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<GraphException>(async () =>
            await Graph.CreateAsync(
                source,
                duplicateRelationship,
                target,
                null,
                transaction,
                TestContext.Current.CancellationToken));

        await transaction.CommitAsync();

        await Assert.ThrowsAsync<EntityNotFoundException>(async () =>
            await Graph.GetNodeAsync<Person>(source.Id, null, TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<EntityNotFoundException>(async () =>
            await Graph.GetNodeAsync<Person>(target.Id, null, TestContext.Current.CancellationToken));
    }
}

[Relationship(Label = "UNIQUE_SUBGRAPH_RELATIONSHIP")]
internal sealed record UniqueSubgraphRelationship : Relationship
{
    public UniqueSubgraphRelationship() : base(string.Empty, string.Empty) { }

    [Property(IsUnique = true)]
    public string Code { get; init; } = string.Empty;
}
