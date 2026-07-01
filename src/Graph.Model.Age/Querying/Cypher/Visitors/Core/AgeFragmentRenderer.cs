// Copyright 2025 Savas Parastatidis

namespace Cvoya.Graph.Model.Age.Querying.Cypher.Visitors.Core;

using System.Text;
using System.Text.RegularExpressions;
using System.Diagnostics.CodeAnalysis;
using Cvoya.Graph.Model;
using Cvoya.Graph.Model.Cypher.Querying.Cypher.Visitors.Core;

/// <summary>
/// Renders a Cypher query string from the fragment sequence produced by the fragment pipeline.
/// </summary>
internal static class AgeFragmentRenderer
{
    /// <summary>
    /// Groups all fragment types extracted from the fragment sequence into a single bundle,
    /// reducing the number of repetitive OfType/ToList calls in the Render method.
    /// </summary>
    private sealed record FragmentBundle(
        IReadOnlyList<ProjectionFragment> ProjectionFragments,
        IReadOnlyList<WhereFragment> WhereFragments,
        IReadOnlyList<OrderFragment> OrderFragments,
        IReadOnlyList<AggregationFragment> AggregationFragments,
        IReadOnlyList<GroupByFragment> GroupByFragments,
        IReadOnlyList<CollectFragment> CollectFragments,
        IReadOnlyList<OptionalMatchFragment> OptionalMatchFragments,
        IReadOnlyList<ComplexPropertyLoadingFragment> ComplexPropertyToggles,
        SkipFragment? SkipFragment,
        LimitFragment? LimitFragment,
        bool HasDistinct,
        bool HasReverseOrder,
        WithFragment? WithFragment)
    {
        public static FragmentBundle Extract(IReadOnlyList<QueryFragment> fragments)
        {
            return new FragmentBundle(
                ProjectionFragments: fragments.OfType<ProjectionFragment>().ToList(),
                WhereFragments: fragments.OfType<WhereFragment>().ToList(),
                OrderFragments: fragments.OfType<OrderFragment>().ToList(),
                AggregationFragments: fragments.OfType<AggregationFragment>().ToList(),
                GroupByFragments: fragments.OfType<GroupByFragment>().ToList(),
                CollectFragments: fragments.OfType<CollectFragment>().ToList(),
                OptionalMatchFragments: fragments.OfType<OptionalMatchFragment>().ToList(),
                ComplexPropertyToggles: fragments.OfType<ComplexPropertyLoadingFragment>().ToList(),
                SkipFragment: fragments.OfType<SkipFragment>().LastOrDefault(),
                LimitFragment: fragments.OfType<LimitFragment>().LastOrDefault(),
                HasDistinct: fragments.OfType<DistinctFragment>().Any(),
                HasReverseOrder: fragments.OfType<ReverseOrderFragment>().Any(),
                WithFragment: fragments.OfType<WithFragment>().LastOrDefault()
            );
        }
    }

    public static string Render(IEnumerable<QueryFragment> fragments)
    {
        ArgumentNullException.ThrowIfNull(fragments);

        var fragmentList = fragments.ToList();
        if (fragmentList.Count == 0) return string.Empty;

        // Optimization: Push simple equality WHERE predicates into MATCH patterns
        OptimizePredicatePushDown(fragmentList);

        var bundle = FragmentBundle.Extract(fragmentList);
        var matchClauses = BuildMatchClauses(fragmentList);
        var hasScalarAggregation = bundle.AggregationFragments.Count > 0 && bundle.GroupByFragments.Count == 0;
        var complexPropertyToggle = bundle.ComplexPropertyToggles.LastOrDefault();
        var isComplexPropertyLoadingEnabled = complexPropertyToggle?.IsEnabled ?? false;
        var returnClause = isComplexPropertyLoadingEnabled
            ? null
            : DetermineReturnClause(fragmentList, bundle.ProjectionFragments);
        var shouldReverseOrder = !hasScalarAggregation && bundle.HasReverseOrder;

        // Detect if we need a WITH clause: GroupBy + Collect, explicit GroupBy fragments, or a WithFragment
        bool needsWithClause = bundle.GroupByFragments.Count > 0
            || bundle.CollectFragments.Count > 0
            || bundle.WithFragment is not null;

        var builder = new StringBuilder();

        // MATCH clause
        if (matchClauses.Count > 0)
            builder.AppendLine($"MATCH {string.Join(", ", matchClauses)}");

        // Filter OptionalMatchFragments when projections are present and complex property
        // loading is disabled (projection mode). Only include OPTIONAL MATCH clauses for
        // complex properties that are actually referenced in the projection RETURN expressions.
        // This implements targeted/lazy loading for complex properties (§8.2.6).
        var optionalMatchFragments = bundle.OptionalMatchFragments;
        if (bundle.ProjectionFragments.Count > 0 && !isComplexPropertyLoadingEnabled)
        {
            optionalMatchFragments = FilterReferencedComplexPropertyMatches(
                bundle.OptionalMatchFragments,
                bundle.ProjectionFragments);
        }

        foreach (var opt in optionalMatchFragments)
        {
            if (!string.IsNullOrWhiteSpace(opt.Pattern))
                builder.AppendLine($"OPTIONAL MATCH {opt.Pattern}");
        }

        // Dispatch to the appropriate rendering strategy based on WITH clause presence
        if (bundle.WithFragment is not null && bundle.GroupByFragments.Count == 0 && bundle.CollectFragments.Count == 0)
            RenderWithWithClause(builder, bundle, fragmentList);
        else
            RenderSimple(builder, bundle, fragmentList, returnClause, isComplexPropertyLoadingEnabled, needsWithClause);

        // ORDER BY
        AppendOrderBy(builder, bundle, hasScalarAggregation, shouldReverseOrder, returnClause);

        // SKIP / LIMIT
        AppendSkipLimit(builder, bundle);

        return builder.ToString().Trim();
    }

