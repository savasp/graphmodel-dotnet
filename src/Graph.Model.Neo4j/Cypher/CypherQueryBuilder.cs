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

namespace Cvoya.Graph.Model.Neo4j.Cypher;

using System.Text;

internal class CypherQueryBuilder
{
    private readonly List<string> _matchClauses = [];
    private readonly List<string> _whereClauses = [];
    private readonly List<string> _returnClauses = [];
    private readonly List<(string Expression, bool IsDescending)> _orderByClauses = [];
    private readonly Dictionary<string, object> _parameters = [];

    private int? _skip;
    private int? _limit;
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

    public void EnableComplexPropertyLoading()
    {
        _includeComplexProperties = true;
    }

    public void ClearMatches()
    {
        _matchClauses.Clear();
    }

    public void AddWhere(string condition)
    {
        _whereClauses.Add(condition);
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
        var paramName = $"p{_parameterCounter++}";
        _parameters[paramName] = value;
        return $"${paramName}";
    }

    public CypherQueryResult Build()
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

        // If we need complex properties, we need to modify the query structure
        if (_includeComplexProperties && !string.IsNullOrEmpty(_mainNodeAlias))
        {
            return BuildWithComplexProperties();
        }

        return BuildSimpleQuery();
    }

    public void AddRelationshipMatch(string relationshipType)
    {
        _matchClauses.Add($"()-[r:{relationshipType}]->()");
        _mainNodeAlias = "r"; // Set main alias to the relationship
    }

    private CypherQueryResult BuildExistsQuery()
    {
        var query = new StringBuilder();

        // MATCH clause
        if (_matchClauses.Count > 0)
        {
            query.Append("MATCH ");
            query.AppendJoin(", ", _matchClauses);
            query.AppendLine();
        }

        // WHERE clause
        if (_whereClauses.Count > 0)
        {
            query.Append("WHERE ");
            query.AppendJoin(" AND ", _whereClauses);
            query.AppendLine();
        }

        // For EXISTS queries, return true if any matching nodes exist
        query.AppendLine($"RETURN COUNT({_mainNodeAlias ?? "n"}) > 0 AS exists");

        return new CypherQueryResult(query.ToString().Trim(), new Dictionary<string, object>(_parameters));
    }

    private CypherQueryResult BuildNotExistsQuery()
    {
        var query = new StringBuilder();

        // MATCH clause
        if (_matchClauses.Count > 0)
        {
            query.Append("MATCH ");
            query.AppendJoin(", ", _matchClauses);
            query.AppendLine();
        }

        // WHERE clause - for ALL queries, this will contain the negated predicate
        if (_whereClauses.Count > 0)
        {
            query.Append("WHERE ");
            query.AppendJoin(" AND ", _whereClauses);
            query.AppendLine();
        }

        // For NOT EXISTS queries (used by All), return true if no matching nodes exist
        query.AppendLine($"RETURN COUNT({_mainNodeAlias ?? "n"}) = 0 AS all");

        return new CypherQueryResult(query.ToString().Trim(), new Dictionary<string, object>(_parameters));
    }

    private CypherQueryResult BuildSimpleQuery()
    {
        var query = new StringBuilder();

        // MATCH clause
        if (_matchClauses.Count > 0)
        {
            query.Append("MATCH ");
            query.AppendJoin(", ", _matchClauses);
            query.AppendLine();
        }

        // WHERE clause
        if (_whereClauses.Count > 0)
        {
            query.Append("WHERE ");
            query.AppendJoin(" AND ", _whereClauses);
            query.AppendLine();
        }

        // RETURN clause
        query.Append("RETURN ");
        if (_aggregation != null)
        {
            query.Append(_aggregation);
        }
        else if (_returnClauses.Count > 0)
        {
            query.AppendJoin(", ", _returnClauses);
        }
        else
        {
            query.Append(_mainNodeAlias ?? "n");
        }
        query.AppendLine();

        // ORDER BY clause
        if (_orderByClauses.Count > 0)
        {
            query.Append("ORDER BY ");
            query.AppendJoin(", ", _orderByClauses.Select(o =>
                o.IsDescending ? $"{o.Expression} DESC" : o.Expression));
            query.AppendLine();
        }

        // SKIP/LIMIT
        if (_skip.HasValue)
        {
            query.AppendLine($"SKIP {_skip.Value}");
        }

        if (_limit.HasValue)
        {
            query.AppendLine($"LIMIT {_limit.Value}");
        }

        return new CypherQueryResult(query.ToString().Trim(), new Dictionary<string, object>(_parameters));
    }

    private CypherQueryResult BuildWithComplexProperties()
    {
        var query = new StringBuilder();

        // First, build the main query part
        if (_matchClauses.Count > 0)
        {
            query.Append("MATCH ");
            query.AppendJoin(", ", _matchClauses);
            query.AppendLine();
        }

        if (_whereClauses.Count > 0)
        {
            query.Append("WHERE ");
            query.AppendJoin(" AND ", _whereClauses);
            query.AppendLine();
        }

        // For complex properties, we need to collect the main nodes first
        query.AppendLine($"WITH {_mainNodeAlias}");

        // Add ordering if needed
        if (_orderByClauses.Count > 0)
        {
            query.Append("ORDER BY ");
            query.AppendJoin(", ", _orderByClauses.Select(o =>
                o.IsDescending ? $"{o.Expression} DESC" : o.Expression));
            query.AppendLine();
        }

        // Apply pagination at the node level
        if (_skip.HasValue)
        {
            query.AppendLine($"SKIP {_skip.Value}");
        }

        if (_limit.HasValue)
        {
            query.AppendLine($"LIMIT {_limit.Value}");
        }

        // Now match the complex properties
        query.AppendLine($"OPTIONAL MATCH path = ({_mainNodeAlias})-[*0..]-(target)");
        query.AppendLine($"WHERE ALL(r IN relationships(path) WHERE type(r) STARTS WITH '{GraphDataModel.PropertyRelationshipTypeNamePrefix}')");
        query.AppendLine($"WITH {_mainNodeAlias}, relationships(path) AS rels, nodes(path) AS nodes");
        query.AppendLine("WHERE size(nodes) = size(rels) + 1");
        query.AppendLine($"WITH {_mainNodeAlias},");
        query.AppendLine("     [i IN range(0, size(rels)-1) |");
        query.AppendLine("        {Node: nodes[i+1], RelType: type(rels[i]), RelationshipProperties: properties(rels[i])}");
        query.AppendLine("     ] AS relatedNodes");
        query.AppendLine($"RETURN {_mainNodeAlias}, relatedNodes");

        return new CypherQueryResult(query.ToString().Trim(), new Dictionary<string, object>(_parameters));
    }
}