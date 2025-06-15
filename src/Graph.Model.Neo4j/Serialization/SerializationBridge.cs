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

using System.Collections;
using System.Globalization;
using global::Neo4j.Driver;


/// <summary>
/// Provides methods for converting between Neo4j values and .NET types.
/// </summary>
internal static class SerializationBridge
{
    internal const string MetadataPropertyName = "__metadata__";
    private const string TypeNameKey = "type";

    /// <summary>
    /// Converts a .NET value to a Neo4j-compatible value.
    /// </summary>
    public static object? ToNeo4jValue(object? value)
    {
        return value switch
        {
            null => null,

            // Neo4j native types - pass through
            INode or IRelationship or IPath => value,

            // Primitives and strings
            string or bool or char => value,
            sbyte or byte or short or ushort or int or uint or long or ulong => value,
            float or double or decimal => value,

            // Convert enums to their string representation
            Enum enumValue => enumValue.ToString(),

            // Graph.Model types
            Point point => new global::Neo4j.Driver.Point(
                srId: 4326, // WGS84 for lat/lon
                x: point.X,
                y: point.Y,
                z: point.Z
            ),

            // Temporal types
            DateTime dt => dt.ToUniversalTime(),
            DateTimeOffset dto => dto.UtcDateTime,
            DateOnly date => date.ToDateTime(TimeOnly.MinValue),
            TimeOnly time => time.ToTimeSpan().TotalMilliseconds,
            TimeSpan ts => ts.TotalMilliseconds,

            // Convert Guid to string
            Guid guid => guid.ToString(),

            // Convert Uri to string
            Uri uri => uri.ToString(),

            // Byte arrays stay as-is (Neo4j supports them)
            byte[] => value,

            // Handle collections
            IDictionary dict => ConvertDictionary(dict),
            IEnumerable enumerable => ConvertCollection(enumerable),

            // For any other type, convert to string
            _ => value.ToString()
        };
    }

    /// <summary>
    /// Converts a Neo4j value to a .NET type.
    /// </summary>
    public static object? FromNeo4jValue(object? value, Type targetType)
    {
        if (value == null)
            return targetType.IsValueType && Nullable.GetUnderlyingType(targetType) == null
                ? Activator.CreateInstance(targetType)
                : null;

        // Handle nullable types
        var underlyingType = Nullable.GetUnderlyingType(targetType) ?? targetType;

        // Direct assignment if types match
        if (underlyingType.IsAssignableFrom(value.GetType()))
            return value;

        // Handle Neo4j types
        if (value is INode node && typeof(INode).IsAssignableFrom(underlyingType))
            return node;

        if (value is IRelationship rel && typeof(IRelationship).IsAssignableFrom(underlyingType))
            return rel;

        // Handle conversions
        return underlyingType switch
        {
            _ when underlyingType == typeof(string) => value.ToString(),
            _ when underlyingType == typeof(char) => Convert.ToChar(value),
            _ when underlyingType.IsEnum => ConvertToEnum(value, underlyingType),

            // Graph.Model types
            _ when underlyingType == typeof(Point) => ConvertToPoint(value),

            // Numeric conversions
            _ when underlyingType == typeof(sbyte) => Convert.ToSByte(value),
            _ when underlyingType == typeof(byte) => Convert.ToByte(value),
            _ when underlyingType == typeof(short) => Convert.ToInt16(value),
            _ when underlyingType == typeof(ushort) => Convert.ToUInt16(value),
            _ when underlyingType == typeof(int) => Convert.ToInt32(value),
            _ when underlyingType == typeof(uint) => Convert.ToUInt32(value),
            _ when underlyingType == typeof(long) => Convert.ToInt64(value),
            _ when underlyingType == typeof(ulong) => Convert.ToUInt64(value),
            _ when underlyingType == typeof(float) => Convert.ToSingle(value),
            _ when underlyingType == typeof(double) => Convert.ToDouble(value),
            _ when underlyingType == typeof(decimal) => Convert.ToDecimal(value),

            // Temporal conversions
            _ when underlyingType == typeof(DateTime) => ConvertToDateTime(value),
            _ when underlyingType == typeof(DateTimeOffset) => ConvertToDateTimeOffset(value),
            _ when underlyingType == typeof(DateOnly) => DateOnly.FromDateTime(ConvertToDateTime(value)),
            _ when underlyingType == typeof(TimeOnly) => TimeOnly.FromTimeSpan(ConvertToTimeSpan(value)),
            _ when underlyingType == typeof(TimeSpan) => ConvertToTimeSpan(value),

            // Other conversions
            _ when underlyingType == typeof(Guid) => Guid.Parse(value.ToString()!),
            _ when underlyingType == typeof(Uri) => new Uri(value.ToString()!),
            _ when underlyingType == typeof(byte[]) && value is byte[] => value,

            // Collections
            _ when IsDictionaryType(underlyingType) => ConvertToDictionary(value, underlyingType),
            _ when IsCollectionType(underlyingType) => ConvertToCollection(value, underlyingType),

            // Fallback
            _ => Convert.ChangeType(value, underlyingType, CultureInfo.InvariantCulture)
        };
    }

    private static object ConvertToEnum(object value, Type enumType)
    {
        return value switch
        {
            string str => Enum.Parse(enumType, str, ignoreCase: true),
            _ when IsNumericType(value.GetType()) => Enum.ToObject(enumType, value),
            _ => throw new ArgumentException($"Cannot convert {value.GetType()} to enum {enumType}")
        };
    }

