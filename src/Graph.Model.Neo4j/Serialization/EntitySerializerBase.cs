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
using Neo4j.Driver;

namespace Cvoya.Graph.Model.Neo4j.Serialization;

/// <summary>
/// Base class for generated entity serializers with shared conversion logic
/// </summary>
public abstract class EntitySerializerBase
{
    /// <summary>
    /// Gets the type of the entity this serializer handles
    /// </summary>
    public abstract Type EntityType { get; }

    /// <summary>
    /// Deserializes a Neo4j entity into a .NET object
    /// </summary>
    /// <param name="entity">The Neo4j entity to deserialize</param>
    /// <returns>A task that represents the asynchronous operation, containing the deserialized .NET object</returns>
    public abstract object Deserialize(global::Neo4j.Driver.IEntity entity);

    /// <summary>
    /// Serializes a .NET object into a Neo4j entity representation
    /// </summary>
    /// <param name="entity">The .NET object to serialize</param>
    /// <returns>A dictionary representing the serialized entity</returns>
    public abstract Dictionary<string, object?> Serialize(object entity);

    /// <summary>
    /// Converts a .NET value to a Neo4j compatible value
    /// </summary>
    protected static object ConvertToNeo4jValue(object? value) => value switch
    {
        null => throw new ArgumentNullException(nameof(value), "Value cannot be null"),
        DateTime dt => dt,
        DateTimeOffset dto => dto,
        TimeSpan ts => ts,
        TimeOnly to => to.ToTimeSpan(),
        DateOnly d => d.ToDateTime(TimeOnly.MinValue),
        decimal d => (double)d,
        float f => (double)f,
        Model.Point point => new global::Neo4j.Driver.Point(4326, point.X, point.Y, point.Z),
        IDictionary dict => ConvertDictionary(dict),
        IEnumerable collection when value is not string => ConvertCollection(collection),
        Enum e => e.ToString(),
        _ => value
    };

    /// <summary>
    /// Converts a Neo4j value to the specified .NET type
    /// </summary>
    public static object? ConvertFromNeo4jValue(object? value, Type targetType)
    {
        if (value is null)
        {
            throw new ArgumentNullException(nameof(value), "Value cannot be null");
        }

        var underlyingType = Nullable.GetUnderlyingType(targetType);
        if (underlyingType != null)
        {
            targetType = underlyingType;
        }

        if (value.GetType() == targetType)
            return value;

        return (targetType, value) switch
        {
            (Type t, _) when t == typeof(string) => value.ToString(),
            (Type t, _) when t == typeof(int) => Convert.ToInt32(value),
            (Type t, _) when t == typeof(long) => Convert.ToInt64(value),
            (Type t, _) when t == typeof(double) => Convert.ToDouble(value),
            (Type t, _) when t == typeof(float) => Convert.ToSingle(value),
            (Type t, _) when t == typeof(decimal) => ConvertToDecimal(value),
            (Type t, _) when t == typeof(bool) => Convert.ToBoolean(value),
            (Type t, ZonedDateTime zdt) when t == typeof(DateTime) => zdt.ToDateTimeOffset().DateTime,
            (Type t, LocalDateTime ldt) when t == typeof(DateTime) => ldt.ToDateTime(),
            (Type t, LocalDate ld) when t == typeof(DateTime) => ld.ToDateTime(),
            (Type t, _) when t == typeof(DateTime) => Convert.ToDateTime(value),
            (Type t, ZonedDateTime zdt) when t == typeof(DateTimeOffset) => zdt.ToDateTimeOffset(),
            (Type t, LocalDateTime ldt) when t == typeof(DateTimeOffset) => new DateTimeOffset(ldt.ToDateTime()),
            (Type t, _) when t == typeof(DateTimeOffset) => new DateTimeOffset(Convert.ToDateTime(value)),
            (Type t, LocalTime lt) when t == typeof(TimeOnly) => TimeOnly.FromTimeSpan(lt.ToTimeSpan()),
            (Type t, LocalDate ld) when t == typeof(DateOnly) => DateOnly.FromDateTime(ld.ToDateTime()),
            (Type t, _) when t == typeof(Guid) => Guid.Parse(value.ToString()!),
            (Type t, string strValue) when t.IsEnum => Enum.Parse(targetType, strValue),
            (Type t, _) when t.IsEnum => Enum.ToObject(targetType, value),
            (Type t, global::Neo4j.Driver.Point point) when t == typeof(Model.Point) => new Model.Point(point.X, point.Y, point.Z),
            (Type t, IList neo4jList) when t.IsArray => ConvertToArray(neo4jList, t.GetElementType()!),
            (Type t, IList neo4jList) when t.IsGenericType && t.GetGenericTypeDefinition() == typeof(List<>) => ConvertToList(neo4jList, t),
            _ => throw new NotSupportedException($"Cannot convert Neo4j value of type {value.GetType()} to {targetType}")
        };
    }

    /// <summary>
    /// Safely gets a property value from the Neo4j entity
    /// </summary>
    protected static bool TryGetProperty(global::Neo4j.Driver.IEntity entity, string propertyName, out object? value)
    {
        return entity.Properties.TryGetValue(propertyName, out value);
    }

    private static object ConvertToDecimal(object value) => value switch
    {
        double d => (decimal)d,
        float f => (decimal)f,
        int i => (decimal)i,
        long l => (decimal)l,
        string s when decimal.TryParse(s, out var parsed) => parsed,
        _ => Convert.ToDecimal(value)
    };

    private static IDictionary ConvertDictionary(IDictionary dict)
    {
        var result = new Dictionary<string, object>();
        foreach (DictionaryEntry entry in dict)
        {
            result[entry.Key.ToString() ?? ""] = ConvertToNeo4jValue(entry.Value);
        }
        return result;
    }

    private static object[] ConvertCollection(IEnumerable collection)
    {
        return collection.Cast<object>().Select(ConvertToNeo4jValue).ToArray();
    }

    private static Array ConvertToArray(IList neo4jList, Type elementType)
    {
        var array = Array.CreateInstance(elementType, neo4jList.Count);
        for (int i = 0; i < neo4jList.Count; i++)
        {
            array.SetValue(ConvertFromNeo4jValue(neo4jList[i], elementType), i);
        }
        return array;
    }

    private static object ConvertToList(IList neo4jList, Type listType)
    {
        var elementType = listType.GetGenericArguments()[0];
        var list = Activator.CreateInstance(listType) as IList
            ?? throw new InvalidOperationException($"Failed to create instance of {listType}");

        foreach (var item in neo4jList)
        {
            list.Add(ConvertFromNeo4jValue(item, elementType));
        }

        return list;
    }
}