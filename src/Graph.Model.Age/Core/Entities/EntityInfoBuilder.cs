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

using System.Text.Json;
using Cvoya.Graph.Model;
using Cvoya.Graph.Model.Serialization;

/// <summary>
/// Builds EntityInfo structures from dictionaries, JSON, and collections.
/// </summary>
internal static class EntityInfoBuilder
{
    public static Property CreateSimpleProperty(string name, object? value)
    {
        if (value is null)
            return new Property(null!, name, false, new SimpleValue(null!, typeof(object)));
        return new Property(null!, name, false, new SimpleValue(value, value.GetType()));
    }

    public static EntityInfo CreateEntityInfoFromDictionary(IDictionary<string, object?> dict, string typeName)
    {
        var simpleProps = new Dictionary<string, Property>(StringComparer.Ordinal);
        var complexProps = new Dictionary<string, Property>(StringComparer.Ordinal);
        foreach (var (key, val) in dict)
        {
            if (val is IDictionary<string, object?> nestedDict)
            {
                // Nested complex object — recurse to create nested EntityInfo
                var nestedEntityInfo = CreateEntityInfoFromDictionary(nestedDict, key);
                complexProps[key] = new Property(null!, key, false, nestedEntityInfo);
            }
            else if (val is IList<object?> list)
            {
                // Check if this is a collection of complex objects
                if (list.Count > 0 && list[0] is IDictionary<string, object?>)
                {
                    var elementInfos = list
                        .Select(item => item is IDictionary<string, object?> d
                            ? CreateEntityInfoFromDictionary(d, key)
                            : new EntityInfo(typeof(object), key, Array.Empty<string>(),
                                new Dictionary<string, Property>(StringComparer.Ordinal),
                                new Dictionary<string, Property>(StringComparer.Ordinal)))
                        .ToList();
                    complexProps[key] = new Property(null!, key, false, new EntityCollection(typeof(object), elementInfos));
                }
                else
                {
                    var elementType = DetermineElementType(list);
                    var simpleValues = list.Select(item => new SimpleValue(item ?? null!, item?.GetType() ?? elementType)).ToList();
                    simpleProps[key] = new Property(null!, key, false, new SimpleCollection(simpleValues, elementType));
                }
            }
            else
            {
                simpleProps[key] = CreateSimpleProperty(key, val);
            }
        }
        return new EntityInfo(typeof(object), typeName, Array.Empty<string>(), simpleProps, complexProps);
    }

    public static Dictionary<string, object?> ConvertJsonElementToDictionary(JsonElement element)
    {
        var dict = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var prop in element.EnumerateObject())
        {
            dict[prop.Name] = ConvertJsonElementToValue(prop.Value);
        }
        return dict;
    }

    public static object? ConvertJsonElementToValue(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.TryGetInt64(out var l) ? l : element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Object => ConvertJsonElementToDictionary(element),
            JsonValueKind.Array => element.EnumerateArray().Select(ConvertJsonElementToValue).ToList(),
            _ => null
        };
    }

    public static bool IsComplexCollectionType(Type type)
    {
        if (!type.IsGenericType) return false;
        var elementType = type.GetGenericArguments().FirstOrDefault();
        return elementType != null && GraphDataModel.IsComplex(elementType);
    }

    public static Type DetermineElementType(IList<object?> list)
    {
        foreach (var item in list)
        {
            if (item != null) return item.GetType();
        }
        return typeof(object);
    }

    public static Point ParsePointFromJson(JsonElement json)
    {
        double longitude = 0, latitude = 0, height = 0;
        if (json.TryGetProperty("longitude", out var lon)) longitude = lon.GetDouble();
        else if (json.TryGetProperty("Longitude", out lon)) longitude = lon.GetDouble();
        else if (json.TryGetProperty("x", out var x)) longitude = x.GetDouble();
        else if (json.ValueKind == JsonValueKind.Array && json.GetArrayLength() >= 2)
        {
            longitude = json[0].GetDouble();
            latitude = json[1].GetDouble();
            if (json.GetArrayLength() >= 3) height = json[2].GetDouble();
            return new Point { Longitude = longitude, Latitude = latitude, Height = height };
        }
        if (json.TryGetProperty("latitude", out var lat)) latitude = lat.GetDouble();
        else if (json.TryGetProperty("Latitude", out lat)) latitude = lat.GetDouble();
        else if (json.TryGetProperty("y", out var y)) latitude = y.GetDouble();
        if (json.TryGetProperty("height", out var h)) height = h.GetDouble();
        else if (json.TryGetProperty("Height", out h)) height = h.GetDouble();
        else if (json.TryGetProperty("z", out var z)) height = z.GetDouble();
        return new Point { Longitude = longitude, Latitude = latitude, Height = height };
    }
}
