// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.InMemory;

/// <summary>
/// Implements the shared transaction ownership contract: a null caller transaction means the
/// operation runs in an implicit transaction the provider commits, rolls back, and disposes
/// itself; a caller-supplied transaction is used as-is and its lifecycle is never touched.
/// Foreign <see cref="IGraphTransaction"/> implementations are rejected.
/// </summary>
internal static class TransactionRunner
{
    /// <summary>
    /// Resolves the effective transaction for an operation, creating an implicit one when the
    /// caller passed null.
    /// </summary>
    /// <returns>The transaction plus whether this call created (and therefore owns) it.</returns>
    public static (InMemoryTransaction Transaction, bool Owned) GetOrCreate(
        InMemoryStore store,
        IGraphTransaction? transaction)
    {
        return transaction switch
        {
            null => (new InMemoryTransaction(store), true),
            InMemoryTransaction inMemory when inMemory.BelongsTo(store) => (inMemory, false),
            _ => throw new GraphException(
                "The given transaction is not valid for this in-memory graph store. " +
                "Use InMemoryGraphStore.Graph.GetTransactionAsync() to create one."),
        };
    }

    /// <summary>
    /// Runs <paramref name="operation"/> under the effective transaction, committing an implicit
    /// transaction on success and rolling it back on failure or cancellation. Exceptions that are
    /// not already graph or cancellation exceptions are wrapped in a <see cref="GraphException"/>
    /// carrying <paramref name="errorMessage"/>.
    /// </summary>
    public static async Task<T> ExecuteAsync<T>(
        InMemoryStore store,
        IGraphTransaction? transaction,
        Func<InMemoryTransaction, T> operation,
        string errorMessage,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var (effective, owned) = GetOrCreate(store, transaction);
        try
        {
            var result = operation(effective);
            if (owned)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await effective.CommitAsync().ConfigureAwait(false);
            }

            return result;
        }
        catch (Exception ex)
        {
            if (owned && effective.IsActive)
            {
                try
                {
                    await effective.RollbackAsync().ConfigureAwait(false);
                }
                catch (GraphException)
                {
                    // Best-effort rollback of an implicit transaction; the original failure wins.
                }
            }

            if (ex is GraphException or OperationCanceledException)
            {
                throw;
            }

            throw new GraphException($"{errorMessage}: {ex.Message}", ex);
        }
        finally
        {
            if (owned)
            {
                await effective.DisposeAsync().ConfigureAwait(false);
            }
        }
    }
}
