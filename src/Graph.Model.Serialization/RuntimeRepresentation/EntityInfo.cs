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

namespace Cvoya.Graph.Model.Serialization;

/// <summary>
/// Represents a serialized Graph Model entity: <see cref="INode"/> or <see cref="IRelationship"/>.
/// </summary>
/// <param name="ActualType">The actual type of the entity. This might be different from what the schema
/// expects. If it is different, then it has to be a derived type.</param>
/// <param name="Label">The label for the entity. For strongly-typed entities, this is the primary label.
/// For dynamic entities, this may be the first label or a derived label.</param>
/// <param name="ActualLabels">The actual labels from Neo4j. For nodes, this contains all labels.
/// For relationships, this is null since relationships have a single type.</param>
/// <param name="SimpleProperties">An <see cref="IDictionary{String, Property}"/> of simple properties.
/// <see cref="GraphDataModel.IsSimple(Type)"/> determines if a property is considered simple.</param>
/// <param name="ComplexProperties">A dictionary of complex properties.
/// <see cref="GraphDataModel.IsSimple(Type)"/> determines if a property is considered complex.</param>
public record EntityInfo(
    Type ActualType,
    string Label,
    IReadOnlyList<string> ActualLabels,
    IDictionary<string, Property> SimpleProperties,
    IDictionary<string, Property> ComplexProperties
) : Serialized;
