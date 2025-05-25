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

using System.Collections;
using System.Reflection;
using Cvoya.Graph.Model;
using Microsoft.Extensions.Logging;
using Neo4j.Driver;

namespace Cvoya.Graph.Provider.Neo4j.Conversion;

/// <summary>
/// Handles conversion between Neo4j values and .NET objects.
/// </summary>
internal class Neo4jEntityConverter
{
    private const int WGS84 = 4326;
    private readonly Microsoft.Extensions.Logging.ILogger? _logger;

    /// <summary>
    /// Initializes a new instance of the Neo4jEntityConverter class.
    /// </summary>
    /// <param name="logger">Optional logger for conversion errors</param>
    public Neo4jEntityConverter(Microsoft.Extensions.Logging.ILogger? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    /// Populates a node entity with values from a Neo4j node
    /// </summary>
    public void PopulateNodeEntity(object entity, global::Neo4j.Driver.INode neo4jNode)
    {
        var entityType = entity.GetType();
        var properties = entityType.GetProperties(BindingFlags.Public | BindingFlags.Instance);

        // Special handling for INode.Id - always use the Id property from neo4j node
        if (entity is Cvoya.Graph.Model.INode node && neo4jNode.Properties.ContainsKey("Id"))
        {
            node.Id = neo4jNode.Properties["Id"].As<string>();
        }

        foreach (var prop in properties)
        {
            // Skip navigation properties (collections of relationships)
            if (IsNavigationProperty(prop))
            {
                continue;
            }

            // Skip complex properties (other nodes)
            if (prop.PropertyType.IsAssignableTo(typeof(Cvoya.Graph.Model.INode)))
            {
                continue;
            }

            // Get the property name (considering PropertyAttribute)
            var propertyName = prop.GetCustomAttribute<PropertyAttribute>()?.Label ?? prop.Name;

            if (neo4jNode.Properties.ContainsKey(propertyName))
            {
                var value = neo4jNode.Properties[propertyName];

                try
                {
                    // Convert Neo4j value to .NET type
                    var convertedValue = ConvertFromNeo4jValue(value, prop.PropertyType);
                    prop.SetValue(entity, convertedValue);
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, $"Failed to set property {prop.Name} on {entityType.Name}");
                }
            }
        }
    }

