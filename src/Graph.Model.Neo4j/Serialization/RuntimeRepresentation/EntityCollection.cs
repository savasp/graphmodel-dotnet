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
/// Represents a collection of entities.
/// </summary>
/// <param name="Entities">A collection of entities (<see cref="Entity"/> ).</param>
/// <param name="Type">The type of entities in the collection.</param>
public record EntityCollection(
    Type Type,
    IReadOnlyCollection<Entity> Entities
) : Serialized;

