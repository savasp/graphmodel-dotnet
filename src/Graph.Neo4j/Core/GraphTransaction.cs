// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Neo4j.Core;

using System.Runtime.ExceptionServices;
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
    private readonly GraphContext _context;
    private readonly IAsyncSession _session;
    private IAsyncTransaction? _transaction;
    private bool _committed;
    private bool _rolledBack;
    private bool _rollbackAttempted;
    private bool _disposed;
    private readonly ILogger<GraphTransaction> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="GraphTransaction"/> class.
    /// </summary>
    /// <param name="context">The graph context containing the session.</param>
    /// <param name="isReadOnly">Indicates whether the transaction is read-only.</param>
    /// <exception cref="ArgumentNullException">Thrown if either parameter is null</exception>
    public GraphTransaction(GraphContext context, bool isReadOnly = false)
    {
        _context = context;
        IsReadOnly = isReadOnly;
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

    /// <summary>Gets whether this transaction was opened with read access.</summary>
    internal bool IsReadOnly { get; }

    /// <summary>
    /// Gets whether this transaction was created by the given graph context. Ownership is
    /// reference identity: a transaction from a different store or graph instance is foreign even
    /// when connection settings match.
    /// </summary>
    internal bool BelongsTo(GraphContext context) => ReferenceEquals(_context, context);

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

        await _transaction.CommitAsync().ConfigureAwait(false);
        _committed = true;
    }

    /// <summary>
    /// Rolls back the transaction.
    /// </summary>
    /// <exception cref="GraphException">Thrown if the transaction is not active</exception>
    public async Task RollbackAsync()
    {
        if (_transaction == null || _committed || _rolledBack)
            throw new GraphException("Transaction is not active.");

        _rollbackAttempted = true;
        await _transaction.RollbackAsync().ConfigureAwait(false);
        _rolledBack = true;
    }

    /// <summary>
    /// Disposes the transaction asynchronously.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Exception? cleanupFailure = null;
        var transaction = _transaction;
        _transaction = null;

        if (transaction != null)
        {
            if (!_committed && !_rolledBack && !_rollbackAttempted)
            {
                _rollbackAttempted = true;
                try
                {
                    await transaction.RollbackAsync().ConfigureAwait(false);
                    _rolledBack = true;
                }
                catch (Exception exception)
                {
                    cleanupFailure = exception;
                    _logger.LogWarningGraphTransactionRollbackFailure(exception);
                }
            }

            try
            {
                transaction.Dispose();
            }
            catch (Exception exception)
            {
                cleanupFailure ??= exception;
                _logger.LogWarningGraphTransactionDriverDisposalFailure(exception);
            }
        }

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        try
        {
            await _session.CloseAsync().WaitAsync(cts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException exception) when (cts.IsCancellationRequested)
        {
            _logger.LogWarningGraphTransaction111();
            cleanupFailure ??= new TimeoutException(
                "Timed out while closing the Neo4j session.",
                exception);
        }
        catch (Exception ex)
        {
            cleanupFailure ??= ex;
            _logger.LogErrorGraphTransactionSessionCloseFailure(ex);
        }

        if (cleanupFailure is not null)
        {
            ExceptionDispatchInfo.Capture(cleanupFailure).Throw();
        }
    }

    internal async Task BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_transaction is not null)
        {
            throw new GraphException("Transaction has already started.");
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            _logger.LogDebugGraphTransaction123();
            _transaction = await _session.BeginTransactionAsync().WaitAsync(cancellationToken).ConfigureAwait(false);
            _logger.LogDebugGraphTransaction125();
        }
        catch
        {
            try
            {
                await DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception cleanupException)
            {
                _logger.LogWarningGraphTransactionBeginCleanupFailure(cleanupException);
            }

            throw;
        }
    }
}
