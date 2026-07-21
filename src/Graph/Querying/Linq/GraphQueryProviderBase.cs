// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Querying.Linq;

using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using Cvoya.Graph.Querying.Linq.Helpers;

internal abstract class GraphQueryProviderBase<TTransaction> : IStreamingGraphQueryProvider
    where TTransaction : class, IGraphTransaction
{
    private readonly bool ownsTransaction;

    protected GraphQueryProviderBase(IGraph graph, bool ownsTransaction)
    {
        Graph = graph ?? throw new ArgumentNullException(nameof(graph));
        this.ownsTransaction = ownsTransaction;
    }

    public IGraph Graph { get; }

    public IQueryable CreateQuery(Expression expression)
    {
        ArgumentNullException.ThrowIfNull(expression);

        var elementType = TypeHelpers.GetElementType(expression.Type);

        try
        {
            return (IQueryable)GetType()
                .GetMethod(nameof(CreateQuery), 1, [typeof(Expression)])!
                .MakeGenericMethod(elementType)
                .Invoke(this, [expression])!;
        }
        catch (TargetInvocationException exception) when (
            UnwrapCreateQueryInvocationException && exception.InnerException is not null)
        {
            throw new InvalidOperationException(
                $"Failed to create query for type {elementType}",
                exception.InnerException);
        }
        catch (Exception exception) when (ShouldWrapCreateQueryException(exception))
        {
            throw new InvalidOperationException($"Failed to create query for type {elementType}", exception);
        }
    }

    public IGraphQueryable<TElement> CreateQuery<TElement>(Expression expression)
    {
        ArgumentNullException.ThrowIfNull(expression);

        if (typeof(INode).IsAssignableFrom(typeof(TElement)))
        {
            var queryableType = typeof(GraphNodeQueryable<>).MakeGenericType(typeof(TElement));
            return (IGraphQueryable<TElement>)Activator.CreateInstance(queryableType, this, expression)!;
        }

        if (typeof(IRelationship).IsAssignableFrom(typeof(TElement)))
        {
            var queryableType = typeof(GraphRelationshipQueryable<>).MakeGenericType(typeof(TElement));
            return (IGraphQueryable<TElement>)Activator.CreateInstance(queryableType, this, expression)!;
        }

        return new GraphQueryable<TElement>(this, expression);
    }

    public object? Execute(Expression expression) => ExecuteInternal<object>(expression);

    public TResult Execute<TResult>(Expression expression) => ExecuteInternal<TResult>(expression);

    public Task<object?> ExecuteAsync(
        Expression expression,
        CancellationToken cancellationToken = default) =>
        ExecuteAsync<object?>(expression, cancellationToken);

    public async Task<TResult> ExecuteAsync<TResult>(
        Expression expression,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(expression);
        cancellationToken.ThrowIfCancellationRequested();
        LogExecution(expression, typeof(TResult), streaming: false);

        try
        {
            return await ExecuteCoreAsync<TResult>(expression, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            LogExecutionFailure(exception);
            throw;
        }
    }

    public async IAsyncEnumerable<TResult> StreamAsync<TResult>(
        Expression expression,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(expression);
        cancellationToken.ThrowIfCancellationRequested();
        LogExecution(expression, typeof(TResult), streaming: true);

        TTransaction? transaction = null;
        var completed = false;
        var failureState = new StreamFailureState();

        try
        {
            transaction = await TrackFailureAsync(
                () => GetOrCreateTransactionAsync(cancellationToken),
                failureState).ConfigureAwait(false);

            var enumerator = TrackFailure(
                () => StreamCoreAsync<TResult>(expression, transaction, cancellationToken)
                    .GetAsyncEnumerator(cancellationToken),
                failureState);

            try
            {
                while (await MoveNextWithFailureTrackingAsync(enumerator, failureState).ConfigureAwait(false))
                {
                    yield return TrackFailure(() => enumerator.Current, failureState);
                }
            }
            finally
            {
                await DisposeEnumeratorWithFailureTrackingAsync(enumerator, failureState).ConfigureAwait(false);
            }

            if (ownsTransaction)
            {
                await TrackFailureAsync(
                    async () =>
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        await transaction.CommitAsync().ConfigureAwait(false);
                    },
                    failureState).ConfigureAwait(false);
            }

            completed = true;
        }
        finally
        {
            if (ownsTransaction && transaction is not null)
            {
                Exception? cleanupFailure = null;
                if (!completed && IsTransactionActive(transaction))
                {
                    try
                    {
                        await transaction.RollbackAsync().ConfigureAwait(false);
                    }
                    catch (Exception exception)
                    {
                        LogRollbackFailure(exception);
                        if (!failureState.HasPrimaryFailure)
                        {
                            cleanupFailure = exception;
                        }
                    }
                }

                try
                {
                    await transaction.DisposeAsync().ConfigureAwait(false);
                }
                catch (Exception exception)
                {
                    LogDisposalFailure(exception);
                    if (!failureState.HasPrimaryFailure && cleanupFailure is null)
                    {
                        cleanupFailure = exception;
                    }
                }

                if (cleanupFailure is not null)
                {
                    ExceptionDispatchInfo.Capture(cleanupFailure).Throw();
                }
            }
        }
    }

    protected abstract Task<TResult> ExecuteCoreAsync<TResult>(
        Expression expression,
        CancellationToken cancellationToken);

    protected abstract Task<TTransaction> GetOrCreateTransactionAsync(CancellationToken cancellationToken);

    protected abstract IAsyncEnumerable<TResult> StreamCoreAsync<TResult>(
        Expression expression,
        TTransaction transaction,
        CancellationToken cancellationToken);

    protected abstract bool IsTransactionActive(TTransaction transaction);

    protected virtual bool UnwrapCreateQueryInvocationException => false;

    protected virtual bool ShouldWrapCreateQueryException(Exception exception) => true;

    protected abstract void LogExecution(Expression expression, Type resultType, bool streaming);

    protected abstract void LogExecutionFailure(Exception exception);

    protected abstract void LogRollbackFailure(Exception exception);

    protected abstract void LogEnumeratorDisposalFailure(Exception exception);

    protected abstract void LogDisposalFailure(Exception exception);

    IQueryable<TElement> IQueryProvider.CreateQuery<TElement>(Expression expression) =>
        CreateQuery<TElement>(expression);

    private TResult ExecuteInternal<TResult>(Expression expression) =>
        Task.Run(() => ExecuteAsync<TResult>(expression, CancellationToken.None))
            .GetAwaiter()
            .GetResult();

    private static TResult TrackFailure<TResult>(
        Func<TResult> operation,
        StreamFailureState failureState)
    {
        try
        {
            return operation();
        }
        catch
        {
            failureState.HasPrimaryFailure = true;
            throw;
        }
    }

    private static async Task<TResult> TrackFailureAsync<TResult>(
        Func<Task<TResult>> operation,
        StreamFailureState failureState)
    {
        try
        {
            return await operation().ConfigureAwait(false);
        }
        catch
        {
            failureState.HasPrimaryFailure = true;
            throw;
        }
    }

    private static async Task TrackFailureAsync(
        Func<Task> operation,
        StreamFailureState failureState)
    {
        try
        {
            await operation().ConfigureAwait(false);
        }
        catch
        {
            failureState.HasPrimaryFailure = true;
            throw;
        }
    }

    private static async ValueTask<bool> MoveNextWithFailureTrackingAsync<TResult>(
        IAsyncEnumerator<TResult> enumerator,
        StreamFailureState failureState)
    {
        try
        {
            return await enumerator.MoveNextAsync().ConfigureAwait(false);
        }
        catch
        {
            failureState.HasPrimaryFailure = true;
            throw;
        }
    }

    private async ValueTask DisposeEnumeratorWithFailureTrackingAsync<TResult>(
        IAsyncEnumerator<TResult> enumerator,
        StreamFailureState failureState)
    {
        try
        {
            await enumerator.DisposeAsync().ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            if (failureState.HasPrimaryFailure)
            {
                LogEnumeratorDisposalFailure(exception);
                return;
            }

            failureState.HasPrimaryFailure = true;
            throw;
        }
    }

    private sealed class StreamFailureState
    {
        public bool HasPrimaryFailure { get; set; }
    }
}
