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

using Neo4j.Driver;

namespace Cvoya.Graph.Model.Neo4j.Core;

/// <summary>
/// Represents a Neo4j graph transaction that implements the <see cref="IGraphTransaction"/> interface.
/// </summary>
/// <remarks>
/// This class wraps the Neo4j driver's IAsyncTransaction and provides implementation for the IGraphTransaction interface.
/// </remarks>
internal class GraphTransaction : IGraphTransaction
{
    private readonly IAsyncSession _session;
    private IAsyncTransaction? _transaction;
    private bool _committed;
    private bool _rolledBack;

    /// <summary>
    /// Initializes a new instance of the <see cref="GraphTransaction"/> class.
    /// </summary>
    /// <param name="session">The Neo4j driver session</param>
    /// <param name="transaction">The Neo4j driver transaction</param>
    /// <exception cref="ArgumentNullException">Thrown if either parameter is null</exception>
    public GraphTransaction(IAsyncSession session, IAsyncTransaction transaction)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _transaction = transaction ?? throw new ArgumentNullException(nameof(transaction));
    }

    /// <summary>
    /// Gets a value indicating whether the transaction is active.
    /// </summary>
    /// <value>True if the transaction is active, false otherwise.</value>
    public bool IsActive => _transaction != null && !_committed && !_rolledBack;

    /// <summary>
    /// Gets the Neo4j driver session associated with this transaction.
    /// </summary>
    internal IAsyncSession Session => _session;

    /// <summary>
    /// Gets the underlying Neo4j transaction.
    /// </summary>
    internal IAsyncTransaction Transaction => _transaction ?? throw new InvalidOperationException("Transaction is not active.");

    /// <summary>
    /// Commits the transaction.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown if the transaction is not active</exception>
    public async Task Commit()
    {
        if (_transaction == null || _committed || _rolledBack)
            throw new InvalidOperationException("Transaction is not active.");

        await _transaction.CommitAsync();
        _committed = true;
        _transaction = null;
    }

    /// <summary>
    /// Rolls back the transaction.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown if the transaction is not active</exception>
    public async Task Rollback()
    {
        if (_transaction == null || _committed || _rolledBack)
            throw new InvalidOperationException("Transaction is not active.");

        await _transaction.RollbackAsync();
        _rolledBack = true;
        _transaction = null;
    }

    /// <summary>
    /// Gets the underlying Neo4j transaction.
    /// </summary>
    /// <returns>The Neo4j transaction or null if not active</returns>
    internal IAsyncTransaction? GetNeo4jTransaction() => _transaction;

    /// <summary>
    /// Disposes the transaction asynchronously.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_transaction != null && !_committed && !_rolledBack)
        {
            try
            {
                // Auto-rollback uncommitted transactions
                await _transaction.RollbackAsync();
            }
            catch
            {
                // Ignore rollback errors during disposal
            }
            _transaction = null;
        }

        await _session.CloseAsync();
    }

    /// <summary>
    /// Disposes the transaction synchronously.
    /// </summary>
    public void Dispose()
    {
        DisposeAsync().GetAwaiter().GetResult();
    }
}
