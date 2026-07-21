// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Age.Querying;

using System.Collections.Frozen;
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
/// <remarks>
/// Every predicate this class builds preserves two invariants:
/// <list type="bullet">
/// <item>The <c>'simple'</c> regconfig is required: no stemming and no stop-word removal, so the match
/// set sits exactly on the cross-provider contract floor (case-insensitive, whole-token, all-terms).
/// <c>'english'</c> would make <c>Search("the")</c> diverge from the other providers.</item>
/// <item>The shared tokenizer normalizes raw text before its terms reach Postgres through
/// <c>plainto_tsquery('simple', @query)</c> as a bind parameter: never <c>to_tsquery</c>, never string
/// interpolation.</item>
/// </list>
/// </remarks>
internal static class AgeFullTextSearch
{
    /// <summary>
    /// The maximum combined, deduplicated graphid set accepted by one search. The graphid list rides
    /// to AGE as one <c>agtype</c> parameter blob, so past this bound the provider fails informatively
    /// rather than build an unbounded parameter. Phase-one SQL fetches one row past the limit, so any
    /// count above it means the true match set is larger still. Documented in <c>COMPLIANCE.md</c>.
    /// </summary>
    internal const int MaxMatchedIds = 10_000;

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

    /// <summary>A catalog-vetted physical AGE table.</summary>
    internal readonly record struct GraphLabelTable(string Name);

    /// <summary>One private phase-one match. None of this context reaches entity materialization.</summary>
    internal readonly record struct FullTextMatch(
        long GraphId,
        FullTextTarget Target);

    private sealed record FullTextTablePlan(
        GraphLabelTable Table,
        IReadOnlyList<FullTextCandidate> Candidates,
        bool MatchAllStringValues);

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

        IReadOnlyList<FullTextCandidate> candidates = [];
        IReadOnlySet<string> registeredLabels = FrozenSet<string>.Empty;
        if (scope != SearchScope.Dynamic)
        {
            (candidates, registeredLabels) = await ResolveCandidatesAsync(
                elementType,
                target,
                schemaRegistry,
                includeAllRegistered: scope == SearchScope.Global,
                cancellationToken).ConfigureAwait(false);
        }

        if (scope == SearchScope.Typed && candidates.Count == 0)
        {
            return [];
        }

        var plans = BuildTablePlans(scope, candidates, registeredLabels, tables);
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

