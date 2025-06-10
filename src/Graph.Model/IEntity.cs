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

namespace Cvoya.Graph.Model;

/// <summary>
/// Represents the foundation for all entities in the graph model.
/// This is the base interface for both nodes and relationships, providing the core identity functionality.
/// </summary>
public interface IEntity
{
    /// <summary>
    /// Gets the unique identifier of the entity.
    /// This property is used to uniquely identify and reference the entity within the graph system.
    /// </summary>
    /// <remarks>
    /// Identifiers should be immutable once the entity has been persisted to ensure referential integrity.
    /// </remarks>
    string Id { get; init; }
}
