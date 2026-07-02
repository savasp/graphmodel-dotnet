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

namespace Cvoya.Graph.Model.Age.Core.Internal;

using Cvoya.Graph.Model;
using Microsoft.Extensions.Logging;

internal static class TransactionHelpers
{
    public static async Task<T> ExecuteInTransactionAsync<T>(
        AgeGraphContext graphContext,
        IGraphTransaction? transaction,
        Func<AgeGraphTransaction, Task<T>> function,
        string errorMessage,
        ILogger? logger = null,
        bool isReadOnly = false)
    {
        var ageTransaction = await GetOrCreateTransactionAsync(graphContext, transaction, isReadOnly).ConfigureAwait(false);

        try
        {
            var result = await function(ageTransaction).ConfigureAwait(false);

            if (transaction is null)
            {
                await ageTransaction.CommitAsync().ConfigureAwait(false);
            }

            return result;
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, errorMessage);
            if (transaction is null)
            {
                await ageTransaction.Rollback().ConfigureAwait(false);
            }

            throw;
        }
        finally
        {
            if (transaction == null)
            {
                await ageTransaction.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    public static async Task<AgeGraphTransaction> GetOrCreateTransactionAsync(
        AgeGraphContext graphContext,
        IGraphTransaction? transaction,
        bool isReadOnly = false)
    {
        if (transaction is AgeGraphTransaction ageTransaction)
        {
            // If the transaction was already committed or rolled back, throw
            // rather than silently re-creating the underlying NpgsqlTransaction.
            if (ageTransaction.IsCompleted)
            {
                throw new GraphException("The transaction has already been committed or rolled back. Create a new transaction.");
            }

            if (!ageTransaction.IsActive)
            {
                await ageTransaction.BeginTransactionAsync().ConfigureAwait(false);
            }

            return ageTransaction;
        }

        if (transaction is not null && transaction is not AgeGraphTransaction)
        {
            throw new GraphException("The given transaction is not a valid AGE transaction. You need to use AgeGraph.BeginTransactionAsync() to create a transaction.");
        }

        // Create a new transaction
        var newTransaction = graphContext.CreateTransaction(isReadOnly);
        await newTransaction.BeginTransactionAsync().ConfigureAwait(false);
        return newTransaction;
    }
}
