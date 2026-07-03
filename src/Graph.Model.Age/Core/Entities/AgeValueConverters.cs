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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Cvoya.Graph.Model.Serialization;
using Npgsql.Age.Types;

/// <summary>
/// Static helpers for converting AGE Agtype values to CLR types and EntityInfo structures.
/// </summary>
internal static class AgeValueConverters
{
    public static object? ConvertDictionaryToType(Dictionary<string, object> dict, Type targetType)
    {
        if (dict == null || dict.Count == 0)
            return null;

        try
        {
            var json = JsonSerializer.Serialize(dict);
            var result = JsonSerializer.Deserialize(json, targetType,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            return result;
        }
        catch
        {
            return null;
        }
    }

    public static object? ConvertScalarAgtype(string agTypeStr, Type targetType)
    {
        if (targetType == typeof(string)) return agTypeStr;
        if (targetType == typeof(int) && int.TryParse(agTypeStr, out var intVal)) return intVal;
        if (targetType == typeof(long) && long.TryParse(agTypeStr, out var longVal)) return longVal;
        if (targetType == typeof(double) && double.TryParse(agTypeStr, out var doubleVal)) return doubleVal;
        if (targetType == typeof(float) && float.TryParse(agTypeStr, out var floatVal)) return floatVal;
        if (targetType == typeof(decimal) && decimal.TryParse(agTypeStr, out var decVal)) return decVal;
        if (targetType == typeof(bool) && bool.TryParse(agTypeStr, out var boolVal)) return boolVal;
        if (targetType == typeof(DateTime) || targetType == typeof(DateTime?))
        {
            var strVal = agTypeStr.Trim('"', ' ', '\'');
            if (DateTime.TryParse(strVal, System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.RoundtripKind, out var dtVal)
                || DateTime.TryParseExact(strVal,
                    ["yyyy-MM-ddTHH:mm:ss.FFFFFFF", "yyyy-MM-ddTHH:mm:ss", "yyyy-MM-dd",
                     "yyyy-MM-dd HH:mm:ss.FFFFFFF", "yyyy-MM-dd HH:mm:ss"],
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.RoundtripKind, out dtVal))
            {
                return dtVal.Kind == DateTimeKind.Unspecified
                    ? DateTime.SpecifyKind(dtVal, DateTimeKind.Local) : dtVal;
            }
        }
        if (targetType == typeof(DateTimeOffset) || targetType == typeof(DateTimeOffset?))
        {
            var strVal = agTypeStr.Trim('"', ' ', '\'');
            if (DateTimeOffset.TryParse(strVal, System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.RoundtripKind, out var dtoVal))
                return dtoVal;
        }
        return agTypeStr;
    }

    public static EntityInfo ConvertAgtypeMapToEntityInfo(Agtype map, Type targetType)
    {
        var simpleProps = new Dictionary<string, Property>(StringComparer.Ordinal);
        var complexProps = new Dictionary<string, Property>(StringComparer.Ordinal);

        var json = map.ToString();
        if (string.IsNullOrEmpty(json) || !json.StartsWith("{"))
            return new EntityInfo(targetType, string.Empty, [], simpleProps, complexProps);

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var targetProps = targetType.GetProperties()
                .ToDictionary(p => p.Name, p => p.PropertyType, StringComparer.Ordinal);

            foreach (var prop in root.EnumerateObject())
            {
                var propName = prop.Name;
                var targetPropType = targetProps.TryGetValue(propName, out var tpt) ? tpt : typeof(string);

                object? value = prop.Value.ValueKind switch
                {
                    JsonValueKind.String => prop.Value.GetString(),
                    JsonValueKind.Number => ConvertJsonNumber(prop.Value, targetPropType),
                    JsonValueKind.True => true,
                    JsonValueKind.False => false,
                    JsonValueKind.Null => null,
                    _ => prop.Value.GetRawText()
                };

                if (value != null)
                {
                    simpleProps[propName] = new Property(null!, propName, false,
                        new SimpleValue(value, value.GetType()));
                }
            }
        }
        catch { }

        return new EntityInfo(targetType, string.Empty, [], simpleProps, complexProps);
    }

    public static EntityInfo ConvertJsonElementToEntityInfo(JsonElement elem, Type targetType)
    {
        var simpleProps = new Dictionary<string, Property>(StringComparer.Ordinal);
        var targetProps = targetType.GetProperties()
            .ToDictionary(p => p.Name, p => p.PropertyType, StringComparer.Ordinal);

        foreach (var prop in elem.EnumerateObject())
        {
            var propName = prop.Name;
            var targetPropType = targetProps.TryGetValue(propName, out var tpt) ? tpt : typeof(string);

            object? value = prop.Value.ValueKind switch
            {
                JsonValueKind.String => prop.Value.GetString(),
                JsonValueKind.Number => ConvertJsonNumber(prop.Value, targetPropType),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Null => null,
                _ => prop.Value.GetRawText()
            };

            if (value != null)
            {
                simpleProps[propName] = new Property(null!, propName, false,
                    new SimpleValue(value, value.GetType()));
            }
        }

        return new EntityInfo(targetType, string.Empty, [], simpleProps,
            new Dictionary<string, Property>(StringComparer.Ordinal));
    }

    public static object? ConvertJsonNumber(JsonElement elem, Type targetType)
    {
        if (targetType == typeof(int) || targetType == typeof(int?))
            return elem.TryGetInt32(out var i) ? i : (int)elem.GetDouble();
        if (targetType == typeof(long) || targetType == typeof(long?))
            return elem.TryGetInt64(out var l) ? l : (long)elem.GetDouble();
        if (targetType == typeof(double) || targetType == typeof(double?))
            return elem.GetDouble();
        if (targetType == typeof(float) || targetType == typeof(float?))
            return (float)elem.GetDouble();
        if (targetType == typeof(decimal) || targetType == typeof(decimal?))
            return elem.GetDecimal();
        return elem.GetDouble();
    }
}
