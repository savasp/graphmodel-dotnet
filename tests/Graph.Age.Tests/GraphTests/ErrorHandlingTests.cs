// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Age.Tests.GraphTests;

using Cvoya.Graph.CompatibilityTests;

public class ErrorHandlingTests(AgeHarness harness) : AgeTest(harness), IErrorHandlingTests
{
    async Task IErrorHandlingTests.CreateDuplicateNode_SameId_ThrowsException()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var node = new IErrorHandlingTests.TestNode { Name = "First" };
        await Graph.CreateNodeAsync(node, cancellationToken: cancellationToken);

        await Graph.CreateNodeAsync(
            node with { Name = "Duplicate" },
            cancellationToken: cancellationToken);

        Assert.Equal(2, await Graph.Nodes<IErrorHandlingTests.TestNode>()
            .Where(candidate => candidate.Id == node.Id)
            .CountAsync(cancellationToken));
    }
}
