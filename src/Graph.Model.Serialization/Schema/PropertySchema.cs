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

namespace Cvoya.Graph.Model.Serialization;

/// <summary>
/// Schema information for a property within an entity or relationship.
/// </summary>
/// <param name="PropertyInfo">The .NET property information.</param>
/// <param name="Neo4jPropertyName">The property name as stored in Neo4j.</param>
/// <param name="PropertyType">The type of property (simple, complex, collection).</param>
/// <param name="ElementType">For collections, the type of elements.</param>
/// <param name="IsNullable">Whether the property can be null.</param>
/// <param name="NestedSchema">For complex properties, the schema of the nested type.</param>
public record PropertySchema(
    PropertyInfo PropertyInfo,
    string Neo4jPropertyName,
    PropertyType PropertyType,
    Type? ElementType = null,
    bool IsNullable = false,
    EntitySchema? NestedSchema = null
);

/// <summary>
/// Defines the types of properties in the schema.
/// </summary>
public enum PropertyType
{
    /// <summary>Simple value type or string that maps directly to Neo4j properties.</summary>
    Simple,

    /// <summary>Complex object that gets serialized as nested JSON.</summary>
    Complex,

    /// <summary>Collection of simple values.</summary>
    SimpleCollection,

    /// <summary>Collection of complex objects.</summary>
    ComplexCollection
}