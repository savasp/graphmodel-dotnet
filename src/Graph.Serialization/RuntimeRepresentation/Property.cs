// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Serialization;

using System.Reflection;


/// <summary>
/// Represents metadata about a property for serialization purposes
/// </summary>
/// <param name="PropertyInfo">The <see cref="PropertyInfo"/> for the property.</param>
/// <param name="Label">The label for the property.</param>
/// <param name="IsNullable">Indicates if the property is nullable.</param>
/// <param name="Value">The serialized value of the property, if available.</param>
/// <param name="RelationshipType">The semantic relationship type for a complex property, if applicable.</param>
public record Property(
    PropertyInfo PropertyInfo,
    string Label,
    bool IsNullable = false,
    Serialized? Value = null,
    string? RelationshipType = null);
