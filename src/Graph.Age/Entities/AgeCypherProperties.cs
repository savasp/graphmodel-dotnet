// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Age.Entities;

using Cvoya.Graph.Age.Querying.Cypher;

internal static class AgeCypherProperties
{
    public static string BuildSetClause(
        string alias,
        IReadOnlyDictionary<string, object?> properties,
        IDictionary<string, object?> parameters,
        string parameterPrefix)
    {
        var assignments = new List<string>(properties.Count);
        var index = 0;
        foreach (var (name, value) in properties)
        {
            var parameterName = $"{parameterPrefix}{index++}";
            assignments.Add($"{alias}.{CypherIdentifier.Escape(name, "property name")} = ${parameterName}");
            parameters.Add(parameterName, value);
        }

        return assignments.Count == 0 ? string.Empty : $"SET {string.Join(", ", assignments)}";
    }
}
