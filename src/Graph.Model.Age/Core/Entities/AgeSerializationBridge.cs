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
                ? ToAgeValue(idValue.Object) : null;
        }
        if (entity.ActualLabels.Count > 0)
            properties[nameof(INode.Labels)] = entity.ActualLabels;
        return properties;
    }

    public static Dictionary<string, object?> SerializeAllProperties(EntityInfo entity)
    {
        var properties = SerializeSimpleProperties(entity);
        foreach (var (name, property) in entity.ComplexProperties)
        {
            if (property.Value is EntityInfo nestedEntity)
            {
                var nestedProps = SerializeAllProperties(nestedEntity);
                properties[name] = nestedProps;
            }
            else if (property.Value is EntityCollection entityCollection)
            {
                var items = entityCollection.Entities.Select(e => SerializeAllProperties(e)).ToList();
                // Serialize complex collections as JSON strings to ensure proper
                // round-tripping through AGE, which does not handle
                // List<Dictionary<string, object?>> parameters correctly.
                properties[name] = JsonSerializer.Serialize(items);
            }
        }
        return properties;
    }

    public static object? ToAgeValue(object? value)
    {
        return value switch
        {
            null => null,
            DateTime dt => dt.ToString("o", CultureInfo.InvariantCulture),
            DateTimeOffset dto => dto.ToString("o", CultureInfo.InvariantCulture),
            _ => value
        };
    }

    public static object? FromAgeValue(object? value, Type targetType)
    {
        if (value is null) return null;

        // Unwrap Nullable<T> to its underlying type
        var effectiveType = targetType.IsGenericType && targetType.GetGenericTypeDefinition() == typeof(Nullable<>)
            ? Nullable.GetUnderlyingType(targetType)!
            : targetType;

        if (value is string strVal)
        {
            if (effectiveType == typeof(DateTime)) return DateTime.Parse(strVal, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
            if (effectiveType == typeof(DateTimeOffset)) return DateTimeOffset.Parse(strVal, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
            if (effectiveType == typeof(Point))
            {
                using var doc = JsonDocument.Parse(strVal);
                return EntityInfoBuilder.ParsePointFromJson(doc.RootElement);
            }
            if (effectiveType == typeof(bool))
            {
                if (bool.TryParse(strVal, out var boolResult))
                    return boolResult;
                throw new InvalidCastException($"Cannot convert value '{strVal}' (type: string) to boolean. Expected 'true' or 'false'.");
            }
            if (effectiveType == typeof(int) && int.TryParse(strVal, NumberStyles.Any, CultureInfo.InvariantCulture, out var intVal)) return intVal;
            if (effectiveType == typeof(long) && long.TryParse(strVal, NumberStyles.Any, CultureInfo.InvariantCulture, out var longVal)) return longVal;
            if (effectiveType == typeof(double) && double.TryParse(strVal, NumberStyles.Any, CultureInfo.InvariantCulture, out var doubleVal)) return doubleVal;
            if (effectiveType == typeof(float) && float.TryParse(strVal, NumberStyles.Any, CultureInfo.InvariantCulture, out var floatVal)) return floatVal;
            if (effectiveType == typeof(decimal) && decimal.TryParse(strVal, NumberStyles.Any, CultureInfo.InvariantCulture, out var decVal)) return decVal;
            if (effectiveType == typeof(short) && short.TryParse(strVal, NumberStyles.Any, CultureInfo.InvariantCulture, out var shortVal)) return shortVal;
            if (effectiveType == typeof(byte) && byte.TryParse(strVal, NumberStyles.Any, CultureInfo.InvariantCulture, out var byteVal)) return byteVal;
        }

        return value;
    }

}