    /// <summary>
    /// Populates a relationship entity with values from a Neo4j relationship
    /// </summary>
    public void PopulateRelationshipEntity(object entity, global::Neo4j.Driver.IRelationship neo4jRelationship)
    {
        var entityType = entity.GetType();
        var properties = entityType.GetProperties(BindingFlags.Public | BindingFlags.Instance);

        foreach (var prop in properties)
        {
            // Skip navigation properties (Source/Target)
            if (prop.Name is "Source" or "Target")
            {
                continue;
            }

            // Skip complex properties
            if (prop.PropertyType.IsAssignableTo(typeof(Cvoya.Graph.Model.INode)))
            {
                continue;
            }

            // Get the property name (considering PropertyAttribute)
            var propertyName = prop.GetCustomAttribute<PropertyAttribute>()?.Label ?? prop.Name;

            if (neo4jRelationship.Properties.ContainsKey(propertyName))
            {
                var value = neo4jRelationship.Properties[propertyName];

                try
                {
                    // Convert Neo4j value to .NET type
                    var convertedValue = ConvertFromNeo4jValue(value, prop.PropertyType);
                    prop.SetValue(entity, convertedValue);
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, $"Failed to set property {prop.Name} on {entityType.Name}");
                }
            }
        }
    }

    /// <summary>
    /// Converts a .NET value to a Neo4j compatible value
    /// </summary>
    public object? ConvertToNeo4jValue(object? value) => value switch
    {
        null => null,
        DateTime dt => dt,
        DateTimeOffset dto => dto,
        TimeSpan ts => ts,
        TimeOnly to => to.ToTimeSpan(),
        DateOnly d => d.ToDateTime(TimeOnly.MinValue),
        decimal d => (double)d, // Convert decimal to double for Neo4j storage
        float f => (double)f,   // Convert float to double for Neo4j storage
        Model.Point point => new global::Neo4j.Driver.Point(WGS84, point.X, point.Y, point.Z),
        IDictionary dict => dict.Cast<DictionaryEntry>()
                                .ToDictionary(
                                    entry => entry.Key.ToString() ?? "",
                                    entry => ConvertToNeo4jValue(entry.Value)),
        IEnumerable collection when value is not string =>
            collection.Cast<object?>().Select(ConvertToNeo4jValue).ToArray(),
        Enum e => e.ToString(),
        _ => value
    };

    /// <summary>
    /// Converts a Neo4j value to the corresponding .NET type
    /// </summary>
    public object? ConvertFromNeo4jValue(object? value, Type targetType)
    {
        if (value is null)
            return null;

        // Handle nullable types
        var underlyingType = Nullable.GetUnderlyingType(targetType);
        if (underlyingType != null)
        {
            targetType = underlyingType;
        }

        // Direct type match
        if (value.GetType() == targetType)
            return value;

        // String
        if (targetType == typeof(string))
            return value.ToString();

        // Numbers
        if (targetType == typeof(int))
            return Convert.ToInt32(value);
        if (targetType == typeof(long))
            return Convert.ToInt64(value);
        if (targetType == typeof(double))
            return Convert.ToDouble(value);
        if (targetType == typeof(float))
            return Convert.ToSingle(value);
        if (targetType == typeof(decimal))
            return Convert.ToDecimal(value);

        // Boolean
        if (targetType == typeof(bool))
            return Convert.ToBoolean(value);

        // DateTime
        if (targetType == typeof(DateTime))
        {
            if (value is ZonedDateTime zdt)
                return zdt.ToDateTimeOffset().DateTime;
            if (value is LocalDateTime ldt)
                return ldt.ToDateTime();
            if (value is LocalDate ld)
                return ld.ToDateTime();
            return Convert.ToDateTime(value);
        }

        // DateTimeOffset
        if (targetType == typeof(DateTimeOffset))
        {
            if (value is ZonedDateTime zdt)
                return zdt.ToDateTimeOffset();
            if (value is LocalDateTime ldt)
                return new DateTimeOffset(ldt.ToDateTime());
            return new DateTimeOffset(Convert.ToDateTime(value));
        }

        // TimeOnly
        if (targetType == typeof(TimeOnly) && value is LocalTime lt)
        {
            return TimeOnly.FromTimeSpan(lt.ToTimeSpan());
        }

        // DateOnly
        if (targetType == typeof(DateOnly) && value is LocalDate ld2)
        {
            return DateOnly.FromDateTime(ld2.ToDateTime());
        }

        // Guid
        if (targetType == typeof(Guid))
        {
            return Guid.Parse(value.ToString()!);
        }

        // Enums
        if (targetType.IsEnum)
        {
            if (value is string strValue)
                return Enum.Parse(targetType, strValue);
            return Enum.ToObject(targetType, value);
        }

        // Arrays and Lists
        if (targetType.IsArray)
        {
            var elementType = targetType.GetElementType();
            if (value is IList neo4jList && elementType is not null)
            {
                var array = Array.CreateInstance(elementType, neo4jList.Count);
                for (int i = 0; i < neo4jList.Count; i++)
                {
                    array.SetValue(ConvertFromNeo4jValue(neo4jList[i], elementType), i);
                }
                return array;
            }
        }

        if (targetType.IsGenericType && targetType.GetGenericTypeDefinition() == typeof(List<>))
        {
            var elementType = targetType.GetGenericArguments()[0];
            if (value is IList neo4jList)
            {
                var list = Activator.CreateInstance(targetType) as IList;
                foreach (var item in neo4jList)
                {
                    list?.Add(ConvertFromNeo4jValue(item, elementType));
                }
                return list;
            }
        }

        // Complex types (deserialize from JSON)
        if (value is string jsonString && IsComplexType(targetType))
        {
            try
            {
                return System.Text.Json.JsonSerializer.Deserialize(jsonString, targetType);
            }
            catch
            {
                _logger?.LogWarning($"Failed to deserialize JSON to {targetType.Name}: {jsonString}");
                return null;
            }
        }

        // Try type converter as last resort
        try
        {
            var converter = System.ComponentModel.TypeDescriptor.GetConverter(targetType);
            if (converter.CanConvertFrom(value.GetType()))
            {
                return converter.ConvertFrom(value);
            }
        }
        catch
        {
            // Ignore conversion errors
        }

        throw new NotSupportedException($"Cannot convert Neo4j value of type {value.GetType()} to {targetType}");
    }

    private bool IsNavigationProperty(PropertyInfo prop)
    {
        if (!prop.PropertyType.IsGenericType)
            return false;

        // Check for collection navigation properties (List<T> where T is IRelationship)
        if (prop.PropertyType.GetGenericTypeDefinition() is
            Type genericType && (
            genericType == typeof(List<>) ||
            genericType == typeof(IList<>) ||
            genericType == typeof(ICollection<>)))
        {
            var elementType = prop.PropertyType.GetGenericArguments()[0];
            return elementType.GetInterfaces().Any(i => i == typeof(Model.IRelationship));
        }

        return false;
    }

    private bool IsComplexType(Type type)
    {
        return !type.IsPrimitive &&
               !type.IsEnum &&
               type != typeof(string) &&
               type != typeof(DateTime) &&
               type != typeof(DateTimeOffset) &&
               type != typeof(TimeSpan) &&
               type != typeof(Guid) &&
               type != typeof(decimal) &&
               !type.IsArray &&
               !(type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>));
    }
}