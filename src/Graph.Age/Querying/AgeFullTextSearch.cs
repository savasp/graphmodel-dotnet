// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Age.Querying;

using Cvoya.Graph.Age.Querying.Cypher.Execution;
using Cvoya.Graph.Age.Serialization;

/// <summary>
/// Phase 1 of AGE's two-phase full-text search. AGE cannot express full-text matching in its Cypher
/// subset, so its native mechanism is a Postgres text-search query run as plain SQL over the label
/// tables. This class builds that SQL and returns the public <c>Id</c> values of the matching
/// entities; the query rewriter (<see cref="AgeFullTextSearchRewriter"/>) then replaces the
/// <c>Search(...)</c> operator with a <c>Where(e =&gt; ids.Contains(e.Id))</c> filter so the entirely
/// unchanged shared planner and renderer serve the residual, search-free query.
/// </summary>
/// <remarks>
/// The predicate defines the semantics the later GIN-index work (#291) must preserve:
/// <list type="bullet">
/// <item>The <c>'simple'</c> regconfig is required: no stemming and no stop-word removal, so the match
/// set sits exactly on the cross-provider contract floor (case-insensitive, whole-token, all-terms).
/// <c>'english'</c> would make <c>Search("the")</c> diverge from the other providers.</item>
/// <item>The raw user text reaches Postgres only through <c>plainto_tsquery('simple', @query)</c> as a
/// bind parameter: never <c>to_tsquery</c>, never string interpolation. <c>plainto_tsquery</c> also
/// strips live query metacharacters (<c>~ * : ( )</c>) rather than parsing them.</item>
/// </list>
/// </remarks>
internal static class AgeFullTextSearch
{
    /// <summary>
    /// The maximum number of ids a single search may seed into the residual query. The id list rides
    /// to AGE as one <c>agtype</c> parameter blob; past this bound the provider fails informatively
    /// rather than build an unbounded parameter. Documented in <c>COMPLIANCE.md</c>.
    /// </summary>
    internal const int MaxMatchedIds = 10_000;

    /// <summary>
    /// The unqualified name of the <c>IMMUTABLE</c> function that extracts an entity's searchable text
    /// from its <c>agtype</c> blob. It lives in each graph's own schema (not a shared one) so
    /// concurrent per-graph provisioning never races on a single catalog tuple. The phase-1 coarse
    /// conjunct and the GIN index expression (<see cref="Schema.AgeFullTextIndex"/>) both reference it
    /// by the same schema-qualified name so the planner matches the index expression textually.
    /// </summary>
    internal const string BlobFunctionName = "age_fulltext_blob";

    /// <summary>The schema-qualified reference to the blob function for <paramref name="graphName"/>.</summary>
    internal static string BlobFunctionRef(string graphName) =>
        $"{AgeSqlIdentifier.Quote(graphName, "graph name")}.{BlobFunctionName}";

    private enum FullTextTarget
    {
        Nodes,
        Relationships,
    }

    /// <summary>A concrete candidate type contributing one disjunct to a typed search predicate.</summary>
    internal readonly record struct FullTextCandidate(string Label, IReadOnlyList<string> SearchableProperties);

    /// <summary>
    /// Runs phase 1 on the supplied runner's transaction and returns the matching entities' public
    /// <c>Id</c> values. Executing on the caller's transaction is deliberate: an uncommitted write in
    /// that transaction is visible to the search.
    /// </summary>
    public static async Task<IReadOnlyList<string>> FindMatchingIdsAsync(
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

        var (target, isDynamic) = Classify(elementType);
        var table = target == FullTextTarget.Nodes
            ? SerializationBridge.PhysicalNodeLabel
            : SerializationBridge.PhysicalRelationshipType;

        string sql;
        if (isDynamic)
        {
            sql = BuildDynamicSql(runner.GraphName, table);
        }
        else
        {
            var candidates = await ResolveCandidatesAsync(elementType, target, schemaRegistry, cancellationToken)
                .ConfigureAwait(false);

            // No registered type contributes a searchable property, so nothing can match. The residual
            // query becomes Where(e => [].Contains(e.Id)) and returns an empty result by construction.
            if (candidates.Count == 0)
            {
                return [];
            }

            sql = BuildTypedSql(runner.GraphName, table, candidates);
        }

        var ids = await runner.QueryScalarStringsAsync(sql, query, cancellationToken).ConfigureAwait(false);
        EnforceIdSetLimit(ids.Count);
        return ids;
    }

    /// <summary>
    /// Fails with an actionable error when a single search matches more entities than can ride to AGE
    /// as one parameter blob. The phase-1 SQL fetches one past the limit, so any count above it means
    /// the true match set is larger still.
    /// </summary>
    internal static void EnforceIdSetLimit(int matchedCount)
    {
        if (matchedCount > MaxMatchedIds)
        {
            throw new GraphException(
                $"Full-text search matched more than {MaxMatchedIds} entities, exceeding the AGE provider " +
                "limit. Narrow the query or add filters before searching.");
        }
    }