    /// <summary>
    /// Renders a query that uses a standalone WITH clause (e.g., degree queries).
    /// WhereFragments are split around the WithFragment: those before go before WITH,
    /// those after go after WITH.
    /// </summary>
    private static void RenderWithWithClause(
        StringBuilder builder,
        FragmentBundle bundle,
        List<QueryFragment> fragmentList)
    {
        // Split WhereFragments around the WithFragment
        var withFragmentIndex = fragmentList.FindLastIndex(f => f is WithFragment);
        var beforeWithWhere = bundle.WhereFragments
            .Where(wf => fragmentList.IndexOf(wf) < withFragmentIndex)
            .ToList();
        var afterWithWhere = bundle.WhereFragments
            .Where(wf => fragmentList.IndexOf(wf) > withFragmentIndex)
            .ToList();

        // WHERE before WITH
        if (beforeWithWhere.Count > 0)
        {
            var whereClauses = beforeWithWhere.Select(f => f.PredicateText).ToList();
            builder.AppendLine($"WHERE {string.Join(" AND ", whereClauses)}");
        }

        // WITH clause
        var withExpression = bundle.WithFragment!.WithExpression;
        if (!withExpression.StartsWith("WITH ", StringComparison.OrdinalIgnoreCase))
            withExpression = $"WITH {withExpression}";
        builder.AppendLine(withExpression);

        // WHERE after WITH (degree filter)
        if (afterWithWhere.Count > 0)
        {
            var whereClauses = afterWithWhere.Select(f => f.PredicateText).ToList();
            builder.AppendLine($"WHERE {string.Join(" AND ", whereClauses)}");
        }

        // RETURN
        var returnAfterWith = BuildReturnAfterWithClause(bundle.WithFragment.WithExpression);
        if (!string.IsNullOrWhiteSpace(returnAfterWith))
        {
            var distinctPrefix = bundle.HasDistinct ? "DISTINCT " : string.Empty;
            builder.AppendLine($"RETURN {distinctPrefix}{returnAfterWith}");
        }
    }

    /// <summary>
    /// Renders a query without a standalone WithFragment. Handles standard WHERE,
    /// complex property loading, GroupBy/Collect WITH clauses, and simple RETURN.
    /// </summary>
    private static void RenderSimple(
        StringBuilder builder,
        FragmentBundle bundle,
        List<QueryFragment> fragmentList,
        string? returnClause,
        bool isComplexPropertyLoadingEnabled,
        bool needsWithClause)
    {
        // Standard WHERE handling (before WITH or standalone)
        if (bundle.WhereFragments.Count > 0)
        {
            var whereClauses = bundle.WhereFragments.Select(f => f.PredicateText).ToList();
            builder.AppendLine($"WHERE {string.Join(" AND ", whereClauses)}");
        }

        if (isComplexPropertyLoadingEnabled)
        {
            var alias = DetermineComplexPropertyAlias(bundle.ComplexPropertyToggles, fragmentList);
            AppendComplexPropertyLoadingBlock(builder, alias);
        }
        else if (needsWithClause)
        {
            // GroupBy/Collect WITH clause: emit WITH containing group keys and collect expressions
            var withClause = BuildWithClause(bundle, returnClause);
            if (!string.IsNullOrWhiteSpace(withClause))
                builder.AppendLine(withClause);

            // After WITH, emit RETURN with the aliases from the WITH clause
            var returnAfterWith = BuildReturnAfterWith(bundle, returnClause);
            if (!string.IsNullOrWhiteSpace(returnAfterWith))
            {
                var distinctPrefix = bundle.HasDistinct ? "DISTINCT " : string.Empty;
                builder.AppendLine($"RETURN {distinctPrefix}{returnAfterWith}");
            }
        }
        else if (!string.IsNullOrWhiteSpace(returnClause))
        {
            var distinctPrefix = bundle.HasDistinct ? "DISTINCT " : string.Empty;
            builder.AppendLine($"RETURN {distinctPrefix}{returnClause}");
        }
    }

