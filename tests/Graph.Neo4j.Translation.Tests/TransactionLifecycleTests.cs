// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Neo4j.Translation.Tests;

using System.Reflection;
using Cvoya.Graph.Neo4j;
using Cvoya.Graph.Neo4j.Core;
using global::Neo4j.Driver;

public sealed class TransactionLifecycleTests
{
    [Fact]
    public async Task BeginFailureClosesSessionAndPreservesPrimaryException()
    {
        var beginException = new TestBeginException();
        var lifecycle = new RecordingLifecycle
        {
            BeginException = beginException,
            CloseException = new TestCloseException(),
        };
        var context = CreateContext(lifecycle);

        var exception = await Assert.ThrowsAsync<TestBeginException>(
            () => TransactionHelpers.GetOrCreateTransactionAsync(
                context,
                cancellationToken: TestContext.Current.CancellationToken));

        Assert.Same(beginException, exception);
        Assert.Equal(1, lifecycle.BeginCount);
        Assert.Equal(1, lifecycle.CloseCount);
        Assert.Equal(0, lifecycle.TransactionDisposeCount);
        Assert.Equal(["Session", "Begin", "Close"], lifecycle.Events);
    }

    [Fact]
    public async Task BeginCancellationClosesSessionAndPreservesCancellation()
    {
        var beginCompletion = new TaskCompletionSource<IAsyncTransaction>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        using var cancellationSource = CancellationTokenSource.CreateLinkedTokenSource(
            TestContext.Current.CancellationToken);
        var lifecycle = new RecordingLifecycle
        {
            BeginTask = beginCompletion.Task,
            CloseException = new TestCloseException(),
        };
        var context = CreateContext(lifecycle);

        var beginTask = TransactionHelpers.GetOrCreateTransactionAsync(
            context,
            cancellationToken: cancellationSource.Token);
        Assert.Equal(1, lifecycle.BeginCount);

        cancellationSource.Cancel();
        var exception = await Assert.ThrowsAsync<TaskCanceledException>(
            () => beginTask);

        Assert.Equal(cancellationSource.Token, exception.CancellationToken);
        Assert.Equal(1, lifecycle.BeginCount);
        Assert.Equal(1, lifecycle.CloseCount);
        Assert.Equal(["Session", "Begin", "Close"], lifecycle.Events);

        beginCompletion.TrySetCanceled(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task OperationFailureIsPreservedWhileEveryCleanupStageIsAttemptedOnce()
    {
        var operationException = new TestOperationException();
        var lifecycle = new RecordingLifecycle
        {
            RollbackException = new TestRollbackException(),
            TransactionDisposeException = new TestTransactionDisposeException(),
            CloseException = new TestCloseException(),
        };
        var context = CreateContext(lifecycle);

        var exception = await Assert.ThrowsAsync<TestOperationException>(
            () => TransactionHelpers.ExecuteInTransactionAsync<int>(
                context,
                transaction: null,
                _ => throw operationException,
                "Operation failed",
                cancellationToken: TestContext.Current.CancellationToken));

        Assert.Same(operationException, exception);
        Assert.Equal(1, lifecycle.RollbackCount);
        Assert.Equal(1, lifecycle.TransactionDisposeCount);
        Assert.Equal(1, lifecycle.CloseCount);
        Assert.Equal(
            ["Session", "Begin", "Rollback", "TransactionDispose", "Close"],
            lifecycle.Events);
    }

    [Fact]
    public async Task CommitFailureIsPreservedWhileEveryCleanupStageIsAttemptedOnce()
    {
        var commitException = new TestCommitException();
        var lifecycle = new RecordingLifecycle
        {
            CommitException = commitException,
            RollbackException = new TestRollbackException(),
            TransactionDisposeException = new TestTransactionDisposeException(),
            CloseException = new TestCloseException(),
        };
        var context = CreateContext(lifecycle);

        var exception = await Assert.ThrowsAsync<TestCommitException>(
            () => TransactionHelpers.ExecuteInTransactionAsync(
                context,
                transaction: null,
                _ => Task.FromResult(42),
                "Commit failed",
                cancellationToken: TestContext.Current.CancellationToken));

        Assert.Same(commitException, exception);
        Assert.Equal(1, lifecycle.CommitCount);
        Assert.Equal(1, lifecycle.RollbackCount);
        Assert.Equal(1, lifecycle.TransactionDisposeCount);
        Assert.Equal(1, lifecycle.CloseCount);
        Assert.Equal(
            ["Session", "Begin", "Commit", "Rollback", "TransactionDispose", "Close"],
            lifecycle.Events);
    }

    [Fact]
    public async Task CancellationIsPreservedWhileEveryCleanupStageIsAttemptedOnce()
    {
        var cancellationException = new OperationCanceledException(TestContext.Current.CancellationToken);
        var lifecycle = new RecordingLifecycle
        {
            RollbackException = new TestRollbackException(),
            TransactionDisposeException = new TestTransactionDisposeException(),
            CloseException = new TestCloseException(),
        };
        var context = CreateContext(lifecycle);

        var exception = await Assert.ThrowsAsync<OperationCanceledException>(
            () => TransactionHelpers.ExecuteInTransactionAsync<int>(
                context,
                transaction: null,
                _ => throw cancellationException,
                "Operation cancelled",
                cancellationToken: TestContext.Current.CancellationToken));

        Assert.Same(cancellationException, exception);
        Assert.Equal(1, lifecycle.RollbackCount);
        Assert.Equal(1, lifecycle.TransactionDisposeCount);
        Assert.Equal(1, lifecycle.CloseCount);
        Assert.Equal(
            ["Session", "Begin", "Rollback", "TransactionDispose", "Close"],
            lifecycle.Events);
    }

    [Fact]
    public async Task SuccessfulOperationCommitsAndReleasesDriverTransactionAndSession()
    {
        var lifecycle = new RecordingLifecycle();
        var context = CreateContext(lifecycle);

        var result = await TransactionHelpers.ExecuteInTransactionAsync(
            context,
            transaction: null,
            _ => Task.FromResult(42),
            "Operation failed",
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(42, result);
        Assert.Equal(1, lifecycle.CommitCount);
        Assert.Equal(0, lifecycle.RollbackCount);
        Assert.Equal(1, lifecycle.TransactionDisposeCount);
        Assert.Equal(1, lifecycle.CloseCount);
        Assert.Equal(
            ["Session", "Begin", "Commit", "TransactionDispose", "Close"],
            lifecycle.Events);
    }

    [Fact]
    public async Task SuccessfulOperationSurfacesTransactionDisposalFailureAfterClosingSession()
    {
        var disposeException = new TestTransactionDisposeException();
        var lifecycle = new RecordingLifecycle
        {
            TransactionDisposeException = disposeException,
        };
        var context = CreateContext(lifecycle);

        var exception = await Assert.ThrowsAsync<TestTransactionDisposeException>(
            () => TransactionHelpers.ExecuteInTransactionAsync(
                context,
                transaction: null,
                _ => Task.FromResult(42),
                "Operation failed",
                cancellationToken: TestContext.Current.CancellationToken));

        Assert.Same(disposeException, exception);
        Assert.Equal(1, lifecycle.CommitCount);
        Assert.Equal(1, lifecycle.TransactionDisposeCount);
        Assert.Equal(1, lifecycle.CloseCount);
        Assert.Equal(
            ["Session", "Begin", "Commit", "TransactionDispose", "Close"],
            lifecycle.Events);
    }

    [Fact]
    public async Task SuccessfulOperationSurfacesSessionCloseFailure()
    {
        var closeException = new TestCloseException();
        var lifecycle = new RecordingLifecycle
        {
            CloseException = closeException,
        };
        var context = CreateContext(lifecycle);

        var exception = await Assert.ThrowsAsync<TestCloseException>(
            () => TransactionHelpers.ExecuteInTransactionAsync(
                context,
                transaction: null,
                _ => Task.FromResult(42),
                "Operation failed",
                cancellationToken: TestContext.Current.CancellationToken));

        Assert.Same(closeException, exception);
        Assert.Equal(1, lifecycle.CommitCount);
        Assert.Equal(1, lifecycle.TransactionDisposeCount);
        Assert.Equal(1, lifecycle.CloseCount);
    }

    [Fact]
    public async Task ExplicitDisposalSurfacesFirstCleanupFailureAndIsIdempotent()
    {
        var rollbackException = new TestRollbackException();
        var lifecycle = new RecordingLifecycle();
        var context = CreateContext(lifecycle);
        var transaction = await TransactionHelpers.GetOrCreateTransactionAsync(
            context,
            cancellationToken: TestContext.Current.CancellationToken);
        lifecycle.RollbackException = rollbackException;
        lifecycle.TransactionDisposeException = new TestTransactionDisposeException();
        lifecycle.CloseException = new TestCloseException();

        var exception = await Assert.ThrowsAsync<TestRollbackException>(
            () => transaction.DisposeAsync().AsTask());
        await transaction.DisposeAsync();

        Assert.Same(rollbackException, exception);
        Assert.Equal(1, lifecycle.RollbackCount);
        Assert.Equal(1, lifecycle.TransactionDisposeCount);
        Assert.Equal(1, lifecycle.CloseCount);
        Assert.Equal(
            ["Session", "Begin", "Rollback", "TransactionDispose", "Close"],
            lifecycle.Events);
    }

    [Fact]
    public async Task CallerOwnedTransactionIsNotCommittedRolledBackOrDisposedByHelper()
    {
        var operationException = new TestOperationException();
        var lifecycle = new RecordingLifecycle();
        var context = CreateContext(lifecycle);
        var transaction = await TransactionHelpers.GetOrCreateTransactionAsync(
            context,
            cancellationToken: TestContext.Current.CancellationToken);

        var exception = await Assert.ThrowsAsync<TestOperationException>(
            () => TransactionHelpers.ExecuteInTransactionAsync<int>(
                context,
                transaction,
                _ => throw operationException,
                "Operation failed",
                cancellationToken: TestContext.Current.CancellationToken));

        Assert.Same(operationException, exception);
        Assert.Equal(0, lifecycle.CommitCount);
        Assert.Equal(0, lifecycle.RollbackCount);
        Assert.Equal(0, lifecycle.TransactionDisposeCount);
        Assert.Equal(0, lifecycle.CloseCount);
        Assert.Equal(["Session", "Begin"], lifecycle.Events);

        await transaction.RollbackAsync();
        await transaction.DisposeAsync();
    }

    private static GraphContext CreateContext(RecordingLifecycle lifecycle)
    {
        var store = new Neo4jGraphStore(lifecycle.Driver);
        return Assert.IsType<Neo4jGraph>(store.Graph).Context;
    }

    private sealed class RecordingLifecycle
    {
        public RecordingLifecycle()
        {
            Transaction = DispatchProxy.Create<IAsyncTransaction, TransactionProxy>();
            ((TransactionProxy)Transaction).Lifecycle = this;
            Session = DispatchProxy.Create<IAsyncSession, SessionProxy>();
            ((SessionProxy)Session).Lifecycle = this;
            Driver = DispatchProxy.Create<IDriver, DriverProxy>();
            ((DriverProxy)Driver).Lifecycle = this;
        }

        public IDriver Driver { get; }

        public IAsyncSession Session { get; }

        public IAsyncTransaction Transaction { get; }

        public Exception? BeginException { get; init; }

        public Task<IAsyncTransaction>? BeginTask { get; init; }

        public Exception? CommitException { get; init; }

        public Exception? RollbackException { get; set; }

        public Exception? TransactionDisposeException { get; set; }

        public Exception? CloseException { get; set; }

        public int BeginCount { get; private set; }

        public int CommitCount { get; private set; }

        public int RollbackCount { get; private set; }

        public int TransactionDisposeCount { get; private set; }

        public int CloseCount { get; private set; }

        public List<string> Events { get; } = [];

        public IAsyncSession CreateSession()
        {
            Events.Add("Session");
            return Session;
        }

        public Task<IAsyncTransaction> BeginAsync()
        {
            BeginCount++;
            Events.Add("Begin");
            if (BeginTask is not null)
            {
                return BeginTask;
            }

            return BeginException is null
                ? Task.FromResult(Transaction)
                : Task.FromException<IAsyncTransaction>(BeginException);
        }

        public Task CommitAsync()
        {
            CommitCount++;
            Events.Add("Commit");
            return CommitException is null
                ? Task.CompletedTask
                : Task.FromException(CommitException);
        }

        public Task RollbackAsync()
        {
            RollbackCount++;
            Events.Add("Rollback");
            return RollbackException is null
                ? Task.CompletedTask
                : Task.FromException(RollbackException);
        }

        public object? DisposeTransaction()
        {
            TransactionDisposeCount++;
            Events.Add("TransactionDispose");
            if (TransactionDisposeException is not null)
            {
                throw TransactionDisposeException;
            }

            return null;
        }

        public Task CloseAsync()
        {
            CloseCount++;
            Events.Add("Close");
            return CloseException is null
                ? Task.CompletedTask
                : Task.FromException(CloseException);
        }
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Performance",
        "CA1852:Seal internal types",
        Justification = "DispatchProxy generates a runtime subclass of this type.")]
    private class DriverProxy : DispatchProxy
    {
        public RecordingLifecycle Lifecycle { get; set; } = null!;

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) =>
            targetMethod?.Name switch
            {
                nameof(IDriver.AsyncSession) => Lifecycle.CreateSession(),
                _ => throw new NotSupportedException($"{targetMethod?.Name} should not be called by this test."),
            };
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Performance",
        "CA1852:Seal internal types",
        Justification = "DispatchProxy generates a runtime subclass of this type.")]
    private class SessionProxy : DispatchProxy
    {
        public RecordingLifecycle Lifecycle { get; set; } = null!;

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) =>
            targetMethod?.Name switch
            {
                nameof(IAsyncSession.BeginTransactionAsync) => Lifecycle.BeginAsync(),
                nameof(IAsyncSession.CloseAsync) => Lifecycle.CloseAsync(),
                _ => throw new NotSupportedException($"{targetMethod?.Name} should not be called by this test."),
            };
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Performance",
        "CA1852:Seal internal types",
        Justification = "DispatchProxy generates a runtime subclass of this type.")]
    private class TransactionProxy : DispatchProxy
    {
        public RecordingLifecycle Lifecycle { get; set; } = null!;

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) =>
            targetMethod?.Name switch
            {
                nameof(IAsyncTransaction.CommitAsync) => Lifecycle.CommitAsync(),
                nameof(IAsyncTransaction.RollbackAsync) => Lifecycle.RollbackAsync(),
                nameof(IDisposable.Dispose) => Lifecycle.DisposeTransaction(),
                _ => throw new NotSupportedException($"{targetMethod?.Name} should not be called by this test."),
            };
    }

    private sealed class TestBeginException : Exception;

    private sealed class TestOperationException : Exception;

    private sealed class TestCommitException : Exception;

    private sealed class TestRollbackException : Exception;

    private sealed class TestTransactionDisposeException : Exception;

    private sealed class TestCloseException : Exception;
}
