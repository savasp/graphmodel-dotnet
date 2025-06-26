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

using System.Text;


/// <summary>
/// Handles ORDER BY clause construction for Cypher queries.
/// Extracted from the monolithic CypherQueryBuilder to provide focused responsibility.
/// </summary>
internal class OrderByQueryPart : ICypherQueryPart
{
    private readonly List<(string Expression, bool IsDescending)> _orderByClauses = [];

    public int Order => 7; // ORDER BY comes near the end

    public bool HasContent => _orderByClauses.Count > 0;

    public bool HasOrderBy => _orderByClauses.Count > 0;

    /// <summary>
    /// Adds an ORDER BY expression.
    /// </summary>
    public void AddOrderBy(string expression, bool isDescending = false)
    {
        _orderByClauses.Add((expression, isDescending));
    }

    /// <summary>
    /// Reverses the order of all ORDER BY clauses (for descending sorts).
    /// </summary>
    public void ReverseOrderBy()
    {
        for (var i = 0; i < _orderByClauses.Count; i++)
        {
            var (expression, isDescending) = _orderByClauses[i];
            _orderByClauses[i] = (expression, !isDescending);
        }
    }

    public void AppendTo(StringBuilder builder, Dictionary<string, object?> parameters)
    {
        if (_orderByClauses.Count > 0)
        {
            builder.Append("ORDER BY ");
            var orderByParts = _orderByClauses.Select(o =>
                o.IsDescending ? $"{o.Expression} DESC" : o.Expression);
            builder.AppendJoin(", ", orderByParts);
            builder.AppendLine();
        }
    }
}