    /// <summary>
    /// Appends ORDER BY clause based on the order fragments. For non-aggregation queries,
    /// appends the sorted expressions. For DISTINCT queries with SKIP but no ORDER BY,
    /// appends a default order on the primary return expression.
    /// </summary>
    private static void AppendOrderBy(
        StringBuilder builder,
        FragmentBundle bundle,
        bool hasScalarAggregation,
        bool shouldReverseOrder,
        string? returnClause)
    {
        if (bundle.OrderFragments.Count > 0 && !hasScalarAggregation)
        {
            var orderByClauses = bundle.OrderFragments.Select(f =>
            {
                var desc = f.Descending;
                if (shouldReverseOrder) desc = !desc;
                return desc ? $"{f.Expression} DESC" : $"{f.Expression} ASC";
            }).ToList();
            builder.AppendLine($"ORDER BY {string.Join(", ", orderByClauses)}");
        }
        else if (!hasScalarAggregation && bundle.SkipFragment is not null && bundle.HasDistinct)
        {
            var primaryReturn = bundle.ProjectionFragments.SelectMany(f => f.Returns).FirstOrDefault()
                ?? returnClause?.Split(',', 2)[0].Trim();
            if (!string.IsNullOrWhiteSpace(primaryReturn))
                builder.AppendLine($"ORDER BY {primaryReturn}{(shouldReverseOrder ? " DESC" : string.Empty)}");
        }
    }

    /// <summary>
    /// Appends SKIP and LIMIT clauses when present in the fragment bundle.
    /// </summary>
    private static void AppendSkipLimit(StringBuilder builder, FragmentBundle bundle)
    {
        if (bundle.SkipFragment is not null)
            builder.AppendLine($"SKIP {bundle.SkipFragment.Count}");
        if (bundle.LimitFragment is not null)
            builder.AppendLine($"LIMIT {bundle.LimitFragment.Count}");
    }

    /// <summary>
    /// Builds a WITH clause containing the same projection expressions that would normally
    /// appear in a RETURN clause. Uses the actual return expressions from ProjectionFragment
    /// which already have correct aliases (e.g., "src0.Name AS c_Name").
    /// </summary>
    private static string? BuildWithClause(FragmentBundle bundle, string? returnClause)
    {
        var withParts = new List<string>();

        // Use the actual return expressions from the ProjectionFragment.
        // These already have the correct format: "src0.Name AS c_Name"
        if (returnClause != null)
        {
            foreach (var part in returnClause.Split(',').Select(p => p.Trim()).Where(p => !string.IsNullOrWhiteSpace(p)))
            {
                if (!withParts.Contains(part))
                    withParts.Add(part);
            }
        }

        // Ensure collect expressions from CollectFragment are included.
        // They should already be in returnClause via the ProjectionFragment,
        // but add them if missing (e.g., when no ProjectionFragment captures them).
        foreach (var collect in bundle.CollectFragments)
        {
            var orderByPart = !string.IsNullOrEmpty(collect.OrderByExpression)
                ? $" ORDER BY {collect.OrderByExpression}"
                : "";
            var collectEntry = $"collect({collect.CollectExpression}{orderByPart}) AS {collect.ProjectionColumn}";
            if (!withParts.Contains(collectEntry))
                withParts.Add(collectEntry);
        }

        return withParts.Count > 0 ? $"WITH {string.Join(", ", withParts)}" : null;
    }

    /// <summary>
    /// Builds a RETURN clause after a WITH clause. Strips expressions and keeps only
    /// the column aliases defined in the WITH clause (e.g., "c_Name, c_Friends").
    /// </summary>
    private static string? BuildReturnAfterWith(FragmentBundle bundle, string? returnClause)
    {
        var returnParts = new List<string>();

        // Extract just the alias part from each return expression in the ProjectionFragment.
        // "src0.Name AS c_Name" → "c_Name"
        if (returnClause != null)
        {
            foreach (var part in returnClause.Split(',').Select(p => p.Trim()).Where(p => !string.IsNullOrWhiteSpace(p)))
            {
                var alias = part.Split(" AS ").LastOrDefault()?.Trim() ?? part;
                if (!returnParts.Contains(alias))
                    returnParts.Add(alias);
            }
        }

        // Also add collect projection column aliases if not already included
        foreach (var collect in bundle.CollectFragments)
        {
            if (!string.IsNullOrWhiteSpace(collect.ProjectionColumn) && !returnParts.Contains(collect.ProjectionColumn))
                returnParts.Add(collect.ProjectionColumn);
        }

        return returnParts.Count > 0 ? string.Join(", ", returnParts) : null;
    }

