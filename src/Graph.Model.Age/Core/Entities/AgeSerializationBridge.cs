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

namespace Cvoya.Graph.Model.Age.Core.Entities;

using System.Collections;
using System.Globalization;
using System.Text.Json;
using Cvoya.Graph.Model.Serialization;

/// <summary>
/// Converts between GraphModel simple values and Apache AGE compatible representations.
/// </summary>
internal static class AgeSerializationBridge
{
    public static Dictionary<string, object?> SerializeSimpleProperties(EntityInfo entity)
    {
        var properties = new Dictionary<string, object?>(StringComparer.Ordinal);

        foreach (var (name, property) in entity.SimpleProperties)
        {
            switch (property.Value)
            {
                case SimpleValue simple:
                    properties[name] = ToAgeValue(simple.Object);
                    break;
                case SimpleCollection collection:
                    properties[name] = collection.Values.Select(v => ToAgeValue(v.Object)).ToList();
                    break;
            }
        }

        if (!properties.ContainsKey(nameof(IEntity.Id)))
        {
            properties[nameof(IEntity.Id)] = entity.SimpleProperties.TryGetValue(nameof(IEntity.Id), out var idProp) && idProp.Value is SimpleValue idValue
                ? ToAgeValue(idValue.Object)
                : null;
        }

        if (entity.ActualLabels.Count > 0)
        {
            properties[nameof(INode.Labels)] = entity.ActualLabels;
        }

        return properties;
    }

    public static object? ToAgeValue(object? value)
    {
        if (value is null)
        {
            return null;
        }

        return value switch
        {
            string or bool or byte[] or Guid or Uri => value,
            sbyte or byte or short or ushort or int or uint or long or ulong => value,
            float f => double.Parse(f.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture),
            double => value,
            decimal d => double.Parse(d.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture),
            DateTime dt => dt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
            DateTimeOffset dto => dto.UtcDateTime.ToString("O", CultureInfo.InvariantCulture),
            TimeSpan ts => ts.TotalMilliseconds,
            TimeOnly time => time.ToTimeSpan().TotalMilliseconds,
            DateOnly date => date.ToDateTime(TimeOnly.MinValue).ToString("O", CultureInfo.InvariantCulture),
            Enum enumValue => enumValue.ToString(),
            Point point => JsonSerializer.Serialize(new { point.Longitude, point.Latitude, point.Height }),
            IDictionary dict => SerializeDictionary(dict),
            IEnumerable enumerable => SerializeEnumerable(enumerable),
            JsonElement json => json.ValueKind switch
            {
                JsonValueKind.Object => JsonSerializer.Deserialize<Dictionary<string, object?>>(json.GetRawText())!,
                JsonValueKind.Array => JsonSerializer.Deserialize<List<object?>>(json.GetRawText())!,
                JsonValueKind.String => json.GetString(),
                JsonValueKind.Number => json.TryGetInt64(out var l) ? l : json.GetDouble(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Null => null,
                _ => json.GetRawText()
            },
            _ => value.ToString()
        };
    }

    private static object SerializeDictionary(IDictionary dictionary)
    {
        var result = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (DictionaryEntry entry in dictionary)
        {
            var key = entry.Key?.ToString() ?? string.Empty;
            result[key] = ToAgeValue(entry.Value);
        }

        return result;
    }

    private static object SerializeEnumerable(IEnumerable enumerable)
    {
        var result = new List<object?>();
        foreach (var item in enumerable)
        {
            result.Add(ToAgeValue(item));
        }

        return result;
    }

    /// <summary>
    /// Converts a value retrieved from AGE back to its original .NET type where possible.
    /// AGE stores some types as strings (DateTime, Guid, etc.) so we need to convert them back.
    /// This is a best-effort conversion and only handles common .NET types.
    /// </summary>
    public static object? FromAgeValue(object? value, Type? targetType = null)
    {
        if (value is null)
        {
            return null;
        }

        // If it's a string, try to parse it as common types that AGE stores as strings
        // But be conservative - only convert if we're confident it's that type
        if (value is string str)
        {
            // Try parsing as DateTime (ISO 8601 format with 'T' separator - very specific format)
            if (str.Contains('T') && str.Contains('-') && 
                DateTime.TryParse(str, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dateTime))
            {
                return dateTime;
            }

            // Try parsing as Point (JSON format: {"Longitude":...,"Latitude":...,"Height":...})
            if (str.StartsWith('{') && str.Contains("Longitude", StringComparison.Ordinal))
            {
                try
                {
                    var pointData = JsonSerializer.Deserialize<PointData>(str);
                    if (pointData != null)
                    {
                        return new Point
                        {
                            Longitude = pointData.Longitude,
                            Latitude = pointData.Latitude,
                            Height = pointData.Height
                        };
                    }
                }
                catch
                {
                    // If parsing fails, return the string as-is
                }
            }

            // Note: Enums are stored as strings, but we can't convert them back here without knowing the target type
            // The deserialization layer will handle enum parsing using the type information from the entity schema
            
            // Don't try to parse Guid or Uri - let them stay as strings
            // The serialization layer will handle complex types appropriately
            return str;
        }

        // Numbers - keep as-is
        if (value is sbyte or byte or short or ushort or int or uint or long or ulong or float or double or decimal)
        {
            return value;
        }

        // Collections - recursively convert
        if (value is IList<object?> list)
        {
            return list.Select(v => FromAgeValue(v, null)).ToList();
        }

        if (value is IDictionary<string, object?> dict)
        {
            return dict.ToDictionary(
                kvp => kvp.Key,
                kvp => FromAgeValue(kvp.Value, null),
                StringComparer.Ordinal);
        }

        // Everything else - return as-is
        return value;
    }

    // Helper record for Point deserialization
    private record PointData(double Longitude, double Latitude, double Height);
}
