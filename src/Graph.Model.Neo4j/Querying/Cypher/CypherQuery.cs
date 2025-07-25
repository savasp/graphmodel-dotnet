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

namespace Cvoya.Graph.Model.Neo4j.Querying.Cypher;

/// <summary>
/// Represents a Cypher query with its parameters and optional transaction context.
/// </summary>
internal sealed record CypherQuery(
    string Text,
    IReadOnlyDictionary<string, object?> Parameters)
{
    /// <summary>
    /// Creates an empty query.
    /// </summary>
    public static CypherQuery Empty { get; } = new(string.Empty, new Dictionary<string, object?>());
}