    /// <summary>
    /// Builds a RETURN clause after a standalone WITH clause (e.g., from a degree query).
    /// Strips expressions and keeps only the aliases defined in the WITH expression.
    /// "src0, count(r0) AS degree" → "src0, degree"
    /// </summary>
    private static string? BuildReturnAfterWithClause(string withExpression)
    {
        // WITH starts with "WITH " prefix - strip it
        var expression = withExpression.StartsWith("WITH ", StringComparison.OrdinalIgnoreCase)
            ? withExpression[5..]
            : withExpression;

        var returnParts = new List<string>();
        foreach (var part in expression.Split(',').Select(p => p.Trim()).Where(p => !string.IsNullOrWhiteSpace(p)))
        {
            // Extract just the alias part: "count(r0) AS degree" → "degree", "src0" → "src0"
            var alias = part.Contains(" AS ", StringComparison.OrdinalIgnoreCase)
                ? part.Split(new[] { " AS " }, StringSplitOptions.None)[^1].Trim()
                : part;
            if (!returnParts.Contains(alias))
                returnParts.Add(alias);
        }

        return returnParts.Count > 0 ? string.Join(", ", returnParts) : null;
    }

    private static List<string> BuildMatchClauses(IReadOnlyList<QueryFragment> fragments)
    {
        var matchClauses = new List<string>();
        foreach (var fragment in fragments)
        {
            if (fragment is MatchRootFragment root)
            {
                var pattern = root.Pattern;
                // If adding a standalone node pattern that's already covered by a preceding segment, skip it
                if (matchClauses.Count > 0 && IsRedundantStandaloneNode(pattern, matchClauses[^1]))
                    continue;
                // If adding a full segment pattern and the previous clause is just its starting node, replace it
                if (matchClauses.Count > 0 && IsCoveredByFullSegment(pattern, matchClauses[^1]))
                    matchClauses[^1] = pattern;
                else
                    matchClauses.Add(pattern);
            }
            else if (fragment is MatchSegmentFragment segment)
            {
                var pattern = segment.Pattern;
                if (matchClauses.Count > 0 && pattern.Length > 0 && (pattern[0] is '-' or '<'))
                    matchClauses[^1] = matchClauses[^1] + pattern;
                else if (matchClauses.Count > 0 && TryGetLeadingNodeAlias(pattern, out var leadingAlias, out var remainder) &&
                         TryGetTerminalNodeAlias(matchClauses[^1], out var terminalAlias) &&
                         leadingAlias == terminalAlias)
                    matchClauses[^1] += remainder;
                else
                    matchClauses.Add(pattern);
            }
        }
        return matchClauses;
    }

    private static bool IsRedundantStandaloneNode(string pattern, string lastClause)
    {
        // A standalone node pattern like "(src0:PersonNode)" is redundant if the last clause
        // is already a segment pattern starting with the same node
        var nodeMatch = System.Text.RegularExpressions.Regex.Match(pattern, @"^\((\w+):[^)]+\)$");
        if (!nodeMatch.Success)
            return false;

        var alias = nodeMatch.Groups[1].Value;
        return lastClause.StartsWith($"({alias}:") && lastClause.Contains("-[");
    }

    private static bool IsCoveredByFullSegment(string newPattern, string lastClause)
    {
        // If the new pattern is a full segment "(A:Label)-[r:Rel]->(B:Label)" and the last clause
        // is just the starting node "(A:Label)", replace the last clause with the full pattern
        var segmentMatch = System.Text.RegularExpressions.Regex.Match(newPattern, @"^\((\w+):[^)]+\)-");
        if (!segmentMatch.Success)
            return false;

        var alias = segmentMatch.Groups[1].Value;
        var standaloneNodeRegex = System.Text.RegularExpressions.Regex.Match(lastClause, @"^\((\w+):[^)]+\)$");
        return standaloneNodeRegex.Success && standaloneNodeRegex.Groups[1].Value == alias;
    }

    private static bool TryGetLeadingNodeAlias(string pattern, out string alias, out string remainder)
    {
        var match = System.Text.RegularExpressions.Regex.Match(pattern, @"^\((\w+):[^)]+\)([-<].*)$");
        if (match.Success)
        {
            alias = match.Groups[1].Value;
            remainder = match.Groups[2].Value;
            return true;
        }
        alias = string.Empty;
        remainder = string.Empty;
        return false;
    }

