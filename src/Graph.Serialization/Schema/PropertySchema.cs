// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Serialization;

using System.Reflection;


/// <summary>
/// Schema information for a property within an entity or relationship.
/// </summary>
/// <param name="PropertyInfo">The .NET property information.</param>
/// <param name="PropertyName">The property name as stored in the backing graph store.</param>
/// <param name="PropertyType">The type of property (simple, complex, collection).</param>
/// <param name="ElementType">For collections, the type of elements.</param>
/// <param name="IsNullable">Whether the property can be null.</param>
/// <param name="NestedSchema">For complex properties, the schema of the nested type.</param>
/// <param name="RelationshipType">For complex properties, the semantic graph relationship type.</param>
public record PropertySchema(
    PropertyInfo PropertyInfo,
    string PropertyName,
    PropertyType PropertyType,
    Type? ElementType = null,
    bool IsNullable = false,
    EntitySchema? NestedSchema = null,
    string? RelationshipType = null
)
{
    /// <summary>
    /// Gets whether collection elements may be null according to the declared property schema.
    /// </summary>
    /// <remarks>
    /// Generated schemas set this value from compiler nullability metadata. Hand-authored schemas
    /// default to reflection metadata so nullable reference annotations are not inferred from the
    /// runtime element type alone.
    /// </remarks>
    public bool IsElementNullable { get; init; } = GetElementNullability(PropertyInfo, ElementType);

    private static bool GetElementNullability(PropertyInfo propertyInfo, Type? elementType)
    {
        if (elementType is null)
        {
            return false;
        }

        if (Nullable.GetUnderlyingType(elementType) is not null)
        {
            return true;
        }

        if (elementType.IsValueType)
        {
            return false;
        }

        var propertyNullability = new NullabilityInfoContext().Create(propertyInfo);
        var elementNullability = propertyInfo.PropertyType.IsArray
            ? propertyNullability.ElementType
            : propertyNullability.GenericTypeArguments.FirstOrDefault();

        return elementNullability?.ReadState == NullabilityState.Nullable;
    }
}

/// <summary>
/// Defines the types of properties in the schema.
/// </summary>
public enum PropertyType
{
    /// <summary>Simple value type or string that maps directly to graph storage properties.</summary>
    Simple,

    /// <summary>Complex object that gets serialized as nested JSON.</summary>
    Complex,

    /// <summary>Collection of simple values.</summary>
    SimpleCollection,

    /// <summary>Collection of complex objects.</summary>
    ComplexCollection
}
