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

using System.Globalization;
using System.Text.Json;
using Cvoya.Graph.Model;
using Cvoya.Graph.Model.Serialization;
using Microsoft.Extensions.Logging;
using Npgsql.Age.Types;

/// <summary>
/// Converts AGE vertices/edges back into <see cref="EntityInfo"/> structures for deserialization.
/// </summary>
internal sealed class AgeEntityMapper
{
    private readonly EntityFactory entityFactory;

    public AgeEntityMapper(EntityFactory entityFactory, ILoggerFactory? loggerFactory)
    {
        this.entityFactory = entityFactory;
        _ = loggerFactory?.CreateLogger<AgeEntityMapper>();
    }

    public EntityInfo MapVertex(Vertex vertex, Type targetType)
    {
        var label = vertex.Label;
        var labels = ExtractLabels(vertex);
        var simpleProperties = new Dictionary<string, Property>(StringComparer.Ordinal);
        var complexProperties = new Dictionary<string, Property>(StringComparer.Ordinal);

        foreach (var (key, rawValue) in vertex.Properties)
        {
            var value = NormalizeValue(rawValue);
            
            // Get the property type from the target type for proper conversion
            var propertyInfo = targetType.GetProperty(key);
            var propertyType = propertyInfo?.PropertyType;
            
            // Convert the value using type information
            var convertedValue = ConvertValue(value, key, propertyType);
            
            if (convertedValue is IDictionary<string, object?> dict && !GraphDataModel.IsSimple(dict.GetType()))
            {
                var entityInfo = CreateEntityInfoFromDictionary(dict, key);
                complexProperties[key] = new Property(null!, key, false, entityInfo);
            }
            else if (convertedValue is IList<object?> list && list.Count > 0 && list[0] is IDictionary<string, object?>)
            {
                var elementInfos = list
                    .Cast<IDictionary<string, object?>>()
                    .Select(item => CreateEntityInfoFromDictionary(item, key))
                    .ToList();

                complexProperties[key] = new Property(
                    null!,
                    key,
                    false,
                    new EntityCollection(typeof(object), elementInfos));
            }
            else if (convertedValue is IList<object?> primitiveList)
            {
                // Handle collections of primitive values (e.g., List<string>, List<int>)
                var elementType = DetermineElementType(primitiveList);
                var simpleValues = primitiveList
                    .Select(item => new SimpleValue(item ?? null!, item?.GetType() ?? elementType))
                    .ToList();
                
                simpleProperties[key] = new Property(
                    null!,
                    key,
                    false,
                    new SimpleCollection(simpleValues, elementType));
            }
            else
            {
                simpleProperties[key] = CreateSimpleProperty(key, convertedValue);
            }
        }

        // Ensure Labels property is always present in simpleProperties for deserialization
        // Even if it wasn't stored as a property in AGE, we need it for the EntityInfo
        if (!simpleProperties.ContainsKey(nameof(INode.Labels)))
        {
            simpleProperties[nameof(INode.Labels)] = new Property(
                null!,
                nameof(INode.Labels),
                false,
                new SimpleCollection(
                    labels.Select(l => new SimpleValue(l, typeof(string))).ToList(),
                    typeof(string)));
        }

        return new EntityInfo(targetType, label, labels, simpleProperties, complexProperties);
    }

    public EntityInfo MapEdge(Edge edge, Type targetType)
    {
        var simpleProperties = new Dictionary<string, Property>(StringComparer.Ordinal);

        // First, process all properties from the edge (including Id, StartNodeId, EndNodeId)
        foreach (var (key, rawValue) in edge.Properties)
        {
            var value = NormalizeValue(rawValue);
            
            // Get the property type from the target type for proper conversion
            var propertyInfo = targetType.GetProperty(key);
            var propertyType = propertyInfo?.PropertyType;
            
            // Convert the value using type information
            var convertedValue = ConvertValue(value, key, propertyType);
            simpleProperties[key] = CreateSimpleProperty(key, convertedValue);
        }

        // If Id, StartNodeId, or EndNodeId are not in properties, use AGE's internal IDs as fallback
        if (!simpleProperties.ContainsKey(nameof(IRelationship.Id)))
        {
            simpleProperties[nameof(IRelationship.Id)] = new Property(null!, nameof(IRelationship.Id), false, new SimpleValue(edge.Id.Value.ToString(), typeof(string)));
        }
        if (!simpleProperties.ContainsKey(nameof(IRelationship.StartNodeId)))
        {
            simpleProperties[nameof(IRelationship.StartNodeId)] = new Property(null!, nameof(IRelationship.StartNodeId), false, new SimpleValue(edge.StartId.Value.ToString(), typeof(string)));
        }
        if (!simpleProperties.ContainsKey(nameof(IRelationship.EndNodeId)))
        {
            simpleProperties[nameof(IRelationship.EndNodeId)] = new Property(null!, nameof(IRelationship.EndNodeId), false, new SimpleValue(edge.EndId.Value.ToString(), typeof(string)));
        }

        // Always set Type property from the edge label
        // The Type property should contain the relationship type label
        simpleProperties[nameof(IRelationship.Type)] = new Property(
            null!,
            nameof(IRelationship.Type),
            false,
            new SimpleValue(edge.Label, typeof(string)));

        return new EntityInfo(
            targetType,
            edge.Label,
            Array.Empty<string>(),
            simpleProperties,
            new Dictionary<string, Property>(StringComparer.Ordinal));
    }

