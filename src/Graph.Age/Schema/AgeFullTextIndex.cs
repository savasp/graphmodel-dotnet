// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Age.Schema;

using Cvoya.Graph.Age;
using Cvoya.Graph.Age.Querying;
using Cvoya.Graph.Age.Serialization;
using Npgsql;

/// <summary>
/// Creates the coarse blob-level GIN indexes that accelerate full-text search (#291). AGE stores every
/// node in one table with one <c>agtype</c> blob, so a per-type index is impossible; instead a single
/// GIN expression index per label table covers a fixed, <c>IMMUTABLE</c> "all string values" tsvector.
/// Phase-1 SQL AND-s a coarse conjunct matching that expression verbatim with the precise per-type
/// predicate, so Postgres uses the index and rechecks the precise conjunct — coarse ⊇ precise, so the
/// result set never changes, index or no index.
/// </summary>
internal static class AgeFullTextIndex
{
    /// <summary>The GIN index over the node label table.</summary>
    internal const string NodeIndexName = "cvoya_node_fulltext_gin";

    /// <summary>The GIN index over the relationship label table.</summary>
    internal const string RelationshipIndexName = "cvoya_rel_fulltext_gin";

    /// <summary>
    /// Idempotently creates the extraction function and both GIN indexes on the supplied connection.
    /// Both label tables must already exist (graph provisioning creates them via a throwaway subgraph),
    /// so this runs after provisioning. <c>CREATE INDEX IF NOT EXISTS</c> without <c>CONCURRENTLY</c> is
    /// correct here: it runs at provisioning time on an empty or small store, so the brief table lock is
    /// harmless and keeps the operation transactional.
    /// </summary>
    public static async Task EnsureAsync(
        NpgsqlConnection connection,
        string graphName,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);
        AgeSqlIdentifier.Validate(graphName, "graph name");

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
          SELECT concat_ws(' ', array_agg(kv.value))
          FROM jsonb_each_text(props::text::jsonb) AS kv(key, value)
          WHERE kv.key NOT IN ('inheritance_labels', '{{SerializationBridge.EntityKindPropertyName}}', '{{SerializationBridge.MetadataPropertyName}}')
        $fn$;
        """;

    private static string CreateIndexSql(string graphName, string table, string indexName) =>
        $"CREATE INDEX IF NOT EXISTS {AgeSqlIdentifier.Quote(indexName, "index name")} " +
        $"ON {AgeSqlIdentifier.Quote(graphName, "graph name")}.{AgeSqlIdentifier.Quote(table, "table name")} " +
        $"USING GIN (to_tsvector('simple', {AgeFullTextSearch.BlobFunctionRef(graphName)}(properties)))";

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
