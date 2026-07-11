// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Age.Serialization;

using System.Collections;
using System.Globalization;
using System.Text.Json;

/// <summary>Converts CLR property values to and from AGE-compatible agtype values.</summary>
internal static class SerializationBridge
{
    internal const string MetadataPropertyName = "__metadata__";
    internal const string EntityKindPropertyName = "__graphModelEntityKind__";
    internal const string NodeEntityKind = "Node";
    internal const string PhysicalNodeLabel = "CvoyaNode";
    internal const string PhysicalRelationshipType = "CvoyaRelationship";
    private const string TypeNameKey = "type";

    public static object? ToAgeValue(object? value)
    {
        return value switch
        {
            null => null,
            string or bool or char or sbyte or byte or short or ushort or int or uint or long or ulong or
                float or double or decimal => value,
            Enum enumValue => enumValue.ToString(),
            DateTime dateTime => dateTime.ToString("O", CultureInfo.InvariantCulture),
            DateTimeOffset dateTimeOffset => dateTimeOffset.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
            DateOnly date => date.ToString("O", CultureInfo.InvariantCulture),
            TimeOnly time => time.ToString("O", CultureInfo.InvariantCulture),
            TimeSpan duration => duration.ToString("c", CultureInfo.InvariantCulture),
            Guid guid => guid.ToString("D", CultureInfo.InvariantCulture),
            Uri uri => uri.ToString(),
            Graph.Point point => new Dictionary<string, object?>
            {
                [nameof(Graph.Point.Latitude)] = point.Latitude,
                [nameof(Graph.Point.Longitude)] = point.Longitude,
                [nameof(Graph.Point.Height)] = point.Height,
            },
            byte[] bytes => Convert.ToBase64String(bytes),
            IDictionary dictionary => ConvertDictionary(dictionary),
            IEnumerable sequence => sequence.Cast<object?>().Select(ToAgeValue).ToArray(),
            _ => value.ToString(),
        };
    }

    public static object? FromAgeValue(object? value, Type targetType)
    {
        ArgumentNullException.ThrowIfNull(targetType);
        if (value is JsonElement json)
        {
            value = FromJson(json);
        }

        if (value is null)
        {
            return targetType.IsValueType && Nullable.GetUnderlyingType(targetType) is null
                ? Activator.CreateInstance(targetType)
                : null;
        }

        var effectiveType = Nullable.GetUnderlyingType(targetType) ?? targetType;
        if (effectiveType.IsInstanceOfType(value)) return value;
        if (effectiveType.IsEnum) return Enum.Parse(effectiveType, Convert.ToString(value, CultureInfo.InvariantCulture)!, true);
        if (effectiveType == typeof(Guid)) return Guid.Parse(Convert.ToString(value, CultureInfo.InvariantCulture)!);
        if (effectiveType == typeof(Uri)) return new Uri(Convert.ToString(value, CultureInfo.InvariantCulture)!);
        if (effectiveType == typeof(DateTime)) return DateTime.Parse(Convert.ToString(value, CultureInfo.InvariantCulture)!, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        if (effectiveType == typeof(DateTimeOffset)) return DateTimeOffset.Parse(Convert.ToString(value, CultureInfo.InvariantCulture)!, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        if (effectiveType == typeof(DateOnly)) return DateOnly.Parse(Convert.ToString(value, CultureInfo.InvariantCulture)!, CultureInfo.InvariantCulture);
        if (effectiveType == typeof(TimeOnly)) return TimeOnly.Parse(Convert.ToString(value, CultureInfo.InvariantCulture)!, CultureInfo.InvariantCulture);
        if (effectiveType == typeof(TimeSpan)) return TimeSpan.Parse(Convert.ToString(value, CultureInfo.InvariantCulture)!, CultureInfo.InvariantCulture);
        if (effectiveType == typeof(byte[])) return Convert.FromBase64String(Convert.ToString(value, CultureInfo.InvariantCulture)!);
        if (effectiveType == typeof(Graph.Point)) return ConvertPoint(value);
        return Convert.ChangeType(value, effectiveType, CultureInfo.InvariantCulture);
    }

    public static Dictionary<string, object> CreateMetadata(Type type) => new()
    {
        [MetadataPropertyName] = new Dictionary<string, object>
        {
            [TypeNameKey] = type.AssemblyQualifiedName ?? type.FullName ?? type.Name,
        },
    };

    public static Type? GetTypeFromMetadata(IReadOnlyDictionary<string, object>? properties)
    {
        if (properties is null ||
            !properties.TryGetValue(MetadataPropertyName, out var metadata) ||
            metadata is not IReadOnlyDictionary<string, object> values ||
            !values.TryGetValue(TypeNameKey, out var typeName) ||
            typeName is not string text)
        {
            return null;
        }

        return Type.GetType(text);
    }

    private static Dictionary<string, object?> ConvertDictionary(IDictionary source)
    {
        var result = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (DictionaryEntry entry in source)
        {
            result.Add(Convert.ToString(entry.Key, CultureInfo.InvariantCulture)!, ToAgeValue(entry.Value));
        }

        return result;
    }

    private static Graph.Point ConvertPoint(object value)
    {
        if (value is not IDictionary<string, object?> map)
        {
            throw new InvalidCastException("AGE point values must be represented by an agtype map.");
        }

        return new Graph.Point
        {
            Latitude = Convert.ToDouble(map[nameof(Graph.Point.Latitude)], CultureInfo.InvariantCulture),
            Longitude = Convert.ToDouble(map[nameof(Graph.Point.Longitude)], CultureInfo.InvariantCulture),
            Height = map.TryGetValue(nameof(Graph.Point.Height), out var height) && height is not null
                ? Convert.ToDouble(height, CultureInfo.InvariantCulture)
                : 0,
        };
    }

    private static object? FromJson(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.Null => null,
        JsonValueKind.String => value.GetString(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Number when value.TryGetInt64(out var integer) => integer,
        JsonValueKind.Number when value.TryGetDecimal(out var number) => number,
        JsonValueKind.Array => value.EnumerateArray().Select(FromJson).ToArray(),
        JsonValueKind.Object => value.EnumerateObject().ToDictionary(
            property => property.Name,
            property => FromJson(property.Value),
            StringComparer.Ordinal),
        _ => throw new InvalidCastException($"Unsupported agtype JSON value '{value.ValueKind}'."),
    };
}
