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

namespace Cvoya.Graph.Model.Age.Core;

using System.Collections.Concurrent;
using System.Data;
using System.Globalization;

using Cvoya.Graph.Model;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using Npgsql;

/// <summary>
/// Represents an Apache AGE backed transaction. Uses the connection from the AgeGraphContext.
/// Implementation details will follow once the provider plumbing is in place.
/// </summary>
internal sealed class AgeGraphTransaction : IGraphTransaction
{
    private static readonly ConcurrentDictionary<NpgsqlConnection, AgeGraphTransaction> ActiveTransactions
        = new ConcurrentDictionary<NpgsqlConnection, AgeGraphTransaction>();
    private readonly NpgsqlConnection connection;
    private readonly bool isReadOnly;
    private readonly IsolationLevel isolationLevel;
    private readonly ILogger logger;
    private readonly SemaphoreSlim transactionLock = new(1, 1);
    private NpgsqlTransaction? transaction;
    private bool committed;
    private bool rolledBack;

    public AgeGraphTransaction(
        AgeGraphContext context,
        bool isReadOnly,
        IsolationLevel isolationLevel)
    {
        ArgumentNullException.ThrowIfNull(context);

        connection = context.Connection;
        this.isReadOnly = isReadOnly;
        this.isolationLevel = isolationLevel;
        logger = context.LoggerFactory.CreateLogger<AgeGraphTransaction>()
            ?? NullLogger<AgeGraphTransaction>.Instance;
        GraphName = context.GraphName;
    }

    internal string GraphName { get; }

    internal bool IsReadOnly => isReadOnly;

    internal NpgsqlTransaction Transaction => transaction
        ?? throw new GraphException("Transaction has not been started. Call BeginTransactionAsync first.");

    internal NpgsqlConnection Connection => connection ?? throw new GraphException("Call BeginTransactionAsync first.");

    public bool IsActive => transaction != null && !committed && !rolledBack;

    public async Task BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        // Use semaphore to ensure only one thread can begin a transaction at a time
        await transactionLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (transaction is not null)
            {
                logger.LogDebug("Transaction already started for graph '{GraphName}', reusing existing transaction", GraphName);
                return;
            }
            if (ActiveTransactions.TryGetValue(connection, out var existingTransaction))
            {
                logger.LogDebug("Reusing existing active transaction for graph '{GraphName}'", GraphName);
                transaction = existingTransaction.Transaction;
                return;
            }
            // The connection is already open and configured by AgeGraphStore
            await EnsureGraphExistsAsync(connection, cancellationToken).ConfigureAwait(false);

            try
            {
                transaction = await connection.BeginTransactionAsync(isolationLevel, cancellationToken).ConfigureAwait(false);
                ActiveTransactions[connection] = this;
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("transaction is already in progress"))
            {
                logger.LogDebug(
                    "Connection already has an active transaction for graph '{GraphName}'. " +
                    "This indicates nested transaction usage - an operation is trying to start a new transaction " +
                    "while another is already active. Pass the existing transaction to child operations instead of " +
                    "letting them create new ones.",
                    GraphName);
                throw new GraphException(
                    "A transaction is already active on this connection. " +
                    "Pass the existing transaction to operations that need it, or commit/rollback the current transaction before starting a new one. " +
                    "This often happens when an operation within a transaction calls another operation that also tries to start a transaction.",
                    ex);
            }

            if (isReadOnly)
            {
                await using var cmd = connection.CreateCommand();
                cmd.Transaction = transaction;
                cmd.CommandText = "SET TRANSACTION READ ONLY";
                await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            logger.LogDebug("Started AGE transaction for graph '{GraphName}' with isolation {Isolation}", GraphName, isolationLevel);
        }
        finally
        {
            transactionLock.Release();
        }
    }

    public async Task CommitAsync()
    {
        if (!IsActive)
        {
            throw new GraphException("Transaction is not active.");
        }

        await transaction!.CommitAsync().ConfigureAwait(false);
        committed = true;
        logger.LogDebug("Committed AGE transaction for graph '{GraphName}'", GraphName);
    }

    public async Task Rollback()
    {
        if (!IsActive)
        {
            throw new GraphException("Transaction is not active.");
        }

        await transaction!.RollbackAsync().ConfigureAwait(false);
        rolledBack = true;
        logger.LogDebug("Rolled back AGE transaction for graph '{GraphName}'", GraphName);
    }

    public async ValueTask DisposeAsync()
    {
        if (ActiveTransactions.TryRemove(connection, out var _))
        {
            logger.LogDebug("Removing transaction from active transactions for graph '{GraphName}'", GraphName);
        }
        if (transaction != null && !committed && !rolledBack)
        {
            try
            {
                await transaction.RollbackAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to rollback transaction during disposal.");
            }
        }

        if (transaction != null)
        {
            await transaction.DisposeAsync().ConfigureAwait(false);
            transaction = null;
        }

        transactionLock.Dispose();

        // Don't dispose the connection - it's owned by AgeGraph
    }

    public void Dispose()
    {
        DisposeAsync().GetAwaiter().GetResult();
    }

    private async Task EnsureGraphExistsAsync(NpgsqlConnection npgsqlConnection, CancellationToken cancellationToken)
    {
        await using var existsCmd = npgsqlConnection.CreateCommand();
        existsCmd.CommandText = "SELECT COUNT(*) FROM ag_catalog.ag_graph WHERE name = @graph";
        existsCmd.Parameters.AddWithValue("graph", GraphName);

        var existsResult = await existsCmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        var exists = existsResult is long longValue
            ? longValue > 0
            : Convert.ToInt64(existsResult, System.Globalization.CultureInfo.InvariantCulture) > 0;

        if (!exists)
        {
            await using var createCmd = npgsqlConnection.CreateCommand();
            createCmd.CommandText = "SELECT create_graph(@graph)";
            createCmd.Parameters.AddWithValue("graph", GraphName);
            await createCmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            logger.LogInformation("Created Apache AGE graph '{GraphName}'", GraphName);
        }
    }
}