    /// <summary>
    /// Resolves the searchable candidates and, separately, every label whose CLR schema was
    /// consulted. The two differ: a registered type that includes no property in full-text search
    /// contributes no candidate but is still registered, and must not be mistaken for an
    /// externally managed label whose contract is "every string value".
    /// </summary>
    private static async Task<(IReadOnlyList<FullTextCandidate> Candidates, IReadOnlySet<string> RegisteredLabels)>
        ResolveCandidatesAsync(
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
        var registered = new HashSet<string>(StringComparer.Ordinal);
        foreach (var label in labels)
        {
            var schema = target == FullTextTarget.Nodes
                ? await schemaRegistry.GetNodeSchemaAsync(label, cancellationToken).ConfigureAwait(false)
                : await schemaRegistry.GetRelationshipSchemaAsync(label, cancellationToken).ConfigureAwait(false);
            if (schema is null || !registered.Add(schema.Label))
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

        return (candidates, registered);
    }

    private static List<FullTextTablePlan> BuildTablePlans(
        SearchScope scope,
        IReadOnlyList<FullTextCandidate> candidates,
        IReadOnlySet<string> registeredLabels,
        IReadOnlyList<GraphLabelTable> tables)
    {
        var candidatesByNativeLabel = candidates
            .Where(candidate => CypherIdentifier.IsNativeLabelName(candidate.Label))
            .ToDictionary(candidate => candidate.Label, StringComparer.Ordinal);
        var plans = new List<FullTextTablePlan>();

        foreach (var table in tables)
        {
            if (!IsSearchablePhysicalTable(table.Name))
            {
                continue;
            }

            if (scope == SearchScope.Dynamic)
            {
                plans.Add(new FullTextTablePlan(table, [], MatchAllStringValues: true));
                continue;
            }

            if (candidatesByNativeLabel.TryGetValue(table.Name, out var candidate))
            {
                plans.Add(new FullTextTablePlan(table, [candidate], MatchAllStringValues: false));
            }
            else if (scope == SearchScope.Global && !registeredLabels.Contains(table.Name))
            {
                // An externally managed label has no registered CLR schema, so its dynamic contract
                // is every string property. The residual global root still owns materialization.
                // A registered label that reached here instead contributes no branch: it includes no
                // property in full-text search, so its declared match set is empty.
                plans.Add(new FullTextTablePlan(table, [], MatchAllStringValues: true));
            }
        }

        return plans;
    }

    internal static bool IsSearchablePhysicalTable(string table)
    {
        if (table is "_ag_label_vertex" or "_ag_label_edge"
            || string.Equals(table, SerializationBridge.ComplexNodeLabel, StringComparison.Ordinal)
            || string.Equals(table, SerializationBridge.ComplexRelationshipType, StringComparison.Ordinal))
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

    // agtype -> text -> jsonb once per row so the precise predicates read plain JSON.
    private const string Props = "(properties::text::jsonb)";

    // The all-string predicate is inlined so an ordinary read needs no managed infrastructure and
    // no DDL permission. It is used for dynamic entities and genuinely external unregistered labels.
    private static string FallbackPredicate =>
        "to_tsvector('simple', (SELECT string_agg(age_fulltext_value.value #>> '{}', ' ') " +
        $"FROM jsonb_each({Props}) AS age_fulltext_value(key, value) " +
        "WHERE jsonb_typeof(age_fulltext_value.value) = 'string')) " +
        "@@ plainto_tsquery('simple', @query)";

    private static string BuildSearchSql(
        string graphName,
        FullTextTarget target,
        IReadOnlyList<FullTextTablePlan> plans)
    {
        var branches = plans.Select(plan => BuildTableBranch(graphName, target, plan));
        return
            $"SELECT DISTINCT graph_id, entity_kind{Environment.NewLine}" +
            $"FROM ({Environment.NewLine}{string.Join($"{Environment.NewLine}UNION ALL{Environment.NewLine}", branches)}" +
            $"{Environment.NewLine}) AS age_fulltext_matches{Environment.NewLine}" +
            $"LIMIT {MaxMatchedIds + 1}";
    }

    private static string BuildTableBranch(
        string graphName,
        FullTextTarget target,
        FullTextTablePlan plan)
    {
        var precise = plan.MatchAllStringValues
            ? FallbackPredicate
            : BuildTypedPredicate(plan.Candidates);
        var entityKind = target == FullTextTarget.Nodes ? "Node" : "Relationship";
        return
            $"SELECT id::text::bigint AS graph_id, {SqlString(entityKind)} AS entity_kind{Environment.NewLine}" +
            $"FROM ONLY {BaseTable(graphName, plan.Table.Name)}{Environment.NewLine}" +
            $"WHERE {precise}";
    }

    private static string BuildTypedPredicate(IReadOnlyList<FullTextCandidate> candidates) =>
        string.Join(
            $"{Environment.NewLine}       OR ",
            candidates.Select(BuildCandidatePredicate));

    private static string BuildCandidatePredicate(FullTextCandidate candidate)
    {
        var extractions = string.Join(
            ", ",
            candidate.SearchableProperties.Select(property => $"{Props} ->> {SqlString(property)}"));
        var textPredicate =
            $"to_tsvector('simple', concat_ws(' ', {extractions})) @@ " +
            "plainto_tsquery('simple', @query)";
        return $"({textPredicate})";
    }

    /// <summary>Provider-free seam for one typed table's combined graphid SQL.</summary>
    internal static string BuildTypedSql(
        string graphName,
        string table,
        IReadOnlyList<FullTextCandidate> candidates,
        bool relationship = false)
    {
        var target = relationship ? FullTextTarget.Relationships : FullTextTarget.Nodes;
        return BuildSearchSql(
            graphName,
            target,
            [new FullTextTablePlan(
                new GraphLabelTable(table),
                candidates,
                MatchAllStringValues: false)]);
    }

    /// <summary>Provider-free seam for one dynamic table's combined graphid SQL.</summary>
    internal static string BuildDynamicSql(
        string graphName,
        string table,
        bool relationship = false) =>
        BuildSearchSql(
            graphName,
            relationship ? FullTextTarget.Relationships : FullTextTarget.Nodes,
            [new FullTextTablePlan(
                new GraphLabelTable(table),
                [],
                MatchAllStringValues: true)]);

    // Each branch reads one catalog-vetted label table directly. graphName and the table name are
    // validated as SQL identifiers; the search text is the only user input and rides as the @query
    // bind parameter.
    private static string BaseTable(string graphName, string table) =>
        $"{AgeSqlIdentifier.Quote(graphName, "graph name")}.{AgeSqlIdentifier.Quote(table, "table name")}";

    private static string SqlString(string value) =>
        $"'{value.Replace("'", "''", StringComparison.Ordinal)}'";
}
