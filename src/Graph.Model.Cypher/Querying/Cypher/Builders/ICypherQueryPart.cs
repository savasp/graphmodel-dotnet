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
/// Represents a portion of a Cypher query that can contribute clauses and parameters.
/// </summary>
public interface ICypherQueryPart
{
    /// <summary>
    /// Appends this part's Cypher contribution to the provided builder.
    /// </summary>
    /// <param name="builder">The query text builder.</param>
    /// <param name="parameters">The parameters dictionary.</param>
    void AppendTo(StringBuilder builder, Dictionary<string, object?> parameters);

    /// <summary>
    /// Indicates whether this part has content to contribute.
    /// </summary>
    bool HasContent { get; }

    /// <summary>
    /// Specifies the ordering of the part relative to other parts (lower comes first).
    /// </summary>
    int Order { get; }
}
