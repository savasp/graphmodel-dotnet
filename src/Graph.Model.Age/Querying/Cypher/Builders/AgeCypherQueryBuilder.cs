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

namespace Cvoya.Graph.Model.Age.Querying.Cypher.Builders;

using System.Text;
using System.Text.RegularExpressions;
using Cvoya.Graph.Model.Cypher.Querying.Cypher;
using Cvoya.Graph.Model.Age.Querying.Cypher.Visitors.Core;
using Cvoya.Graph.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// AGE-specific Cypher query builder that generates AGE-compatible queries.
/// Does not inherit from the base CypherQueryBuilder to avoid Neo4j-specific syntax generation.
/// </summary>
internal class AgeCypherQueryBuilder
{
    private readonly ILogger<AgeCypherQueryBuilder> _logger;
    private readonly CypherQueryContext _context;

    // Query components
    private readonly List<string> _matchClauses = new();
    private readonly List<string> _whereClauses = new();
    private readonly List<string> _optionalMatchClauses = new();
    private readonly List<string> _returnClauses = new();
    private readonly List<string> _orderByClauses = new();
    private readonly Dictionary<string, object?> _parameters = new();
    
    private int? _limit;
    private int? _skip;
    private bool _distinct;
    private bool _includeComplexProperties;
    private string? _complexPropertyAlias;
    private bool _shouldReverseOrderBy;
    private GraphTraversalDirection? _traversalDirection;

    /// <summary>
    /// Sets a flag to reverse order by clauses when building the query (for Last operations).
    /// </summary>
    public void SetShouldReverseOrderBy(bool shouldReverse)
    {
        _shouldReverseOrderBy = shouldReverse;
        _logger.LogDebug("Set should reverse ORDER BY: {ShouldReverse}", shouldReverse);
    }

    /// <summary>
    /// Gets the query context providing scope and configuration.
    /// </summary>
    public CypherQueryContext Context => _context;

    /// <summary>
    /// Initializes a new instance of the AgeCypherQueryBuilder.
    /// </summary>
    /// <param name="context">The query context providing scope and configuration.</param>
    public AgeCypherQueryBuilder(CypherQueryContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = context.LoggerFactory?.CreateLogger<AgeCypherQueryBuilder>() ?? 
                  NullLogger<AgeCypherQueryBuilder>.Instance;
    }

    /// <summary>
    /// Adds a MATCH clause to the query.
    /// </summary>
    public void AddMatch(string alias, string label)
    {
        var matchClause = $"({alias}:{label})";
        _matchClauses.Add(matchClause);
        _logger.LogDebug("Added MATCH clause: {Match}", matchClause);
    }

