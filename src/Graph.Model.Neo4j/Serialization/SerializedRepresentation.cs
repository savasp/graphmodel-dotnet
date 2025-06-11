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

using System.Reflection;

namespace Cvoya.Graph.Model.Neo4j.Serialization;

/// <summary>
/// Represents a base class for entities in the graph model.
/// </summary>
public abstract record Serialized;

/// <summary>
/// Represents a serialized entity.
/// </summary>
/// <param name="Type">The type of the entity.</param>
/// <param name="Label">The label for the entity.</param>
/// <param name="SimpleProperties">A dictionary of simple properties</param>
/// <param name="ComplexProperties">A dictionary of complex properties</param>
public record Entity(
    Type Type,
    string Label,
    IReadOnlyDictionary<string, PropertyRepresentation> SimpleProperties,
    IReadOnlyDictionary<string, PropertyRepresentation> ComplexProperties
) : Serialized;

/// <summary>
/// Represents a serialized simple value.
/// </summary>
/// <param name="Object">The object value.</param>
/// <param name="Type">The type of the value.</param>
public record SimpleValue(
    object Object,
    Type Type
) : Serialized;

/// <summary>
/// Represents a collection of entities.
/// </summary>
/// <param name="Entities">A collection of entities (<see cref="Entity"/> ).</param>
/// <param name="Type">The type of entities in the collection.</param>
public record EntityCollection(
    Type Type,
    IReadOnlyCollection<Entity> Entities
) : Serialized;

/// <summary>
/// Represents a collection of values.
/// </summary>
/// <param name="Values"></param>
/// <param name="ElementType">The type of elements in the collection.</param>
public record SimpleCollection(
    IReadOnlyCollection<SimpleValue> Values,
    Type ElementType
) : Serialized;

/// <summary>
/// Represents metadata about a property for serialization purposes
/// </summary>
/// <param name="PropertyInfo">The property information.</param>
/// <param name="Label">The label for the property.</param>
/// <param name="IsNullable">Indicates if the property is nullable.</param>
/// <param name="Value">The serialized value of the property, if available.</param>
public record PropertyRepresentation(
    PropertyInfo PropertyInfo,
    string Label,
    bool IsNullable = false,
    Serialized? Value = null);

