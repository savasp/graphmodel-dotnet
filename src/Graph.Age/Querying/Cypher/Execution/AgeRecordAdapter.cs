// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Age.Querying.Cypher.Execution;

using System.Collections;
using System.Globalization;
using System.Text.Json;
using Cvoya.Graph.Age.Entities;
using Cvoya.Graph.Age.Serialization;
using Cvoya.Graph.Serialization.Results;
using Npgsql.Age.Types;

/// <summary>Adapts Npgsql AGE values into the provider-neutral result wire model.</summary>
internal sealed class AgeRecordAdapter
{
    public GraphRecord Adapt(AgeRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        return new GraphRecord(record.Values.ToDictionary(
            pair => pair.Key,
            pair => AdaptValue(pair.Value),
            StringComparer.Ordinal));
    }

    public List<GraphRecord> Adapt(IReadOnlyList<AgeRecord> records)
    {
        ArgumentNullException.ThrowIfNull(records);
        return records.Select(Adapt).ToList();
    }

    private GraphValue AdaptValue(object? value)
    {
        return value switch
        {
            null => GraphValue.Scalar(null),
            Agtype agtype => AdaptAgtype(agtype),
            Vertex<Dictionary<string, object>> vertex => AdaptVertex(vertex),
            Edge<Dictionary<string, object>> edge => AdaptEdge(edge),
            Path path => AdaptPath(path),
            JsonElement json => AdaptJson(json),
            IDictionary<string, object> map => AdaptMap(map),
            IDictionary dictionary => AdaptMap(dictionary),
            DateTime dateTime => GraphValue.Scalar(
                dateTime.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture)),
            DateTimeOffset dateTimeOffset => GraphValue.Scalar(
                dateTimeOffset.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture)),
            string text when TryParseTemporal(text, out var temporal) => GraphValue.Scalar(temporal),
            IEnumerable sequence when value is not string and not byte[] =>
                GraphValue.List(sequence.Cast<object?>().Select(AdaptValue).ToArray()),
            _ => GraphValue.Scalar(value),
        };
    }

    private GraphValue AdaptAgtype(Agtype value)
    {
        if (value.IsNull)
        {
            return GraphValue.Scalar(null);
        }

        if (value.IsVertex)
        {
            return AdaptVertex(value.GetVertex());
        }

        if (value.IsEdge)
        {
            return AdaptEdge(value.GetEdge());
        }

        if (value.IsPath)
        {
            return AdaptPath(value.GetPath());
        }

        if (value.IsMap)
        {
            return AdaptMap((IEnumerable<KeyValuePair<string, object>>)value.GetMap());
        }

        if (value.IsArray)
        {
            return GraphValue.List(value.GetList().Select(AdaptValue).ToArray());
        }

        return GraphValue.Scalar(ParseScalar(value));
    }

    private GraphValue AdaptVertex(Vertex<Dictionary<string, object>> vertex)
    {
        var isComplexValue = string.Equals(
            vertex.Label,
            SerializationBridge.ComplexNodeLabel,
            StringComparison.Ordinal) &&
            IsTrue(vertex.Properties, ComplexPropertyStorage.NodeMarkerProperty);
        var hasHierarchy = vertex.Properties.TryGetValue("inheritance_labels", out var hierarchy);
        var labels = new List<string>();
        if (!isComplexValue &&
            (!SerializationBridge.IsEncodedRootStorageName(vertex.Label, relationship: false) || !hasHierarchy) &&
            !string.IsNullOrWhiteSpace(vertex.Label))
        {
            labels.Add(vertex.Label);
        }

        if ((isComplexValue || !string.Equals(
                vertex.Label,
                SerializationBridge.ComplexNodeLabel,
                StringComparison.Ordinal)) && hasHierarchy)
        {
            labels.AddRange(ReadStrings(hierarchy!));
        }

        var properties = vertex.Properties
            .Where(pair => !IsInternalStorageProperty(pair.Key))
            .ToDictionary(pair => pair.Key, pair => AdaptValue(pair.Value), StringComparer.Ordinal);
        return GraphValue.Node(vertex.Id.Value.ToString(CultureInfo.InvariantCulture), labels.Distinct().ToArray(), properties);
    }

    private GraphValue AdaptEdge(Edge<Dictionary<string, object>> edge)
    {
        var isComplexProperty = string.Equals(
            edge.Label,
            SerializationBridge.ComplexRelationshipType,
            StringComparison.Ordinal) &&
            IsTrue(edge.Properties, ComplexPropertyStorage.RelationshipMarkerProperty);
        var relationshipType = (isComplexProperty ||
            SerializationBridge.IsEncodedRootStorageName(edge.Label, relationship: true)) &&
            edge.Properties.TryGetValue("inheritance_labels", out var hierarchy)
                ? ReadStrings(hierarchy).FirstOrDefault()
                : edge.Label;
        var properties = edge.Properties
            .Where(pair => !IsInternalStorageProperty(pair.Key))
            .ToDictionary(pair => pair.Key, pair => AdaptValue(pair.Value), StringComparer.Ordinal);
        return GraphValue.Relationship(
            edge.Id.Value.ToString(CultureInfo.InvariantCulture),
            relationshipType ?? string.Empty,
            edge.StartId.Value.ToString(CultureInfo.InvariantCulture),
            edge.EndId.Value.ToString(CultureInfo.InvariantCulture),
            properties);
    }

    private GraphValue AdaptPath(Path path) => GraphValue.Path(path.Segments.Select(segment => segment switch
    {
        Vertex<Dictionary<string, object>> vertex => AdaptVertex(vertex),
        Edge<Dictionary<string, object>> edge => AdaptEdge(edge),
        _ => throw new GraphException($"Unsupported AGE path segment '{segment.GetType().Name}'."),
    }).ToArray());

    private GraphValue AdaptMap(IEnumerable<KeyValuePair<string, object>> map)
    {
        var values = map.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        if (TryAdaptPoint(values, out var point))
        {
            return GraphValue.Scalar(point);
        }

        return GraphValue.Map(values.ToDictionary(
            pair => pair.Key,
            pair => pair.Key == "ComplexProperties"
                ? FlattenLists(AdaptValue(pair.Value))
                : AdaptValue(pair.Value),
            StringComparer.Ordinal));
    }

    private GraphValue AdaptMap(IDictionary map)
    {
        var values = new Dictionary<string, GraphValue>(StringComparer.Ordinal);
        foreach (DictionaryEntry entry in map)
        {
            if (entry.Key is not string key)
            {
                throw new GraphException("AGE result maps must use string keys.");
            }

            values.Add(key, key == "ComplexProperties"
                ? FlattenLists(AdaptValue(entry.Value))
                : AdaptValue(entry.Value));
        }

        return GraphValue.Map(values);
    }

    private static GraphValue FlattenLists(GraphValue value)
    {
        if (value.Kind != GraphValueKind.List)
        {
            return value;
        }

        var items = new List<GraphValue>();
        foreach (var item in value.Items)
        {
            if (item.Kind == GraphValueKind.List)
            {
                items.AddRange(FlattenLists(item).Items);
            }
            else
            {
                items.Add(item);
            }
        }

        return GraphValue.List(items);
    }

    private GraphValue AdaptJson(JsonElement value)
    {
        return value.ValueKind switch
        {
            JsonValueKind.Null => GraphValue.Scalar(null),
            JsonValueKind.String => GraphValue.Scalar(value.GetString()),
            JsonValueKind.True => GraphValue.Scalar(true),
            JsonValueKind.False => GraphValue.Scalar(false),
            JsonValueKind.Number when value.TryGetInt64(out var integer) => GraphValue.Scalar(integer),
            JsonValueKind.Number when value.TryGetDecimal(out var number) => GraphValue.Scalar(number),
            JsonValueKind.Number => GraphValue.Scalar(value.GetDouble()),
            JsonValueKind.Array => GraphValue.List(value.EnumerateArray().Select(AdaptJson).ToArray()),
            JsonValueKind.Object => AdaptJsonObject(value),
            _ => throw new GraphException($"Unsupported AGE JSON value kind '{value.ValueKind}'."),
        };
    }

    private GraphValue AdaptJsonObject(JsonElement value)
    {
        if (value.TryGetProperty("$type", out var typeProperty))
        {
            return typeProperty.GetString() switch
            {
                "vertex" => AdaptJsonVertex(value),
                "edge" => AdaptJsonEdge(value),
                "path" => GraphValue.Path(value.GetProperty("segments").EnumerateArray().Select(AdaptJson).ToArray()),
                _ => throw new GraphException("AGE returned an unsupported annotated agtype object."),
            };
        }

        if (value.TryGetProperty(nameof(Point.Latitude), out var latitude) &&
            value.TryGetProperty(nameof(Point.Longitude), out var longitude))
        {
            return GraphValue.Scalar(new Point
            {
                Latitude = latitude.GetDouble(),
                Longitude = longitude.GetDouble(),
                Height = value.TryGetProperty(nameof(Point.Height), out var height) ? height.GetDouble() : 0,
            });
        }

        return GraphValue.Map(value.EnumerateObject().ToDictionary(
            property => property.Name,
            property => property.Name == "ComplexProperties"
                ? FlattenLists(AdaptJson(property.Value))
                : AdaptJson(property.Value),
            StringComparer.Ordinal));
    }

    private GraphValue AdaptJsonVertex(JsonElement value)
    {
        var propertiesElement = value.GetProperty("properties");
        var physicalLabel = value.GetProperty("label").GetString()!;
        var isComplexValue = string.Equals(
            physicalLabel,
            SerializationBridge.ComplexNodeLabel,
            StringComparison.Ordinal) &&
            IsTrue(propertiesElement, ComplexPropertyStorage.NodeMarkerProperty);
        var hasHierarchy = propertiesElement.TryGetProperty("inheritance_labels", out var hierarchy);
        var labels = isComplexValue ||
            (SerializationBridge.IsEncodedRootStorageName(physicalLabel, relationship: false) && hasHierarchy)
                ? []
                : new List<string> { physicalLabel };
        if ((isComplexValue || !string.Equals(
                physicalLabel,
                SerializationBridge.ComplexNodeLabel,
                StringComparison.Ordinal)) && hasHierarchy)
        {
            labels.AddRange(ReadStrings(hierarchy));
        }

        var properties = propertiesElement.EnumerateObject()
            .Where(property => !IsInternalStorageProperty(property.Name))
            .ToDictionary(property => property.Name, property => AdaptJson(property.Value), StringComparer.Ordinal);
        return GraphValue.Node(JsonId(value.GetProperty("id")), labels.Distinct().ToArray(), properties);
    }

    private GraphValue AdaptJsonEdge(JsonElement value)
    {
        var propertiesElement = value.GetProperty("properties");
        var physicalType = value.GetProperty("label").GetString()!;
        var isComplexProperty = string.Equals(
            physicalType,
            SerializationBridge.ComplexRelationshipType,
            StringComparison.Ordinal) &&
            IsTrue(propertiesElement, ComplexPropertyStorage.RelationshipMarkerProperty);
        var relationshipType = (isComplexProperty ||
            SerializationBridge.IsEncodedRootStorageName(physicalType, relationship: true)) &&
            propertiesElement.TryGetProperty("inheritance_labels", out var hierarchy)
                ? ReadStrings(hierarchy).FirstOrDefault()
                : physicalType;
        var properties = propertiesElement.EnumerateObject()
            .Where(property => !IsInternalStorageProperty(property.Name))
            .ToDictionary(property => property.Name, property => AdaptJson(property.Value), StringComparer.Ordinal);
        return GraphValue.Relationship(
            JsonId(value.GetProperty("id")),
            relationshipType ?? string.Empty,
            JsonId(value.GetProperty("start_id")),
            JsonId(value.GetProperty("end_id")),
            properties);
    }

    private static string JsonId(JsonElement value) => value.ValueKind == JsonValueKind.String
        ? value.GetString()!
        : value.GetRawText();

    private static bool IsInternalStorageProperty(string name) =>
        name is "inheritance_labels" or
            ComplexPropertyStorage.NodeMarkerProperty or
            ComplexPropertyStorage.RelationshipMarkerProperty;

    private static bool IsTrue(Dictionary<string, object> properties, string name) =>
        properties.TryGetValue(name, out var value) && value switch
        {
            true => true,
            JsonElement { ValueKind: JsonValueKind.True } => true,
            _ => false,
        };

    private static bool IsTrue(JsonElement properties, string name) =>
        properties.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.True;

    private static bool TryAdaptPoint(
        Dictionary<string, object> values,
        out Point point)
    {
        point = default!;
        if (!values.TryGetValue(nameof(Point.Latitude), out var latitude) ||
            !values.TryGetValue(nameof(Point.Longitude), out var longitude))
        {
            return false;
        }

        point = new Point
        {
            Latitude = Convert.ToDouble(ScalarObject(latitude), CultureInfo.InvariantCulture),
            Longitude = Convert.ToDouble(ScalarObject(longitude), CultureInfo.InvariantCulture),
            Height = values.TryGetValue(nameof(Point.Height), out var height)
                ? Convert.ToDouble(ScalarObject(height), CultureInfo.InvariantCulture)
                : 0,
        };
        return true;
    }

    private static object? ScalarObject(object? value) => value is JsonElement json ? json.ValueKind switch
    {
        JsonValueKind.Number when json.TryGetDecimal(out var number) => number,
        JsonValueKind.String => json.GetString(),
        _ => json.GetRawText(),
    } : value;

    private static bool TryParseTemporal(string value, out DateTimeOffset temporal)
    {
        temporal = default;
        var timeSeparator = value.IndexOf('T', StringComparison.Ordinal);
        var hasOffset = value.EndsWith('Z') ||
            (timeSeparator >= 0 && (value.LastIndexOf('+') > timeSeparator || value.LastIndexOf('-') > timeSeparator));
        return hasOffset &&
            DateTimeOffset.TryParseExact(
                value,
                "O",
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out temporal);
    }

    private static object? ParseScalar(Agtype value)
    {
        var text = value.ToString().Trim();
        if (text.EndsWith("::numeric", StringComparison.Ordinal))
        {
            return decimal.Parse(text[..^9], NumberStyles.Float, CultureInfo.InvariantCulture);
        }

        if (text.StartsWith('"'))
        {
            var stringValue = value.GetString();
            return TryParseTemporal(stringValue, out var temporal) ? temporal : stringValue;
        }

        if (text is "true" or "false")
        {
            return text == "true";
        }

        if (long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var integer))
        {
            return integer;
        }

        if (decimal.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
        {
            return number;
        }

        return text switch
        {
            "NaN" => double.NaN,
            "Infinity" => double.PositiveInfinity,
            "-Infinity" => double.NegativeInfinity,
            "null" => null,
            _ => throw new GraphException("AGE returned an unsupported scalar value."),
        };
    }

    private static IEnumerable<string> ReadStrings(object? value)
    {
        return value switch
        {
            null => [],
            JsonElement { ValueKind: JsonValueKind.Array } json =>
                json.EnumerateArray().Select(item => item.GetString()).OfType<string>(),
            IEnumerable sequence when value is not string =>
                sequence.Cast<object?>().Select(item => Convert.ToString(item, CultureInfo.InvariantCulture))
                    .OfType<string>(),
            string text when text.StartsWith('[') => JsonSerializer.Deserialize<string[]>(text) ?? [],
            string text => [text],
            _ => [Convert.ToString(value, CultureInfo.InvariantCulture)!],
        };
    }
}
