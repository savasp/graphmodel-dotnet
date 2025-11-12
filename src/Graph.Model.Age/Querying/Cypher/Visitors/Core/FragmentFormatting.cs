// Copyright 2025 Savas Parastatidis

namespace Cvoya.Graph.Model.Age.Querying.Cypher.Visitors.Core;

using Cvoya.Graph.Model.Cypher.Querying.Cypher.Visitors.Core;

/// <summary>
/// Shared helpers for formatting query fragments into Cypher snippets.
/// Extracted so both renderer and analysis utilities produce consistent strings.
/// </summary>
internal static class FragmentFormatting
{
    public static string BuildAggregationExpression(AggregationFragment fragment)
    {
        return fragment.AggregationType switch
        {
            "any" => $"count({fragment.Expression}) > 0",
            "all" => $"count({fragment.Expression}) = 0",
            _ => $"{fragment.AggregationType}({fragment.Expression})"
        };
    }
}
