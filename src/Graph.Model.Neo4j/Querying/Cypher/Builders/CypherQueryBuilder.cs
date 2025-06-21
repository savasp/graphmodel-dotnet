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

namespace Cvoya.Graph.Model.Neo4j.Querying.Cypher.Builders;

using System.Linq;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

internal class CypherQueryBuilder(ILoggerFactory? loggerFactory = null)
{
    private readonly ILogger<CypherQueryBuilder> _logger = loggerFactory?.CreateLogger<CypherQueryBuilder>()
        ?? NullLogger<CypherQueryBuilder>.Instance;

    private readonly List<string> _matchClauses = [];
    private readonly List<string> _whereClauses = [];
    private readonly List<string> _returnClauses = [];
    private readonly List<(string Expression, bool IsDescending)> _orderByClauses = [];
    private readonly Dictionary<string, object?> _parameters = [];
    private readonly List<string> _optionalMatchClauses = [];
    private readonly List<string> _withClauses = [];
    private readonly List<string> _unwindClauses = [];
    private readonly List<string> _groupByClauses = [];
    private int? _limit;
    private int? _skip;
    private bool _isDistinct;
    private bool _isRelationshipQuery;

    private string? _aggregation;
    private bool _isExistsQuery;
    private bool _isNotExistsQuery;
    private bool _includeComplexProperties;
    private string? _mainNodeAlias;
    private int _parameterCounter;
    private bool _loadPathSegment;
    private bool _hasUserProjections = false;

    public bool HasUserProjections => _hasUserProjections;

    public bool HasExplicitReturn => _returnClauses.Count > 0 || _aggregation != null || _isExistsQuery || _isNotExistsQuery;

    public bool IsRelationshipQuery => _isRelationshipQuery;

    public void SetExistsQuery()
    {
        _isExistsQuery = true;
    }

    public void SetNotExistsQuery()
    {
        _isNotExistsQuery = true;
    }

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

