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

using Cvoya.Graph.Model;

namespace Cvoya.Graph.Model.Neo4j;

using SessionTransaction = (global::Neo4j.Driver.IAsyncSession, global::Neo4j.Driver.IAsyncTransaction);

internal static class TransactionHelpers
{
    /// <summary>
    /// Gets or creates a Neo4j transaction.
    /// </summary>
    /// <param name="driver">The Neo4j driver</param>
    /// <param name="databaseName">The name of the database</param>
    /// <param name="transaction">Optional existing transaction</param>
    /// <returns>A tuple containing the session and transaction</returns>
    /// <exception cref="InvalidOperationException">Thrown when the transaction is not active or not a Neo4j transaction</exception>
    public static async Task<SessionTransaction> GetOrCreateTransaction(
        global::Neo4j.Driver.IDriver driver,
        string databaseName,
        IGraphTransaction? transaction)
    {
        if (transaction is GraphTransaction neo4jTx && neo4jTx.IsActive)
        {
            var tx = neo4jTx.GetTransaction() ?? throw new InvalidOperationException("Transaction is not active.");
            return (neo4jTx.Session, tx);
        }
        else if (transaction is null)
        {
            var session = driver.AsyncSession(
                builder => builder.WithDatabase(databaseName));
            var tx = await session.BeginTransactionAsync();
            return (session, tx);
        }
        else
        {
            throw new InvalidOperationException("Transaction is not active or not a Neo4j transaction.");
        }
    }
}