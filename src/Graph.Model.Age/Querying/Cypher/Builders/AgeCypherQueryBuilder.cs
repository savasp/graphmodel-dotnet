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
using Cvoya.Graph.Model.Cypher.Querying.Cypher;
using Cvoya.Graph.Model.Age.Querying.Cypher.Visitors.Core;
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
        // For multi-hop traversal, we need to handle pattern chaining
        if (_matchClauses.Count > 0 && pattern.StartsWith("-[", StringComparison.Ordinal))
        {
            // This is a continuation pattern (starts with -[...]) - chain it to the last pattern
            var lastPattern = _matchClauses[^1];
            _matchClauses[^1] = lastPattern + pattern;
            _logger.LogDebug("Chained MATCH pattern: {Pattern} -> {CombinedPattern}", pattern, _matchClauses[^1]);
        }
        else
        {
            // This is a new independent pattern
            _matchClauses.Add(pattern);
            _logger.LogDebug("Added MATCH pattern: {Pattern}", pattern);
        }
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
        _returnClauses.Add(returnClause);
        _logger.LogDebug("Added RETURN clause: {Return}", returnClause);
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
    /// Reverses the order of ORDER BY clauses.
    /// </summary>
    public void ReverseOrderBy()
    {
        for (int i = 0; i < _orderByClauses.Count; i++)
        {
            if (_orderByClauses[i].EndsWith(" ASC"))
            {
                _orderByClauses[i] = _orderByClauses[i].Replace(" ASC", " DESC");
            }
            else if (_orderByClauses[i].EndsWith(" DESC"))
            {
                _orderByClauses[i] = _orderByClauses[i].Replace(" DESC", " ASC");
            }
        }
        _logger.LogDebug("Reversed ORDER BY clauses");
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
    /// Enables complex property loading using AGE-compatible syntax.
    /// </summary>
    public void EnableComplexPropertyLoading()
    {
        _includeComplexProperties = true;
        _logger.LogDebug("Enabled AGE-compatible complex property loading");
    }

    /// <summary>
    /// Disables complex property loading.
    /// </summary>
    public void DisableComplexPropertyLoading()
    {
        _includeComplexProperties = false;
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

        // Build ORDER BY - must come after RETURN but before LIMIT in AGE
        if (_orderByClauses.Count > 0)
        {
            query.AppendLine($"ORDER BY {string.Join(", ", _orderByClauses)}");
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
    private void AppendAgeComplexPropertyLoading(StringBuilder query)
    {
        _logger.LogDebug("Appending AGE-compatible complex property loading");

        var alias = "n"; // Default alias - could be made configurable

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
