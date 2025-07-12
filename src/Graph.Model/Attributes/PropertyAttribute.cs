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

using Cvoya.Graph.Model;

/// <summary>
/// Enhanced attribute to configure property behavior in the graph database.
/// </summary>
/// <remarks>
/// Use this attribute on properties of classes implementing IEntity to control 
/// how they are represented in the graph storage system, including indexing,
/// constraints, validation, and storage configuration.
/// </remarks>
/// <example>
/// <code>
/// public class Person : INode
/// {
///     public string Id { get; set; } = Guid.NewGuid().ToString();
///     
///     [Property(Label = "email", IsUnique = true, IsIndexed = true, IsRequired = true)]
///     public string Email { get; set; } = string.Empty;
///     
///     [Property(IsIndexed = true, Validation = new PropertyValidation { MinLength = 2, MaxLength = 50 })]
///     public string FirstName { get; set; } = string.Empty;
///     
///     [Property(Ignore = true)]
///     public string TempCalculation { get; set; }
/// }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Property, Inherited = true, AllowMultiple = false)]
public class PropertyAttribute() : Attribute
{
    /// <summary>
    /// Gets or sets the label to use for the property in the graph. If null, the property name is used as-is.
    /// </summary>
    /// <value>The custom property name used for graph storage, or null to use the actual property name.</value>
    public string? Label { get; set; } = null;

    /// <summary>
    /// Gets or sets whether to ignore this property when serializing to the graph.
    /// </summary>
    /// <value>True if the property should be ignored, otherwise false.</value>
    public bool Ignore { get; set; } = false;

    /// <summary>
    /// Gets or sets whether this property should be indexed for query performance.
    /// </summary>
    /// <value>True if the property should be indexed, otherwise false.</value>
    public bool IsIndexed { get; set; } = false;

    /// <summary>
    /// Gets or sets whether this property should have unique values across entities with the same label/type.
    /// </summary>
    /// <value>True if the property should have unique values, otherwise false.</value>
    public bool IsUnique { get; set; } = false;

    /// <summary>
    /// Gets or sets whether this property is required (cannot be null).
    /// </summary>
    /// <value>True if the property is required, otherwise false.</value>
    public bool IsRequired { get; set; } = false;

    /// <summary>
    /// Gets or sets validation rules for this property.
    /// </summary>
    /// <value>The validation rules for this property.</value>
    public PropertyValidation? Validation { get; set; } = null;

    /// <summary>
    /// Gets or sets whether this property should be included in full text search indexes.
    /// </summary>
    /// <value>True if the property should be included in full text search, false to exclude it. 
    /// If null (default), string properties are included by default.</value>
    public bool? IncludeInFullTextSearch { get; set; } = null;
}

/// <summary>
/// Defines validation rules for properties.
/// </summary>
public class PropertyValidation
{
    /// <summary>
    /// Gets or sets the minimum length for string properties.
    /// </summary>
    /// <value>The minimum length for string properties, or null if not specified.</value>
    public int? MinLength { get; set; } = null;

    /// <summary>
    /// Gets or sets the maximum length for string properties.
    /// </summary>
    /// <value>The maximum length for string properties, or null if not specified.</value>
    public int? MaxLength { get; set; } = null;

    /// <summary>
    /// Gets or sets the minimum value for numeric properties.
    /// </summary>
    /// <value>The minimum value for numeric properties, or null if not specified.</value>
    public object? MinValue { get; set; } = null;

    /// <summary>
    /// Gets or sets the maximum value for numeric properties.
    /// </summary>
    /// <value>The maximum value for numeric properties, or null if not specified.</value>
    public object? MaxValue { get; set; } = null;

    /// <summary>
    /// Gets or sets a regular expression pattern for string validation.
    /// </summary>
    /// <value>The regular expression pattern for string validation, or null if not specified.</value>
    public string? Pattern { get; set; } = null;
}
