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

using System.Collections;
using System.Text;
using Cvoya.Graph.Model;
using Microsoft.Extensions.Logging;
using Npgsql.Age;
using Npgsql.Age.Types;

/// <summary>
/// Logs conflict details when unique constraint validation fails.
/// </summary>
internal static class ConflictLogger
{
    public static async Task LogConflictDetailsAsync(
        AgeGraphContext context,
        string alias,
        string labelForMatch,
        bool isRelationship,
        string whereClause,
        IReadOnlyDictionary<string, object?> parameters,
        CancellationToken cancellationToken)
    {
        var logger = context.LoggerFactory.CreateLogger(nameof(AgeEntityAttributeValidator));

        try
        {
            var escapedLabel = CypherQueryHelper.EscapeLabel(labelForMatch);
            var matchPattern = isRelationship
                ? $"()-[{alias}:{escapedLabel}]-()"
                : $"({alias}:{escapedLabel})";

            var cypher = $"MATCH {matchPattern} WHERE {whereClause} RETURN {alias} LIMIT 3";
            await using var command = context.Connection.CreateCypherCommand(context.GraphName, cypher, new Dictionary<string, object?>(parameters));
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

            var conflicts = new List<string>();
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                if (!reader.IsDBNull(0))
                {
                    var agtype = reader.GetFieldValue<Agtype>(0);
                    conflicts.Add(FormatAgtype(agtype));
                }
            }

            if (conflicts.Count == 0)
            {
                logger.LogWarning("Composite key conflict reported but no matching entities were found for logging.");
            }
            else
            {
                logger.LogWarning("Composite key conflict details: {Conflicts}", string.Join(" | ", conflicts));
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to log composite key conflict details.");
        }
    }

    internal static string FormatAgtype(Agtype agtype)
    {
        if (agtype.IsEdge)
        {
            var edge = agtype.GetEdge();
            var builder = new StringBuilder();
            builder.Append("Edge(");
            builder.Append("Id=").Append(edge.Id);
            builder.Append(", Label=").Append(edge.Label);
            builder.Append(", Start=").Append(edge.StartId);
            builder.Append(", End=").Append(edge.EndId);
            builder.Append(", Properties={");

            var first = true;
            foreach (var (key, value) in edge.Properties)
            {
                if (!first)
                {
                    builder.Append(", ");
                }
                first = false;
                builder.Append(key).Append('=').Append(FormatValue(value));
            }

            builder.Append("})");
            return builder.ToString();
        }

        if (agtype.IsVertex)
        {
            var vertex = agtype.GetVertex();
            var builder = new StringBuilder();
            builder.Append("Vertex(");
            builder.Append("Id=").Append(vertex.Id);
            builder.Append(", Label=").Append(vertex.Label);
            builder.Append(", Properties={");

            var first = true;
            foreach (var (key, value) in vertex.Properties)
            {
                if (!first)
                {
                    builder.Append(", ");
                }
                first = false;
                builder.Append(key).Append('=').Append(FormatValue(value));
            }

            builder.Append("})");
            return builder.ToString();
        }

        return agtype.ToString() ?? "<null>";
    }

    private static string FormatValue(object? value)
    {
        return value switch
        {
            null => "<null>",
            string s => s,
            Agtype nested => FormatAgtype(nested),
            IEnumerable<object?> list => $"[{string.Join(", ", list.Select(FormatValue))}]",
            IEnumerable enumerable => $"[{string.Join(", ", enumerable.Cast<object?>().Select(FormatValue))}]",
            _ => value.ToString() ?? string.Empty
        };
    }
}
