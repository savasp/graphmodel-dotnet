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

using global::Neo4j.Driver;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;


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
    private readonly ILogger<GraphTransaction> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="GraphTransaction"/> class.
    /// </summary>
    /// <param name="context">The graph context containing the session.</param>
    /// <param name="isReadOnly">Indicates whether the transaction is read-only.</param>
    /// <exception cref="ArgumentNullException">Thrown if either parameter is null</exception>
    public GraphTransaction(GraphContext context, bool isReadOnly = false)
    {
        _session = context.Driver.AsyncSession(c => c
            .WithDatabase(context.DatabaseName)
            .WithDefaultAccessMode(isReadOnly ? AccessMode.Read : AccessMode.Write));
        _logger = context.LoggerFactory?.CreateLogger<GraphTransaction>()
            ?? NullLogger<GraphTransaction>.Instance;
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
    internal IAsyncTransaction Transaction => _transaction
        ?? throw new GraphException($"The transaction has not started yet. Call {nameof(BeginTransactionAsync)} first.");

    /// <summary>
    /// Commits the transaction.
    /// </summary>
    /// <exception cref="GraphException">Thrown if the transaction is not active</exception>
    public async Task CommitAsync()
    {
        if (_transaction == null || _committed || _rolledBack)
            throw new GraphException("Transaction is not active.");

        await _transaction.CommitAsync();
        _committed = true;
    }

    /// <summary>
    /// Rolls back the transaction.
    /// </summary>
    /// <exception cref="GraphException">Thrown if the transaction is not active</exception>
    public async Task Rollback()
    {
        if (_transaction == null || _committed || _rolledBack)
            throw new GraphException("Transaction is not active.");

        await _transaction.RollbackAsync();
        _rolledBack = true;
    }

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
                _transaction.Dispose();
                _transaction = null;
            }
            catch
            {
                // Ignore rollback errors during disposal
            }
        }

        // Close the session
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await _session.CloseAsync().WaitAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Closing session timed out. The session may not have been closed properly.");
        }
        catch (Exception ex)
        {
            _logger.LogError("An error occurred while closing the session: {Message}", ex.Message);
        }
    }

    /// <summary>
    /// Disposes the transaction synchronously.
    /// </summary>
    public void Dispose()
    {
        DisposeAsync().GetAwaiter().GetResult();
    }

    internal async Task BeginTransactionAsync()
    {
        _logger.LogDebug("Beginning new transaction");
        _transaction = await _session.BeginTransactionAsync();
        _logger.LogDebug("Successfully began transaction");
    }
}
