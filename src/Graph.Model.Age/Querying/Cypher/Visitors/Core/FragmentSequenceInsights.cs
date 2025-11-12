// Copyright 2025 Savas Parastatidis

namespace Cvoya.Graph.Model.Age.Querying.Cypher.Visitors.Core;

using System;
using System.Collections.Generic;
using System.Linq;
using Cvoya.Graph.Model.Cypher.Querying.Cypher.Visitors.Core;

/// <summary>
/// Utility helpers for inspecting the fragment buffer produced during query translation.
/// Used by visitors to avoid falling back to the legacy builder state.
/// </summary>
internal static class FragmentSequenceInsights
{
    public static bool HasMatchFragments(IReadOnlyList<QueryFragment> fragments)
    {
        for (var i = 0; i < fragments.Count; i++)
        {
            if (fragments[i] is MatchRootFragment or MatchSegmentFragment)
            {
                return true;
            }
        }

        return false;
    }

    public static string? GetLastReturnClause(IReadOnlyList<QueryFragment> fragments)
    {
        string? fallbackAlias = null;

        for (var index = fragments.Count - 1; index >= 0; index--)
        {
            var fragment = fragments[index];
            switch (fragment)
            {
                case ProjectionFragment projection:
                {
                    for (var i = projection.Returns.Length - 1; i >= 0; i--)
                    {
                        var value = projection.Returns[i];
                        if (!string.IsNullOrWhiteSpace(value))
                        {
                            return value.Trim();
                        }
                    }
                    break;
                }
                case AggregationFragment aggregation:
                    return FragmentFormatting.BuildAggregationExpression(aggregation);
                default:
                {
                    var alias = fragment.CurrentAlias;
                    if (!string.IsNullOrWhiteSpace(alias) && !string.Equals(alias, "ps", StringComparison.Ordinal))
                    {
                        fallbackAlias ??= alias.Trim();
                    }
                    break;
                }
            }
        }

        return fallbackAlias;
    }

    public static IReadOnlyList<string> GetLatestProjectionReturns(IReadOnlyList<QueryFragment> fragments)
    {
        for (var index = fragments.Count - 1; index >= 0; index--)
        {
            if (fragments[index] is ProjectionFragment projection)
            {
                var values = projection.Returns
                    .Where(static value => !string.IsNullOrWhiteSpace(value))
                    .Select(static value => value.Trim())
                    .ToArray();

                if (values.Length > 0)
                {
                    return values;
                }
            }
        }

        return Array.Empty<string>();
    }

    public static bool HasExplicitReturnFragments(IReadOnlyList<QueryFragment> fragments)
    {
        for (var i = 0; i < fragments.Count; i++)
        {
            if (fragments[i] is ProjectionFragment or AggregationFragment)
            {
                return true;
            }
        }

        return false;
    }
}