    private static bool TryGetTerminalNodeAlias(string pattern, out string alias)
    {
        var match = System.Text.RegularExpressions.Regex.Match(pattern, @"\((\w+):[^)]+\)\s*$");
        if (match.Success) { alias = match.Groups[1].Value; return true; }
        alias = string.Empty;
        return false;
    }

    private static string? DetermineReturnClause(IReadOnlyList<QueryFragment> fragments, IReadOnlyList<ProjectionFragment> projectionFragments)
    {
        var aggregationFragments = fragments.OfType<AggregationFragment>().ToList();
        var groupByFragments = fragments.OfType<GroupByFragment>().ToList();

        if (aggregationFragments.Count > 0)
        {
            var aggregationExprs = aggregationFragments.Select(FragmentFormatting.BuildAggregationExpression).ToList();

            // If there are group-by fragments, include projection (group key) expressions too
            if (groupByFragments.Count > 0 && projectionFragments.Count > 0)
            {
                var projectionExprs = projectionFragments
                    .SelectMany(f => f.Returns.Where(v => !string.IsNullOrWhiteSpace(v)))
                    .ToList();
                if (projectionExprs.Count > 0)
                    return string.Join(", ", projectionExprs.Concat(aggregationExprs));
            }

            return string.Join(", ", aggregationExprs);
        }

        if (projectionFragments.Count > 0)
        {
            // Use the LAST projection fragment (most recent Select in the chain)
            for (var i = projectionFragments.Count - 1; i >= 0; i--)
            {
                var values = projectionFragments[i].Returns.Where(v => !string.IsNullOrWhiteSpace(v)).ToList();
                if (values.Count > 0) return string.Join(", ", values);
            }
        }

        // For path segment queries (MatchSegmentFragment or MatchRootFragment with full segment pattern),
        // return ALL created aliases so the SQL column definitions and Cypher RETURN columns match,
        // and the ResultMaterializer can reconstruct the full IGraphPathSegment.
        // Check the last fragment that is either a MatchSegmentFragment or a MatchRootFragment
        // whose pattern contains edge markers (-[ or <-[), indicating it came from PathSegments
        // rather than a simple node source initialization.
        // IMPORTANT: We must exclude relationship-type fragments (NodeType is IRelationship)
        // because those should return only the relationship alias (e.g., r0), not all three aliases.
        for (var i = fragments.Count - 1; i >= 0; i--)
        {
            var frag = fragments[i];
            if (frag is MatchSegmentFragment seg && seg.CreatedAliases.Length >= 3)
            {
                return string.Join(", ", seg.CreatedAliases);
            }
            if (frag is MatchRootFragment root && root.CreatedAliases.Length >= 3
                && (root.Pattern.Contains("-[") || root.Pattern.Contains("<-[")))
            {
                // Only use 3-alias return for path segment queries (NodeType is INode, not IRelationship).
                // Relationship queries also have 3 created aliases but should only return the relationship alias.
                if (!typeof(IRelationship).IsAssignableFrom(root.NodeType))
                {
                    return string.Join(", ", root.CreatedAliases);
                }
            }
        }

        for (var i = fragments.Count - 1; i >= 0; i--)
        {
            var alias = fragments[i].CurrentAlias;
            if (!string.IsNullOrWhiteSpace(alias) && alias != "ps") return alias;
        }
        return null;
    }

    private static string DetermineComplexPropertyAlias(IReadOnlyList<ComplexPropertyLoadingFragment> toggles, IReadOnlyList<QueryFragment> fragments)
    {
        var toggleAlias = toggles.Where(t => t.IsEnabled && !string.IsNullOrWhiteSpace(t.TargetAlias))
            .Select(t => t.TargetAlias!.Trim()).LastOrDefault();
        if (!string.IsNullOrWhiteSpace(toggleAlias)) return toggleAlias;
        var fallback = fragments.LastOrDefault(f => !string.IsNullOrWhiteSpace(f.CurrentAlias))?.CurrentAlias;
        if (!string.IsNullOrWhiteSpace(fallback)) return fallback!;
        return "src0";
    }

