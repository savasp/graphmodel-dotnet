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
    private bool disposed;
    private volatile bool provisioned;

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
    {
        this.dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
        ownsDataSource = false;
        GraphName = ResolveGraphName(graphName);
        LoggerFactory = loggerFactory ?? NullLoggerFactory.Instance;
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
        if (disposed)
        {
            return;
        }

        disposed = true;
        if (ownsDataSource)
        {
            await dataSource.DisposeAsync().ConfigureAwait(false);
        }
    }

    internal string GraphName { get; }

    internal ILoggerFactory LoggerFactory { get; }

    internal async Task<NpgsqlConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
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

    internal async Task CreateGraphIfNotExistsAsync(
        NpgsqlConnection connection,
        CancellationToken cancellationToken)
    {
        var exists = connection.CreateCommand();
        await using var existsLease = exists.ConfigureAwait(false);
        exists.CommandText = "SELECT EXISTS (SELECT 1 FROM ag_catalog.ag_graph WHERE name = @name)";
        exists.Parameters.AddWithValue("name", GraphName);
        if (await exists.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) is true)
        {
            await EnsurePhysicalLabelsAsync(connection, cancellationToken).ConfigureAwait(false);
            await Schema.AgeFullTextIndex.EnsureAsync(connection, GraphName, cancellationToken).ConfigureAwait(false);
            provisioned = true;
            return;
        }

        var create = connection.CreateCommand();
        await using var createLease = create.ConfigureAwait(false);
        create.CommandText = "SELECT ag_catalog.create_graph(@name)";
        create.Parameters.AddWithValue("name", GraphName);
        await create.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        await EnsurePhysicalLabelsAsync(connection, cancellationToken).ConfigureAwait(false);
        await Schema.AgeFullTextIndex.EnsureAsync(connection, GraphName, cancellationToken).ConfigureAwait(false);
        provisioned = true;
    }

    /// <summary>
    /// Provisions the graph on first use only. The existence probe and physical-label round-trips
    /// would otherwise run (and write) on every transaction begin, including read-only ones.
    /// </summary>
    internal async Task EnsureGraphProvisionedAsync(
        NpgsqlConnection connection,
        CancellationToken cancellationToken)
    {
        if (provisioned)
        {
            return;
        }

        await CreateGraphIfNotExistsAsync(connection, cancellationToken).ConfigureAwait(false);
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Security",
        "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "GraphName is validated by AgeSqlIdentifier.Validate at construction; the labels are compile-time constants.")]
    private async Task EnsurePhysicalLabelsAsync(
        NpgsqlConnection connection,
        CancellationToken cancellationToken)
    {
        var command = connection.CreateCommand();
        await using var commandLease = command.ConfigureAwait(false);
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
