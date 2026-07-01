// Copyright 2025 Savas Parastatidis

namespace Cvoya.Graph.Model.Age.Querying.Cypher.Visitors.Core;

using Cvoya.Graph.Model.Cypher.Querying.Cypher.Visitors.Core;

/// <summary>
/// Shared helpers for formatting query fragments into Cypher snippets.
/// </summary>
internal static class FragmentFormatting
{
    public static string BuildAggregationExpression(AggregationFragment fragment)
    {
        // Use * for count() since AGE doesn't support count(nodeVariable)
        var expression = string.Equals(fragment.AggregationType, "count", StringComparison.OrdinalIgnoreCase)
            ? "*"
            : fragment.Expression;
        return fragment.AggregationType switch
        {
            "any" => $"count(*) > 0",
            "all" => $"count(*) = 0",
            _ => $"{fragment.AggregationType}({expression})"
        };
    }
}