    private static void AppendComplexPropertyLoadingBlock(StringBuilder builder, string alias)
    {
        builder.AppendLine($"OPTIONAL MATCH ({alias})-[prop_rel]->(prop_node)");
        builder.AppendLine($"WHERE type(prop_rel) STARTS WITH '{GraphDataModel.PropertyRelationshipTypeNamePrefix}'");
        builder.AppendLine($"WITH {alias}, ");
        builder.AppendLine("     collect({");
        builder.AppendLine($"         ParentNode: {alias},");
        builder.AppendLine("         Relationship: prop_rel,");
        builder.AppendLine("         SequenceNumber: coalesce(prop_rel.SequenceNumber, 0),");
        builder.AppendLine("         Property: prop_node");
        builder.AppendLine("     }) AS complex_properties");
        builder.AppendLine("RETURN {");
        builder.AppendLine($"    Node: {alias},");
        builder.AppendLine("    ComplexProperties: complex_properties");
        builder.AppendLine("}");
    }

    // -----------------------------------------------------------------------
    //  Predicate Push-Down Optimization (§8.2.4)
    // -----------------------------------------------------------------------

    /// <summary>
    /// Optimization pass: pushes simple equality WhereFragment predicates into
    /// the preceding MATCH pattern as inline node property maps.
    /// Modifies the fragment list in-place.
    ///
    /// Handles three forms of WhereFragment predicate text:
    ///   1. Simple equality: "src0.Name = $param_0"
    ///   2. Compound AND:    "(src0.Name = $param_0 AND src0.Age = $param_1)"
    ///   3. Mixed AND:       "(src0.Name = $param_0 AND src0.Age > $param_1)"
    ///
    /// In cases 2 and 3, the AND expression is split and each conjunct is evaluated
    /// individually. Simple equalities on the root node alias are pushed; non-simple
    /// conjuncts remain as WHERE.
    /// </summary>
    private static void OptimizePredicatePushDown(List<QueryFragment> fragments)
    {
        // 1. Find the root MATCH fragment's node alias.
        //    When there are multiple MatchRootFragments (e.g. due to PathSegments),
        //    BuildMatchClauses replaces the first with the last if the last is a
        //    superset pattern. We need to push into the fragment that will actually
        //    be rendered — that's the LAST MatchRootFragment whose pattern or alias
        //    references the root node.
        var rootCandidates = fragments
            .Select((f, i) => (Fragment: f as MatchRootFragment, Index: i))
            .Where(x => x.Fragment is not null)
            .ToList();

        if (rootCandidates.Count == 0) return;

        // Determine the root alias from the FIRST MatchRootFragment
        var rootAlias = rootCandidates[0].Fragment!.CurrentAlias;
        if (string.IsNullOrWhiteSpace(rootAlias)) return;

        // Find the "winning" MatchRootFragment — the last one that references rootAlias
        // in its CurrentAlias or its pattern start (since BuildMatchClauses replaces
        // earlier standalone patterns with later full-segment patterns).
        MatchRootFragment? targetMatch = null;
        int targetIndex = -1;
        for (int idx = rootCandidates.Count - 1; idx >= 0; idx--)
        {
            var candidate = rootCandidates[idx].Fragment!;
            if (candidate.CurrentAlias == rootAlias ||
                candidate.Pattern.StartsWith($"({rootAlias}:", StringComparison.Ordinal))
            {
                targetMatch = candidate;
                targetIndex = rootCandidates[idx].Index;
                break;
            }
        }

        if (targetMatch is null) return;

        // 2. Find pushable predicates from WhereFragments.
        var pushableIndices = new List<int>();
        var propertyFilters = new List<(string Property, string Value)>();

        for (int i = 0; i < fragments.Count; i++)
        {
            if (fragments[i] is WhereFragment where)
            {
                CollectPushablePredicates(
                    where.PredicateText, rootAlias,
                    out var wherePushable, out var whereKeep);

                if (wherePushable.Count > 0)
                {
                    propertyFilters.AddRange(wherePushable);

                    if (whereKeep.Count == 0)
                    {
                        // Entire WhereFragment was consumed — mark for removal
                        pushableIndices.Add(i);
                    }
                    else
                    {
                        // Some conjuncts remain — replace the WhereFragment with the kept parts
                        var keptPredicate = string.Join(" AND ", whereKeep);
                        // Re-wrap in parentheses if more than one part remains
                        if (whereKeep.Count > 1)
                            keptPredicate = $"({keptPredicate})";
                        fragments[i] = new WhereFragment(
                            keptPredicate, where.ConsumesAliases, where.CurrentAlias!);
                    }
                }
            }
        }

        if (propertyFilters.Count == 0) return;

        // 3. Modify the winning MatchRootFragment pattern to include property filters
        var newPattern = targetMatch.Pattern;
        foreach (var (prop, val) in propertyFilters)
        {
            newPattern = AddNodePropertyFilter(newPattern, rootAlias, prop, val);
        }

        fragments[targetIndex] = new MatchRootFragment(
            newPattern, targetMatch.Label, targetMatch.NodeType,
            targetMatch.CreatedAliases, targetMatch.CurrentAlias!);

        // 4. Remove fully-consumed WhereFragments (reverse order to preserve indices)
        foreach (var idx in pushableIndices.OrderByDescending(i => i))
        {
            fragments.RemoveAt(idx);
        }
    }