    private static Model.Point ConvertToPoint(object value)
    {
        return value switch
        {
            global::Neo4j.Driver.Point neo4jPoint => new Model.Point(neo4jPoint.X, neo4jPoint.Y, neo4jPoint.Z), // Note: Neo4j uses X=lon, Y=lat
            _ => throw new ArgumentException($"Cannot convert {value.GetType()} to Point")
        };
    }

    private static bool IsNumericType(Type type)
    {
        return type == typeof(sbyte) || type == typeof(byte) ||
               type == typeof(short) || type == typeof(ushort) ||
               type == typeof(int) || type == typeof(uint) ||
               type == typeof(long) || type == typeof(ulong) ||
               type == typeof(float) || type == typeof(double) ||
               type == typeof(decimal);
    }

    /// <summary>
    /// Creates metadata dictionary for a node or relationship.
    /// </summary>
    public static Dictionary<string, object> CreateMetadata(Type type)
    {
        return new Dictionary<string, object>
        {
            [MetadataPropertyName] = new Dictionary<string, object>
            {
                [TypeNameKey] = type.AssemblyQualifiedName ?? type.FullName ?? type.Name
            }
        };
    }

    /// <summary>
    /// Extracts type information from metadata if present.
    /// </summary>
    public static Type? GetTypeFromMetadata(IReadOnlyDictionary<string, object>? properties)
    {
        if (properties == null)
            return null;

        if (!properties.TryGetValue(MetadataPropertyName, out var metadata))
            return null;

        if (metadata is not IReadOnlyDictionary<string, object> metaDict)
            return null;

        if (!metaDict.TryGetValue(TypeNameKey, out var typeName) || typeName is not string typeNameStr)
            return null;

        return Type.GetType(typeNameStr);
    }

    private static DateTime ConvertToDateTime(object value)
    {
        return value switch
        {
            DateTime dt => dt,
            DateTimeOffset dto => dto.UtcDateTime,
            ZonedDateTime zdt => zdt.ToDateTimeOffset().UtcDateTime,
            LocalDateTime ldt => ldt.ToDateTime(),
            string s => DateTime.Parse(s, CultureInfo.InvariantCulture),
            _ => Convert.ToDateTime(value)
        };
    }

    private static DateTimeOffset ConvertToDateTimeOffset(object value)
    {
        return value switch
        {
            DateTimeOffset dto => dto,
            DateTime dt => new DateTimeOffset(dt),
            ZonedDateTime zdt => zdt.ToDateTimeOffset(),
            string s => DateTimeOffset.Parse(s, CultureInfo.InvariantCulture),
            _ => new DateTimeOffset(Convert.ToDateTime(value))
        };
    }

    private static TimeSpan ConvertToTimeSpan(object value)
    {
        return value switch
        {
            TimeSpan ts => ts,
            double ms => TimeSpan.FromMilliseconds(ms),
            long ms => TimeSpan.FromMilliseconds(ms),
            string s => TimeSpan.Parse(s, CultureInfo.InvariantCulture),
            _ => TimeSpan.FromMilliseconds(Convert.ToDouble(value))
        };
    }

    private static object? ConvertDictionary(IDictionary dict)
    {
        var result = new Dictionary<string, object?>();
        foreach (DictionaryEntry entry in dict)
        {
            var key = entry.Key?.ToString() ?? string.Empty;
            result[key] = ToNeo4jValue(entry.Value);
        }
        return result;
    }

    private static object? ConvertCollection(IEnumerable enumerable)
    {
        var list = new List<object?>();
        foreach (var item in enumerable)
        {
            list.Add(ToNeo4jValue(item));
        }
        return list;
    }

    private static bool IsDictionaryType(Type type)
    {
        return type.IsGenericType &&
               (type.GetGenericTypeDefinition() == typeof(Dictionary<,>) ||
                type.GetGenericTypeDefinition() == typeof(IDictionary<,>));
    }

    private static bool IsCollectionType(Type type)
    {
        return type.IsArray ||
               (type.IsGenericType && type.GetInterfaces().Any(i =>
                   i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>)));
    }

    private static object? ConvertToDictionary(object value, Type targetType)
    {
        if (value is not IDictionary sourceDict)
            return null;

        var keyType = targetType.GetGenericArguments()[0];
        var valueType = targetType.GetGenericArguments()[1];
        var dictType = typeof(Dictionary<,>).MakeGenericType(keyType, valueType);
        var dict = (IDictionary)Activator.CreateInstance(dictType)!;

        foreach (DictionaryEntry entry in sourceDict)
        {
            var key = Convert.ChangeType(entry.Key, keyType);
            var val = FromNeo4jValue(entry.Value, valueType);
            dict.Add(key, val);
        }

        return dict;
    }

    private static object? ConvertToCollection(object value, Type targetType)
    {
        if (value is not IEnumerable sourceCollection)
            return null;

        var elementType = targetType.IsArray
            ? targetType.GetElementType()!
            : targetType.GetGenericArguments()[0];

        var list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(elementType))!;

        foreach (var item in sourceCollection)
        {
            list.Add(FromNeo4jValue(item, elementType));
        }

        return targetType.IsArray
            ? list.GetType().GetMethod("ToArray")!.Invoke(list, null)
            : list;
    }
}