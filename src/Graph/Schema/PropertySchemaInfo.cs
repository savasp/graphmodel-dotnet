// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph;

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
    public bool IsIndexed { get; set; }

    /// <summary>
    /// Gets whether this property should be used as a key for the entity.
    /// </summary>
    public bool IsKey { get; set; }

    /// <summary>
    /// Gets whether this property should have unique values across entities with the same label/type.
    /// </summary>
    public bool IsUnique { get; set; }

    /// <summary>
    /// Gets whether this property is required (cannot be null).
    /// </summary>
    public bool IsRequired { get; set; }

    /// <summary>
    /// Gets whether this property should be ignored when serializing to the graph.
    /// </summary>
    public bool Ignore { get; set; }

    /// <summary>
    /// Gets whether this property should be included in full text search indexes.
    /// </summary>
    public bool IncludeInFullTextSearch { get; set; } = true;

    /// <summary>
    /// Gets validation rules for this property.
    /// </summary>
    public PropertyValidation Validation { get; set; }
}