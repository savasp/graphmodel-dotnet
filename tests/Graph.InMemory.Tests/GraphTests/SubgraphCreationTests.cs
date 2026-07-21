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
            Code = "duplicate",
        };
        await Graph.CreateRelationshipAsync(
            Graph.Nodes<Person>().Where(person => person.TestKey == existingSource.TestKey),
            existingRelationship,
            Graph.Nodes<Person>().Where(person => person.TestKey == existingTarget.TestKey),
            cancellationToken: TestContext.Current.CancellationToken);

        var source = new Person { FirstName = "New source" };
        var target = new Person { FirstName = "New target" };
        var duplicateRelationship = new UniqueSubgraphRelationship
        {
            Code = existingRelationship.Code,
        };

        await using var transaction = await Graph.GetTransactionAsync(TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<GraphException>(async () =>
            await Graph.CreateAsync(
                source,
                duplicateRelationship,
                target,
                RelationshipDirection.Outgoing,
                transaction,
                TestContext.Current.CancellationToken));

        await transaction.CommitAsync();

        Assert.Null(await Graph.Nodes<Person>().Where(person => person.TestKey == source.TestKey)
            .SingleOrDefaultAsync(TestContext.Current.CancellationToken));
        Assert.Null(await Graph.Nodes<Person>().Where(person => person.TestKey == target.TestKey)
            .SingleOrDefaultAsync(TestContext.Current.CancellationToken));
    }
}

[Relationship(Label = "UNIQUE_SUBGRAPH_RELATIONSHIP")]
internal sealed record UniqueSubgraphRelationship : Relationship
{
    [Property(IsUnique = true)]
    public string Code { get; init; } = string.Empty;
}
