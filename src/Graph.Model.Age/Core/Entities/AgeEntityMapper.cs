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
using static Cvoya.Graph.Model.Age.Core.Entities.LabelsExtractor;

/// <summary>
/// Converts AGE vertices/edges back into EntityInfo structures for deserialization.
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

        // Resolve the most derived type for class hierarchy support.
        var resolvedType = EntityTypeResolver.ResolveType(targetType, label, labels);
        if (resolvedType != targetType)
        {
            _logger.LogDebug("MapVertex: Resolved type from label '{Label}': {Type} (was {OriginalType})",
                labels.FirstOrDefault() ?? label, resolvedType.Name, targetType.Name);
        }

        var simpleProperties = new Dictionary<string, Property>(StringComparer.Ordinal);
        var complexProperties = new Dictionary<string, Property>(StringComparer.Ordinal);

        _logger.LogDebug("MapVertex: Label={Label}, PropertiesCount={Count}, TargetType={Type}",
            label, vertex.Properties.Count, resolvedType.Name);

        foreach (var (key, rawValue) in vertex.Properties)
        {
            // Skip internal AGE inheritance property that is handled separately
            if (key == "inheritance_labels")
                continue;

            var value = NormalizeValue(rawValue);
            var csharpPropertyName = MapAgePropertyNameToCSharp(key);

            _logger.LogDebug("MapVertex: Processing property '{AgeKey}' -> '{CSharpKey}', Value={Value}",
                key, csharpPropertyName, value);

            var propertyInfo = resolvedType.GetProperty(csharpPropertyName);
            var propertyType = propertyInfo?.PropertyType;
            var convertedValue = ConvertValue(value, csharpPropertyName, propertyType);

            // If the raw value is a JsonElement Object and the target property is complex,
            // convert it to a Dictionary so it can be matched by the IDictionary check below.
            // This handles complex POCO properties (like AddressValue, MemorySourceNode) 
            // that are stored as JSON objects in AGE and returned as JsonElement from the vertex.
            if (rawValue is JsonElement rawJson && rawJson.ValueKind == JsonValueKind.Object
                && propertyType != null && !GraphDataModel.IsSimple(propertyType))
            {
                convertedValue = EntityInfoBuilder.ConvertJsonElementToDictionary(rawJson);
            }
            // Also handle the case where ConvertValue returns a JsonElement for complex types
            else if (convertedValue is JsonElement convJson && convJson.ValueKind == JsonValueKind.Object
                && propertyType != null && !GraphDataModel.IsSimple(propertyType))
            {
                convertedValue = EntityInfoBuilder.ConvertJsonElementToDictionary(convJson);
            }

            // Handle JSON string that contains a serialized complex collection.
            var parsedCollection = TryParseJsonCollection(convertedValue, csharpPropertyName);
            if (parsedCollection is IList<object?> parsedItems && parsedItems != convertedValue)
            {
                _logger.LogDebug("MapVertex: Parsed JSON collection for property '{Prop}' with {Count} items",
                    csharpPropertyName, parsedItems.Count);
                convertedValue = parsedCollection;
            }

            if (convertedValue is IDictionary<string, object?> dict && !GraphDataModel.IsSimple(dict.GetType()))
            {
                var entityInfo = EntityInfoBuilder.CreateEntityInfoFromDictionary(dict, csharpPropertyName);
                complexProperties[csharpPropertyName] = new Property(null!, csharpPropertyName, false, entityInfo);
                continue;
            }
            else if (convertedValue is IList<object?> list)
            {
                bool isComplexCollection = false;
                if (list.Count > 0 && list[0] is IDictionary<string, object?>)
                    isComplexCollection = true;
                else if (propertyType != null && GraphDataModel.IsComplex(propertyType))
                    isComplexCollection = true;

                if (isComplexCollection)
                {
                    var elementInfos = list.Select(item =>
                    {
                        if (item is JsonElement jsonElement)
                            return EntityInfoBuilder.CreateEntityInfoFromDictionary(EntityInfoBuilder.ConvertJsonElementToDictionary(jsonElement), csharpPropertyName);
                        if (item is IDictionary<string, object?> d)
                            return EntityInfoBuilder.CreateEntityInfoFromDictionary(d, csharpPropertyName);
                        return new EntityInfo(typeof(object), csharpPropertyName, Array.Empty<string>(), new Dictionary<string, Property>(StringComparer.Ordinal), new Dictionary<string, Property>(StringComparer.Ordinal));
                    }).ToList();

                    complexProperties[csharpPropertyName] = new Property(null!, csharpPropertyName, false, new EntityCollection(typeof(object), elementInfos));
                }
                else
                {
                    var elementType = EntityInfoBuilder.DetermineElementType(list);
                    var simpleValues = list.Select(item => new SimpleValue(item ?? null!, item?.GetType() ?? elementType)).ToList();
                    simpleProperties[csharpPropertyName] = new Property(null!, csharpPropertyName, false, new SimpleCollection(simpleValues, elementType));
                }
            }
            else
            {
                simpleProperties[csharpPropertyName] = EntityInfoBuilder.CreateSimpleProperty(csharpPropertyName, convertedValue);
            }
        }

        if (!simpleProperties.ContainsKey(nameof(INode.Labels)))
        {
            simpleProperties[nameof(INode.Labels)] = new Property(null!, nameof(INode.Labels), false,
                new SimpleCollection(labels.Select(l => new SimpleValue(l, typeof(string))).ToList(), typeof(string)));
        }

        return new EntityInfo(
            resolvedType,
            label,
            labels.ToArray(),
            simpleProperties,
            complexProperties,
            InheritanceLabels: labels.ToArray()
        );
    }

    public EntityInfo MapEdge(Edge edge, Type targetType)
    {
        var label = edge.Label;
        var allLabels = ExtractLabels(edge);

        // Resolve the most derived type for relationship hierarchy support.
        var resolvedType = EntityTypeResolver.ResolveType(targetType, label, allLabels);
        if (resolvedType != targetType)
        {
            _logger.LogDebug("MapEdge: Resolved type from label '{Label}': {Type} (was {OriginalType})",
                allLabels.FirstOrDefault() ?? label, resolvedType.Name, targetType.Name);
        }

        var simpleProperties = new Dictionary<string, Property>(StringComparer.Ordinal);

        foreach (var (key, rawValue) in edge.Properties)
        {
            // Skip internal AGE inheritance property that is handled separately
            if (key == "inheritance_labels")
                continue;

            var value = NormalizeValue(rawValue);
            var csharpPropertyName = MapAgePropertyNameToCSharp(key);
            var propertyInfo = resolvedType.GetProperty(csharpPropertyName);
            var propertyType = propertyInfo?.PropertyType;
            var convertedValue = ConvertValue(value, csharpPropertyName, propertyType);
            simpleProperties[csharpPropertyName] = EntityInfoBuilder.CreateSimpleProperty(csharpPropertyName, convertedValue);
        }

        if (!simpleProperties.ContainsKey(nameof(IRelationship.Id)))
            simpleProperties[nameof(IRelationship.Id)] = new Property(null!, nameof(IRelationship.Id), false, new SimpleValue(edge.Id.Value.ToString(), typeof(string)));
        if (!simpleProperties.ContainsKey(nameof(IRelationship.StartNodeId)))
            simpleProperties[nameof(IRelationship.StartNodeId)] = new Property(null!, nameof(IRelationship.StartNodeId), false, new SimpleValue(edge.StartId.Value.ToString(), typeof(string)));
        if (!simpleProperties.ContainsKey(nameof(IRelationship.EndNodeId)))
            simpleProperties[nameof(IRelationship.EndNodeId)] = new Property(null!, nameof(IRelationship.EndNodeId), false, new SimpleValue(edge.EndId.Value.ToString(), typeof(string)));

        // Set Type property from the edge label
        simpleProperties[nameof(IRelationship.Type)] = new Property(
            null!,
            nameof(IRelationship.Type),
            false,
            new SimpleValue(edge.Label, typeof(string)));

        return new EntityInfo(
            resolvedType,
            edge.Label,
            allLabels.ToArray(),
            simpleProperties,
            new Dictionary<string, Property>(StringComparer.Ordinal),
            InheritanceLabels: allLabels.ToArray()
        );
    }

    private static string MapAgePropertyNameToCSharp(string ageKey)
    {
        return ageKey switch
        {
            "user_id" => "Id",
            _ => ageKey
        };
    }

    /// <summary>
    /// Attempts to parse a string value as a JSON serialized complex collection.
    /// Handles both proper JSON arrays and AGE's concatenated-object format.
    /// Returns the parsed list or the original value if parsing fails.
    /// </summary>
    private static object? TryParseJsonCollection(object? value, string propertyName)
    {
        if (value is string strVal && strVal.Length >= 2 && (strVal[0] == '[' || strVal[0] == '{'))
        {
            var jsonToParse = strVal[0] == '[' ? strVal : $"[{strVal}]";
            try
            {
                using var doc = JsonDocument.Parse(jsonToParse);
                if (doc.RootElement.ValueKind == JsonValueKind.Array && doc.RootElement.GetArrayLength() > 0
                    && doc.RootElement[0].ValueKind == JsonValueKind.Object)
                {
                    var parsedItems = doc.RootElement.EnumerateArray()
                        .Select(e => (object)EntityInfoBuilder.ConvertJsonElementToDictionary(e))
                        .Where(static item => item is IDictionary<string, object?>)
                        .ToList();
                    if (parsedItems.Count > 0)
                    {
                        return parsedItems;
                    }
                }
            }
            catch
            {
                // Not valid JSON — treat as a regular string
            }
        }
        return value;
    }

    private static object? NormalizeValue(object? rawValue)
    {
        if (rawValue is Agtype agtypeValue)
        {
            if (agtypeValue.IsVertex) return agtypeValue.GetVertex();
            if (agtypeValue.IsEdge) return agtypeValue.GetEdge();
            return agtypeValue.ToString();
        }
        return rawValue;
    }

    private static object? ConvertValue(object? value, string propertyName, Type? targetType)
    {
        if (value is null) return null;
        if (targetType == null || targetType == typeof(object)) return value;

        // Unwrap Nullable<T> to its underlying type for comparison
        var effectiveType = targetType.IsGenericType && targetType.GetGenericTypeDefinition() == typeof(Nullable<>)
            ? Nullable.GetUnderlyingType(targetType)!
            : targetType;

        if (value is JsonElement jsonElement)
        {
            switch (effectiveType)
            {
                case not null when effectiveType == typeof(string):
                    return jsonElement.GetString();
                case not null when effectiveType == typeof(int):
                    return jsonElement.GetInt32();
                case not null when effectiveType == typeof(long):
                    return jsonElement.GetInt64();
                case not null when effectiveType == typeof(double):
                    return jsonElement.GetDouble();
                case not null when effectiveType == typeof(bool):
                    return jsonElement.GetBoolean();
                case not null when effectiveType == typeof(DateTime):
                    return jsonElement.GetDateTime();
                case not null when effectiveType == typeof(Guid):
                    return jsonElement.GetGuid();
                case not null when effectiveType == typeof(Point):
                    return EntityInfoBuilder.ParsePointFromJson(jsonElement);
                default:
                    return jsonElement.ToString()!;
            }
        }

        if (value is string strVal)
        {
            if (effectiveType == typeof(DateTime))
                return DateTime.Parse(strVal, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
            if (effectiveType == typeof(DateTimeOffset))
                return DateTimeOffset.Parse(strVal, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
            if (effectiveType == typeof(Point))
                return EntityInfoBuilder.ParsePointFromJson(JsonDocument.Parse(strVal).RootElement);
            if (effectiveType == typeof(bool))
            {
                if (strVal.Equals("true", StringComparison.OrdinalIgnoreCase) || strVal == "1") return true;
                if (strVal.Equals("false", StringComparison.OrdinalIgnoreCase) || strVal == "0") return false;
            }
            if (effectiveType == typeof(int) && int.TryParse(strVal, NumberStyles.Any, CultureInfo.InvariantCulture, out var intVal)) return intVal;
            if (effectiveType == typeof(long) && long.TryParse(strVal, NumberStyles.Any, CultureInfo.InvariantCulture, out var longVal)) return longVal;
            if (effectiveType == typeof(double) && double.TryParse(strVal, NumberStyles.Any, CultureInfo.InvariantCulture, out var doubleVal)) return doubleVal;
            if (effectiveType == typeof(float) && float.TryParse(strVal, NumberStyles.Any, CultureInfo.InvariantCulture, out var floatVal)) return floatVal;
            if (effectiveType == typeof(decimal) && decimal.TryParse(strVal, NumberStyles.Any, CultureInfo.InvariantCulture, out var decVal)) return decVal;
        }

        // Konnektr 2.x: complex property values arrive as Dictionary<string, object>
        // from InferredObjectConverter. Convert to target C# type for simple POCOs.
        var dictResult = ConvertDictionaryToSimplePoco(value, effectiveType);
        if (dictResult != value) return dictResult;

        // Handle numeric type conversions for edge entity property values
        // that come from AGE as types different from the target CLR type.
        var numericResult = CoerceNumericType(value, effectiveType);
        if (numericResult != value) return numericResult;

        return value;
    }

    /// <summary>
    /// Attempts to convert IDictionary&lt;string, object?&gt; to a simple CLR type via JSON serialization.
    /// Returns the original value if no conversion applies.
    /// </summary>
    private static object? ConvertDictionaryToSimplePoco(object? value, Type effectiveType)
    {
        if (value is IDictionary<string, object?> dict
            && effectiveType != typeof(IDictionary<string, object>)
            && effectiveType != typeof(Dictionary<string, object>)
            && GraphDataModel.IsSimple(effectiveType))
        {
            try
            {
                var json = JsonSerializer.Serialize(dict);
                var result = JsonSerializer.Deserialize(json, effectiveType,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (result != null)
                    return result;
            }
            catch
            {
                // Fall through — the dictionary will be handled by MapVertex's IDictionary check
            }
        }
        return value;
    }

    /// <summary>
    /// Coerces numeric types (e.g., decimal→double, long→int) when AGE returns
    /// a different numeric type than the CLR property expects.
    /// Returns the original value if no coercion applies.
    /// </summary>
    private static object? CoerceNumericType(object? value, Type effectiveType)
    {
        if (value is not string && effectiveType != value?.GetType())
        {
            try
            {
                if (effectiveType == typeof(double) && value is decimal decVal)
                    return (double)decVal;
                if (effectiveType == typeof(float) && value is decimal decFloat)
                    return (float)decFloat;
                if (effectiveType == typeof(int) && value is long longVal)
                    return (int)longVal;
                if (effectiveType == typeof(long) && value is int intVal)
                    return (long)intVal;
                return Convert.ChangeType(value, effectiveType, CultureInfo.InvariantCulture);
            }
            catch
            {
                // If conversion fails, fall through to return original value
            }
        }
        return value;
    }
}
