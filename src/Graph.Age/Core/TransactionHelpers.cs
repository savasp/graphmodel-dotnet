// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Age.Core;

using Microsoft.Extensions.Logging;
using Npgsql;

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
                    logger?.LogWarningTransactionHelpers54(ex);
                }
                catch (InvalidOperationException ex)
                {
                    logger?.LogWarningTransactionHelpers58(ex);
                }
                catch (NpgsqlException ex)
                {
                    logger?.LogWarningTransactionHelpers62(ex);
                }
            }

            throw;
        }
        catch (Exception ex)
        {
            logger?.LogErrorTransactionHelpers70(ex, errorMessage);
            failed = true;
            if (transaction == null)
            {
                // A failed operation often leaves the connection unusable, which is exactly when
                // rollback is most likely to throw NpgsqlException; a throwing rollback must not
                // replace the original exception as the reported cause.
                try
                {
                    await tx.RollbackAsync().ConfigureAwait(false);
                }
                catch (GraphException rollbackException)
                {
                    logger?.LogWarningTransactionHelpers83(rollbackException);
                }
                catch (InvalidOperationException rollbackException)
                {
                    logger?.LogWarningTransactionHelpers87(rollbackException);
                }
                catch (NpgsqlException rollbackException)
                {
                    logger?.LogWarningTransactionHelpers91(rollbackException);
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
                    logger?.LogWarningTransactionHelpers107(disposeException);
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
