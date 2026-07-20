// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph;

/// <summary>
/// Enhanced attribute to configure property behavior in the graph database.
/// </summary>
/// <remarks>
/// Use this attribute on properties of classes implementing IEntity to control 
/// how they are represented in the graph storage system, including indexing,
/// constraints, validation, and storage configuration.
/// Entity types may declare no domain key. When multiple properties declare
/// <see cref="IsKey"/>, they form one composite key tuple.
/// </remarks>
/// <example>
/// <code>
/// // Keyless domain entity. The inherited Id member is not a domain key.
/// [Node("Person")]
/// public sealed record Person : Node
/// {
///     public string Name { get; init; } = string.Empty;
///
///     [Property(Ignore = true)]
///     public string DisplayText =&gt; Name.ToUpperInvariant();
/// }
///
/// // Explicit composite domain key, unique within the Customer label.
/// [Node("Customer")]
/// public sealed record Customer : Node
/// {
///     [Property(IsKey = true)]
///     public string Tenant { get; init; } = string.Empty;
///
///     [Property(IsKey = true)]
///     public string CustomerNumber { get; init; } = string.Empty;
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
    public string? Label { get; set; }

    /// <summary>
    /// Gets or sets whether to ignore this property when serializing to the graph.
    /// </summary>
    /// <value>True if the property should be ignored, otherwise false.</value>
    public bool Ignore { get; set; }

    /// <summary>
    /// Gets or sets whether this property participates in the entity's domain key tuple.
    /// </summary>
    /// <value>True if the property is a key, otherwise false.</value>
    /// <remarks>
    /// A key is optional domain schema metadata, not provider-native graph element identity or an implicit
    /// mutation target. All key properties on an entity type form one ordered tuple that is unique within
    /// the mapped node label or relationship type. Key properties must be non-null, graph-storable scalar
    /// values and implicitly set <see cref="IsIndexed"/> and <see cref="IsRequired"/>. A component of a
    /// composite key is not independently unique unless <see cref="IsUnique"/> is also explicitly set.
    /// Key values are not implicitly immutable.
    /// </remarks>
    public bool IsKey { get; set; }

    /// <summary>
    /// Gets or sets whether this property should be indexed for query performance.
    /// </summary>
    /// <value>True if the property should be indexed, otherwise false.</value>
    public bool IsIndexed { get; set; }

    /// <summary>
    /// Gets or sets whether this property should have unique values across entities with the same label/type.
    /// </summary>
    /// <value>True if the property should have unique values, otherwise false.</value>
    /// <remarks>
    /// This represents an explicit per-property uniqueness declaration. It is not inferred from
    /// <see cref="IsKey"/>; a key tuple supplies its own uniqueness semantics.
    /// </remarks>
    public bool IsUnique { get; set; }

    /// <summary>
    /// Gets or sets whether this property is required (cannot be null).
    /// </summary>
    /// <value>True if the property is required, otherwise false.</value>
    public bool IsRequired { get; set; }

    /// <summary>
    /// Gets or sets whether this property should be included in full text search indexes.
    /// </summary>
    /// <value>True if the property should be included in full text search, false to exclude it. 
    /// If true (default), string properties are included by default.</value>
    public bool IncludeInFullTextSearch { get; set; } = true;

    /// <summary>
    /// Gets or sets the minimum length for string properties.
    /// </summary>
    /// <value>The minimum length for string properties, or null if not specified.</value>
    public int MinLength { get; init; } = int.MinValue;

    /// <summary>
    /// Gets or sets the maximum length for string properties.
    /// </summary>
    /// <value>The maximum length for string properties, or null if not specified.</value>
    public int MaxLength { get; init; } = int.MaxValue;

    /// <summary>
    /// Gets or sets a regular expression pattern for string validation.
    /// </summary>
    /// <value>The regular expression pattern for string validation, or null if not specified.</value>
    public string Pattern { get; init; } = string.Empty;
}
