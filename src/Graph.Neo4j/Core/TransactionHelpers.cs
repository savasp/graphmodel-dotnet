// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Neo4j.Core;

using global::Neo4j.Driver;
using Microsoft.Extensions.Logging;


internal static class TransactionHelpers
{
    public static async Task<T> ExecuteInTransactionAsync<T>(
        GraphContext graphContext,
        IGraphTransaction? transaction,
        Func<GraphTransaction, Task<T>> function,
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
            if (transaction == null)
            {
                try
                {
                    await tx.RollbackAsync().ConfigureAwait(false);
                }
                catch (GraphException ex)
                {
                    logger?.LogWarningTransactionHelpers53(ex);
                }
                catch (Neo4jException ex)
                {
                    logger?.LogWarningTransactionHelpers57(ex);
                }
                catch (InvalidOperationException ex)
                {
                    logger?.LogWarningTransactionHelpers61(ex);
                }
            }

            throw;
        }
        catch (Exception ex)
        {
            logger?.LogErrorTransactionHelpers69(ex, errorMessage);
            if (transaction == null)
            {
                await tx.RollbackAsync().ConfigureAwait(false);
            }

            throw;
        }
        finally
        {
            if (transaction == null)
            {
                await tx.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    public static async Task<GraphTransaction> GetOrCreateTransactionAsync(
        GraphContext graphContext,
        IGraphTransaction? transaction = null,
        bool isReadOnly = false,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (transaction is null)
        {
            var tx = new GraphTransaction(graphContext, isReadOnly);
            await tx.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            return tx;
        }

        if (transaction is not GraphTransaction graphTransaction)
        {
            throw new GraphException(
                "The given transaction is not a valid Neo4j transaction. Use Neo4jGraphStore.Graph.GetTransactionAsync() to create one.");
        }

        if (!graphTransaction.BelongsTo(graphContext))
        {
            throw new GraphException(
                "The given transaction was created by a different Neo4j graph store. A transaction can only be used with the graph that created it.");
        }

        return graphTransaction;
    }
}
