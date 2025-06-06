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
/// Attribute to customize aspects of entity properties in the graph.
/// </summary>
/// <param name="label">Optional custom label for the property in the graph.</param>
/// <remarks>
/// Use this attribute on properties of classes implementing IEntity to control 
/// how they are represented in the graph storage system.
/// </remarks>
/// <example>
/// <code>
/// public class Person : INode
/// {
///     public string Id { get; set; } = Guid.NewGuid().ToString();
///     
///     [Property("full_name")]
///     public string FullName { get; set; } = string.Empty;
///     
///     [Property(Ignore = true)]
///     public string TempCalculation { get; set; }
/// }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Property, Inherited = false, AllowMultiple = false)]
public class PropertyAttribute(string? label = null) : Attribute
{
    /// <summary>
    /// Gets the label to use for the property in the graph.
    /// If null, the property name is used as-is.
    /// </summary>
    /// <value>The custom property name used for graph storage, or null to use the actual property name.</value>
    public string? Label { get; } = label;

    /// <summary>
    /// Gets or sets whether to ignore this property when serializing to the graph.
    /// </summary>
    /// <value>True if the property should be ignored, otherwise false.</value>
    public bool Ignore { get; set; }

    /// <summary>
    /// Gets or sets whether this property should be indexed in the graph database.
    /// </summary>
    /// <value>True if the property should be indexed, otherwise false.</value>
    public bool Index { get; set; }
}
