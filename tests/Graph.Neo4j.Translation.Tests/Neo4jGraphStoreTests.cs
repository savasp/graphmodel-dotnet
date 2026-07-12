// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Neo4j.Translation.Tests;

using System.Reflection;
using Cvoya.Graph.Neo4j;
using global::Neo4j.Driver;

public class Neo4jGraphStoreTests
{
    [Fact]
    public void Constructor_WithoutPasswordArgumentOrEnvironment_ThrowsClearException()
    {
        var originalPassword = Environment.GetEnvironmentVariable("NEO4J_PASSWORD");

        try
        {
            Environment.SetEnvironmentVariable("NEO4J_PASSWORD", null);

            var exception = Assert.Throws<InvalidOperationException>(
                () => new Neo4jGraphStore(
                    "bolt://localhost:7687",
                    "neo4j",
                    password: null));

            Assert.Contains("password argument or the NEO4J_PASSWORD environment variable", exception.Message);
        }
        finally
        {
            Environment.SetEnvironmentVariable("NEO4J_PASSWORD", originalPassword);
        }
    }

    [Fact]
    public async Task DisposeAsync_DisposesOwnedDriverOnce()
    {
        var (driver, tracker) = TrackingDriverProxy.Create();
        var store = new Neo4jGraphStore(driver, ownsDriver: true);

        await store.DisposeAsync();
        await store.DisposeAsync();

        Assert.Equal(1, tracker.DisposeAsyncCallCount);
        Assert.Equal(0, tracker.DisposeCallCount);
    }

    [Fact]
    public async Task DisposeAsync_DoesNotDisposeExternalDriver()
    {
        var (driver, tracker) = TrackingDriverProxy.Create();
        var store = new Neo4jGraphStore(driver);

        await store.DisposeAsync();
        await store.DisposeAsync();

        Assert.Equal(0, tracker.DisposeAsyncCallCount);
        Assert.Equal(0, tracker.DisposeCallCount);
    }

    [Fact]
    public async Task GraphUseAfterStoreDispose_ThrowsObjectDisposedException()
    {
        var (driver, _) = TrackingDriverProxy.Create();
        var store = new Neo4jGraphStore(driver);

        await store.DisposeAsync();

        await Assert.ThrowsAsync<ObjectDisposedException>(
            () => store.Graph.GetTransactionAsync(TestContext.Current.CancellationToken));
    }

    private class TrackingDriverProxy : DispatchProxy
    {
        public int DisposeAsyncCallCount { get; private set; }
        public int DisposeCallCount { get; private set; }

        public static (IDriver Driver, TrackingDriverProxy Tracker) Create()
        {
            var driver = DispatchProxy.Create<IDriver, TrackingDriverProxy>();
            return (driver, (TrackingDriverProxy)driver);
        }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            return targetMethod?.Name switch
            {
                nameof(IAsyncDisposable.DisposeAsync) => RecordDisposeAsync(),
                nameof(IDisposable.Dispose) => RecordDispose(),
                _ => throw new NotSupportedException($"{targetMethod?.Name} should not be called by this test.")
            };
        }

        private object RecordDisposeAsync()
        {
            DisposeAsyncCallCount++;
            return ValueTask.CompletedTask;
        }

        private object? RecordDispose()
        {
            DisposeCallCount++;
            return null;
        }
    }
}
