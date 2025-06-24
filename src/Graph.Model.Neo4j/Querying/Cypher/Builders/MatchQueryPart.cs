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

using System.Text;

namespace Cvoya.Graph.Model.Neo4j.Querying.Cypher.Builders;

/// <summary>
/// Handles MATCH clause construction for Cypher queries.
/// Extracted from the monolithic CypherQueryBuilder to provide focused responsibility.
/// </summary>
internal class MatchQueryPart : ICypherQueryPart
{
    private readonly List<string> _matchClauses = [];
    private readonly List<string> _optionalMatchClauses = [];

    public int Order => 1; // MATCH comes first in Cypher queries

    public bool HasContent => _matchClauses.Count > 0 || _optionalMatchClauses.Count > 0;

    /// <summary>
    /// Adds a simple node match pattern.
    /// </summary>
    public void AddMatch(string alias, string? label = null, string? pattern = null)
    {
        var match = new StringBuilder($"({alias}");

        if (!string.IsNullOrEmpty(label))
        {
            match.Append($":{label}");
        }

        match.Append(')');

        if (!string.IsNullOrEmpty(pattern))
        {
            match.Append(pattern);
        }

        _matchClauses.Add(match.ToString());
    }

    /// <summary>
    /// Adds a complex match pattern (e.g., relationship patterns).
    /// </summary>
    public void AddMatchPattern(string fullPattern)
    {
        // Avoid duplicating exact patterns
        if (_matchClauses.Contains(fullPattern))
        {
            return;
        }
        _matchClauses.Add(fullPattern);
    }

    /// <summary>
    /// Adds an OPTIONAL MATCH clause.
    /// </summary>
    public void AddOptionalMatch(string pattern)
    {
        _optionalMatchClauses.Add(pattern);
    }

    /// <summary>
    /// Adds a relationship match with direction and depth constraints.
    /// </summary>
    public void AddRelationshipMatch(string relationshipType, GraphTraversalDirection? direction = null,
        int? minDepth = null, int? maxDepth = null)
    {
        var depthPattern = BuildDepthPattern(minDepth, maxDepth);
        var directionPattern = BuildDirectionPattern(direction);

        var pattern = $"(src){directionPattern}[r:{relationshipType}{depthPattern}]{GetOppositeDirection(directionPattern)}(tgt)";
        _matchClauses.Add(pattern);
    }

    /// <summary>
    /// Clears all match clauses.
    /// </summary>
    public void ClearMatches()
    {
        _matchClauses.Clear();
        _optionalMatchClauses.Clear();
    }

    public void AppendTo(StringBuilder builder, Dictionary<string, object?> parameters)
    {
        // Add regular MATCH clauses
        if (_matchClauses.Count > 0)
        {
            builder.Append("MATCH ");
            builder.AppendJoin(", ", _matchClauses);
            builder.AppendLine();
        }

        // Add OPTIONAL MATCH clauses
        foreach (var optionalMatch in _optionalMatchClauses)
        {
            builder.AppendLine($"OPTIONAL MATCH {optionalMatch}");
        }
    }

    private static string BuildDepthPattern(int? minDepth, int? maxDepth)
    {
        return (minDepth, maxDepth) switch
        {
            (null, int max) => $"*1..{max}",
            (int min, int max) => $"*{min}..{max}",
            (int min, null) => $"*{min}..",
            _ => ""
        };
    }

    private static string BuildDirectionPattern(GraphTraversalDirection? direction)
    {
        return direction switch
        {
            GraphTraversalDirection.Outgoing => "-",
            GraphTraversalDirection.Incoming => "<-",
            GraphTraversalDirection.Both => "-",
            _ => "-"
        };
    }

    private static string GetOppositeDirection(string directionPattern)
    {
        return directionPattern switch
        {
            "-" => "->",
            "<-" => "-",
            _ => "->"
        };
    }
}