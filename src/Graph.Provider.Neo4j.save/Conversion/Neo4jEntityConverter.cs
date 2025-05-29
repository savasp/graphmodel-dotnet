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
    /// Create a new node from a Neo4j entity
    /// </summary>
    /// <typeparam name="T">The type of the Node</typeparam>
    /// <param name="neo4jEntity">The Neo4j entity</param>
    /// <returns>A new instance of the Node type populated with values from the Neo4j entity</returns>
    public Task<T> DeserializeNode<T>(global::Neo4j.Driver.IEntity neo4jEntity)
        where T : class, Model.INode, new()
    {
        return CreateEntityWithSimpleProperties<T>(neo4jEntity);
    }

    /// <summary>
    /// Creates an object with values from a Neo4j entity
    /// </summary>
    /// <param name="type">The type of the property</param>
    /// <param name="neo4jEntity">The Neo4j entity containing the values</param>
    /// <returns>The created object</returns>
    public Task<object> DeserializeObjectFromNeo4jEntity(Type type, global::Neo4j.Driver.IEntity neo4jEntity)
    {
        // Populate the object with values from the Neo4j entity
        var obj = Activator.CreateInstance(type) ?? throw new InvalidOperationException($"Failed to create instance of type {type.FullName}");

        PopulateEntityFromNeo4j(obj, neo4jEntity);

        return Task.FromResult(obj);
    }

    /// <summary>
    /// Creates a relationship with values from a Neo4j relationship
    /// </summary>
    /// <typeparam name="T">The type of the relationship</typeparam>
    /// <returns>A new instance of the relationship type populated with values from the Neo4j relationship</returns>
    public Task<T> DeserializeRelationship<T>(global::Neo4j.Driver.IEntity neo4jEntity)
        where T : class, Model.IRelationship, new()
    {
        return CreateEntityWithSimpleProperties<T>(neo4jEntity);
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

        throw new NotSupportedException($"Cannot convert Neo4j value of type {value.GetType()} to {targetType}");
    }

    /// <summary>
    /// Creates a new entity from a Neo4j entity
    /// </summary>
    private Task<T> CreateEntityWithSimpleProperties<T>(global::Neo4j.Driver.IEntity neo4jEntity)
        where T : class, Model.IEntity, new()
    {
        var obj = new T();

        PopulateEntityFromNeo4j(obj, neo4jEntity);

        return Task.FromResult(obj);
    }

    private void PopulateEntityFromNeo4j(object obj, global::Neo4j.Driver.IEntity neo4jEntity)
    {
        var (simpleProperties, _) = Helpers.GetSimpleAndComplexProperties(obj);

        foreach (var property in simpleProperties)
        {
            // Get the property name (check if there is a PropertyAttribute)
            var propertyName = property.Key.GetCustomAttribute<PropertyAttribute>()?.Label ?? property.Key.Name;

            if (neo4jEntity.Properties.ContainsKey(propertyName))
            {
                var value = neo4jEntity.Properties[propertyName];

                try
                {
                    // Convert Neo4j value to .NET type
                    var convertedValue = ConvertFromNeo4jValue(value, property.Key.PropertyType);
                    property.Key.SetValue(obj, convertedValue);
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Failed to set property {PropertyName} on {EntityType}", property.Key.Name, obj.GetType().Name);
                }
            }
        }
    }
}