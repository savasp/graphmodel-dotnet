// Copyright 2025 Savas Parastatidis

namespace Cvoya.Graph.Model.Age.Querying.Cypher.Visitors.Core;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Cvoya.Graph.Model;
using Cvoya.Graph.Model.Cypher.Querying.Cypher.Visitors.Core;

/// <summary>
/// Renders a Cypher query string from the fragment sequence produced by the fragment pipeline shim.
/// Accepts both AGE-specific fragments (MatchRootFragment, etc.) and shared fragments (WhereFragment, ProjectionFragment, etc.).
/// </summary>
internal static class AgeFragmentRenderer
{
    public static string Render(IEnumerable<QueryFragment> fragments)
    {
        ArgumentNullException.ThrowIfNull(fragments);

        var fragmentList = fragments.ToList();
        if (fragmentList.Count == 0)
        {
            return string.Empty;
        }

        var projectionFragments = fragmentList.OfType<ProjectionFragment>().ToList();
        var matchClauses = BuildMatchClauses(fragmentList);
        var optionalMatchFragments = fragmentList.OfType<OptionalMatchFragment>().ToList();
        var whereClauses = fragmentList.OfType<WhereFragment>().Select(static f => f.PredicateText).ToList();
        var complexPropertyToggles = fragmentList.OfType<ComplexPropertyLoadingFragment>().ToList();
        var complexPropertyToggle = complexPropertyToggles.LastOrDefault();
        var isComplexPropertyLoadingEnabled = complexPropertyToggle?.IsEnabled ?? false;
    var aggregationFragments = fragmentList.OfType<AggregationFragment>().ToList();
        var groupByFragments = fragmentList.OfType<GroupByFragment>().ToList();
        var hasScalarAggregation = aggregationFragments.Count > 0 && groupByFragments.Count == 0;
        var returnClause = isComplexPropertyLoadingEnabled
            ? null
            : DetermineReturnClause(fragmentList, projectionFragments);
        var orderFragments = fragmentList.OfType<OrderFragment>().ToList();
        var skipFragment = fragmentList.OfType<SkipFragment>().LastOrDefault();
        var limitFragment = fragmentList.OfType<LimitFragment>().LastOrDefault();
        var isDistinct = fragmentList.OfType<DistinctFragment>().Any();
        var shouldReverseOrder = !hasScalarAggregation && fragmentList.OfType<ReverseOrderFragment>().Any();

        var builder = new StringBuilder();

        if (matchClauses.Count > 0)
        {
            builder.AppendLine($"MATCH {string.Join(", ", matchClauses)}");
        }

        if (optionalMatchFragments.Count > 0)
        {
            foreach (var optionalFragment in optionalMatchFragments)
            {
                if (!string.IsNullOrWhiteSpace(optionalFragment.Pattern))
                {
                    builder.AppendLine($"OPTIONAL MATCH {optionalFragment.Pattern}");
                }
            }
        }

        if (whereClauses.Count > 0)
        {
            builder.AppendLine($"WHERE {string.Join(" AND ", whereClauses)}");
        }

        if (isComplexPropertyLoadingEnabled)
        {
            var alias = DetermineComplexPropertyAlias(complexPropertyToggles, fragmentList);
            AppendComplexPropertyLoadingBlock(builder, alias);
        }
        else if (!string.IsNullOrWhiteSpace(returnClause))
        {
            var distinctPrefix = isDistinct ? "DISTINCT " : string.Empty;
            builder.AppendLine($"RETURN {distinctPrefix}{returnClause}");
        }

        if (orderFragments.Count > 0 && !hasScalarAggregation)
        {
            var orderByClauses = orderFragments
                .Select(fragment =>
                {
                    var isDescending = fragment.Descending;
                    if (shouldReverseOrder)
                    {
                        isDescending = !isDescending;
                    }

                    return isDescending ? $"{fragment.Expression} DESC" : $"{fragment.Expression} ASC";
                })
                .ToList();

            builder.AppendLine($"ORDER BY {string.Join(", ", orderByClauses)}");
        }
        else if (!hasScalarAggregation && skipFragment is not null && isDistinct)
        {
            var primaryReturn = projectionFragments
                .SelectMany(static fragment => fragment.Returns)
                .FirstOrDefault()
                ?? returnClause?.Split(',', 2)[0].Trim();

            if (!string.IsNullOrWhiteSpace(primaryReturn))
            {
                var directionSuffix = shouldReverseOrder ? " DESC" : string.Empty;
                builder.AppendLine($"ORDER BY {primaryReturn}{directionSuffix}");
            }
        }

        if (skipFragment is not null)
        {
            builder.AppendLine($"SKIP {skipFragment.Count}");
        }

        if (limitFragment is not null)
        {
            builder.AppendLine($"LIMIT {limitFragment.Count}");
        }

        return builder.ToString().Trim();
    }

