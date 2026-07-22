// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph;

using System.Reflection;

/// <summary>Validates provider-neutral constraints declared on graph properties.</summary>
internal static class PropertyConstraintValidation
{
    /// <summary>
    /// Rejects constraints whose logical values do not have portable graph equality semantics.
    /// </summary>
    internal static void Validate(PropertyInfo property)
    {
        ArgumentNullException.ThrowIfNull(property);

        var attribute = property.GetCustomAttribute<PropertyAttribute>(inherit: true);
        if (attribute is null ||
            (!attribute.IsKey && !attribute.IsUnique) ||
            !GraphDataModel.IsCollectionOfSimple(property.PropertyType) ||
            GraphDataModel.IsSimple(property.PropertyType))
        {
            return;
        }

        var constraints = new List<string>(capacity: 2);
        if (attribute.IsKey)
        {
            constraints.Add(nameof(PropertyAttribute.IsKey));
        }

        if (attribute.IsUnique)
        {
            constraints.Add(nameof(PropertyAttribute.IsUnique));
        }

        throw new InvalidPropertyConstraintException(
            $"Property '{property.DeclaringType?.FullName}.{property.Name}' cannot declare " +
            $"{string.Join(" and ", constraints)} because simple collections cannot be key or unique values.");
    }
}

/// <summary>Identifies a deterministic provider-neutral property constraint failure.</summary>
internal sealed class InvalidPropertyConstraintException(string message) : GraphException(message);