    /// <summary>
    /// Adds a custom MATCH pattern to the query.
    /// For multi-hop traversal, patterns are chained together.
    /// </summary>
    public void AddMatchPattern(string pattern)
    {
        Console.WriteLine($"[AddMatchPattern] pattern='{pattern}', existingCount={_matchClauses.Count}");
        if (_matchClauses.Count > 0)
        {
            Console.WriteLine($"[AddMatchPattern] last pattern='{_matchClauses[^1]}'");
        }

        if (_matchClauses.Count > 0 && TryGetStandaloneNodeAlias(_matchClauses[^1], out var standaloneAlias) &&
            pattern.StartsWith($"({standaloneAlias}:", StringComparison.Ordinal))
        {
            _matchClauses[^1] = pattern;
            _logger.LogDebug("Replaced standalone node MATCH with traversal pattern: {Pattern}", pattern);
            return;
        }
        
        // For multi-hop traversal, we need to handle pattern chaining
        if (_matchClauses.Count > 0 && pattern.StartsWith("-[", StringComparison.Ordinal))
        {
            // This is a continuation pattern (starts with -[...]) - chain it to the last pattern
            var lastPattern = _matchClauses[^1];
            _matchClauses[^1] = lastPattern + pattern;
            Console.WriteLine($"[AddMatchPattern] CHAINED via StartsWith('-['): {_matchClauses[^1]}");
            _logger.LogDebug("Chained MATCH pattern: {Pattern} -> {CombinedPattern}", pattern, _matchClauses[^1]);
        }
        else if (_matchClauses.Count > 0 && TryGetLeadingNodeAlias(pattern, out var leadingAlias, out var patternRemainder) &&
                 TryGetTerminalNodeAlias(_matchClauses[^1], out var terminalAlias) &&
                 string.Equals(leadingAlias, terminalAlias, StringComparison.Ordinal))
        {
            // The new pattern begins with the same alias as the terminal node of the previous pattern.
            // Append only the continuation (relationship + target node) to preserve a single chained MATCH clause.
            _matchClauses[^1] += patternRemainder;
            Console.WriteLine($"[AddMatchPattern] CHAINED via shared node alias '{leadingAlias}': {_matchClauses[^1]}");
            _logger.LogDebug("Chained MATCH pattern using shared alias {Alias}: {CombinedPattern}", leadingAlias, _matchClauses[^1]);
        }
        else if (_matchClauses.Count > 0 && pattern.Contains(")-[") && pattern.EndsWith("->") && 
                 _matchClauses[^1].StartsWith("(src", StringComparison.Ordinal))
        {
            // This is a hop pattern ending with "->" that should be PREPENDED to the existing pattern
            // Pattern: "(srcX:Label)-[relX:...]->" and existing: "(srcY:Label)..."
            // Result: "(srcX:Label)-[relX:...]->(srcY:Label)..."
            var existingPattern = _matchClauses[^1];
            _matchClauses[^1] = pattern + existingPattern;
            Console.WriteLine($"[AddMatchPattern] PREPENDED: {_matchClauses[^1]}");
            _logger.LogDebug("Prepended MATCH pattern: {Pattern} + {Existing} -> {CombinedPattern}", 
                pattern, existingPattern, _matchClauses[^1]);
        }
        else
        {
            // This is a new independent pattern
            _matchClauses.Add(pattern);
            Console.WriteLine($"[AddMatchPattern] NEW INDEPENDENT pattern");
            _logger.LogDebug("Added MATCH pattern: {Pattern}", pattern);
        }
    }

    private static bool TryGetLeadingNodeAlias(string pattern, out string alias, out string remainder)
    {
        var match = Regex.Match(pattern, @"^\((\w+):[^)]+\)([-<].*)$");
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
        var match = Regex.Match(pattern, @"\((\w+):[^)]+\)\s*$");
        if (match.Success)
        {
            alias = match.Groups[1].Value;
            return true;
        }

        alias = string.Empty;
        return false;
    }

    /// <summary>
    /// Checks if any MATCH patterns have been added.
    /// </summary>
    public bool HasMatchPatterns => _matchClauses.Count > 0;

    /// <summary>
    /// Gets the number of MATCH patterns that have been added.
    /// </summary>
    public int MatchPatternsCount => _matchClauses.Count;

    /// <summary>
    /// Checks if any RETURN clauses have been added.
    /// </summary>
    public bool HasReturnClauses => _returnClauses.Count > 0;

    /// <summary>
    /// Checks if any RETURN clauses contain aggregation functions.
    /// </summary>
    public bool HasAggregationInReturn => _returnClauses.Any(clause => 
        clause.Contains("count(") || clause.Contains("sum(") || clause.Contains("avg(") || 
        clause.Contains("min(") || clause.Contains("max("));

    /// <summary>
    /// Adds an OPTIONAL MATCH clause to the query.
    /// </summary>
    public void AddOptionalMatch(string pattern)
    {
        _optionalMatchClauses.Add(pattern);
        _logger.LogDebug("Added OPTIONAL MATCH: {Pattern}", pattern);
    }

    /// <summary>
    /// Adds a WHERE condition to the query.
    /// </summary>
    public void AddWhere(string condition)
    {
        _whereClauses.Add(condition);
        _logger.LogDebug("Added WHERE condition: {Condition}", condition);
    }

