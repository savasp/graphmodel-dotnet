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

using System.Reflection;

/// <summary>
/// Schema information for a property.
/// </summary>
public class PropertySchemaInfo
{
    /// <summary>
    /// Gets the PropertyInfo for this property.
    /// </summary>
    public PropertyInfo PropertyInfo { get; set; } = null!;

    /// <summary>
    /// Gets the name used for this property in the graph database.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets whether this property should be indexed for query performance.
    /// </summary>
    public bool IsIndexed { get; set; } = false;

    /// <summary>
    /// Gets whether this property should be used as a key for the entity.
    /// </summary>
    public bool IsKey { get; set; } = false;

    /// <summary>
    /// Gets whether this property should have unique values across entities with the same label/type.
    /// </summary>
    public bool IsUnique { get; set; } = false;

    /// <summary>
    /// Gets whether this property is required (cannot be null).
    /// </summary>
    public bool IsRequired { get; set; } = false;

    /// <summary>
    /// Gets whether this property should be ignored when serializing to the graph.
    /// </summary>
    public bool Ignore { get; set; } = false;

    /// <summary>
    /// Gets whether this property should be included in full text search indexes.
    /// </summary>
    public bool IncludeInFullTextSearch { get; set; } = true;

    /// <summary>
    /// Gets validation rules for this property.
    /// </summary>
    public PropertyValidation Validation { get; set; }
}