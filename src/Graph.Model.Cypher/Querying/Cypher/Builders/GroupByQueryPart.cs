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

namespace Cvoya.Graph.Model.Cypher.Querying.Cypher.Builders;

using System.Text;

/// <summary>
/// Handles GROUP BY clause construction for Cypher queries.
/// Extracted from ReturnQueryPart to ensure proper ordering of GROUP BY before RETURN.
/// </summary>
public class GroupByQueryPart : ICypherQueryPart
{
    private readonly List<string> _groupByClauses = [];

    /// <summary>
    /// Gets the order in which this query part should appear in the final query.
    /// </summary>
    public int Order => 5; // GROUP BY comes before RETURN (Order = 6)

    /// <summary>
    /// Gets a value indicating whether this query part has any content to append.
    /// </summary>
    public bool HasContent => _groupByClauses.Count > 0;

    /// <summary>
    /// Adds a GROUP BY expression.
    /// </summary>
    /// <param name="expression">The expression to group by.</param>
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

    /// <summary>
    /// Appends the GROUP BY clause content to the query builder.
    /// </summary>
    /// <param name="builder">The string builder to append to.</param>
    /// <param name="parameters">The parameters dictionary for the query.</param>
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