    /// <summary>
    /// Adds a RETURN clause to the query.
    /// </summary>
    public void AddReturn(string returnClause)
    {
        if (string.IsNullOrWhiteSpace(returnClause))
        {
            return;
        }

        var trimmedClause = returnClause.Trim();

        // If this return assigns an alias (AS), remove any existing entry that referenced the same
        // raw expression so we don't keep both "tgt1" and "tgt1 AS EndNode".
        var asIndex = trimmedClause.IndexOf(" AS ", StringComparison.OrdinalIgnoreCase);
        if (asIndex >= 0)
        {
            var expressionPortion = trimmedClause[..asIndex].Trim();
            _returnClauses.RemoveAll(clause => string.Equals(clause.Trim(), expressionPortion, StringComparison.Ordinal));
        }

        // Avoid duplicates of identical return clauses
        if (_returnClauses.Any(existing => string.Equals(existing.Trim(), trimmedClause, StringComparison.Ordinal)))
        {
            _logger.LogDebug("Skipped duplicate RETURN clause: {Return}", trimmedClause);
            return;
        }

        _returnClauses.Add(trimmedClause);
        _logger.LogDebug("Added RETURN clause: {Return}", trimmedClause);
    }

    /// <summary>
    /// Clears all RETURN clauses from the query.
    /// </summary>
    public void ClearReturn()
    {
        _returnClauses.Clear();
        _logger.LogDebug("Cleared all RETURN clauses");
    }

    /// <summary>
    /// Gets the last RETURN clause added to the query, or null if none exist.
    /// </summary>
    public string? GetLastReturnClause()
    {
        return _returnClauses.Count > 0 ? _returnClauses[^1] : null;
    }

    /// <summary>
    /// Gets all RETURN clauses as a read-only list.
    /// </summary>
    public IReadOnlyList<string> GetReturnClauses()
    {
        return _returnClauses.AsReadOnly();
    }

    /// <summary>
    /// Adds an ORDER BY clause to the query.
    /// </summary>
    public void AddOrderBy(string expression, bool descending = false)
    {
        var orderClause = descending ? $"{expression} DESC" : $"{expression} ASC";
        _orderByClauses.Add(orderClause);
        _logger.LogDebug("Added ORDER BY: {Order}", orderClause);
    }

    /// <summary>
    /// Clears all ORDER BY clauses.
    /// Used when ORDER BY is not applicable (e.g., in aggregation queries).
    /// </summary>
    public void ClearOrderBy()
    {
        if (_orderByClauses.Count == 0)
        {
            return;
        }

        _orderByClauses.Clear();
        _logger.LogDebug("Cleared ORDER BY clauses");
    }

    /// <summary>
    /// Gets a default RETURN clause when none was explicitly specified.
    /// For traversal queries, returns the last target node alias. For relationship queries, returns the relationship alias.
    /// </summary>
    private string? GetDefaultReturnClause()
    {
        if (_matchClauses.Count == 0)
            return null;

        var lastMatch = _matchClauses[^1];

        if (typeof(IRelationship).IsAssignableFrom(_context.Scope.RootType))
        {
            var relationshipMatches = Regex.Matches(lastMatch, @"\[(\w+)(?::[^\]]+)?\]");
            if (relationshipMatches.Count > 0)
            {
                var relationshipAlias = relationshipMatches[^1].Groups[1].Value;
                _logger.LogDebug("Detected default relationship alias from pattern: {Alias}", relationshipAlias);
                return relationshipAlias;
            }
        }

        var nodeMatch = Regex.Match(lastMatch, @"\((\w+):[^)]+\)[^(]*$");
        if (nodeMatch.Success)
        {
            var alias = nodeMatch.Groups[1].Value;
            _logger.LogDebug("Detected default return alias from pattern: {Alias}", alias);
            return alias;
        }

        nodeMatch = Regex.Match(lastMatch, @"\((\w+):");
        if (nodeMatch.Success)
        {
            var alias = nodeMatch.Groups[1].Value;
            _logger.LogDebug("Detected fallback return alias from pattern: {Alias}", alias);
            return alias;
        }

        return null;
    }

    private static bool TryGetStandaloneNodeAlias(string pattern, out string alias)
    {
        var match = Regex.Match(pattern, @"^\((\w+):[^)]+\)$");
        if (match.Success)
        {
            alias = match.Groups[1].Value;
            return true;
        }

        alias = string.Empty;
        return false;
    }

