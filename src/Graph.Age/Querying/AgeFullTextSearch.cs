// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Age.Querying;

using Cvoya.Graph.Age.Entities;
using Cvoya.Graph.Age.Querying.Cypher;
using Cvoya.Graph.Age.Querying.Cypher.Execution;
using Cvoya.Graph.Age.Schema;
using Cvoya.Graph.Age.Serialization;
using Cvoya.Graph.Querying;

/// <summary>
/// Phase 1 of AGE's two-phase full-text search. PostgreSQL searches the physical AGE label tables
/// and returns transaction-local graphids; the residual Cypher query correlates those graphids with
/// <c>id(alias)</c> through <see cref="AgeFullTextSearchRewriter"/>.
/// </summary>
internal static class AgeFullTextSearch
{
    /// <summary>The maximum combined, deduplicated graphid set accepted by one search.</summary>
    internal const int MaxMatchedIds = 10_000;

    /// <summary>The managed extraction function used only when its matching GIN index is present.</summary>
    internal const string BlobFunctionName = "age_fulltext_blob";

    /// <summary>The schema-qualified reference to the blob function for <paramref name="graphName"/>.</summary>
    internal static string BlobFunctionRef(string graphName) =>
        $"{AgeSqlIdentifier.Quote(graphName, "graph name")}.{BlobFunctionName}";

    internal enum FullTextTarget
    {
        Nodes,
        Relationships,
    }

    private enum SearchScope
    {
        Typed,
        Global,
        Dynamic,
    }

    /// <summary>A concrete candidate type and its included searchable properties.</summary>
    internal readonly record struct FullTextCandidate(
        string Label,
        IReadOnlyList<string> SearchableProperties);

    /// <summary>A catalog-vetted physical AGE table and its optional managed acceleration.</summary>
    internal readonly record struct GraphLabelTable(string Name, bool HasManagedIndex);

    /// <summary>One private phase-one match. None of this context reaches entity materialization.</summary>
    internal readonly record struct FullTextMatch(
        long GraphId,
        FullTextTarget Target,
        string StorageName);

    private sealed record FullTextTablePlan(
        GraphLabelTable Table,
        IReadOnlyList<FullTextCandidate> Candidates,
        bool MatchAllStringValues,
        bool IsLegacy);

    /// <summary>
    /// Runs phase 1 on the supplied runner's transaction and returns physical AGE graphids with
    /// private kind/table context. The shared transaction preserves visibility of uncommitted writes.
    /// </summary>
    internal static async Task<IReadOnlyList<FullTextMatch>> FindMatchesAsync(
        Type elementType,
        string query,
        SchemaRegistry schemaRegistry,
        AgeQueryRunner runner,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(elementType);
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(schemaRegistry);
        ArgumentNullException.ThrowIfNull(runner);

        var terms = FullTextQueryTokenizer.Tokenize(query);
        if (terms.Count == 0)
        {
            return [];
        }

        var normalizedQuery = string.Join(' ', terms);
        var (target, scope) = Classify(elementType);
        var tables = await runner
            .DiscoverFullTextTablesAsync(target == FullTextTarget.Relationships, cancellationToken)
            .ConfigureAwait(false);

        IReadOnlyList<FullTextCandidate> candidates = scope == SearchScope.Dynamic
            ? []
            : await ResolveCandidatesAsync(
                elementType,
                target,
                schemaRegistry,
                includeAllRegistered: scope == SearchScope.Global,
                cancellationToken).ConfigureAwait(false);
        if (scope == SearchScope.Typed && candidates.Count == 0)
        {
            return [];
        }

        var plans = BuildTablePlans(target, scope, candidates, tables);
        if (plans.Count == 0)
        {
            return [];
        }

        var sql = BuildSearchSql(runner.GraphName, target, plans);
        var matches = await runner
            .QueryFullTextMatchesAsync(sql, normalizedQuery, target, cancellationToken)
            .ConfigureAwait(false);
        EnforceIdSetLimit(matches.Count);
        return matches;
    }

    /// <summary>Fails when the combined distinct match set exceeds the provider bound.</summary>
    internal static void EnforceIdSetLimit(int matchedCount)
    {
        if (matchedCount > MaxMatchedIds)
        {
            throw new GraphException(
                $"Full-text search matched more than {MaxMatchedIds} entities, exceeding the AGE provider " +
                "limit. Narrow the query or add filters before searching.");
        }
    }

    private static (FullTextTarget Target, SearchScope Scope) Classify(Type elementType)
    {
        if (elementType == typeof(Graph.DynamicNode))
        {
            return (FullTextTarget.Nodes, SearchScope.Dynamic);
        }

        if (elementType == typeof(Graph.DynamicRelationship))
        {
            return (FullTextTarget.Relationships, SearchScope.Dynamic);
        }

        if (elementType == typeof(Graph.INode))
        {
            return (FullTextTarget.Nodes, SearchScope.Global);
        }

        if (elementType == typeof(Graph.IRelationship))
        {
            return (FullTextTarget.Relationships, SearchScope.Global);
        }

        if (elementType == typeof(Graph.IEntity))
        {
            return (FullTextTarget.Nodes, SearchScope.Global);
        }

        return typeof(Graph.IRelationship).IsAssignableFrom(elementType)
            ? (FullTextTarget.Relationships, SearchScope.Typed)
            : (FullTextTarget.Nodes, SearchScope.Typed);
    }

