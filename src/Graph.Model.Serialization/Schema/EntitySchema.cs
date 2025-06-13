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
/// Schema information for an entity type, mapping .NET types to Neo4j node structure.
/// </summary>
/// <param name="ExpectedType">The .NET <see cref="Type"/> expected for the entity. Entities might have an actual type
/// that is different from the expected type, as long as it is a derived type of the expected one.</param>
/// <param name="Label">The label for this entity.</param>
/// <param name="IsNullable">Indicates if this entity can be null.</param>
/// <param name="IsSimple">Indicates if this entity is considered simple.
/// The predicate <see cref="GraphDataModel.IsSimple(System.Type)"/> determines if a property is considered simple or complex.</param>
/// <param name="SimpleProperties">A dictionary of simple properties, where the key is the property name and the value is a <see cref="PropertySchema"/>.</param>
/// <param name="ComplexProperties">A dictionary of complex properties, where the key is the property name and the value is a <see cref="PropertySchema"/>.</param>
public record EntitySchema(
    Type ExpectedType,
    string Label,
    bool IsNullable,
    bool IsSimple,
    IReadOnlyDictionary<string, PropertySchema> SimpleProperties,
    IReadOnlyDictionary<string, PropertySchema> ComplexProperties
);