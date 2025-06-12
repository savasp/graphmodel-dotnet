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
/// Schema information for an entity type, mapping .NET types to Neo4j node structure.
/// </summary>
/// <param name="Type">The .NET type this schema represents.</param>
/// <param name="Label">The Neo4j label for this entity.</param>
/// <param name="HasComplexProperties">Indicates if this entity has properties that are complex objects.</param>
/// <param name="Properties">Property mapping information, keyed by Neo4j property name.</param>
public record EntitySchema(
    Type Type,
    string Label,
    IReadOnlyDictionary<string, PropertySchema> Properties,
    bool HasComplexProperties = false
);