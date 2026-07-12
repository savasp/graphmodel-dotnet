// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Core.Tests;

using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using Cvoya.Graph.Querying.Linq;

public sealed class GraphQueryProviderBaseTests
{
    [Fact]
    public async Task AbandonedOwnedStreamRollsBackAndDisposesTransaction()
    {
        var transaction = new RecordingTransaction();
        var provider = new RecordingProvider(transaction, [1, 2]);
        var cancellationToken = TestContext.Current.CancellationToken;
        var enumerator = provider.StreamAsync<int>(Expression.Constant(0), cancellationToken)
            .GetAsyncEnumerator(cancellationToken);

        Assert.True(await enumerator.MoveNextAsync());
        await enumerator.DisposeAsync();

        Assert.Equal(0, transaction.CommitCount);
        Assert.Equal(1, transaction.RollbackCount);
        Assert.Equal(1, transaction.DisposeCount);
    }

    [Fact]
    public async Task ExhaustedOwnedStreamCommitsAndDisposesTransaction()
    {
        var transaction = new RecordingTransaction();
        var provider = new RecordingProvider(transaction, [1, 2]);

        var values = new List<int>();
        await foreach (var value in provider.StreamAsync<int>(
            Expression.Constant(0),
            TestContext.Current.CancellationToken))
        {
            values.Add(value);
        }

        Assert.Equal([1, 2], values);
        Assert.Equal(1, transaction.CommitCount);
        Assert.Equal(0, transaction.RollbackCount);
        Assert.Equal(1, transaction.DisposeCount);
    }

    [Fact]
    public async Task DriverRollbackFailureIsClassifiedLoggedAndDoesNotMaskCleanup()
    {
        var transaction = new RecordingTransaction
        {
            RollbackException = new TestDriverException(),
        };
        var provider = new RecordingProvider(transaction, [1, 2]);
        var cancellationToken = TestContext.Current.CancellationToken;
        var enumerator = provider.StreamAsync<int>(Expression.Constant(0), cancellationToken)
            .GetAsyncEnumerator(cancellationToken);

        Assert.True(await enumerator.MoveNextAsync());
        await enumerator.DisposeAsync();

        Assert.Equal(1, transaction.RollbackCount);
        Assert.Equal(1, transaction.DisposeCount);
        Assert.Equal(1, provider.RollbackFailureCount);
    }

    private sealed class RecordingProvider(RecordingTransaction transaction, IReadOnlyList<int> values)
        : GraphQueryProviderBase<RecordingTransaction>(CreateGraph(), ownsTransaction: true)
    {
        public int RollbackFailureCount { get; private set; }

        protected override Task<TResult> ExecuteCoreAsync<TResult>(
            Expression expression,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        protected override Task<RecordingTransaction> GetOrCreateTransactionAsync(
            CancellationToken cancellationToken) => Task.FromResult(transaction);

        protected override async IAsyncEnumerable<TResult> StreamCoreAsync<TResult>(
            Expression expression,
            RecordingTransaction transaction,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            foreach (var value in values)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Task.Yield();
                yield return (TResult)(object)value;
            }
        }

        protected override bool IsTransactionActive(RecordingTransaction transaction) => transaction.IsActive;

        protected override bool IsDriverException(Exception exception) => exception is TestDriverException;

        protected override void LogExecution(Expression expression, Type resultType, bool streaming)
        {
        }

        protected override void LogExecutionFailure(Exception exception)
        {
        }

        protected override void LogRollbackFailure(Exception exception) => RollbackFailureCount++;
    }

    private sealed class RecordingTransaction : IGraphTransaction
    {
        private bool completed;

        public Exception? RollbackException { get; init; }

        public bool IsActive => !completed;

        public int CommitCount { get; private set; }

        public int RollbackCount { get; private set; }

        public int DisposeCount { get; private set; }

        public Task CommitAsync()
        {
            CommitCount++;
            completed = true;
            return Task.CompletedTask;
        }

        public Task RollbackAsync()
        {
            RollbackCount++;
            if (RollbackException is not null)
            {
                throw RollbackException;
            }

            completed = true;
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            DisposeCount++;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class TestDriverException : Exception;

    private static IGraph CreateGraph() => DispatchProxy.Create<IGraph, GraphProxy>();

    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Performance",
        "CA1852:Seal internal types",
        Justification = "DispatchProxy generates a runtime subclass of this type.")]
    private class GraphProxy : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) =>
            throw new NotSupportedException();
    }
}