        // Keep track of the main node alias
        _mainNodeAlias ??= alias;
    }

    public void AddMatchPattern(string fullPattern)
    {
        // For relationship patterns, ensure we don't duplicate them
        if (fullPattern.Contains("-[") && _matchClauses.Any(c => c.Contains("-[") && c.Contains("]->")))
        {
            return; // Skip if we already have a relationship pattern
        }
        _matchClauses.Add(fullPattern);
    }

    public void EnableComplexPropertyLoading()
    {
        _includeComplexProperties = true;
    }

    public void EnablePathSegmentLoading()
    {
        _includeComplexProperties = true;
        _loadPathSegment = true;
    }

    public void ClearMatches()
    {
        _matchClauses.Clear();
    }

    public void ClearWhere()
    {
        _whereClauses.Clear();
    }

    public void AddWhere(string condition)
    {
        // Don't add duplicate WHERE clauses
        if (!_whereClauses.Contains(condition))
        {
            _whereClauses.Add(condition);
        }
    }

    public void ClearReturn()
    {
        _returnClauses.Clear();
    }

    public void SetMainNodeAlias(string alias)
    {
        _mainNodeAlias = alias;
    }

    public void AddOrderBy(string expression, bool isDescending = false)
    {
        _orderByClauses.Add((expression, isDescending));
    }

    public void SetSkip(int skip) => _skip = skip;
    public void SetLimit(int limit) => _limit = limit;
    public void SetAggregation(string function, string expression) => _aggregation = $"{function}({expression})";

    public string AddParameter(object? value)
    {
        _logger.LogDebug("Adding parameter with value: {Value}", value);

        // Check if we already have this value as a parameter
        var existingParam = _parameters.FirstOrDefault(p => Equals(p.Value, value));
        if (existingParam.Key != null)
        {
            return $"${existingParam.Key}";
        }

        var paramName = $"p{_parameterCounter++}";
        _parameters[paramName] = value;
        return $"${paramName}";
    }

    public CypherQuery Build()
    {
        _logger.LogDebug("Building Cypher query");

        // Handle special query types first
        if (_isExistsQuery)
        {
            return BuildExistsQuery();
        }

        if (_isNotExistsQuery)
        {
            return BuildNotExistsQuery();
        }

        // Handle complex properties if needed
        if (_includeComplexProperties)
        {
            return BuildWithComplexProperties();
        }

        // Otherwise build a simple query
        return BuildSimpleQuery();
    }

    public void AddRelationshipMatch(string relationshipType)
    {
        _logger.LogDebug("AddRelationshipMatch called with type: {Type}", relationshipType);

        // Use the same pattern as path segments
        _matchClauses.Add($"(src)-[r:{relationshipType}]->(tgt)");
        _mainNodeAlias = "r"; // Set main alias to the relationship

        _isRelationshipQuery = true;

        // Enable path segment loading since relationships are path segments
        EnablePathSegmentLoading();
    }

    public bool HasOrderBy => _orderByClauses.Any();

    public void ReverseOrderBy()
    {
        _logger.LogDebug("Reversing ORDER BY clauses");

        // Flip the IsDescending flag for all order clauses
        for (int i = 0; i < _orderByClauses.Count; i++)
        {
            var (expression, isDescending) = _orderByClauses[i];
            _orderByClauses[i] = (expression, !isDescending);
        }
    }

    private CypherQuery BuildSimpleQuery()
    {
        _logger.LogDebug("Building simple query");

        var query = new StringBuilder();

        // Build the main query structure
        AppendMatchClauses(query);
        AppendWhereClauses(query);
        AppendReturnClause(query);
        AppendOrderByClauses(query);
        AppendPaginationClauses(query);

        return new CypherQuery(query.ToString().Trim(), new Dictionary<string, object?>(_parameters));
    }

    private void AppendMatchClauses(StringBuilder query)
    {
        _logger.LogDebug("Appending MATCH clauses");

        if (_matchClauses.Count > 0)
        {
            query.Append("MATCH ");
            query.AppendJoin(", ", _matchClauses);
            query.AppendLine();
        }

        // Add optional matches if any
        foreach (var optionalMatch in _optionalMatchClauses)
        {
            query.AppendLine($"OPTIONAL MATCH {optionalMatch}");
        }
    }

    private void AppendWhereClauses(StringBuilder query)
    {
        _logger.LogDebug("Appending WHERE clauses");

        if (_whereClauses.Count > 0)
        {
            query.Append("WHERE ");
            query.AppendJoin(" AND ", _whereClauses);
            query.AppendLine();
        }
    }

    private void AppendReturnClause(StringBuilder query)
    {
        _logger.LogDebug("Appending RETURN clause");

        // Handle WITH clauses first
        foreach (var withClause in _withClauses)
        {
            query.AppendLine($"WITH {withClause}");
        }

        // Handle UNWIND clauses
        foreach (var unwindClause in _unwindClauses)
        {
            query.AppendLine($"UNWIND {unwindClause}");
        }

        // Build the RETURN clause
        query.Append("RETURN ");

        if (_aggregation != null)
        {
            query.Append(_aggregation);
        }
        else if (_returnClauses.Count > 0)
        {
            var returnExpression = _isDistinct
                ? $"DISTINCT {string.Join(", ", _returnClauses)}"
                : string.Join(", ", _returnClauses);
            query.Append(returnExpression);
        }
        else
        {
            // Default return - let the complex property handling take care of structured returns
            query.Append(_mainNodeAlias ?? "n");
        }

        query.AppendLine();

        // Add GROUP BY if needed
        if (_groupByClauses.Count > 0)
        {
            query.Append("GROUP BY ");
            query.AppendJoin(", ", _groupByClauses);
            query.AppendLine();
        }
    }

    private void AppendOrderByClauses(StringBuilder query)
    {
        _logger.LogDebug("Appending ORDER BY clauses");

        if (_orderByClauses.Count > 0)
        {
            query.Append("ORDER BY ");
            var orderByParts = _orderByClauses.Select(o =>
                o.IsDescending ? $"{o.Expression} DESC" : o.Expression);
            query.AppendJoin(", ", orderByParts);
            query.AppendLine();
        }
    }

    private void AppendPaginationClauses(StringBuilder query)
    {
        _logger.LogDebug("Appending pagination clauses");

        if (_skip.HasValue)
        {
            query.AppendLine($"SKIP {_skip.Value}");
        }

        if (_limit.HasValue)
        {
            query.AppendLine($"LIMIT {_limit.Value}");
        }
    }

    private CypherQuery BuildExistsQuery()
    {
        _logger.LogDebug("Building EXISTS query");

        var query = new StringBuilder();

        AppendMatchClauses(query);
        AppendWhereClauses(query);

        // For EXISTS queries, we return a count > 0
        query.AppendLine($"RETURN COUNT({_mainNodeAlias ?? "n"}) > 0 AS exists");

        return new CypherQuery(query.ToString().Trim(), new Dictionary<string, object?>(_parameters));
    }

    private CypherQuery BuildNotExistsQuery()
    {
        _logger.LogDebug("Building NOT EXISTS query");

        var query = new StringBuilder();

        AppendMatchClauses(query);
        AppendWhereClauses(query);

        // For NOT EXISTS queries (used by All), return true if no matching nodes exist
        query.AppendLine($"RETURN COUNT({_mainNodeAlias ?? "n"}) = 0 AS all");

        return new CypherQuery(query.ToString().Trim(), new Dictionary<string, object?>(_parameters));
    }

    private CypherQuery BuildWithComplexProperties()
    {
        _logger.LogDebug("Building query with complex properties");

        var query = new StringBuilder();

        // First part: get the main nodes
        AppendMatchClauses(query);
        AppendWhereClauses(query);

        // For path segments, we don't need the intermediate WITH clause
        if (_loadPathSegment)
        {
            // Skip the WITH clause for path segments - go straight to complex property loading
            AppendComplexPropertyMatchesForPathSegment(query);
        }
        else if (!string.IsNullOrEmpty(_mainNodeAlias))
        {
            // For regular node queries, collect main nodes with ordering and pagination
            AppendOrderByClauses(query);
            AppendPaginationClauses(query);
            AppendComplexPropertyMatchesForSingleNode(query);
        }
        else
        {
            // Fallback - just add complex property matches
            AppendComplexPropertyMatchesForSingleNode(query);
        }

        return new CypherQuery(query.ToString().Trim(), new Dictionary<string, object?>(_parameters));
    }

    private void AppendComplexPropertyMatchesForSingleNode(StringBuilder query)
    {
        _logger.LogDebug("Appending complex property matches for single node");

        query.AppendLine(@$"
            // All complex property paths from src
            OPTIONAL MATCH src_path = ({_mainNodeAlias})-[rels*1..]->(prop)
            WHERE ALL(rel in rels WHERE type(rel) STARTS WITH '{GraphDataModel.PropertyRelationshipTypeNamePrefix}')
            WITH {_mainNodeAlias},
                CASE 
                    WHEN src_path IS NULL THEN []
                    ELSE [i IN range(0, size(rels)-1) | {{
                        ParentNode: nodes(src_path)[i],
                        Relationship: rels[i],
                        Property: nodes(src_path)[i+1]
                    }}]
                END AS src_flat_property

            WITH {_mainNodeAlias},
                reduce(flat = [], l IN collect(src_flat_property) | flat + l) AS src_flat_properties

            RETURN {{
                Node: {_mainNodeAlias},
                ComplexProperties: src_flat_properties
            }} AS Node
        ");
    }

    private void AppendComplexPropertyMatchesForPathSegment(StringBuilder query)
    {
        _logger.LogDebug("Appending complex property matches for path segment");

        query.AppendLine(@$"
            // All complex property paths from source node
            OPTIONAL MATCH src_path = (src)-[rels*1..]->(prop)
            WHERE ALL(rel in rels WHERE type(rel) STARTS WITH '{GraphDataModel.PropertyRelationshipTypeNamePrefix}')
            WITH src, r, tgt, 
                CASE 
                    WHEN src_path IS NULL THEN []
                    ELSE [i IN range(0, size(rels)-1) | {{
                        ParentNode: nodes(src_path)[i],
                        Relationship: rels[i],
                        Property: nodes(src_path)[i+1]
                    }}]
                END AS src_flat_property

            WITH src, r, tgt,
                reduce(flat = [], l IN collect(src_flat_property) | flat + l) AS src_flat_properties

            // All complex property paths from target node
            OPTIONAL MATCH tgt_path = (tgt)-[trels*1..]->(tprop)
            WHERE ALL(rel in trels WHERE type(rel) STARTS WITH '{GraphDataModel.PropertyRelationshipTypeNamePrefix}')
            WITH src, r, tgt, src_flat_properties,
                CASE 
                    WHEN tgt_path IS NULL THEN []
                    ELSE [i IN range(0, size(trels)-1) | {{
                        ParentNode: nodes(tgt_path)[i],
                        Relationship: trels[i],
                        Property: nodes(tgt_path)[i+1]
                    }}]
                END AS tgt_flat_property

            WITH tgt, r, src, src_flat_properties,
                reduce(flat = [], l IN collect(tgt_flat_property) | flat + l) AS tgt_flat_properties

            RETURN {{
                StartNode: {{
                    Node: src,
                    ComplexProperties: src_flat_properties
                }},
                Relationship: r,
                EndNode: {{
                    Node: tgt,
                    ComplexProperties: tgt_flat_properties
                }}
            }} AS path_segment
        ");
    }

    public void AddOptionalMatch(string pattern)
    {
        _logger.LogDebug("AddOptionalMatch called with pattern: '{Pattern}'", pattern);
        _optionalMatchClauses.Add(pattern);
    }

    public void AddLimit(int limit)
    {
        _logger.LogDebug("AddLimit called with value: {Limit}", limit);
        _limit = limit;
    }

    public void AddSkip(int skip)
    {
        _logger.LogDebug("AddSkip called with value: {Skip}", skip);
        _skip = skip;
    }

    public void AddWith(string expression)
    {
        _logger.LogDebug("AddWith called with expression: '{Expression}'", expression);
        _withClauses.Add(expression);
    }

    public void AddUnwind(string expression)
    {
        _logger.LogDebug("AddUnwind called with expression: '{Expression}'", expression);
        _unwindClauses.Add(expression);
    }

    public void AddGroupBy(string expression)
    {
        _logger.LogDebug("AddGroupBy called with expression: '{Expression}'", expression);
        _groupByClauses.Add(expression);
    }

    public void SetDistinct(bool distinct)
    {
        _logger.LogDebug("SetDistinct called with value: {Value}", distinct);
        _isDistinct = distinct;
    }

    public bool HasReturnClause => _returnClauses.Any();

    public void AddReturn(string expression, string? alias = null)
    {
        if (!string.IsNullOrWhiteSpace(alias))
        {
            _returnClauses.Add($"{expression} AS {alias}");
        }
        else
        {
            _returnClauses.Add(expression);
        }
    }

    public void AddUserProjection(string expression, string? alias = null)
    {
        _hasUserProjections = true;
        AddReturn(expression, alias);
    }

    public void AddInfrastructureReturn(string expression, string? alias = null)
    {
        // This is for infrastructure returns like path segments - don't mark as user projection
        AddReturn(expression, alias);
    }

}