    private static IReadOnlyList<string> ExtractLabels(Vertex vertex)
    {
        if (vertex.Properties.TryGetValue(nameof(INode.Labels), out var value))
        {
            return value switch
            {
                IList<object?> list => list.Select(v => v?.ToString() ?? string.Empty).Where(static v => !string.IsNullOrWhiteSpace(v)).ToList(),
                IEnumerable<string> stringList => stringList.ToList(),
                _ => []
            };
        }

        if (!string.IsNullOrEmpty(vertex.Label))
        {
            return vertex.Label.Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }

        return [];
    }

    private static object? NormalizeValue(object? rawValue)
    {
        switch (rawValue)
        {
            case null:
                return null;
            case JsonElement jsonElement:
                return jsonElement.ValueKind switch
                {
                    JsonValueKind.Null => null,
                    JsonValueKind.True => true,
                    JsonValueKind.False => false,
                    JsonValueKind.Number => jsonElement.TryGetInt64(out var l) ? l : jsonElement.GetDouble(),
                    JsonValueKind.String => jsonElement.GetString(),
                    JsonValueKind.Object => JsonSerializer.Deserialize<Dictionary<string, object?>>(jsonElement.GetRawText()),
                    JsonValueKind.Array => JsonSerializer.Deserialize<List<object?>>(jsonElement.GetRawText()),
                    _ => jsonElement.GetRawText()
                };
            default:
                return rawValue;
        }
    }

    private EntityInfo CreateEntityInfoFromDictionary(IDictionary<string, object?> dictionary, string name)
    {
        var simpleProperties = new Dictionary<string, Property>(StringComparer.Ordinal);
        foreach (var (key, value) in dictionary)
        {
            var normalized = NormalizeValue(value);
            simpleProperties[key] = CreateSimpleProperty(key, normalized);
        }

        return new EntityInfo(
            typeof(object),
            name,
            Array.Empty<string>(),
            simpleProperties,
            new Dictionary<string, Property>(StringComparer.Ordinal));
    }

    private static object? ConvertValue(object? value, string propertyName, Type? targetType)
    {
        // For the Id field, keep it as a string (don't convert Guid)
        if (propertyName == nameof(IEntity.Id))
        {
            return value;
        }

        // If no target type information, do basic conversion
        if (targetType == null)
        {
            return AgeSerializationBridge.FromAgeValue(value);
        }

        // Handle nullable types
        var underlyingType = Nullable.GetUnderlyingType(targetType) ?? targetType;

        // If value is null, return null
        if (value == null)
        {
            return null;
        }

        // If types already match, no conversion needed
        if (underlyingType.IsAssignableFrom(value.GetType()))
        {
            return value;
        }

        // Special handling for specific types that AGE stores as strings
        if (value is string str)
        {
            // Enums
            if (underlyingType.IsEnum)
            {
                return Enum.Parse(underlyingType, str, ignoreCase: true);
            }

            // Guid
            if (underlyingType == typeof(Guid))
            {
                return Guid.Parse(str);
            }

            // Uri
            if (underlyingType == typeof(Uri))
            {
                return new Uri(str);
            }

            // DateTime (ISO 8601 format)
            if (underlyingType == typeof(DateTime) && str.Contains('T'))
            {
                return DateTime.Parse(str, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
            }

            // Point (JSON format)
            if (underlyingType == typeof(Point) && str.StartsWith('{'))
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
                    // Fall through to return the string as-is
                }
            }
        }

        // For other conversions, use FromAgeValue which handles dictionaries, lists, etc.
        return AgeSerializationBridge.FromAgeValue(value);
    }

    private record PointData(double Longitude, double Latitude, double Height);

    private static Type DetermineElementType(IList<object?> list)
    {
        // Find the first non-null element to determine the type
        var firstNonNull = list.FirstOrDefault(item => item != null);
        return firstNonNull?.GetType() ?? typeof(object);
    }

    private static Property CreateSimpleProperty(string key, object? value) =>
        value is null
            ? new Property(null!, key, true, new SimpleValue(null!, typeof(object)))
            : new Property(null!, key, false, new SimpleValue(value, value.GetType()));
}
