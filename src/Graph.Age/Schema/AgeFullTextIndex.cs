// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Age.Schema;

using Cvoya.Graph.Age;
using Cvoya.Graph.Age.Querying;
using Cvoya.Graph.Age.Serialization;
using Npgsql;

/// <summary>
/// Creates the coarse blob-level GIN indexes that accelerate searches of the legacy CVOYA tables.
/// Native logical and externally managed label tables remain correct through phase one's inline
/// function-free predicate. When a managed index is present, phase-one SQL AND-s its coarse
/// expression with the precise per-type predicate, so the result set never changes with or without
/// managed infrastructure.
/// </summary>
internal static class AgeFullTextIndex
{
    /// <summary>The GIN index over the node label table.</summary>
    internal const string NodeIndexName = "cvoya_node_fulltext_gin";

    /// <summary>The GIN index over the relationship label table.</summary>
    internal const string RelationshipIndexName = "cvoya_rel_fulltext_gin";

    /// <summary>Drops and rebuilds both indexes, refreshing the extraction function between them.</summary>
    internal static async Task RecreateAsync(
        NpgsqlConnection connection,
        string graphName,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);
        AgeSqlIdentifier.Validate(graphName, "graph name");

        await ExecuteAsync(connection, DropIndexSql(graphName, NodeIndexName), cancellationToken).ConfigureAwait(false);
        await ExecuteAsync(connection, DropIndexSql(graphName, RelationshipIndexName), cancellationToken).ConfigureAwait(false);
        await ExecuteAsync(connection, CreateBlobFunctionSql(graphName), cancellationToken).ConfigureAwait(false);
        await ExecuteAsync(
            connection,
            CreateIndexSql(graphName, SerializationBridge.PhysicalNodeLabel, NodeIndexName),
            cancellationToken).ConfigureAwait(false);
        await ExecuteAsync(
            connection,
            CreateIndexSql(graphName, SerializationBridge.PhysicalRelationshipType, RelationshipIndexName),
            cancellationToken).ConfigureAwait(false);
    }

    // The extraction function must be IMMUTABLE end-to-end or Postgres refuses the expression index.
    // It excludes the framework's internal keys so the coarse text mirrors the dynamic-search predicate
    // exactly. It is created in the graph's OWN schema (not a shared one), so concurrent per-graph
    // provisioning never contends on a single catalog tuple; the phase-1 coarse conjunct references it
    // by the same schema-qualified name so the planner matches the index expression textually.
    private static string CreateBlobFunctionSql(string graphName) =>
        $$"""
        CREATE OR REPLACE FUNCTION {{AgeFullTextSearch.BlobFunctionRef(graphName)}}(props ag_catalog.agtype)
        RETURNS text
        LANGUAGE sql
        IMMUTABLE
        PARALLEL SAFE
        RETURNS NULL ON NULL INPUT
        AS $fn$
          SELECT string_agg(kv.value #>> '{}', ' ')
          FROM jsonb_each(props::text::jsonb) AS kv(key, value)
          WHERE jsonb_typeof(kv.value) = 'string'
            AND kv.key NOT IN ('inheritance_labels', '{{SerializationBridge.EntityKindPropertyName}}', '{{SerializationBridge.MetadataPropertyName}}')
        $fn$;
        """;

    private static string CreateIndexSql(string graphName, string table, string indexName) =>
        $"CREATE INDEX IF NOT EXISTS {AgeSqlIdentifier.Quote(indexName, "index name")} " +
        $"ON {AgeSqlIdentifier.Quote(graphName, "graph name")}.{AgeSqlIdentifier.Quote(table, "table name")} " +
        $"USING GIN (to_tsvector('simple', {AgeFullTextSearch.BlobFunctionRef(graphName)}(properties)))";

    private static string DropIndexSql(string graphName, string indexName) =>
        $"DROP INDEX IF EXISTS {AgeSqlIdentifier.Quote(graphName, "graph name")}." +
        AgeSqlIdentifier.Quote(indexName, "index name");

    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Security",
        "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "Callers build the SQL from validated SQL identifiers and compile-time constants; " +
            "no user input reaches this statement.")]
    private static async Task ExecuteAsync(
        NpgsqlConnection connection,
        string sql,
        CancellationToken cancellationToken)
    {
        var command = connection.CreateCommand();
        await using var commandLease = command.ConfigureAwait(false);
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}