    private static (FullTextTarget Target, bool IsDynamic) Classify(Type elementType)
    {
        if (elementType == typeof(Graph.DynamicNode))
        {
            return (FullTextTarget.Nodes, true);
        }

        if (elementType == typeof(Graph.DynamicRelationship))
        {
            return (FullTextTarget.Relationships, true);
        }

        // IEntity and INode both search nodes: AgeGraph.Search over IEntity is built on Nodes<INode>,
        // so a mixed root never reaches phase 1 as a cross-table union.
        return typeof(Graph.IRelationship).IsAssignableFrom(elementType)
            ? (FullTextTarget.Relationships, false)
            : (FullTextTarget.Nodes, false);
    }

    private static async Task<IReadOnlyList<FullTextCandidate>> ResolveCandidatesAsync(
        Type elementType,
        FullTextTarget target,
        SchemaRegistry schemaRegistry,
        CancellationToken cancellationToken)
    {
        // Untyped search (INode/IRelationship/IEntity) has no single domain label to expand, so it
        // ranges over every registered type of the target kind. A concrete or abstract domain type
        // expands to itself plus its registered concrete subtypes, so SearchNodes<Person> also matches
        // Manager rows (whose inheritance_labels carry both labels).
        var untyped = elementType == typeof(Graph.INode)
            || elementType == typeof(Graph.IRelationship)
            || elementType == typeof(Graph.IEntity);

        IEnumerable<string> labels = untyped
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

    // agtype -> text -> jsonb once per row so the precise predicates read plain JSON.
    private const string Props = "(properties::text::jsonb)";

    // The coarse conjunct, matching the GIN index expression (AgeFullTextIndex) verbatim so the planner
    // can use the index; without the index it is a correct (slower) sequential scan. It over-matches
    // relative to the precise per-type predicate (coarse ⊇ precise), so AND-ing the two never changes
    // the result set. The dynamic all-values predicate IS this coarse expression.
    private static string CoarsePredicate(string graphName) =>
        $"to_tsvector('simple', {BlobFunctionRef(graphName)}(properties)) @@ plainto_tsquery('simple', @query)";

    /// <summary>
    /// Builds the typed phase-1 SQL: the coarse (indexable) conjunct AND-ed with a disjunction of
    /// per-candidate-type predicates, each a <c>to_tsvector</c> over that type's searchable properties
    /// AND-ed with an <c>inheritance_labels</c> membership test for that type's own label. The label
    /// test naturally excludes complex-property value-node rows, which carry the value type's label, not
    /// the owner's.
    /// </summary>
    internal static string BuildTypedSql(
        string graphName,
        string table,
        IReadOnlyList<FullTextCandidate> candidates)
    {
        var predicates = string.Join(
            $"{Environment.NewLine}       OR ",
            candidates.Select(BuildCandidatePredicate));
        return
            $"SELECT {Props} ->> 'Id' AS id{Environment.NewLine}" +
            $"FROM {BaseTable(graphName, table)}{Environment.NewLine}" +
            $"WHERE {CoarsePredicate(graphName)}{Environment.NewLine}" +
            $"  AND ({predicates}){Environment.NewLine}" +
            $"LIMIT {MaxMatchedIds + 1}";
    }

    private static string BuildCandidatePredicate(FullTextCandidate candidate)
    {
        var extractions = string.Join(
            ", ",
            candidate.SearchableProperties.Select(property => $"{Props} ->> {SqlString(property)}"));
        return
            $"(to_tsvector('simple', concat_ws(' ', {extractions})) @@ plainto_tsquery('simple', @query) " +
            $"AND jsonb_exists({Props} -> 'inheritance_labels', {SqlString(candidate.Label)}))";
    }

    /// <summary>
    /// Builds the dynamic phase-1 SQL for <see cref="Graph.DynamicNode"/>/<see cref="Graph.DynamicRelationship"/>:
    /// the coarse "all string values" predicate, which is exactly the dynamic match set (there is no
    /// schema to name searchable properties) and is served directly by the GIN index.
    /// </summary>
    internal static string BuildDynamicSql(string graphName, string table) =>
        $"SELECT {Props} ->> 'Id' AS id{Environment.NewLine}" +
        $"FROM {BaseTable(graphName, table)}{Environment.NewLine}" +
        $"WHERE {CoarsePredicate(graphName)}{Environment.NewLine}" +
        $"LIMIT {MaxMatchedIds + 1}";

    // All nodes/relationships live in one agtype-blob label table per kind; the graph name doubles as
    // the Postgres schema. The query reads the base table directly (not a derived table) so the coarse
    // conjunct over the raw `properties` column matches the GIN index expression. graphName and table
    // are validated as SQL identifiers; the search text is the only user input and rides as @query.
    private static string BaseTable(string graphName, string table) =>
        $"{AgeSqlIdentifier.Quote(graphName, "graph name")}.{AgeSqlIdentifier.Quote(table, "table name")}";

    private static string SqlString(string value) =>
        $"'{value.Replace("'", "''", StringComparison.Ordinal)}'";
}
