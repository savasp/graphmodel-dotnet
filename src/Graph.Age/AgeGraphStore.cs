// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Age;

using Cvoya.Graph.Age.Core;
using Cvoya.Graph.Age.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Npgsql.Age;

/// <summary>Represents an Apache AGE graph hosted by PostgreSQL.</summary>
/// <remarks>
/// A store created from a connection string owns its <see cref="NpgsqlDataSource"/>. A store
/// created from an existing data source leaves that data source under caller ownership. AGE is
/// loaded and its search path configured whenever a provider connection is opened.
/// </remarks>
public sealed class AgeGraphStore : IAsyncDisposable
{
    private readonly NpgsqlDataSource dataSource;
    private readonly bool ownsDataSource;
    private readonly SemaphoreSlim provisioningGate = new(1, 1);
    private int disposed;
    private volatile bool graphReady;

    /// <summary>Initializes a store from a PostgreSQL connection string.</summary>
    /// <param name="connectionString">The connection string, or <see langword="null"/> to use <c>AGE_CONNECTION_STRING</c>.</param>
    /// <param name="graphName">The AGE graph name, or <see langword="null"/> to use <c>AGE_GRAPH</c> or <c>cvoya_graph</c>.</param>
    /// <param name="schemaRegistry">An optional schema registry.</param>
    /// <param name="loggerFactory">An optional logger factory.</param>
    public AgeGraphStore(
        string? connectionString = null,
        string? graphName = null,
        SchemaRegistry? schemaRegistry = null,
        ILoggerFactory? loggerFactory = null)
    {
        connectionString ??= Environment.GetEnvironmentVariable("AGE_CONNECTION_STRING");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Apache AGE connection string must be provided through the connectionString argument or AGE_CONNECTION_STRING.");
        }

