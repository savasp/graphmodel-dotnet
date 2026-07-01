// Copyright 2025 Savas Parastatidis
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//      https://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

namespace Cvoya.Graph.Model.Age.Core.Entities;

using Cvoya.Graph.Model;
using Npgsql.Age;
using static Cvoya.Graph.Model.Age.Querying.Cypher.Visitors.Core.ExpressionTranslationHelper;

/// <summary>
/// Helper methods for building Cypher queries used in entity validation.
/// </summary>
internal static class CypherQueryHelper
{
    public static string GetQualifiedProperty(string alias, string propertyGraphName)
    {
        var mapped = MapPropertyName(propertyGraphName);
        return $"{alias}.`{EscapePropertyName(mapped)}`";
    }

    public static string GetQualifiedIdProperty(string alias)
    {
        var mapped = MapPropertyName(nameof(IEntity.Id));
        return $"{alias}.`{EscapePropertyName(mapped)}`";
    }

    public static string EscapeLabel(string label) => $"`{label.Replace("`", "``")}`";

    public static string EscapePropertyName(string propertyName) => propertyName.Replace("`", "``");

    public static async Task<bool> HasConflictAsync(
        AgeGraphContext context,
        AgeGraphTransaction transaction,
        string alias,
        string labelForMatch,
        bool isRelationship,
        string whereClause,
        IReadOnlyDictionary<string, object?> parameters,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(whereClause))
        {
            return false;
        }

        var escapedLabel = EscapeLabel(labelForMatch);
        var matchPattern = isRelationship
            ? $"()-[{alias}:{escapedLabel}]-()"
            : $"({alias}:{escapedLabel})";

        var cypher = $"MATCH {matchPattern} WHERE {whereClause} RETURN 1 LIMIT 1";
        await using var command = context.Connection.CreateCypherCommand(context.GraphName, cypher, new Dictionary<string, object?>(parameters));
        command.Transaction = transaction?.Transaction;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return false;
        }

        return !reader.IsDBNull(0);
    }
}
