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
        Assert.Equal(["Rollback", "Dispose"], transaction.Events);
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
        Assert.Equal(["Commit", "Dispose"], transaction.Events);
    }

    [Fact]
    public async Task AbandonedStreamSurfacesRollbackFailureAfterDisposal()
    {
        var rollbackException = new TestDriverException();
        var transaction = new RecordingTransaction
        {
            RollbackException = rollbackException,
        };
        var provider = new RecordingProvider(transaction, [1, 2]);
        var cancellationToken = TestContext.Current.CancellationToken;
        var enumerator = provider.StreamAsync<int>(Expression.Constant(0), cancellationToken)
            .GetAsyncEnumerator(cancellationToken);

        Assert.True(await enumerator.MoveNextAsync());
        var exception = await Assert.ThrowsAsync<TestDriverException>(
            () => enumerator.DisposeAsync().AsTask());

        Assert.Same(rollbackException, exception);
        Assert.Equal(1, transaction.RollbackCount);
        Assert.Equal(1, transaction.DisposeCount);
        Assert.Equal(1, provider.RollbackFailureCount);
        Assert.Equal(["Rollback", "Dispose"], transaction.Events);
    }

    [Fact]
    public async Task StreamFailureIsPreservedWhenRollbackAndDisposalFail()
    {
        var streamException = new TestStreamException();
        var transaction = new RecordingTransaction
        {
            RollbackException = new TestRollbackException(),
            DisposeException = new TestDisposeException(),
        };
        var provider = new RecordingProvider(transaction, [])
        {
            StreamException = streamException,
        };

        var exception = await Assert.ThrowsAsync<TestStreamException>(
            () => ConsumeAsync(provider.StreamAsync<int>(
                Expression.Constant(0),
                TestContext.Current.CancellationToken)));

        Assert.Same(streamException, exception);
        Assert.Equal(1, transaction.RollbackCount);
        Assert.Equal(1, transaction.DisposeCount);
        Assert.Equal(1, provider.RollbackFailureCount);
        Assert.Equal(1, provider.DisposalFailureCount);
        Assert.Equal(["Rollback", "Dispose"], transaction.Events);
    }

    [Fact]
    public async Task CancellationIsPreservedWhenDisposalFails()
    {
        var cancellationException = new OperationCanceledException(TestContext.Current.CancellationToken);
        var transaction = new RecordingTransaction
        {
            DisposeException = new TestDisposeException(),
        };
        var provider = new RecordingProvider(transaction, [])
        {
            StreamException = cancellationException,
        };

        var exception = await Assert.ThrowsAsync<OperationCanceledException>(
            () => ConsumeAsync(provider.StreamAsync<int>(
                Expression.Constant(0),
                TestContext.Current.CancellationToken)));

        Assert.Same(cancellationException, exception);
        Assert.Equal(1, transaction.RollbackCount);
        Assert.Equal(1, transaction.DisposeCount);
        Assert.Equal(1, provider.DisposalFailureCount);
    }

    [Fact]
    public async Task CommitFailureIsPreservedWhenDisposalFails()
    {
        var commitException = new TestCommitException();
        var transaction = new RecordingTransaction
        {
            CommitException = commitException,
            DisposeException = new TestDisposeException(),
        };
        var provider = new RecordingProvider(transaction, [1]);

        var exception = await Assert.ThrowsAsync<TestCommitException>(
            () => ConsumeAsync(provider.StreamAsync<int>(
                Expression.Constant(0),
                TestContext.Current.CancellationToken)));

        Assert.Same(commitException, exception);
        Assert.Equal(1, transaction.CommitCount);
        Assert.Equal(1, transaction.RollbackCount);
        Assert.Equal(1, transaction.DisposeCount);
        Assert.Equal(1, provider.DisposalFailureCount);
        Assert.Equal(["Commit", "Rollback", "Dispose"], transaction.Events);
    }

    [Fact]
    public async Task SuccessfulStreamSurfacesDisposalFailure()
    {
        var disposeException = new TestDisposeException();
        var transaction = new RecordingTransaction
        {
            DisposeException = disposeException,
        };
        var provider = new RecordingProvider(transaction, [1]);

        var exception = await Assert.ThrowsAsync<TestDisposeException>(
            () => ConsumeAsync(provider.StreamAsync<int>(
                Expression.Constant(0),
                TestContext.Current.CancellationToken)));

        Assert.Same(disposeException, exception);
        Assert.Equal(1, transaction.CommitCount);
        Assert.Equal(0, transaction.RollbackCount);
        Assert.Equal(1, transaction.DisposeCount);
        Assert.Equal(1, provider.DisposalFailureCount);
    }

    [Fact]
    public async Task AbandonedStreamSurfacesDisposalFailureAfterRollback()
    {
        var disposeException = new TestDisposeException();
        var transaction = new RecordingTransaction
        {
            DisposeException = disposeException,
        };
        var provider = new RecordingProvider(transaction, [1, 2]);
        var cancellationToken = TestContext.Current.CancellationToken;
        var enumerator = provider.StreamAsync<int>(Expression.Constant(0), cancellationToken)
            .GetAsyncEnumerator(cancellationToken);

        Assert.True(await enumerator.MoveNextAsync());
        var exception = await Assert.ThrowsAsync<TestDisposeException>(
            () => enumerator.DisposeAsync().AsTask());

        Assert.Same(disposeException, exception);
        Assert.Equal(1, transaction.RollbackCount);
        Assert.Equal(1, transaction.DisposeCount);
        Assert.Equal(1, provider.DisposalFailureCount);
        Assert.Equal(["Rollback", "Dispose"], transaction.Events);
    }

    [Fact]
    public async Task CallerOwnedStreamDoesNotCleanUpSuppliedTransaction()
    {
        var streamException = new TestStreamException();
        var transaction = new RecordingTransaction();
        var provider = new RecordingProvider(transaction, [], ownsTransaction: false)
        {
            StreamException = streamException,
        };

        var exception = await Assert.ThrowsAsync<TestStreamException>(
            () => ConsumeAsync(provider.StreamAsync<int>(
                Expression.Constant(0),
                TestContext.Current.CancellationToken)));

        Assert.Same(streamException, exception);
        Assert.Equal(0, transaction.CommitCount);
        Assert.Equal(0, transaction.RollbackCount);
        Assert.Equal(0, transaction.DisposeCount);
        Assert.Empty(transaction.Events);
    }

    private sealed class RecordingProvider(
        RecordingTransaction transaction,
        IReadOnlyList<int> values,
        bool ownsTransaction = true)
        : GraphQueryProviderBase<RecordingTransaction>(CreateGraph(), ownsTransaction)
    {
        public int RollbackFailureCount { get; private set; }

        public int DisposalFailureCount { get; private set; }

        public Exception? StreamException { get; init; }

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

            if (StreamException is not null)
            {
                throw StreamException;
            }
        }

        protected override bool IsTransactionActive(RecordingTransaction transaction) => transaction.IsActive;

        protected override void LogExecution(Expression expression, Type resultType, bool streaming)
        {
        }

        protected override void LogExecutionFailure(Exception exception)
        {
        }

        protected override void LogRollbackFailure(Exception exception) => RollbackFailureCount++;

        protected override void LogDisposalFailure(Exception exception) => DisposalFailureCount++;
    }

    private sealed class RecordingTransaction : IGraphTransaction
    {
        private bool completed;

        public Exception? RollbackException { get; init; }

        public Exception? CommitException { get; init; }

        public Exception? DisposeException { get; init; }

        public bool IsActive => !completed;

        public int CommitCount { get; private set; }

        public int RollbackCount { get; private set; }

        public int DisposeCount { get; private set; }

        public List<string> Events { get; } = [];

        public Task CommitAsync()
        {
            CommitCount++;
            Events.Add("Commit");
            if (CommitException is not null)
            {
                throw CommitException;
            }

            completed = true;
            return Task.CompletedTask;
        }

        public Task RollbackAsync()
        {
            RollbackCount++;
            Events.Add("Rollback");
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
            Events.Add("Dispose");
            if (DisposeException is not null)
            {
                throw DisposeException;
            }

            return ValueTask.CompletedTask;
        }
    }

    private sealed class TestDriverException : Exception;

    private sealed class TestStreamException : Exception;

    private sealed class TestCommitException : Exception;

    private sealed class TestRollbackException : Exception;

    private sealed class TestDisposeException : Exception;

    private static async Task ConsumeAsync<T>(IAsyncEnumerable<T> stream)
    {
        await foreach (var _ in stream)
        {
        }
    }

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
