// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Neo4j.Linq;

using System.Reflection;


/// <summary>
/// Helper methods for handling complex properties in queries.
/// </summary>
internal static class ComplexPropertyHelper
{
    /// <summary>
    /// Determines if a property is a complex property that needs special handling.
    /// </summary>
    public static bool IsComplexProperty(PropertyInfo property)
    {
        var propertyType = property.PropertyType;

        // Simple types are not complex
        if (IsSimpleType(propertyType))
            return false;

        // Collections of simple types are not complex
        if (IsCollectionOfSimpleTypes(propertyType))
            return false;

        // Everything else is complex (other entities, custom types, etc.)
        return true;
    }

    /// <summary>
    /// Checks if a type is considered "simple" for graph storage.
    /// </summary>
    public static bool IsSimpleType(Type type) => GraphDataModel.IsSimple(type);

    /// <summary>
    /// Checks if a type is a collection of simple types.
    /// </summary>
    public static bool IsCollectionOfSimpleTypes(Type type) => GraphDataModel.IsCollectionOfSimple(type);

    private static bool IsCollectionType(Type type, out Type elementType)
    {
        elementType = null!;

        // Array
        if (type.IsArray)
        {
            elementType = type.GetElementType()!;
            return true;
        }

        // Generic collection
        if (type.IsGenericType)
        {
            var genericDef = type.GetGenericTypeDefinition();
            if (genericDef == typeof(List<>) ||
                genericDef == typeof(IList<>) ||
                genericDef == typeof(ICollection<>) ||
                genericDef == typeof(IEnumerable<>) ||
                genericDef == typeof(HashSet<>) ||
                genericDef == typeof(ISet<>))
            {
                elementType = type.GetGenericArguments()[0];
                return true;
            }
        }

        return false;
    }
}