    /// <summary>
    /// Sets the LIMIT for the query.
    /// </summary>
    public void SetLimit(int limit)
    {
        _limit = limit;
        _logger.LogDebug("Set LIMIT: {Limit}", limit);
    }

    /// <summary>
    /// Sets the SKIP for the query.
    /// </summary>
    public void SetSkip(int skip)
    {
        _skip = skip;
        _logger.LogDebug("Set SKIP: {Skip}", skip);
    }

    /// <summary>
    /// Sets the DISTINCT flag for the query.
    /// </summary>
    public void SetDistinct(bool distinct)
    {
        _distinct = distinct;
        _logger.LogDebug("Set DISTINCT: {Distinct}", distinct);
    }

    /// <summary>
    /// Sets the traversal direction for relationship patterns.
    /// </summary>
    public void SetTraversalDirection(GraphTraversalDirection direction)
    {
        _traversalDirection = direction;
        _logger.LogDebug("Set traversal direction: {Direction}", direction);
    }

    /// <summary>
    /// Gets the current traversal direction.
    /// </summary>
    public GraphTraversalDirection? TraversalDirection => _traversalDirection;

    /// <summary>
    /// Clears any explicitly configured traversal direction so subsequent traversals use the default.
    /// </summary>
    public void ClearTraversalDirection()
    {
        if (_traversalDirection.HasValue)
        {
            _logger.LogDebug("Cleared traversal direction");
        }

        _traversalDirection = null;
    }

    /// <summary>
    /// Enables complex property loading using AGE-compatible syntax.
    /// </summary>
    /// <param name="alias">Alias that represents the node being hydrated.</param>
    public void EnableComplexPropertyLoading(string? alias = null)
    {
        _includeComplexProperties = true;
        _complexPropertyAlias = alias;
        _logger.LogDebug("Enabled AGE-compatible complex property loading for alias {Alias}", alias ?? "<unspecified>");
    }

    /// <summary>
    /// Disables complex property loading.
    /// </summary>
    public void DisableComplexPropertyLoading()
    {
        _includeComplexProperties = false;
        _complexPropertyAlias = null;
        _logger.LogDebug("Disabled complex property loading");
    }

    /// <summary>
    /// Adds a parameter to the query and returns the parameter reference.
    /// </summary>
    public string AddParameter(object? value)
    {
        // Check if parameter already exists to avoid duplicates
        var existingParam = _parameters.FirstOrDefault(p => Equals(p.Value, value));
        if (existingParam.Key != null)
        {
            return $"${existingParam.Key}";
        }

        var paramName = $"param_{_parameters.Count}";
        _parameters[paramName] = value;
        _logger.LogDebug("Added parameter: {ParamName} = {Value}", paramName, value);
        return $"${paramName}";
    }

