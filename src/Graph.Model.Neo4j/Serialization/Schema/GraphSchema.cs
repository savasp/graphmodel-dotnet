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

namespace Cvoya.Graph.Model.Neo4j.Serialization;

/// <summary>
/// Represents the complete schema information needed to reconstruct object graphs from Neo4j query results.
/// </summary>
/// <param name="EntitySchemas">Schema information for all entity types, keyed by Neo4j label.</param>
/// <param name="RelationshipSchemas">Schema information for all relationship types, keyed by Neo4j relationship type.</param>
public record GraphSchema(
    IReadOnlyDictionary<string, EntitySchema> EntitySchemas,
    IReadOnlyDictionary<string, RelationshipSchema> RelationshipSchemas
);