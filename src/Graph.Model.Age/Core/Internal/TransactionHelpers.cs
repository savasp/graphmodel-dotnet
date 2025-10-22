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
        ILogger? logger = null)
    {
        var ageTransaction = await GetOrCreateTransactionAsync(graphContext, transaction).ConfigureAwait(false);

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
            if (transaction is null)
            {
                await ageTransaction.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    public static async Task<AgeGraphTransaction> GetOrCreateTransactionAsync(
        AgeGraphContext graphContext,
        IGraphTransaction? transaction = null,
        bool isReadOnly = false)
    {
        if (transaction is null)
        {
            var ageTransaction = graphContext.CreateTransaction(isReadOnly);
            await ageTransaction.BeginTransactionAsync().ConfigureAwait(false);
            return ageTransaction;
        }

        if (transaction is not AgeGraphTransaction ageGraphTransaction)
        {
            throw new GraphException("The provided transaction does not belong to the AGE provider. Use AgeGraph.GetTransactionAsync to create an AGE transaction instance.");
        }

        if (isReadOnly && !ageGraphTransaction.IsReadOnly)
        {
            throw new GraphException("A write transaction cannot be reused as read-only.");
        }

        return ageGraphTransaction;
    }
}
