// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph;

/// <summary>
/// Configures the graph relationship used to store a complex property.
/// </summary>
/// <remarks>
/// By default, a complex property is connected to its owning node by a relationship whose
/// type is the property name. Use this attribute when the graph relationship needs a different
/// semantic name.
/// </remarks>
[AttributeUsage(AttributeTargets.Property, Inherited = true, AllowMultiple = false)]
public sealed class ComplexPropertyAttribute : Attribute
{
    /// <summary>
    /// Gets or sets the relationship type used for the complex property.
    /// </summary>
    public string? RelationshipType { get; set; }
}