        var builder = new NpgsqlDataSourceBuilder(connectionString);
        builder.UseAge();
        dataSource = builder.Build();
        ownsDataSource = true;
        GraphName = ResolveGraphName(graphName);
        LoggerFactory = loggerFactory ?? NullLoggerFactory.Instance;
        Graph = new AgeGraph(this, GraphName, schemaRegistry ?? new SchemaRegistry(), LoggerFactory);
    }

    /// <summary>Initializes a store from an externally owned AGE-enabled data source.</summary>
    /// <param name="dataSource">A data source configured with <c>UseAge()</c>.</param>
    /// <param name="graphName">The AGE graph name.</param>
    /// <param name="schemaRegistry">An optional schema registry.</param>
    /// <param name="loggerFactory">An optional logger factory.</param>
    public AgeGraphStore(
        NpgsqlDataSource dataSource,
        string graphName,
        SchemaRegistry? schemaRegistry = null,
        ILoggerFactory? loggerFactory = null)
        : this(dataSource, graphName, schemaRegistry, loggerFactory, batchExecutionObserver: null)
    {
    }

    internal AgeGraphStore(
        NpgsqlDataSource dataSource,
        string graphName,
        SchemaRegistry? schemaRegistry,
        ILoggerFactory? loggerFactory,
        Action? batchExecutionObserver)
    {
        this.dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
        ownsDataSource = false;
        GraphName = ResolveGraphName(graphName);
        LoggerFactory = loggerFactory ?? NullLoggerFactory.Instance;
        BatchExecutionObserver = batchExecutionObserver;
        Graph = new AgeGraph(this, GraphName, schemaRegistry ?? new SchemaRegistry(), LoggerFactory);
    }

    /// <summary>Gets the graph abstraction for this store.</summary>
    public IGraph Graph { get; }

    /// <summary>Creates the configured AGE graph when it does not already exist.</summary>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    public async Task CreateGraphIfNotExistsAsync(CancellationToken cancellationToken = default)
    {
        var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var connectionLease = connection.ConfigureAwait(false);
        await CreateGraphIfNotExistsAsync(connection, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0)
        {
            return;
        }

        // SemaphoreSlim.Dispose cannot run concurrently with WaitAsync or Release. A caller may have
        // passed OpenConnectionAsync's disposed check without reaching the gate yet, so its current
        // count cannot prove that the gate is quiescent. AvailableWaitHandle is never read, meaning the
        // gate owns no operating-system handle and can safely be reclaimed with the store instead.

        if (ownsDataSource)
        {
            await dataSource.DisposeAsync().ConfigureAwait(false);
        }
    }

    internal string GraphName { get; }

    internal ILoggerFactory LoggerFactory { get; }

    /// <summary>
    /// Test instrumentation invoked immediately before a graph batch crosses the Npgsql execution
    /// boundary. It deliberately stays internal so measuring round trips does not expand the public API.
    /// </summary>
    internal Action? BatchExecutionObserver { get; }

    /// <summary>
    /// Test instrumentation invoked once per execution of the graph-creation sequence, including
    /// attempts that go on to fail. It deliberately stays internal so counting provisioning runs does
    /// not expand the public API.
    /// </summary>
    internal Action? ProvisioningObserver { get; set; }

    /// <summary>
    /// Test instrumentation invoked when a caller reaches the provisioning gate, before it waits on
    /// it. Paired with <see cref="ProvisioningObserver"/> it lets a test prove every racer was
    /// committed to the gate while a sequence was still in flight, rather than arriving after one
    /// finished and taking the graph-ready fast path - which would leave the test passing vacuously.
    /// </summary>
    internal Action? ProvisioningGateObserver { get; set; }

    internal async Task<NpgsqlConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref disposed) != 0, this);
        var connection = await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await ConfigureConnectionAsync(connection, cancellationToken).ConfigureAwait(false);
            return connection;
        }
        catch
        {
            await connection.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>
    /// Runs the shared provisioning core unconditionally. An explicit request to create the graph
    /// re-verifies the database rather than trusting what this store created earlier, because the
    /// caller may be recovering from a drop this store never observed.
    /// </summary>
    internal Task CreateGraphIfNotExistsAsync(
        NpgsqlConnection connection,
        CancellationToken cancellationToken) =>
        ProvisionAsync(connection, skipWhenProvisioned: false, cancellationToken);

    /// <summary>
    /// Creates the graph on first write use only. Read-only transactions use
    /// <see cref="EnsureGraphExistsAsync"/> and never enter this mutating path.
    /// </summary>
    internal Task EnsureGraphCreatedAsync(
        NpgsqlConnection connection,
        CancellationToken cancellationToken) =>
        graphReady
            ? Task.CompletedTask
            : ProvisionAsync(connection, skipWhenProvisioned: true, cancellationToken);

    /// <summary>Verifies that the configured graph exists without mutating database state.</summary>
    internal async Task EnsureGraphExistsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        if (!await GraphExistsAsync(connection, transaction, cancellationToken).ConfigureAwait(false))
        {
            throw new GraphException($"Apache AGE graph '{GraphName}' does not exist.");
        }
    }

    /// <summary>
    /// Serializes graph creation within this store and publishes <see cref="graphReady"/> only after the
    /// graph-creation sequence has succeeded.
    /// </summary>
    /// <remarks>
    /// The gate keeps concurrent first-use operations on one store from each running the sequence;
    /// <see cref="Schema.AgeProvisioningLock"/> extends that to the peers this store cannot see. Because
    /// the flag is published last, a failed or cancelled attempt leaves the graph unconfirmed and the
    /// next caller free to retry.
    /// </remarks>
    private async Task ProvisionAsync(
        NpgsqlConnection connection,
        bool skipWhenProvisioned,
        CancellationToken cancellationToken)
    {
        ProvisioningGateObserver?.Invoke();

        await provisioningGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // The second check: callers that queued behind the winning first-use initialization
            // inherit its result instead of each repeating it.
            if (skipWhenProvisioned && graphReady)
            {
                return;
            }

            // An explicit call re-verifies a graph that may have been dropped since this store first
            // created it. Clear the cached success before that attempt so a failure or cancellation
            // cannot leave later implicit first use trusting stale state.
            graphReady = false;
            await ProvisionCoreAsync(connection, cancellationToken).ConfigureAwait(false);
            graphReady = true;
        }
        finally
        {
            provisioningGate.Release();
        }
    }

    /// <summary>
    /// The one idempotent opening sequence for writes: probe for the graph and create only the graph
    /// itself when absent. Logical label/type tables are created by the authorized write that first
    /// needs them; managed full-text infrastructure is explicit through <c>RecreateIndexesAsync</c>.
    /// </summary>
    /// <remarks>
    /// It runs in a single transaction holding <see cref="Schema.AgeProvisioningLock"/>, so peers racing
    /// the same graph name run it one at a time and a failure part-way rolls the graph back instead of
    /// leaving a half-created graph behind. Nothing is interpreted as an "already exists" race here:
    /// permission, connectivity, syntax, timeout, and cancellation failures all propagate as themselves.
    /// </remarks>
    private async Task ProvisionCoreAsync(
        NpgsqlConnection connection,
        CancellationToken cancellationToken)
    {
        ProvisioningObserver?.Invoke();

        var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await using var transactionLease = transaction.ConfigureAwait(false);
        await Schema.AgeProvisioningLock
            .AcquireAsync(connection, transaction, GraphName, cancellationToken)
            .ConfigureAwait(false);

        if (!await GraphExistsAsync(connection, transaction, cancellationToken).ConfigureAwait(false))
        {
            await CreateGraphAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<bool> GraphExistsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        var command = connection.CreateCommand();
        await using var commandLease = command.ConfigureAwait(false);
        command.Transaction = transaction;
        command.CommandText = "SELECT EXISTS (SELECT 1 FROM ag_catalog.ag_graph WHERE name = @name)";
        command.Parameters.AddWithValue("name", GraphName);
        return await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) is true;
    }

    private async Task CreateGraphAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        var command = connection.CreateCommand();
        await using var commandLease = command.ConfigureAwait(false);
        command.Transaction = transaction;
        command.CommandText = "SELECT ag_catalog.create_graph(@name)";
        command.Parameters.AddWithValue("name", GraphName);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Explicitly creates the legacy managed tables needed by the current full-text implementation.
    /// Ordinary reads and writes never call this method.
    /// </summary>
    internal async Task EnsureManagedFullTextTablesAsync(
        NpgsqlConnection connection,
        CancellationToken cancellationToken)
    {
        await EnsureGraphCreatedAsync(connection, cancellationToken).ConfigureAwait(false);

        var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await using var transactionLease = transaction.ConfigureAwait(false);
        await Schema.AgeProvisioningLock
            .AcquireAsync(connection, transaction, GraphName, cancellationToken)
            .ConfigureAwait(false);
        await EnsurePhysicalLabelsAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Security",
        "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "GraphName is validated by AgeSqlIdentifier.Validate at construction; the labels are compile-time constants.")]
    private async Task EnsurePhysicalLabelsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        var command = connection.CreateCommand();
        await using var commandLease = command.ConfigureAwait(false);
        command.Transaction = transaction;
        command.CommandText = $"""
            SELECT *
            FROM ag_catalog.cypher(
                '{GraphName}',
                $$CREATE (source:{SerializationBridge.PhysicalNodeLabel})
                  CREATE (target:{SerializationBridge.PhysicalNodeLabel})
                  CREATE (source)-[relationship:{SerializationBridge.PhysicalRelationshipType}]->(target)
                  DELETE relationship, source, target
                  RETURN true AS provisioned$$)
            AS (provisioned agtype)
            """;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static string ResolveGraphName(string? graphName)
    {
        graphName ??= Environment.GetEnvironmentVariable("AGE_GRAPH") ?? "cvoya_graph";
        return AgeSqlIdentifier.Validate(graphName, "graph name");
    }

    private static async Task ConfigureConnectionAsync(
        NpgsqlConnection connection,
        CancellationToken cancellationToken)
    {
        var command = connection.CreateCommand();
        await using var commandLease = command.ConfigureAwait(false);
        command.CommandText = "LOAD 'age'; SET search_path = ag_catalog, \"$user\", public";
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}
