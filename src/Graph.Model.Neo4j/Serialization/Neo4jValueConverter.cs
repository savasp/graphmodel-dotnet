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

namespace Cvoya.Graph.Model.Neo4j.Serialization;

using Cvoya.Graph.Model.Serialization;

/// <summary>
/// Neo4j-specific implementation of IValueConverter that delegates to SerializationBridge.
/// Handles Neo4j's specific type conversions including temporal types, spatial types, and native Neo4j objects.
/// </summary>
internal sealed class Neo4jValueConverter : IValueConverter
{
    /// <summary>
    /// Converts a Property from EntityInfo to the target type T using Neo4j-specific type conversion logic.
    /// </summary>
    /// <typeparam name="T">Target type for conversion</typeparam>
    /// <param name="property">Property containing SimpleValue, SimpleCollection, EntityInfo, or EntityCollection</param>
    /// <returns>Converted value of type T, or null if conversion fails</returns>
    /// <exception cref="ArgumentNullException">Thrown when property is null</exception>
    /// <exception cref="NotSupportedException">Thrown when property.Value type is not supported</exception>
    public T? ConvertValue<T>(Property property)
    {
        ArgumentNullException.ThrowIfNull(property);

        return property.Value switch
        {
            SimpleValue simple => ConvertSimpleValue<T>(simple),
            SimpleCollection collection => ConvertSimpleCollection<T>(collection),
            EntityInfo entityInfo => ConvertEntityInfo<T>(entityInfo),
            EntityCollection entityCollection => ConvertEntityCollection<T>(entityCollection),
            null => default(T),
            _ => throw new NotSupportedException($"Unsupported property value type: {property.Value.GetType()}")
        };
    }

    private T? ConvertSimpleValue<T>(SimpleValue simpleValue)
    {
        try
        {
            // Delegate to SerializationBridge for Neo4j-specific type conversions
            var converted = SerializationBridge.FromNeo4jValue(simpleValue.Object, typeof(T));
            return (T?)converted;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to convert SimpleValue of type {simpleValue.Type.Name} to {typeof(T).Name}", ex);
        }
    }

    /// <summary>
    /// Converts a SimpleCollection to the target type T, handling arrays and various collection interfaces.
    /// </summary>
    private T? ConvertSimpleCollection<T>(SimpleCollection collection)
    {
        try
        {
            var targetType = typeof(T);
            
            // If T is not a collection type, try to convert the first element
            if (!CollectionTypeHelper.IsCollectionType(targetType))
            {
                return collection.Values.Count > 0 
                    ? ConvertSimpleValue<T>(collection.Values.First())
                    : default(T);
            }
            
            // Convert all elements using the collection's element type
            var elementType = CollectionTypeHelper.GetElementType(targetType);
            var convertedValues = collection.Values
                .Select(v => SerializationBridge.FromNeo4jValue(v.Object, elementType))
                .ToList();
            
            return targetType.IsArray 
                ? ConvertToArray<T>(convertedValues, elementType)
                : ConvertToList<T>(convertedValues, elementType);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to convert SimpleCollection to {typeof(T).Name}", ex);
        }
    }

    /// <summary>
    /// Converts a list of values to an array of the target type.
    /// </summary>
    private static T ConvertToArray<T>(List<object?> values, Type elementType)
    {
        var array = Array.CreateInstance(elementType, values.Count);
        for (int i = 0; i < values.Count; i++)
        {
            array.SetValue(values[i], i);
        }
        return (T)(object)array;
    }

    /// <summary>
    /// Converts a list of values to a List&lt;T&gt; and returns it as the target type.
    /// </summary>
    private static T ConvertToList<T>(List<object?> values, Type elementType)
    {
        var listType = typeof(List<>).MakeGenericType(elementType);
        var list = (System.Collections.IList)Activator.CreateInstance(listType)!;
        foreach (var value in values)
        {
            list.Add(value);
        }
        return (T)list;
    }

    /// <summary>
    /// Converts EntityInfo to the target type. EntityInfo typically requires higher-level processing.
    /// </summary>
    private T? ConvertEntityInfo<T>(EntityInfo entityInfo) => ConvertEntityType<T>(entityInfo, "EntityInfo");

    /// <summary>
    /// Converts EntityCollection to the target type. EntityCollection typically requires higher-level processing.
    /// </summary>
    private T? ConvertEntityCollection<T>(EntityCollection entityCollection) => ConvertEntityType<T>(entityCollection, "EntityCollection");

    /// <summary>
    /// Common conversion logic for EntityInfo and EntityCollection types.
    /// </summary>
    private static T? ConvertEntityType<T>(object entityObject, string typeName)
    {
        var targetType = typeof(T);
        
        // Return the entity object directly if types match
        if (targetType == entityObject.GetType() || targetType == typeof(object))
        {
            return (T)entityObject;
        }
        
        // For other types, we can't directly convert without more context
        throw new InvalidOperationException(
            $"Cannot directly convert {typeName} to {targetType.Name}. {typeName} requires materialization through EntityFactory.");
    }

}