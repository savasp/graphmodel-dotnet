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
using Cvoya.Graph.Model.Neo4j.Querying.Cypher.Visitors.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cvoya.Graph.Model.Neo4j.Querying.Cypher.Builders;

/// <summary>
/// Handles MATCH clause construction for Cypher queries.
/// Extracted from the monolithic CypherQueryBuilder to provide focused responsibility.
/// </summary>
internal class MatchQueryPart : ICypherQueryPart
{
    private readonly List<string> _matchClauses = [];
    private readonly List<string> _optionalMatchClauses = [];
    private readonly List<string> _additionalMatchStatements = [];
    private readonly ILogger _logger;

    public int Order => 1; // MATCH comes first in Cypher queries

    public bool HasContent => _matchClauses.Count > 0 || _optionalMatchClauses.Count > 0 || _additionalMatchStatements.Count > 0;

    public MatchQueryPart(CypherQueryContext context)
    {
        _logger = context.LoggerFactory?.CreateLogger<MatchQueryPart>()
            ?? NullLogger<MatchQueryPart>.Instance;
    }

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
        // Check if this looks like a complex property match (contains parentheses and arrow)
        // These should be separate MATCH statements to avoid cross products
        if (fullPattern.Contains(")-[") && fullPattern.Contains("]->("))
        {
            // Avoid duplicating exact patterns
            if (_additionalMatchStatements.Contains(fullPattern))
            {
                return;
            }
            _additionalMatchStatements.Add(fullPattern);
            _logger.LogDebug("[MatchQueryPart] Added to additional MATCH statements: {Pattern}", fullPattern);
        }
        else
        {
            // Avoid duplicating exact patterns
            if (_matchClauses.Contains(fullPattern))
            {
                return;
            }
            _matchClauses.Add(fullPattern);
            _logger.LogDebug("[MatchQueryPart] Added to main MATCH clauses: {Pattern}", fullPattern);
        }
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
        _additionalMatchStatements.Clear();
    }

    /// <summary>
    /// Clears only the main match clauses, preserving additional match statements (like complex properties).
    /// </summary>
    public void ClearMainMatches()
    {
        _matchClauses.Clear();
        _optionalMatchClauses.Clear();
        // Keep _additionalMatchStatements intact
    }

    public void AppendTo(StringBuilder builder, Dictionary<string, object?> parameters)
    {
        _logger.LogDebug("[MatchQueryPart] AppendTo called - Main clauses: {MainCount}, Additional: {AdditionalCount}, Optional: {OptionalCount}",
            _matchClauses.Count, _additionalMatchStatements.Count, _optionalMatchClauses.Count);

        // Add regular MATCH clauses
        if (_matchClauses.Count > 0)
        {
            builder.Append("MATCH ");
            builder.AppendJoin(", ", _matchClauses);
            builder.AppendLine();
            _logger.LogDebug("[MatchQueryPart] Added main MATCH clauses: {Clauses}", string.Join(", ", _matchClauses));
        }

        // Add additional MATCH statements (for complex properties, etc.)
        foreach (var additionalMatch in _additionalMatchStatements)
        {
            builder.AppendLine($"MATCH {additionalMatch}");
            _logger.LogDebug("[MatchQueryPart] Added additional MATCH: {Match}", additionalMatch);
        }

        // Add OPTIONAL MATCH clauses
        foreach (var optionalMatch in _optionalMatchClauses)
        {
            builder.AppendLine($"OPTIONAL MATCH {optionalMatch}");
            _logger.LogDebug("[MatchQueryPart] Added OPTIONAL MATCH: {Match}", optionalMatch);
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