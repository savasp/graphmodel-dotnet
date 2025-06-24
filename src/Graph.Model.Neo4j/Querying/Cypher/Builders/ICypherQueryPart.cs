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
/// Represents a part of a Cypher query that can contribute to the query building process.
/// This interface enables decomposition of the monolithic CypherQueryBuilder into focused parts.
/// </summary>
internal interface ICypherQueryPart
{
    /// <summary>
    /// Contributes this part's content to the query builder.
    /// </summary>
    /// <param name="builder">The target string builder</param>
    /// <param name="parameters">The parameters dictionary</param>
    void AppendTo(StringBuilder builder, Dictionary<string, object?> parameters);

    /// <summary>
    /// Indicates whether this part has any content to contribute.
    /// </summary>
    bool HasContent { get; }

    /// <summary>
    /// The priority order for this part in the query (lower values come first).
    /// </summary>
    int Order { get; }
}