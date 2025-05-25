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
using Microsoft.Extensions.Logging;
using Neo4j.Driver;

namespace Cvoya.Graph.Provider.Neo4j.Query;

/// <summary>
/// Executes Cypher queries against a Neo4j database.
/// </summary>
internal class Neo4jQueryExecutor
{
    private readonly string _databaseName;
    private readonly IDriver _driver;
    private readonly ILogger? _logger;

    /// <summary>
    /// Initializes a new instance of the Neo4jQueryExecutor class.
    /// </summary>
    /// <param name="driver">The Neo4j driver</param>
    /// <param name="databaseName">The database name</param>
    /// <param name="logger">Optional logger</param>
    public Neo4jQueryExecutor(IDriver driver, string databaseName, ILogger? logger = null)
    {
        _driver = driver ?? throw new ArgumentNullException(nameof(driver));
        _databaseName = databaseName ?? throw new ArgumentNullException(nameof(databaseName));
        _logger = logger;
    }

    /// <summary>
    /// Executes a Cypher query and returns the results as dynamic objects.
    /// </summary>
    /// <param name="cypher">The Cypher query text</param>
    /// <param name="parameters">Optional query parameters</param>
    /// <param name="transaction">Optional transaction</param>
    /// <returns>Collection of dynamic results</returns>
    /// <exception cref="GraphException">Thrown when the query execution fails</exception>
    public async Task<IEnumerable<dynamic>> ExecuteCypher(string cypher, object? parameters = null, IGraphTransaction? transaction = null)
    {
        if (string.IsNullOrEmpty(cypher)) throw new ArgumentNullException(nameof(cypher));

        var (session, tx) = await GetOrCreateTransaction(transaction);
        try
        {
            return await ExecuteCypherInternal(tx, cypher, parameters);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to execute Cypher query.");
            throw new GraphException("Failed to execute Cypher query.", ex);
        }
        finally
        {
            if (transaction is null)
            {
                await session.CloseAsync();
            }
        }
    }

    /// <summary>
    /// Internal method to execute a Cypher query within a transaction.
    /// </summary>
    public async Task<IEnumerable<dynamic>> ExecuteCypherInternal(IAsyncTransaction transaction, string cypher, object? parameters = null)
    {
        if (string.IsNullOrEmpty(cypher)) throw new ArgumentNullException(nameof(cypher));

        var results = new List<dynamic>();
        var cursor = await transaction.RunAsync(cypher, parameters);
        while (await cursor.FetchAsync())
        {
            var record = cursor.Current;
            results.Add(record.Values);
        }
        return results;
    }

    /// <summary>
    /// Gets or creates a Neo4j transaction.
    /// </summary>
    /// <param name="transaction">Optional existing transaction</param>
    /// <returns>A tuple containing the session and transaction</returns>
    /// <exception cref="InvalidOperationException">Thrown when the transaction is not active or not a Neo4j transaction</exception>
    public async Task<(IAsyncSession, IAsyncTransaction)> GetOrCreateTransaction(IGraphTransaction? transaction)
    {
        if (transaction is Neo4jGraphTransaction neo4jTx && neo4jTx.IsActive)
        {
            var tx = neo4jTx.GetTransaction() ?? throw new InvalidOperationException("Transaction is not active.");
            return (neo4jTx.Session, tx);
        }
        else if (transaction is null)
        {
            var session = _driver.AsyncSession(builder => builder.WithDatabase(_databaseName));
            var tx = await session.BeginTransactionAsync();
            return (session, tx);
        }
        else
        {
            throw new InvalidOperationException("Transaction is not active or not a Neo4j transaction.");
        }
    }
}