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
/// Handles GROUP BY clause construction for Cypher queries.
/// Extracted from ReturnQueryPart to ensure proper ordering of GROUP BY before RETURN.
/// </summary>
internal class GroupByQueryPart : ICypherQueryPart
{
    private readonly List<string> _groupByClauses = [];

    public int Order => 5; // GROUP BY comes before RETURN (Order = 6)

    public bool HasContent => _groupByClauses.Count > 0;

    /// <summary>
    /// Adds a GROUP BY expression.
    /// </summary>
    public void AddGroupBy(string expression)
    {
        _groupByClauses.Add(expression);
    }

    /// <summary>
    /// Clears all GROUP BY clauses.
    /// </summary>
    public void ClearGroupBy()
    {
        _groupByClauses.Clear();
    }

    public void AppendTo(StringBuilder builder, Dictionary<string, object?> parameters)
    {
        if (_groupByClauses.Count > 0)
        {
            builder.Append("GROUP BY ");
            builder.AppendJoin(", ", _groupByClauses);
            builder.AppendLine();
        }
    }
}