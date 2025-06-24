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
/// Handles LIMIT and SKIP clause construction for Cypher queries.
/// Extracted from the monolithic CypherQueryBuilder to provide focused responsibility.
/// </summary>
internal class PaginationQueryPart : ICypherQueryPart
{
    private int? _limit;
    private int? _skip;

    public int Order => 8; // LIMIT/SKIP comes at the end

    public bool HasContent => _limit.HasValue || _skip.HasValue;

    /// <summary>
    /// Sets the LIMIT value.
    /// </summary>
    public void SetLimit(int limit)
    {
        _limit = limit;
    }

    /// <summary>
    /// Sets the SKIP value.
    /// </summary>
    public void SetSkip(int skip)
    {
        _skip = skip;
    }

    /// <summary>
    /// Convenience method for AddLimit.
    /// </summary>
    public void AddLimit(int limit)
    {
        SetLimit(limit);
    }

    /// <summary>
    /// Convenience method for AddSkip.
    /// </summary>
    public void AddSkip(int skip)
    {
        SetSkip(skip);
    }

    public void AppendTo(StringBuilder builder, Dictionary<string, object?> parameters)
    {
        if (_skip.HasValue)
        {
            builder.AppendLine($"SKIP {_skip.Value}");
        }

        if (_limit.HasValue)
        {
            builder.AppendLine($"LIMIT {_limit.Value}");
        }
    }
}