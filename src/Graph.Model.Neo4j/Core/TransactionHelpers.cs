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
        ILogger? logger = null)
    {
        var tx = await GetOrCreateTransactionAsync(graphContext, transaction);

        try
        {
            var result = await function(tx);

            if (transaction == null)
            {
                await tx.CommitAsync();
            }

            return result;
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, errorMessage);
            if (transaction == null)
            {
                await tx.Rollback();
            }

            throw;
        }
        finally
        {
            if (transaction == null)
            {
                await tx.DisposeAsync();
            }
        }
    }

    public static async Task<GraphTransaction> GetOrCreateTransactionAsync(
        GraphContext graphContext,
        IGraphTransaction? transaction = null,
        bool isReadOnly = false)
    {
        if (transaction is null)
        {
            var tx = new GraphTransaction(graphContext, isReadOnly);
            await tx.BeginTransactionAsync();
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