    /// <summary>
    /// Analyzes a WhereFragment predicate text and extracts pushable simple equalities
    /// on the root alias. Returns the pushable ones and the ones that must stay.
    /// Handles both simple predicates and parenthesized AND-expressions.
    /// </summary>
    private static void CollectPushablePredicates(
        string predicateText, string rootAlias,
        out List<(string Property, string Value)> pushable,
        out List<string> keep)
    {
        pushable = new List<(string, string)>();
        keep = new List<string>();

        // Try to parse as simple equality first
        if (TryParseSimpleEquality(predicateText, out var alias, out var property, out var value))
        {
            if (alias == rootAlias && IsNodeAlias(alias) && IsSafeValue(value))
            {
                pushable.Add((property, value));
            }
            else
            {
                keep.Add(predicateText);
            }
            return;
        }

        // Check for parenthesized AND expression: (left AND right)
        // Strip outer parentheses if present
        var inner = predicateText;
        if (inner.StartsWith("(", StringComparison.Ordinal) && inner.EndsWith(")", StringComparison.Ordinal))
        {
            // Only strip if parentheses are balanced at the outer level
            inner = inner[1..^1];
        }

        // Split by " AND " (but not within nested parentheses)
        var parts = SplitAnd(inner);

        if (parts.Count > 1)
        {
            foreach (var part in parts)
            {
                if (TryParseSimpleEquality(part, out var pAlias, out var pProperty, out var pValue)
                    && pAlias == rootAlias
                    && IsNodeAlias(pAlias)
                    && IsSafeValue(pValue))
                {
                    pushable.Add((pProperty, pValue));
                }
                else
                {
                    keep.Add(part);
                }
            }
        }
        else
        {
            // Not an AND expression — keep the whole predicate
            keep.Add(predicateText);
        }
    }

    /// <summary>
    /// Splits a predicate string by " AND " at the top level (not within nested parentheses).
    /// </summary>
    private static List<string> SplitAnd(string predicate)
    {
        var result = new List<string>();
        int depth = 0;
        int start = 0;

        for (int i = 0; i < predicate.Length; i++)
        {
            if (predicate[i] == '(')
                depth++;
            else if (predicate[i] == ')')
                depth--;
            else if (depth == 0 && i + 5 <= predicate.Length &&
                     predicate[i] == ' ' && predicate[i + 1] == 'A' && predicate[i + 2] == 'N' &&
                     predicate[i + 3] == 'D' && predicate[i + 4] == ' ')
            {
                // Found top-level " AND "
                var part = predicate[start..i].Trim();
                if (part.Length > 0)
                    result.Add(part);
                i += 4; // skip " AND "
                start = i + 1;
            }
        }

        // Last part
        var lastPart = predicate[start..].Trim();
        if (lastPart.Length > 0)
            result.Add(lastPart);

        return result;
    }

    private static readonly Lazy<Regex> SimpleEqualityRegex = new(() => new Regex(
        @"^(\w+)\.(\w+)\s*=\s*(.+)$",
        RegexOptions.Compiled));

    /// <summary>
    /// Tries to parse a predicate string as a simple equality: alias.Property = value.
    /// </summary>
    private static bool TryParseSimpleEquality(
        string predicate,
        [NotNullWhen(true)] out string? alias,
        [NotNullWhen(true)] out string? property,
        [NotNullWhen(true)] out string? value)
    {
        var match = SimpleEqualityRegex.Value.Match(predicate);
        if (match.Success)
        {
            alias = match.Groups[1].Value;
            property = match.Groups[2].Value;
            value = match.Groups[3].Value.Trim();
            return true;
        }

        alias = null;
        property = null;
        value = null;
        return false;
    }

    /// <summary>
    /// Determines whether the given alias refers to a node or a relationship based on naming conventions.
    /// This heuristic assumes relationship aliases start with 'r' followed by a digit.
    /// This is fragile and should be replaced with explicit alias role tracking in fragment metadata.
    /// </summary>
    private static bool IsNodeAlias(string alias)
        => !(alias.Length > 1 && alias[0] == 'r' && char.IsDigit(alias[1]));

    private static readonly Lazy<Regex> SafeValueRegex = new(() => new Regex(
        @"^(\$param_\d+|\$literal_\d+|""[^""]*""|'[^']*'|\d+(\.\d+)?|true|false|null)$",
        RegexOptions.Compiled));

