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

internal class CypherQueryBuilder
{
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
    private string? _relationshipSourceAlias;
    private string? _relationshipTargetAlias;

    private string? _aggregation;
    private bool _isExistsQuery;
    private bool _isNotExistsQuery;
    private bool _includeComplexProperties;
    private string? _mainNodeAlias;
    private int _parameterCounter;

    public bool HasExplicitReturn => _returnClauses.Count > 0 || _aggregation != null || _isExistsQuery || _isNotExistsQuery;

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

    public void AddReturn(string expression, string? alias = null)
    {
        var returnClause = alias != null ? $"{expression} AS {alias}" : expression;
        _returnClauses.Add(returnClause);
    }

    public void AddOrderBy(string expression, bool isDescending = false)
    {
        _orderByClauses.Add((expression, isDescending));
    }

    public void SetSkip(int skip) => _skip = skip;
    public void SetLimit(int limit) => _limit = limit;
    public void SetAggregation(string function, string expression) => _aggregation = $"{function}({expression})";

    public string AddParameter(object value)
    {
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
        _matchClauses.Add($"(src)-[r:{relationshipType}]->(tgt)");
        _mainNodeAlias = "r"; // Set main alias to the relationship

        // Track that this is a relationship query
        _relationshipSourceAlias = "src";
        _relationshipTargetAlias = "tgt";
        _returnClauses.Add("src.Id AS StartNodeId");
        _returnClauses.Add("tgt.Id AS EndNodeId");
        _isRelationshipQuery = true;
    }

    public bool HasOrderBy => _orderByClauses.Any();

    public void ReverseOrderBy()
    {
        // Flip the IsDescending flag for all order clauses
        for (int i = 0; i < _orderByClauses.Count; i++)
        {
            var (expression, isDescending) = _orderByClauses[i];
            _orderByClauses[i] = (expression, !isDescending);
        }
    }

    private CypherQuery BuildSimpleQuery()
    {
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
        if (_whereClauses.Count > 0)
        {
            query.Append("WHERE ");
            query.AppendJoin(" AND ", _whereClauses);
            query.AppendLine();
        }
    }

    private void AppendReturnClause(StringBuilder query)
    {
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
        else if (_isRelationshipQuery && _relationshipSourceAlias != null && _relationshipTargetAlias != null)
        {
            // For relationship queries without explicit returns, return the full set
            query.Append($"{_relationshipSourceAlias}.Id as StartNodeId, {_mainNodeAlias}, {_relationshipTargetAlias}.Id as EndNodeId");
        }
        else
        {
            // Default return
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
        var query = new StringBuilder();

        AppendMatchClauses(query);
        AppendWhereClauses(query);

        // For EXISTS queries, we return a count > 0
        query.AppendLine($"RETURN COUNT({_mainNodeAlias ?? "n"}) > 0 AS exists");

        return new CypherQuery(query.ToString().Trim(), new Dictionary<string, object?>(_parameters));
    }

    private CypherQuery BuildNotExistsQuery()
    {
        var query = new StringBuilder();

        AppendMatchClauses(query);
        AppendWhereClauses(query);

        // For NOT EXISTS queries (used by All), return true if no matching nodes exist
        query.AppendLine($"RETURN COUNT({_mainNodeAlias ?? "n"}) = 0 AS all");

        return new CypherQuery(query.ToString().Trim(), new Dictionary<string, object?>(_parameters));
    }

    private CypherQuery BuildWithComplexProperties()
    {
        var query = new StringBuilder();

        // First part: get the main nodes
        AppendMatchClauses(query);
        AppendWhereClauses(query);

        // Collect main nodes with ordering and pagination
        query.AppendLine($"WITH {_mainNodeAlias}");
        AppendOrderByClauses(query);
        AppendPaginationClauses(query);

        // Second part: match complex properties
        AppendComplexPropertyMatches(query);

        return new CypherQuery(query.ToString().Trim(), new Dictionary<string, object?>(_parameters));
    }

    private void AppendComplexPropertyMatches(StringBuilder query)
    {
        // Match the complex properties
        query.AppendLine($"OPTIONAL MATCH path = ({_mainNodeAlias})-[*0..]-(target)");
        query.AppendLine($"WHERE ALL(r IN relationships(path) WHERE type(r) STARTS WITH '{GraphDataModel.PropertyRelationshipTypeNamePrefix}')");
        query.AppendLine($"WITH {_mainNodeAlias}, relationships(path) AS rels, nodes(path) AS nodes");
        query.AppendLine("WHERE size(nodes) = size(rels) + 1");
        query.AppendLine($"WITH {_mainNodeAlias},");
        query.AppendLine("     [i IN range(0, size(rels)-1) |");
        query.AppendLine("        {Node: nodes[i+1], RelType: type(rels[i]), RelationshipProperties: properties(rels[i])}");
        query.AppendLine("     ] AS relatedNodes");
        query.AppendLine($"RETURN {_mainNodeAlias}, relatedNodes");
    }

    public void AddOptionalMatch(string pattern)
    {
        _optionalMatchClauses.Add(pattern);
    }

    public void AddLimit(int limit)
    {
        _limit = limit;
    }

    public void AddSkip(int skip)
    {
        _skip = skip;
    }

    public void AddWith(string expression)
    {
        _withClauses.Add(expression);
    }

    public void AddUnwind(string expression)
    {
        _unwindClauses.Add(expression);
    }

    public void AddGroupBy(string expression)
    {
        _groupByClauses.Add(expression);
    }

    public void SetDistinct(bool distinct)
    {
        _isDistinct = distinct;
    }

    public bool HasReturnClause => _returnClauses.Any();
}