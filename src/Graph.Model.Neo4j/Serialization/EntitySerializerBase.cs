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
    /// <param name="entity">The entity to deserialize</param>
    /// <returns>A .NET object representing the deserialized entity</returns>
    public abstract object Deserialize(Dictionary<string, IntermediateRepresentation> entity);

    /// <summary>
    /// Serializes a .NET object into a Neo4j entity representation
    /// </summary>
    /// <param name="entity">The .NET object to serialize</param>
    /// <returns>A dictionary representing the serialized entity</returns>
    public abstract Dictionary<string, IntermediateRepresentation> Serialize(object entity);

    /// <summary>
    /// Converts a .NET value to a Neo4j compatible value
    /// </summary>
    public static object? ConvertToNeo4jValue(object? value) => value switch
    {
        null => null,
        string s => s,
        bool b => b,
        byte b => (long)b,
        sbyte sb => (long)sb,
        short s => (long)s,
        ushort us => (long)us,
        int i => (long)i,
        uint ui => (long)ui,
        long l => l,
        ulong ul => (long)ul,
        float f => (double)f,
        double d => d,
        decimal dec => (double)dec,
        DateTime dt => new ZonedDateTime(dt),
        DateTimeOffset dto => new ZonedDateTime(dto),
        TimeSpan ts => new LocalTime(ts.Hours, ts.Minutes, ts.Seconds, ts.Milliseconds * 1_000_000),
        TimeOnly to => new LocalTime(to.Hour, to.Minute, to.Second, to.Nanosecond),
        DateOnly d => new LocalDate(d.Year, d.Month, d.Day),
        Guid g => g.ToString(),
        Uri uri => uri.ToString(),
        Enum e => e.ToString(),
        byte[] bytes => bytes,
        Model.Point p => new Point(p.X, p.Y, p.Z),
        IEnumerable enumerable => ConvertCollection(enumerable),
        _ => throw new NotSupportedException($"Type {value.GetType()} is not supported for Neo4j conversion")
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
        if (value == null)
            return null;

        return (targetType, value) switch
        {
            (Type t, _) when t == typeof(string) => value.ToString(),
            (Type t, _) when t == typeof(int) => Convert.ToInt32(value),
            (Type t, _) when t == typeof(long) => Convert.ToInt64(value),
            (Type t, _) when t == typeof(double) => Convert.ToDouble(value),
            (Type t, _) when t == typeof(float) => Convert.ToSingle(value),
            (Type t, _) when t == typeof(decimal) => ConvertToDecimal(value),
            (Type t, _) when t == typeof(bool) => Convert.ToBoolean(value),
            (Type t, _) when t == typeof(byte) => Convert.ToByte(value),
            (Type t, _) when t == typeof(sbyte) => Convert.ToSByte(value),
            (Type t, _) when t == typeof(short) => Convert.ToInt16(value),
            (Type t, _) when t == typeof(ushort) => Convert.ToUInt16(value),
            (Type t, _) when t == typeof(uint) => Convert.ToUInt32(value),
            (Type t, _) when t == typeof(ulong) => Convert.ToUInt64(value),
            (Type t, ZonedDateTime zdt) when t == typeof(DateTime) => zdt.ToDateTimeOffset().DateTime,
            (Type t, LocalDateTime ldt) when t == typeof(DateTime) => ldt.ToDateTime(),
            (Type t, LocalDate ld) when t == typeof(DateTime) => ld.ToDateTime(),
            (Type t, _) when t == typeof(DateTime) => Convert.ToDateTime(value),
            (Type t, ZonedDateTime zdt) when t == typeof(DateTimeOffset) => zdt.ToDateTimeOffset(),
            (Type t, LocalDateTime ldt) when t == typeof(DateTimeOffset) => new DateTimeOffset(ldt.ToDateTime()),
            (Type t, _) when t == typeof(DateTimeOffset) => new DateTimeOffset(Convert.ToDateTime(value)),
            (Type t, LocalTime lt) when t == typeof(TimeOnly) => TimeOnly.FromTimeSpan(lt.ToTimeSpan()),
            (Type t, LocalDate ld) when t == typeof(DateOnly) => DateOnly.FromDateTime(ld.ToDateTime()),
            (Type t, LocalTime lt) when t == typeof(TimeSpan) => lt.ToTimeSpan(),
            (Type t, LocalDate ld) when t == typeof(DateTime) => ConvertToDateOnly(ld),
            (Type t, LocalDateTime ldt) when t == typeof(DateOnly) => DateOnly.FromDateTime(ldt.ToDateTime()),
            (Type t, LocalTime lt) when t == typeof(DateTime) => lt.ToString(),
            (Type t, _) when t == typeof(Guid) => Guid.Parse(value.ToString()!),
            (Type t, string strValue) when t.IsEnum => Enum.Parse(targetType, strValue),
            (Type t, _) when t.IsEnum => Enum.ToObject(targetType, value),
            (Type t, global::Neo4j.Driver.Point point) when t == typeof(Model.Point) => new Model.Point(point.X, point.Y, point.Z),
            (Type t, IList neo4jList) when t.IsArray => ConvertToArray(neo4jList, t.GetElementType()!),
            (Type t, IList neo4jList) when t.IsGenericType && t.GetGenericTypeDefinition().IsAssignableTo(typeof(IEnumerable<>)) => ConvertToList(neo4jList, t),
            _ => throw new NotSupportedException($"Cannot convert Neo4j value of type {value.GetType()} to {targetType}")
        };
    }

    /// <summary>
    /// Converts a collection of Neo4j values to an array of objects
    /// </summary>
    public static object[] ConvertCollection(IEnumerable collection)
    {
        return collection.Cast<object>().Select(ConvertToNeo4jValue).Where(x => x is not null).Select(x => x!).ToArray();
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

    private static DateOnly ConvertToDateOnly(object value) => value switch
    {
        LocalDate ld => new DateOnly(ld.Year, ld.Month, ld.Day),
        LocalDateTime ldt => DateOnly.FromDateTime(ldt.ToDateTime()),
        ZonedDateTime zdt => DateOnly.FromDateTime(zdt.ToDateTimeOffset().DateTime),
        _ => DateOnly.Parse(value.ToString()!)
    };
}