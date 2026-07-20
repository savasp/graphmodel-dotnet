// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Age.Tests;

using System.Collections.Concurrent;

public sealed class AgeGraphCleanupFixtureTests
{
    [Fact]
    public async Task DisposeAsync_DropsOnlyGraphsWhoseCreationSucceeded()
    {
        var dropped = new List<string>();
        var fixture = new AgeGraphCleanupFixture((graphName, _) =>
        {
            dropped.Add(graphName);
            return Task.CompletedTask;
        });
        var created = fixture.CreateGraphName("cvoya_created");
        _ = fixture.CreateGraphName("cvoya_partial_setup");
        fixture.MarkGraphCreated(created);

        await fixture.DisposeAsync();

        Assert.Equal([created], dropped);
    }

    [Fact]
    public async Task DisposeAsync_AfterSetupFailure_StillDropsTheCreatedGraph()
    {
        string? dropped = null;
        var fixture = new AgeGraphCleanupFixture((graphName, _) =>
        {
            dropped = graphName;
            return Task.CompletedTask;
        });
        var graphName = fixture.CreateGraphName("cvoya_failed_setup");

        var setupFailure = await Record.ExceptionAsync(async () =>
        {
            fixture.MarkGraphCreated(graphName);
            await Task.Yield();
            throw new InvalidOperationException("Setup failed after graph creation.");
        });
        await fixture.DisposeAsync();

        Assert.Equal("Setup failed after graph creation.", setupFailure?.Message);
        Assert.Equal(graphName, dropped);
    }

    [Fact]
    public async Task DisposeAsync_CalledRepeatedly_DropsTheGraphOnce()
    {
        var dropCount = 0;
        var fixture = new AgeGraphCleanupFixture((_, _) =>
        {
            Interlocked.Increment(ref dropCount);
            return Task.CompletedTask;
        });
        var graphName = fixture.CreateGraphName("cvoya_repeated_cleanup");
        fixture.MarkGraphCreated(graphName);

        await fixture.DisposeAsync();
        await fixture.DisposeAsync();

        Assert.Equal(1, dropCount);
    }

    [Fact]
    public async Task ParallelFixtures_DropOnlyTheirOwnGraphs()
    {
        var firstDropped = new ConcurrentBag<string>();
        var secondDropped = new ConcurrentBag<string>();
        var first = new AgeGraphCleanupFixture((graphName, _) =>
        {
            firstDropped.Add(graphName);
            return Task.CompletedTask;
        });
        var second = new AgeGraphCleanupFixture((graphName, _) =>
        {
            secondDropped.Add(graphName);
            return Task.CompletedTask;
        });
        var firstGraph = first.CreateGraphName("cvoya_parallel");
        var secondGraph = second.CreateGraphName("cvoya_parallel");
        first.MarkGraphCreated(firstGraph);
        second.MarkGraphCreated(secondGraph);

        await Task.WhenAll(first.DisposeAsync().AsTask(), second.DisposeAsync().AsTask());

        Assert.Equal([firstGraph], firstDropped);
        Assert.Equal([secondGraph], secondDropped);
    }

    [Fact]
    public async Task CleanupFailure_IsSeparateFromPrimaryFailureAndNamesEveryLeakedGraph()
    {
        var attempted = new List<string>();
        var fixture = new AgeGraphCleanupFixture((graphName, _) =>
        {
            attempted.Add(graphName);
            throw new InvalidOperationException("Database cleanup failed.");
        });
        var firstGraph = fixture.CreateGraphName("cvoya_cleanup_failure");
        var secondGraph = fixture.CreateGraphName("cvoya_cleanup_failure");
        fixture.MarkGraphCreated(firstGraph);
        fixture.MarkGraphCreated(secondGraph);

        var primaryFailure = await Record.ExceptionAsync(async () =>
        {
            await Task.Yield();
            throw new InvalidOperationException("Primary test failure.");
        });
        var cleanupFailure = await Record.ExceptionAsync(() => fixture.DisposeAsync().AsTask());

        Assert.Equal("Primary test failure.", primaryFailure?.Message);
        var aggregate = Assert.IsType<AggregateException>(cleanupFailure);
        Assert.Contains(firstGraph, aggregate.ToString(), StringComparison.Ordinal);
        Assert.Contains(secondGraph, aggregate.ToString(), StringComparison.Ordinal);
        // Cleanup drops graphs in ordinal name order, which for GUID-suffixed names is
        // unrelated to creation order.
        string[] expectedAttempts = [firstGraph, secondGraph];
        Array.Sort(expectedAttempts, StringComparer.Ordinal);
        Assert.Equal(expectedAttempts, attempted);
    }
}
