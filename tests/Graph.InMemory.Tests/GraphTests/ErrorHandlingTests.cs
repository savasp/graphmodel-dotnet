// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.InMemory.Tests.GraphTests;

using Cvoya.Graph.CompatibilityTests;

public class ErrorHandlingTests(InMemoryHarness harness) : InMemoryTest(harness), IErrorHandlingTests
{
    // Replaces the transitional shared default until the coordinated contract switch updates all
    // providers: a plain Id is modeled data, not an implicit uniqueness constraint.
    [Fact]
    public async Task CreateDuplicateNode_SameId_ThrowsException()
    {
        var id = Guid.NewGuid().ToString("N");
        await Graph.CreateNodeAsync(
            new IErrorHandlingTests.TestNode { Id = id, Name = "First" },
            cancellationToken: TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(
            new IErrorHandlingTests.TestNode { Id = id, Name = "Second" },
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(
            2,
            await Graph.Nodes<IErrorHandlingTests.TestNode>()
                .Where(node => node.Id == id)
                .CountAsync(TestContext.Current.CancellationToken));
    }
}
