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
    private readonly ILogger<AgeEntityMapper> _logger;

    public AgeEntityMapper(EntityFactory entityFactory, ILoggerFactory? loggerFactory)
    {
        this.entityFactory = entityFactory;
        _logger = loggerFactory?.CreateLogger<AgeEntityMapper>() ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<AgeEntityMapper>.Instance;
    }

    public EntityInfo MapVertex(Vertex vertex, Type targetType)
    {
        var label = vertex.Label;
        var labels = ExtractLabels(vertex);
        var simpleProperties = new Dictionary<string, Property>(StringComparer.Ordinal);
        var complexProperties = new Dictionary<string, Property>(StringComparer.Ordinal);

        _logger.LogDebug("MapVertex: Label={Label}, PropertiesCount={Count}, TargetType={Type}", 
            label, vertex.Properties.Count, targetType.Name);

        foreach (var (key, rawValue) in vertex.Properties)
        {
            var value = NormalizeValue(rawValue);
            
            // Reverse the property name mapping from AGE back to C# property names
            var csharpPropertyName = MapAgePropertyNameToCSharp(key);
            
            _logger.LogDebug("MapVertex: Processing property '{AgeKey}' -> '{CSharpKey}', Value={Value}", 
                key, csharpPropertyName, value);
            
            // Get the property type from the target type for proper conversion
            var propertyInfo = targetType.GetProperty(csharpPropertyName);
            var propertyType = propertyInfo?.PropertyType;
            
            // Convert the value using type information
            var convertedValue = ConvertValue(value, csharpPropertyName, propertyType);
            
            if (convertedValue is IDictionary<string, object?> dict && !GraphDataModel.IsSimple(dict.GetType()))
            {
                var entityInfo = CreateEntityInfoFromDictionary(dict, csharpPropertyName);
                complexProperties[csharpPropertyName] = new Property(null!, csharpPropertyName, false, entityInfo);
            }
            else if (convertedValue is IList<object?> list)
            {
                // Handle collections - both complex and primitive
                // Use property type information to determine if this should be a complex collection
                bool isComplexCollection = false;
                
                if (list.Count > 0 && list[0] is IDictionary<string, object?>)
                {
                    // Non-empty collection with dictionary elements - definitely complex
                    isComplexCollection = true;
                }
                else if (propertyType != null && IsComplexCollectionType(propertyType))
                {
                    // Empty collection but property type indicates it should be complex
                    isComplexCollection = true;
                }

                if (isComplexCollection)
                {
                    // Collection of complex objects
                    var elementInfos = list
                        .Select(item => 
                        {
                            // Convert JsonElement to dictionary if needed
                            if (item is JsonElement jsonElement)
                            {
                                var dict = ConvertJsonElementToDictionary(jsonElement);
                                return CreateEntityInfoFromDictionary(dict, csharpPropertyName);
                            }
                            else if (item is IDictionary<string, object?> dict)
                            {
                                return CreateEntityInfoFromDictionary(dict, csharpPropertyName);
                            }
                            else
                            {
                                // Create empty EntityInfo for unexpected items
                                return new EntityInfo(
                                    typeof(object),
                                    csharpPropertyName,
                                    Array.Empty<string>(),
                                    new Dictionary<string, Property>(StringComparer.Ordinal),
                                    new Dictionary<string, Property>(StringComparer.Ordinal));
                            }
                        })
                        .ToList();

                    complexProperties[csharpPropertyName] = new Property(
                        null!,
                        csharpPropertyName,
                        false,
                        new EntityCollection(typeof(object), elementInfos));
                }
                else
                {
                    // Collection of primitive values
                    var elementType = DetermineElementType(list);
                    var simpleValues = list
                        .Select(item => new SimpleValue(item ?? null!, item?.GetType() ?? elementType))
                        .ToList();
                    
                    simpleProperties[csharpPropertyName] = new Property(
                        null!,
                        csharpPropertyName,
                        false,
                        new SimpleCollection(simpleValues, elementType));
                }
            }
            else
            {
                simpleProperties[csharpPropertyName] = CreateSimpleProperty(csharpPropertyName, convertedValue);
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

        return new EntityInfo(
            // Use polymorphic resolution to find the most specific type from labels
            ActualType: null,
            label, 
            labels, 
            simpleProperties, 
            complexProperties,
            // Pass labels as InheritanceLabels for polymorphic type resolution
            InheritanceLabels: labels
        );
    }

    public EntityInfo MapEdge(Edge edge, Type targetType)
    {
        var simpleProperties = new Dictionary<string, Property>(StringComparer.Ordinal);

        // First, process all properties from the edge (including Id, StartNodeId, EndNodeId)
        foreach (var (key, rawValue) in edge.Properties)
        {
            var value = NormalizeValue(rawValue);
            
            // Reverse the property name mapping from AGE back to C# property names
            var csharpPropertyName = MapAgePropertyNameToCSharp(key);
            
            // Get the property type from the target type for proper conversion
            var propertyInfo = targetType.GetProperty(csharpPropertyName);
            var propertyType = propertyInfo?.PropertyType;
            
            // Convert the value using type information
            var convertedValue = ConvertValue(value, csharpPropertyName, propertyType);
            simpleProperties[csharpPropertyName] = CreateSimpleProperty(csharpPropertyName, convertedValue);
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

        // Extract inheritance information for polymorphic type resolution
        var allLabels = ExtractLabels(edge);
        var inheritanceLabels = ExtractInheritanceLabels(edge);

        return new EntityInfo(
            // Always use polymorphic resolution to find the most specific type from labels
            ActualType: null,
            edge.Label,
            allLabels,
            simpleProperties,
            new Dictionary<string, Property>(StringComparer.Ordinal),
            inheritanceLabels);
    }

    private static IReadOnlyList<string> ExtractLabels(Edge edge)
    {
        // For AGE inheritance support, check for inheritance_labels property first
        if (edge.Properties.TryGetValue("inheritance_labels", out var inheritanceValue))
        {
            return inheritanceValue switch
            {
                string[] stringArray => stringArray.ToList(),
                IList<object?> list => list.Select(v => v?.ToString() ?? string.Empty).Where(static v => !string.IsNullOrWhiteSpace(v)).ToList(),
                IEnumerable<string> stringList => stringList.ToList(),
                _ => []
            };
        }
        
        // Fallback to standard Labels property
        if (edge.Properties.TryGetValue(nameof(INode.Labels), out var value))
        {
            return value switch
            {
                IList<object?> list => list.Select(v => v?.ToString() ?? string.Empty).Where(static v => !string.IsNullOrWhiteSpace(v)).ToList(),
                IEnumerable<string> stringList => stringList.ToList(),
                _ => []
            };
        }

        // Final fallback to edge label
        if (!string.IsNullOrEmpty(edge.Label))
        {
            return edge.Label.Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }

        return [];
    }

    private static IReadOnlyList<string>? ExtractInheritanceLabels(Edge edge)
    {
        // For AGE polymorphic support, extract inheritance_labels property
        if (edge.Properties.TryGetValue("inheritance_labels", out var inheritanceValue))
        {
            return inheritanceValue switch
            {
                string[] stringArray => stringArray.ToList(),
                IList<object?> list => list.Select(v => v?.ToString() ?? string.Empty).Where(static v => !string.IsNullOrWhiteSpace(v)).ToList(),
                IEnumerable<string> stringList => stringList.ToList(),
                _ => null
            };
        }
        
        return null;
    }

    private static IReadOnlyList<string> ExtractLabels(Vertex vertex)
    {
        // For AGE inheritance support, check for inheritance_labels property first
        if (vertex.Properties.TryGetValue("inheritance_labels", out var inheritanceValue))
        {
            return inheritanceValue switch
            {
                string[] stringArray => stringArray.ToList(),
                IList<object?> list => list.Select(v => v?.ToString() ?? string.Empty).Where(static v => !string.IsNullOrWhiteSpace(v)).ToList(),
                IEnumerable<string> stringList => stringList.ToList(),
                _ => []
            };
        }
        
        // Fallback to standard Labels property
        if (vertex.Properties.TryGetValue(nameof(INode.Labels), out var value))
        {
            return value switch
            {
                IList<object?> list => list.Select(v => v?.ToString() ?? string.Empty).Where(static v => !string.IsNullOrWhiteSpace(v)).ToList(),
                IEnumerable<string> stringList => stringList.ToList(),
                _ => []
            };
        }

        // Final fallback to vertex label
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

    private static Dictionary<string, object?> ConvertJsonElementToDictionary(JsonElement element)
    {
        var dictionary = new Dictionary<string, object?>(StringComparer.Ordinal);
        
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                dictionary[property.Name] = ConvertJsonElementToObject(property.Value);
            }
        }
        
        return dictionary;
    }

    private static object? ConvertJsonElementToObject(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Object => ConvertJsonElementToDictionary(element),
            JsonValueKind.Array => element.EnumerateArray().Select(ConvertJsonElementToObject).ToList(),
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.TryGetInt64(out var longValue) ? longValue : element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            _ => element.ToString()
        };
    }

    private static bool IsComplexCollectionType(Type propertyType)
    {
        // Check if this is a collection type (List<T>, IList<T>, etc.)
        if (!propertyType.IsGenericType)
            return false;

        var genericTypeDefinition = propertyType.GetGenericTypeDefinition();
        if (genericTypeDefinition != typeof(List<>) && 
            genericTypeDefinition != typeof(IList<>) && 
            genericTypeDefinition != typeof(ICollection<>) &&
            genericTypeDefinition != typeof(IEnumerable<>))
            return false;

        // Get the element type
        var elementType = propertyType.GetGenericArguments()[0];
        
        // Check if the element type is a complex type (not a simple value type)
        return !GraphDataModel.IsSimple(elementType);
    }

    private EntityInfo CreateEntityInfoFromDictionary(IDictionary<string, object?> dictionary, string name)
    {
        var simpleProperties = new Dictionary<string, Property>(StringComparer.Ordinal);
        var complexProperties = new Dictionary<string, Property>(StringComparer.Ordinal);
        
        foreach (var (key, value) in dictionary)
        {
            var normalized = NormalizeValue(value);
            
            // Check if this property should be treated as a complex object
            if (normalized is IDictionary<string, object?> nestedDict && !GraphDataModel.IsSimple(nestedDict.GetType()))
            {
                // Recursively create EntityInfo for nested complex objects
                var nestedEntityInfo = CreateEntityInfoFromDictionary(nestedDict, key);
                complexProperties[key] = new Property(null!, key, false, nestedEntityInfo);
            }
            else if (normalized is IList<object?> list && list.Count > 0 && list[0] is IDictionary<string, object?>)
            {
                // Handle collections of complex objects
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
            else
            {
                // Handle as simple property
                simpleProperties[key] = CreateSimpleProperty(key, normalized);
            }
        }

        return new EntityInfo(
            typeof(object),
            name,
            Array.Empty<string>(),
            simpleProperties,
            complexProperties);
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

        // Handle numeric type conversions (PostgreSQL may return Int32 when we expect Int64, etc.)
        if (IsNumericType(underlyingType) && IsNumericType(value.GetType()))
        {
            try
            {
                return Convert.ChangeType(value, underlyingType, CultureInfo.InvariantCulture);
            }
            catch
            {
                // If conversion fails, fall through to other handlers
            }
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

    private static bool IsNumericType(Type type)
    {
        return type == typeof(sbyte) || type == typeof(byte) ||
               type == typeof(short) || type == typeof(ushort) ||
               type == typeof(int) || type == typeof(uint) ||
               type == typeof(long) || type == typeof(ulong) ||
               type == typeof(float) || type == typeof(double) ||
               type == typeof(decimal);
    }

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

    /// <summary>
    /// Maps AGE property names back to C# property names.
    /// This reverses the mapping applied during node/relationship creation.
    /// </summary>
    private static string MapAgePropertyNameToCSharp(string agePropertyName)
    {
        return agePropertyName switch
        {
            // Map AGE "user_id" field back to C# "Id" property
            "user_id" => "Id",
            
            // For all other properties, keep the same name
            _ => agePropertyName
        };
    }
}
