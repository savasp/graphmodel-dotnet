// Copyright 2025 Savas Parastatidis
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//      https://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

namespace Cvoya.Graph.Model.Neo4j.Core;

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
                    await tx.Rollback().ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    logger?.LogWarning(ex, "Failed to roll back cancelled transaction");
                }
            }

            throw;
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, errorMessage);
            if (transaction == null)
            {
                await tx.Rollback().ConfigureAwait(false);
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
                "The given transaction is not a valid Neo4j transaction. You need to use Neo4jStore.Graph.BeginTransaction() to create a transaction.");
        }

        return graphTransaction;
    }
}
