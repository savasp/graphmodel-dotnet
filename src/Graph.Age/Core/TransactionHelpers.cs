// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Age.Core;

using Microsoft.Extensions.Logging;


internal static class TransactionHelpers
{
    public static async Task<T> ExecuteInTransactionAsync<T>(
        AgeGraphContext graphContext,
        IGraphTransaction? transaction,
        Func<AgeGraphTransaction, Task<T>> function,
        string errorMessage,
        ILogger? logger = null,
        bool isReadOnly = false,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var tx = await GetOrCreateTransactionAsync(
            graphContext,
            transaction,
            isReadOnly,
            cancellationToken).ConfigureAwait(false);

        var failed = false;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            var result = await function(tx).ConfigureAwait(false);

            if (transaction == null)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await tx.CommitAsync().ConfigureAwait(false);
            }

            return result;
        }
        catch (OperationCanceledException)
        {
            failed = true;
            if (transaction == null)
            {
                try
                {
                    await tx.RollbackAsync().ConfigureAwait(false);
                }
                catch (GraphException ex)
                {
                    logger?.LogWarning(ex, "Failed to roll back cancelled transaction");
                }
                catch (InvalidOperationException ex)
                {
                    logger?.LogWarning(ex, "Failed to roll back cancelled transaction");
                }
            }

            throw;
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, errorMessage);
            failed = true;
            if (transaction == null)
            {
                // A failed operation often leaves the connection unusable; a throwing rollback
                // must not replace the original exception as the reported cause.
                try
                {
                    await tx.RollbackAsync().ConfigureAwait(false);
                }
                catch (Exception rollbackException)
                {
                    logger?.LogWarning(rollbackException, "Failed to roll back failed transaction");
                }
            }

            throw;
        }
        finally
        {
            if (transaction == null)
            {
                try
                {
                    await tx.DisposeAsync().ConfigureAwait(false);
                }
                catch (Exception disposeException) when (failed)
                {
                    logger?.LogWarning(disposeException, "Failed to dispose transaction after a failed operation");
                }
            }
        }
    }

    public static async Task<AgeGraphTransaction> GetOrCreateTransactionAsync(
        AgeGraphContext graphContext,
        IGraphTransaction? transaction = null,
        bool isReadOnly = false,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (transaction is null)
        {
            var tx = new AgeGraphTransaction(graphContext, isReadOnly);
            await tx.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            return tx;
        }

        if (transaction is not AgeGraphTransaction graphTransaction)
        {
            throw new GraphException(
                "The given transaction is not a valid AGE transaction. Use AgeGraphStore.Graph.GetTransactionAsync() to create it.");
        }

        return graphTransaction;
    }
}