    /// <summary>
    /// Ensures the RHS is a parameter reference or literal, not another property access.
    /// </summary>
    private static bool IsSafeValue(string value)
        => SafeValueRegex.Value.IsMatch(value);

    /// <summary>
    /// Modifies a node pattern string to include or append a property filter.
    /// </summary>
    private static string AddNodePropertyFilter(string pattern, string alias, string property, string value)
    {
        // Find the node pattern "(alias:Label)" or "(alias:Label {...})"
        var nodeRegex = new Regex($@"(\({Regex.Escape(alias)}:[^){{}}]*(?:{{[^}}]*}})?\))");
        var match = nodeRegex.Match(pattern);
        if (!match.Success) return pattern;

        var nodePattern = match.Value;

        // Check if it already has a property map
        if (nodePattern.EndsWith("})", StringComparison.Ordinal))
        {
            // Append to existing map: (alias:Label {prop1: val1, prop2: val2})
            var newNodePattern = nodePattern[..^2] + $", {property}: {value}" + "})";
            return ReplaceFirst(pattern, nodePattern, newNodePattern);
        }
        else if (nodePattern.EndsWith(")", StringComparison.Ordinal))
        {
            // Add new property map: (alias:Label {property: value})
            var newNodePattern = nodePattern[..^1] + $" {{{property}: {value}}}" + ")";
            return ReplaceFirst(pattern, nodePattern, newNodePattern);
        }

        return pattern;
    }

    /// <summary>
    /// Replaces only the first occurrence of 'search' in 'text' with 'replace'.
    /// </summary>
    private static string ReplaceFirst(string text, string search, string replace)
    {
        int pos = text.IndexOf(search, StringComparison.Ordinal);
        if (pos < 0) return text;
        return text[..pos] + replace + text[(pos + search.Length)..];
    }

    /// <summary>
    /// Filters <see cref="OptionalMatchFragment"/>s to only include those whose complex property
    /// alias (e.g., <c>cp_HomeAddress</c>) is actually referenced in the projection RETURN expressions.
    /// This implements targeted/lazy loading (§8.2.6): instead of loading ALL complex properties,
    /// only the ones needed by the projection are loaded.
    /// </summary>
    /// <param name="optionalMatchFragments">All OptionalMatchFragments in the fragment sequence.</param>
    /// <param name="projectionFragments">The ProjectionFragments containing RETURN expressions.</param>
    /// <returns>Filtered list containing only referenced complex property fragments.</returns>
    private static IReadOnlyList<OptionalMatchFragment> FilterReferencedComplexPropertyMatches(
        IReadOnlyList<OptionalMatchFragment> optionalMatchFragments,
        IReadOnlyList<ProjectionFragment> projectionFragments)
    {
        if (optionalMatchFragments.Count == 0 || projectionFragments.Count == 0)
            return optionalMatchFragments;

        // Extract the set of complex property names referenced in projection RETURN expressions.
        // Look for patterns like "cp_PropertyName." or "cp_PropertyName" (followed by AS or end).
        var referencedComplexPropertyNames = new HashSet<string>();
        var cpPattern = new System.Text.RegularExpressions.Regex(
            @"\bcp_(\w+)\b",
            System.Text.RegularExpressions.RegexOptions.Compiled);

        foreach (var projection in projectionFragments)
        {
            foreach (var returnExpr in projection.Returns)
            {
                if (string.IsNullOrWhiteSpace(returnExpr))
                    continue;
                var matches = cpPattern.Matches(returnExpr);
                foreach (System.Text.RegularExpressions.Match match in matches)
                {
                    if (match.Success && match.Groups.Count >= 2)
                    {
                        var propName = match.Groups[1].Value;
                        referencedComplexPropertyNames.Add(propName);
                    }
                }
            }
        }

        // If no complex properties are referenced in the projection, filter out ALL OptionalMatchFragments
        // that follow the complex property pattern (have cp_ in their created aliases).
        if (referencedComplexPropertyNames.Count == 0)
        {
            return optionalMatchFragments
                .Where(opt => !opt.CreatedAliases.Any(a => a.StartsWith("cp_", StringComparison.Ordinal)))
                .ToList();
        }

        // Filter to only include fragments whose cp_ alias matches a referenced property.
        // The pattern is: cp_{PropertyName} in the created aliases.
        return optionalMatchFragments
            .Where(opt => opt.CreatedAliases.Any(a =>
            {
                // Extract property name from cp_{PropertyName}
                if (!a.StartsWith("cp_", StringComparison.Ordinal))
                    return false;
                var propName = a[3..]; // Strip "cp_" prefix
                return referencedComplexPropertyNames.Contains(propName);
            }))
            .ToList();
    }
}
