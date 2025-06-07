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
    private int _parameterCounter;

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
    public void SetExistsQuery() => _isExistsQuery = true;

    public string AddParameter(object value)
    {
        var paramName = $"p{_parameterCounter++}";
        _parameters[paramName] = value;
        return $"${paramName}";
    }

    public CypherQueryResult Build()
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
        else if (_isExistsQuery)
        {
            query.Append(_matchClauses.Count > 0 ? "count(n) > 0" : "false");
        }
        else if (_returnClauses.Count > 0)
        {
            query.AppendJoin(", ", _returnClauses);
        }
        else
        {
            query.Append("n");
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
}