    private static List<string> BuildMatchClauses(IReadOnlyList<QueryFragment> fragments)
    {
        var matchClauses = new List<string>();

        foreach (var fragment in fragments)
        {
            switch (fragment)
            {
                case MatchRootFragment rootFragment:
                    matchClauses.Add(rootFragment.Pattern);
                    break;
                case MatchSegmentFragment segmentFragment:
                    var pattern = segmentFragment.Pattern;

                    if (matchClauses.Count > 0 && pattern.StartsWith("-[", StringComparison.Ordinal))
                    {
                        matchClauses[^1] = matchClauses[^1] + pattern;
                    }
                    else if (matchClauses.Count > 0 && TryGetLeadingNodeAlias(pattern, out var leadingAlias, out var patternRemainder) &&
                             TryGetTerminalNodeAlias(matchClauses[^1], out var terminalAlias) &&
                             string.Equals(leadingAlias, terminalAlias, StringComparison.Ordinal))
                    {
                        matchClauses[^1] += patternRemainder;
                    }
                    else if (matchClauses.Count > 0 && TryGetStandaloneNodeAlias(matchClauses[^1], out var standaloneAlias) &&
                             pattern.StartsWith($"({standaloneAlias}:", StringComparison.Ordinal))
                    {
                        matchClauses[^1] = pattern;
                    }
                    else if (matchClauses.Count > 0 && pattern.Contains(")-[", StringComparison.Ordinal) && pattern.EndsWith("->", StringComparison.Ordinal) && matchClauses[^1].StartsWith("(src", StringComparison.Ordinal))
                    {
                        matchClauses[^1] = pattern + matchClauses[^1];
                    }
                    else
                    {
                        matchClauses.Add(pattern);
                    }

                    break;
            }
        }

        return matchClauses;
    }

    private static bool TryGetStandaloneNodeAlias(string pattern, out string alias)
    {
        var match = System.Text.RegularExpressions.Regex.Match(pattern, @"^\((\w+):[^)]+\)$");
        if (match.Success)
        {
            alias = match.Groups[1].Value;
            return true;
        }

        alias = string.Empty;
        return false;
    }

    private static bool TryGetLeadingNodeAlias(string pattern, out string alias, out string remainder)
    {
        var match = System.Text.RegularExpressions.Regex.Match(pattern, @"^\((\w+):[^)]+\)([-<].*)$");
        if (!match.Success)
        {
            alias = string.Empty;
            remainder = string.Empty;
            return false;
        }

        alias = match.Groups[1].Value;
        remainder = match.Groups[2].Value;
        return true;
    }

    private static bool TryGetTerminalNodeAlias(string pattern, out string alias)
    {
        var match = System.Text.RegularExpressions.Regex.Match(pattern, @"\((\w+):[^)]+\)\s*$");
        if (match.Success)
        {
            alias = match.Groups[1].Value;
            return true;
        }

        alias = string.Empty;
        return false;
    }

    private static string? DetermineReturnClause(IReadOnlyList<QueryFragment> fragments, IReadOnlyList<ProjectionFragment> projectionFragments)
    {
        // Check for aggregation fragments first - they take precedence over projections and aliases
        var aggregationFragments = fragments.OfType<AggregationFragment>().ToList();
        if (aggregationFragments.Count > 0)
        {
            var aggregationExpressions = aggregationFragments
                .Select(FragmentFormatting.BuildAggregationExpression)
                .ToList();
            
            if (aggregationExpressions.Count > 0)
            {
                return string.Join(", ", aggregationExpressions);
            }
        }

        if (projectionFragments.Count > 0)
        {
            for (var i = projectionFragments.Count - 1; i >= 0; i--)
            {
                var projectionValues = projectionFragments[i]
                    .Returns
                    .Where(static value => !string.IsNullOrWhiteSpace(value))
                    .ToList();

                if (projectionValues.Count > 0)
                {
                    return string.Join(", ", projectionValues);
                }
            }
        }

        for (var i = fragments.Count - 1; i >= 0; i--)
        {
            var currentAlias = fragments[i].CurrentAlias;
            if (!string.IsNullOrWhiteSpace(currentAlias) && currentAlias != "ps")
            {
                return currentAlias;
            }
        }

        return null;
    }

    private static string DetermineComplexPropertyAlias(IReadOnlyList<ComplexPropertyLoadingFragment> toggles, IReadOnlyList<QueryFragment> fragments)
    {
        var toggleAlias = toggles
            .Where(static toggle => toggle.IsEnabled && !string.IsNullOrWhiteSpace(toggle.TargetAlias))
            .Select(static toggle => toggle.TargetAlias!.Trim())
            .LastOrDefault();

        if (!string.IsNullOrWhiteSpace(toggleAlias))
        {
            return toggleAlias;
        }

        var fallbackAlias = fragments.LastOrDefault(fragment => !string.IsNullOrWhiteSpace(fragment.CurrentAlias))?.CurrentAlias;
        if (!string.IsNullOrWhiteSpace(fallbackAlias))
        {
            return fallbackAlias!;
        }

        return "src0";
    }

    private static void AppendComplexPropertyLoadingBlock(StringBuilder builder, string alias)
    {
        builder.AppendLine($@"OPTIONAL MATCH ({alias})-[prop_rel]->(prop_node)");
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
}