    private static async Task<IReadOnlyList<FullTextCandidate>> ResolveCandidatesAsync(
        Type elementType,
        FullTextTarget target,
        SchemaRegistry schemaRegistry,
        bool includeAllRegistered,
        CancellationToken cancellationToken)
    {
        IEnumerable<string> labels = includeAllRegistered
            ? target == FullTextTarget.Nodes
                ? await schemaRegistry.GetRegisteredNodeLabelsAsync(cancellationToken).ConfigureAwait(false)
                : await schemaRegistry.GetRegisteredRelationshipTypesAsync(cancellationToken).ConfigureAwait(false)
            : Labels.GetCompatibleLabels(elementType);

        var candidates = new List<FullTextCandidate>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var label in labels)
        {
            var schema = target == FullTextTarget.Nodes
                ? await schemaRegistry.GetNodeSchemaAsync(label, cancellationToken).ConfigureAwait(false)
                : await schemaRegistry.GetRelationshipSchemaAsync(label, cancellationToken).ConfigureAwait(false);
            if (schema is null || !seen.Add(schema.Label))
            {
                continue;
            }

            var searchable = schema.Properties.Values
                .Where(property => !property.Ignore
                    && property.PropertyInfo.PropertyType == typeof(string)
                    && property.IncludeInFullTextSearch)
                .Select(property => property.Name)
                .ToArray();
            if (searchable.Length > 0)
            {
                candidates.Add(new FullTextCandidate(schema.Label, searchable));
            }
        }