    /// <summary>
    /// Builds the final CypherQuery with AGE-compatible syntax.
    /// </summary>
    public CypherQuery Build()
    {
    _logger.LogDebug("Building AGE-compatible Cypher query");

        var query = new StringBuilder();

        // Build MATCH clauses
        if (_matchClauses.Count > 0)
        {
            query.AppendLine($"MATCH {string.Join(", ", _matchClauses)}");
        }

        // Build OPTIONAL MATCH clauses
        foreach (var optionalMatch in _optionalMatchClauses)
        {
            query.AppendLine($"OPTIONAL MATCH {optionalMatch}");
        }

        // Build WHERE clauses
        if (_whereClauses.Count > 0)
        {
            query.AppendLine($"WHERE {string.Join(" AND ", _whereClauses)}");
        }

        // Handle complex properties with AGE-compatible syntax
        if (_includeComplexProperties)
        {
            AppendAgeComplexPropertyLoading(query);
        }

        // Build RETURN - must come before ORDER BY and LIMIT in AGE
        if (_returnClauses.Count > 0 && !_includeComplexProperties)
        {
            var distinctClause = _distinct ? "DISTINCT " : "";
            query.AppendLine($"RETURN {distinctClause}{string.Join(", ", _returnClauses)}");
        }
        else if (_returnClauses.Count == 0 && !_includeComplexProperties && _matchClauses.Count > 0)
        {
            // Add default RETURN clause if none was explicitly added
            // For traversal queries, return the last alias in the pattern
            var defaultReturn = GetDefaultReturnClause();
            if (!string.IsNullOrEmpty(defaultReturn))
            {
                var distinctClause = _distinct ? "DISTINCT " : "";
                query.AppendLine($"RETURN {distinctClause}{defaultReturn}");
                _logger.LogDebug("Added default RETURN clause: {Return}", defaultReturn);
            }
        }

        // Build ORDER BY - must come after RETURN but before LIMIT in AGE
        if (_orderByClauses.Count > 0)
        {
            // Apply reversal if this is a Last operation
            if (_shouldReverseOrderBy)
            {
                ReverseOrderBy();
                _logger.LogDebug("Applied deferred ORDER BY reversal for Last operation");
            }
            query.AppendLine($"ORDER BY {string.Join(", ", _orderByClauses)}");
        }
        // If we have DISTINCT + SKIP but no explicit ORDER BY, add implicit ordering for deterministic results
        else if (_distinct && _skip.HasValue && _returnClauses.Count > 0)
        {
            // Use the first return clause as the ordering column for deterministic results
            var firstReturnClause = _returnClauses[0];
            var orderDirection = _shouldReverseOrderBy ? " DESC" : "";
            query.AppendLine($"ORDER BY {firstReturnClause}{orderDirection}");
            _logger.LogDebug("Added implicit ORDER BY for DISTINCT + SKIP: {OrderBy}", firstReturnClause);
        }

        // Build SKIP/LIMIT - must come after ORDER BY in AGE
        if (_skip.HasValue)
        {
            query.AppendLine($"SKIP {_skip}");
        }

        if (_limit.HasValue)
        {
            query.AppendLine($"LIMIT {_limit}");
        }

        var finalQuery = query.ToString().Trim();
        _logger.LogInformation("Generated AGE query with LIMIT:\n{Query}", finalQuery);

        return new CypherQuery(finalQuery, _parameters);
    }

    /// <summary>
    /// Appends AGE-compatible complex property loading syntax.
    /// </summary>
    public void ReverseOrderBy()
    {
        if (_orderByClauses.Count == 0)
        {
            return;
        }

        for (var i = 0; i < _orderByClauses.Count; i++)
        {
            var clause = _orderByClauses[i];

            if (clause.EndsWith(" DESC", StringComparison.OrdinalIgnoreCase))
            {
                _orderByClauses[i] = clause[..^5] + " ASC";
            }
            else if (clause.EndsWith(" ASC", StringComparison.OrdinalIgnoreCase))
            {
                _orderByClauses[i] = clause[..^4] + " DESC";
            }
            else
            {
                _orderByClauses[i] = $"{clause} DESC";
            }
        }

        _logger.LogDebug("Reversed ORDER BY clauses for Last() operation");
    }

    /// <summary>
    /// Appends AGE-compatible complex property loading syntax.
    /// </summary>
    private void AppendAgeComplexPropertyLoading(StringBuilder query)
    {
        _logger.LogDebug("Appending AGE-compatible complex property loading");

        var alias = _complexPropertyAlias;

        if (string.IsNullOrWhiteSpace(alias))
        {
            alias = _context.Scope.CurrentAlias ?? _context.Scope.GetNumberedAlias("src");
            _logger.LogDebug("Falling back to contextual alias {Alias} for complex property loading", alias);
        }

        // Simple AGE-compatible complex property loading without Neo4j list comprehensions
        query.AppendLine($@"OPTIONAL MATCH ({alias})-[prop_rel]->(prop_node)
WHERE type(prop_rel) STARTS WITH '__PROPERTY__'
WITH {alias}, 
     collect({{
         ParentNode: {alias},
         Relationship: prop_rel,
         SequenceNumber: coalesce(prop_rel.SequenceNumber, 0),
         Property: prop_node
     }}) AS complex_properties
RETURN {{
    Node: {alias},
    ComplexProperties: complex_properties
}}");

        _logger.LogDebug("Added AGE-compatible complex property loading");
    }
}
