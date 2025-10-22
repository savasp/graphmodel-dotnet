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

using System.Data;
using System.Globalization;

using Cvoya.Graph.Model;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using Npgsql;

/// <summary>
/// Represents an Apache AGE backed transaction. Implementation details will follow once the provider plumbing is in place.
/// </summary>
internal sealed class AgeGraphTransaction : IGraphTransaction
{
    private readonly NpgsqlDataSource dataSource;
    private readonly bool isReadOnly;
    private readonly IsolationLevel isolationLevel;
    private readonly ILogger logger;
    private NpgsqlConnection? connection;
    private NpgsqlTransaction? transaction;
    private bool committed;
    private bool rolledBack;

    public AgeGraphTransaction(
        AgeGraphContext context,
        bool isReadOnly,
        IsolationLevel isolationLevel)
    {
        ArgumentNullException.ThrowIfNull(context);

        dataSource = context.DataSource;
        this.isReadOnly = isReadOnly;
        this.isolationLevel = isolationLevel;
        logger = context.LoggerFactory.CreateLogger<AgeGraphTransaction>()
            ?? NullLogger<AgeGraphTransaction>.Instance;
        GraphName = context.GraphName;
    }

    internal string GraphName { get; }

    internal NpgsqlTransaction Transaction => transaction
        ?? throw new GraphException("Transaction has not been started. Call BeginTransactionAsync first.");

    internal NpgsqlConnection Connection => connection
        ?? throw new GraphException("Connection has not been opened. Call BeginTransactionAsync first.");

    public bool IsActive => transaction != null && !committed && !rolledBack;

    public async Task BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (transaction != null)
        {
            return;
        }

    connection = await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
    await ConfigureSessionAsync(connection, cancellationToken).ConfigureAwait(false);
    await EnsureGraphExistsAsync(connection, cancellationToken).ConfigureAwait(false);

        transaction = await connection.BeginTransactionAsync(isolationLevel, cancellationToken).ConfigureAwait(false);

        if (isReadOnly)
        {
            await using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = "SET TRANSACTION READ ONLY";
            await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        logger.LogDebug("Started AGE transaction for graph '{GraphName}' with isolation {Isolation}", GraphName, isolationLevel);
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

        if (connection != null)
        {
            await connection.DisposeAsync().ConfigureAwait(false);
            connection = null;
        }
    }

    public void Dispose()
    {
        DisposeAsync().GetAwaiter().GetResult();
    }

    private async Task ConfigureSessionAsync(NpgsqlConnection npgsqlConnection, CancellationToken cancellationToken)
    {
        try
        {
            await using var loadCmd = npgsqlConnection.CreateCommand();
            loadCmd.CommandText = "LOAD 'age'";
            await loadCmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (PostgresException ex) when (ex.SqlState == "58P01")
        {
            logger.LogWarning("Apache AGE extension is not installed: {Message}", ex.Message);
            throw new GraphException("Apache AGE extension is not installed", ex);
        }

        await using var searchPathCmd = npgsqlConnection.CreateCommand();
        searchPathCmd.CommandText = "SET search_path = ag_catalog, \"$user\", public";
        await searchPathCmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
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
