// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph;

/// <summary>
/// Schema information for an entity type.
/// </summary>
public class EntitySchemaInfo
{
    /// <summary>
    /// Gets the .NET type of the entity.
    /// </summary>
    public Type Type { get; set; } = null!;

    /// <summary>
    /// Gets the single label/type name used in the graph database. A node type maps to exactly one label,
    /// unique (case-insensitive) across all types loaded in the process; registry lookups
    /// (<see cref="SchemaRegistry.GetNodeSchema"/> and its siblings) are keyed on it.
    /// </summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>
    /// Gets the property schema information for this entity.
    /// </summary>
    public IDictionary<string, PropertySchemaInfo> Properties { get; set; } = new Dictionary<string, PropertySchemaInfo>();

    /// <summary>
    /// Gets the ordered domain-key tuple for this entity.
    /// </summary>
    /// <remarks>
    /// Zero key properties is valid. The tuple is ordered by mapped property name using ordinal comparison
    /// so every schema consumer observes the same order. Its uniqueness scope is this entity's mapped node
    /// label or relationship type within one configured graph store. The tuple does not represent graph
    /// element identity and is not an implicit mutation target.
    /// </remarks>
    /// <returns>The ordered key-property schema information, or an empty sequence for a keyless entity.</returns>
    public IEnumerable<PropertySchemaInfo> GetKeyProperties()
    {
        return Properties.Values.Where(p => p.IsKey).OrderBy(p => p.Name, StringComparer.Ordinal);
    }

    /// <summary>
    /// Gets whether this entity has a composite key (multiple key properties).
    /// </summary>
    /// <returns>True if the entity has multiple key properties, false otherwise.</returns>
    public bool HasCompositeKey()
    {
        return Properties.Values.Count(p => p.IsKey) > 1;
    }

    /// <summary>
    /// Gets whether this entity declares a domain key tuple.
    /// </summary>
    /// <returns>True if the entity has at least one key property, false otherwise.</returns>
    public bool HasKey()
    {
        return Properties.Values.Any(p => p.IsKey);
    }
}