        return candidates;
    }

    private static List<FullTextTablePlan> BuildTablePlans(
        FullTextTarget target,
        SearchScope scope,
        IReadOnlyList<FullTextCandidate> candidates,
        IReadOnlyList<GraphLabelTable> tables)
    {
        var legacyTable = target == FullTextTarget.Nodes
            ? SerializationBridge.PhysicalNodeLabel
            : SerializationBridge.PhysicalRelationshipType;
        var candidatesByNativeLabel = candidates
            .Where(candidate => CypherIdentifier.IsNativeLabelName(candidate.Label))
            .ToDictionary(candidate => candidate.Label, StringComparer.Ordinal);
        var plans = new List<FullTextTablePlan>();

        foreach (var table in tables)
        {
            if (!IsSafePhysicalTable(table.Name))
            {
                continue;
            }

            var isLegacy = string.Equals(table.Name, legacyTable, StringComparison.Ordinal);
            if (scope == SearchScope.Dynamic)
            {
                plans.Add(new FullTextTablePlan(table, [], MatchAllStringValues: true, isLegacy));
                continue;
            }

            if (isLegacy)
            {
                if (candidates.Count > 0)
                {
                    plans.Add(new FullTextTablePlan(table, candidates, MatchAllStringValues: false, IsLegacy: true));
                }

                continue;
            }

            if (candidatesByNativeLabel.TryGetValue(table.Name, out var candidate))
            {
                plans.Add(new FullTextTablePlan(table, [candidate], MatchAllStringValues: false, IsLegacy: false));
            }
            else if (scope == SearchScope.Global)
            {
                // An externally managed label has no registered CLR schema, so its dynamic contract
                // is every string property. The residual global root still owns materialization.
                plans.Add(new FullTextTablePlan(table, [], MatchAllStringValues: true, IsLegacy: false));
            }
        }

        return plans;
    }

    private static bool IsSafePhysicalTable(string table)
    {
        if (table is "_ag_label_vertex" or "_ag_label_edge")
        {
            return false;
        }

        try
        {
            _ = AgeSqlIdentifier.Validate(table, "table name");
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private const string Props = "(properties::text::jsonb)";

    private static string ManagedPredicate(string graphName) =>
        $"to_tsvector('simple', {BlobFunctionRef(graphName)}(properties)) @@ " +
        "plainto_tsquery('simple', @query)";

    private static string FallbackPredicate =>
        "to_tsvector('simple', (SELECT string_agg(age_fulltext_value.value #>> '{}', ' ') " +
        $"FROM jsonb_each({Props}) AS age_fulltext_value(key, value) " +
        "WHERE jsonb_typeof(age_fulltext_value.value) = 'string' " +
        $"AND age_fulltext_value.key NOT IN ('{AgeElementMatcher.InheritanceLabelsProperty}', " +
        $"'{SerializationBridge.EntityKindPropertyName}', '{SerializationBridge.MetadataPropertyName}'))) " +
        "@@ plainto_tsquery('simple', @query)";

    private static string BuildSearchSql(
        string graphName,
        FullTextTarget target,
        IReadOnlyList<FullTextTablePlan> plans)
    {
        var branches = plans.Select(plan => BuildTableBranch(graphName, target, plan));
        return
            $"SELECT graph_id, entity_kind, min(storage_name) AS storage_name{Environment.NewLine}" +
            $"FROM ({Environment.NewLine}{string.Join($"{Environment.NewLine}UNION ALL{Environment.NewLine}", branches)}" +
            $"{Environment.NewLine}) AS age_fulltext_matches{Environment.NewLine}" +
            $"GROUP BY graph_id, entity_kind{Environment.NewLine}" +
            $"LIMIT {MaxMatchedIds + 1}";
    }

    private static string BuildTableBranch(
        string graphName,
        FullTextTarget target,
        FullTextTablePlan plan)
    {
        var precise = plan.MatchAllStringValues
            ? plan.Table.HasManagedIndex ? ManagedPredicate(graphName) : FallbackPredicate
            : BuildTypedPredicate(plan.Candidates, plan.IsLegacy);
        if (!plan.MatchAllStringValues && plan.Table.HasManagedIndex)
        {
            precise = $"{ManagedPredicate(graphName)} AND ({precise})";
        }

        var rootPredicate = LegacyRootPredicate(target, plan.IsLegacy);
        var predicate = rootPredicate is null ? precise : $"{rootPredicate} AND ({precise})";
        var entityKind = target == FullTextTarget.Nodes ? "Node" : "Relationship";
        return
            $"SELECT id::text::bigint AS graph_id, {SqlString(entityKind)} AS entity_kind, " +
            $"{SqlString(plan.Table.Name)} AS storage_name{Environment.NewLine}" +
            $"FROM ONLY {BaseTable(graphName, plan.Table.Name)}{Environment.NewLine}" +
            $"WHERE {predicate}";
    }

    private static string? LegacyRootPredicate(FullTextTarget target, bool isLegacy)
    {
        if (!isLegacy)
        {
            return null;
        }

        return target == FullTextTarget.Nodes
            ? $"NOT jsonb_exists({Props}, {SqlString(SerializationBridge.EntityKindPropertyName)})"
            : $"({Props} -> {SqlString(ComplexPropertyStorage.RelationshipMarkerProperty)}) " +
                "IS DISTINCT FROM 'true'::jsonb";
    }

    private static string BuildTypedPredicate(
        IReadOnlyList<FullTextCandidate> candidates,
        bool legacy) =>
        string.Join(
            $"{Environment.NewLine}       OR ",
            candidates.Select(candidate => BuildCandidatePredicate(candidate, legacy)));

    private static string BuildCandidatePredicate(FullTextCandidate candidate, bool legacy)
    {
        var extractions = string.Join(
            ", ",
            candidate.SearchableProperties.Select(property => $"{Props} ->> {SqlString(property)}"));
        var textPredicate =
            $"to_tsvector('simple', concat_ws(' ', {extractions})) @@ " +
            "plainto_tsquery('simple', @query)";
        return legacy
            ? $"({textPredicate} AND jsonb_exists({Props} -> " +
                $"'{AgeElementMatcher.InheritanceLabelsProperty}', {SqlString(candidate.Label)}))"
            : $"({textPredicate})";
    }

    /// <summary>Provider-free seam for one typed table's combined graphid SQL.</summary>
    internal static string BuildTypedSql(
        string graphName,
        string table,
        IReadOnlyList<FullTextCandidate> candidates,
        bool relationship = false,
        bool hasManagedIndex = false)
    {
        var target = relationship ? FullTextTarget.Relationships : FullTextTarget.Nodes;
        var legacyTable = relationship
            ? SerializationBridge.PhysicalRelationshipType
            : SerializationBridge.PhysicalNodeLabel;
        return BuildSearchSql(
            graphName,
            target,
            [new FullTextTablePlan(
                new GraphLabelTable(table, hasManagedIndex),
                candidates,
                MatchAllStringValues: false,
                IsLegacy: string.Equals(table, legacyTable, StringComparison.Ordinal))]);
    }

    /// <summary>Provider-free seam for one dynamic table's combined graphid SQL.</summary>
    internal static string BuildDynamicSql(
        string graphName,
        string table,
        bool relationship = false,
        bool hasManagedIndex = false) =>
        BuildSearchSql(
            graphName,
            relationship ? FullTextTarget.Relationships : FullTextTarget.Nodes,
            [new FullTextTablePlan(
                new GraphLabelTable(table, hasManagedIndex),
                [],
                MatchAllStringValues: true,
                IsLegacy: string.Equals(
                    table,
                    relationship
                        ? SerializationBridge.PhysicalRelationshipType
                        : SerializationBridge.PhysicalNodeLabel,
                    StringComparison.Ordinal))]);

    private static string BaseTable(string graphName, string table) =>
        $"{AgeSqlIdentifier.Quote(graphName, "graph name")}.{AgeSqlIdentifier.Quote(table, "table name")}";

    private static string SqlString(string value) =>
        $"'{value.Replace("'", "''", StringComparison.Ordinal)}'";
}
