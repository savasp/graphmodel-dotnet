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

namespace Cvoya.Graph.Model.Serialization;

using System.Collections;

/// <summary>
/// Utility class for collection type analysis and manipulation.
/// Provides common methods for detecting and working with various collection types.
/// </summary>
public static class CollectionTypeHelper
{
    /// <summary>
    /// Gets the element type for collection types, or returns the type itself if not a collection.
    /// Supports arrays and common generic collection interfaces.
    /// </summary>
    /// <param name="type">The type to analyze</param>
    /// <returns>The element type for collections, or the original type if not a collection</returns>
    public static Type GetElementTypeOrSelf(Type type)
    {
        if (type.IsArray)
            return type.GetElementType()!;

        if (type.IsGenericType)
        {
            var genericTypeDefinition = type.GetGenericTypeDefinition();
            if (IsStandardCollectionInterface(genericTypeDefinition))
            {
                return type.GetGenericArguments()[0];
            }
        }

        return type;
    }

    /// <summary>
    /// Determines if the specified type is a collection type (array or standard collection interface).
    /// </summary>
    /// <param name="type">The type to check</param>
    /// <returns>True if the type is a collection, false otherwise</returns>
    public static bool IsCollectionType(Type type)
    {
        if (type.IsArray)
            return true;

        if (type.IsGenericType)
        {
            var genericTypeDefinition = type.GetGenericTypeDefinition();
            return IsStandardCollectionInterface(genericTypeDefinition);
        }

        return false;
    }

    /// <summary>
    /// Gets the element type for a collection type.
    /// </summary>
    /// <param name="collectionType">The collection type</param>
    /// <returns>The element type</returns>
    /// <exception cref="ArgumentException">Thrown when the type is not a supported collection type</exception>
    public static Type GetElementType(Type collectionType)
    {
        if (collectionType.IsArray)
            return collectionType.GetElementType()!;

        if (collectionType.IsGenericType && IsStandardCollectionInterface(collectionType.GetGenericTypeDefinition()))
            return collectionType.GetGenericArguments()[0];

        throw new ArgumentException($"Type {collectionType.Name} is not a supported collection type", nameof(collectionType));
    }

    /// <summary>
    /// Converts a list of objects to the specified collection type.
    /// </summary>
    /// <typeparam name="T">The target collection type</typeparam>
    /// <param name="items">The items to convert</param>
    /// <param name="elementType">The element type of the collection</param>
    /// <returns>A collection of type T containing the items</returns>
    public static T ConvertToCollectionType<T>(List<object> items, Type elementType)
    {
        var targetType = typeof(T);

        if (targetType.IsArray)
        {
            var array = Array.CreateInstance(elementType, items.Count);
            for (int i = 0; i < items.Count; i++)
            {
                array.SetValue(items[i], i);
            }
            return (T)(object)array;
        }

        if (targetType.IsGenericType && IsStandardCollectionInterface(targetType.GetGenericTypeDefinition()))
        {
            var listType = typeof(List<>).MakeGenericType(elementType);
            var list = (System.Collections.IList)Activator.CreateInstance(listType)!;
            foreach (var item in items)
            {
                list.Add(item);
            }
            return (T)list;
        }

        throw new ArgumentException($"Unsupported collection type: {targetType.Name}");
    }

    /// <summary>
    /// Checks if the generic type definition represents a standard collection interface.
    /// </summary>
    /// <param name="genericTypeDefinition">The generic type definition to check</param>
    /// <returns>True if it's a standard collection interface, false otherwise</returns>
    private static bool IsStandardCollectionInterface(Type genericTypeDefinition) =>
        genericTypeDefinition == typeof(List<>) ||
        genericTypeDefinition == typeof(IList<>) ||
        genericTypeDefinition == typeof(ICollection<>) ||
        genericTypeDefinition == typeof(IEnumerable<>);
}