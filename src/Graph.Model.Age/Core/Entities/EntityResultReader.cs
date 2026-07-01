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

using Cvoya.Graph.Model.Serialization;
using Microsoft.Extensions.Logging;
using Npgsql;
using Npgsql.Age.Types;
using static Cvoya.Graph.Model.Age.Core.Entities.AgeValueConverters;

/// <summary>
/// Reads multi-column rows from an NpgsqlDataReader and builds EntityInfo structures.
/// Supports projections, path segments, and complex property deserialization.
/// </summary>
internal sealed class EntityResultReader
{
    private readonly EntityFactory _entityFactory;
    private readonly AgeEntityMapper _entityMapper;
    private readonly ILogger _logger;

    public EntityResultReader(EntityFactory entityFactory, AgeEntityMapper entityMapper, ILogger logger)
    {
        _entityFactory = entityFactory ?? throw new ArgumentNullException(nameof(entityFactory));
        _entityMapper = entityMapper ?? throw new ArgumentNullException(nameof(entityMapper));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Reads a multi-column row from the reader and builds an EntityInfo with simple properties
    /// matching the column names. This supports anonymous type projections like
    /// .Select(p => new { p.Name, p.Age }) where the RETURN clause produces multiple columns.
    /// </summary>
    public async Task<EntityInfo?> ReadMultiColumnRowAsync(
        NpgsqlDataReader reader,
        Type elementType,
        CancellationToken cancellationToken)
    {
        var simpleProps = new Dictionary<string, Property>(StringComparer.Ordinal);
        var complexProps = new Dictionary<string, Property>(StringComparer.Ordinal);
        var fieldCount = reader.FieldCount;

        for (int i = 0; i < fieldCount; i++)
        {
            var columnName = reader.GetName(i);
            var propertyName = columnName.StartsWith("c_", StringComparison.Ordinal)
                ? columnName.Substring(2)
                : columnName;

            // Map path segment aliases to property names
            if (columnName is "src0" or "src1" or "src2" or "src3" or "src4")
                propertyName = "StartNode";
            else if (columnName is "r0" or "r1" or "r2" or "r3" or "r4")
                propertyName = "Relationship";
            else if (columnName is "tgt0" or "tgt1" or "tgt2" or "tgt3" or "tgt4")
                propertyName = "EndNode";

            if (await reader.IsDBNullAsync(i, cancellationToken).ConfigureAwait(false))
                continue;

            try
            {
                var agVal = reader.GetFieldValue<Agtype>(i);
                string agStr;
                try { agStr = agVal.GetString() ?? agVal.ToString()!; } catch { agStr = agVal.ToString() ?? "null"; }
                _logger.LogDebug("ReadMultiColumnRowAsync: Column {ColName} (prop {PropName}), IsVertex={IsV}, IsEdge={IsE}, AgStr={AgStr}",
                    columnName, propertyName, agVal.IsVertex, agVal.IsEdge,
                    agStr?.Substring(0, Math.Min(100, agStr.Length)));

                if (agVal.IsVertex)
                {
                    var vertex = agVal.GetVertex();
                    var nestedTarget = elementType.GetProperty(propertyName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.IgnoreCase)?.PropertyType ?? typeof(object);
                    var nestedEntityInfo = _entityMapper.MapVertex(vertex, nestedTarget);
                    complexProps[propertyName] = new Property(null!, propertyName, false, nestedEntityInfo);
                }
                else if (agVal.IsEdge)
                {
                    var edge = agVal.GetEdge();
                    var nestedTarget = elementType.GetProperty(propertyName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.IgnoreCase)?.PropertyType ?? typeof(object);
                    var nestedEntityInfo = _entityMapper.MapEdge(edge, nestedTarget);
                    complexProps[propertyName] = new Property(null!, propertyName, false, nestedEntityInfo);
                }
                else
                {
                    ProcessScalarColumn(agVal, propertyName, elementType, simpleProps);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "ReadMultiColumnRowAsync: Failed to read column {ColumnName} (property {PropertyName})", columnName, propertyName);
            }
        }

        // Group path segment columns (_src, _r, _tgt suffixes) into single complex properties
        GroupPathSegmentColumns(complexProps, elementType);

        if (simpleProps.Count == 0 && complexProps.Count == 0)
            return null;

        return new EntityInfo(
            elementType,
            string.Empty,
            Array.Empty<string>(),
            simpleProps,
            complexProps);
    }

    private void ProcessScalarColumn(Agtype agVal, string propertyName, Type elementType,
        Dictionary<string, Property> simpleProps)
    {
        var targetProp = elementType.GetProperty(propertyName,
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.IgnoreCase);
        var targetType = targetProp?.PropertyType ?? typeof(string);
        object? convertedValue = null;

        // Check for Agtype list/array (from collect() expressions)
        if (targetType.IsGenericType &&
            (targetType.GetGenericTypeDefinition() == typeof(List<>) ||
             targetType.GetGenericTypeDefinition() == typeof(IList<>) ||
             targetType.GetGenericTypeDefinition() == typeof(IReadOnlyList<>)))
        {
            convertedValue = ProcessListColumn(agVal, targetType, propertyName);
        }

        if (convertedValue == null)
            convertedValue = ReadTypedAgtypeValue(agVal, targetType);

        // If typed accessor didn't produce a value, try getting as string
        if (convertedValue == null)
        {
            try { convertedValue = agVal.GetString(); }
            catch { /* ignore */ }
        }

        // Try JSON deserialization for complex types
        if (convertedValue == null && targetType != null && targetType != typeof(string)
            && !GraphDataModel.IsSimple(targetType)
            && !typeof(System.Collections.IDictionary).IsAssignableFrom(targetType))
        {
            convertedValue = TryDeserializeComplexType(agVal, targetType, propertyName);
        }

        // Fallback to string conversion
        if (convertedValue == null && targetType != null)
            convertedValue = ConvertScalarAgtype(agVal.ToString() ?? string.Empty, targetType);
        else if (convertedValue == null)
            convertedValue = agVal.ToString();

        if (convertedValue != null)
            simpleProps[propertyName] = new Property(null!, propertyName, false,
                new SimpleValue(convertedValue, convertedValue.GetType()));
    }

    private object? ProcessListColumn(Agtype agVal, Type targetType, string propertyName)
    {
        var listElementType = targetType.GetGenericArguments()[0];
        try
        {
            var agList = agVal.GetList();
            if (agList != null && agList.Count > 0)
            {
                _logger.LogDebug("ReadMultiColumnRowAsync: Got list for {Prop} with {Count} elements",
                    propertyName, agList.Count);
                var typedList = new List<object?>();
                foreach (var rawElem in agList)
                {
                    object? elemObj = rawElem switch
                    {
                        null => null,
                        Vertex<Dictionary<string, object>> v => v,
                        Edge<Dictionary<string, object>> e => e,
                        Dictionary<string, object> dict => ConvertDictionaryToType(dict!, listElementType),
                        System.Text.Json.JsonElement jsonElem =>
                            ConvertJsonElementToEntityInfo(jsonElem, listElementType),
                        _ => rawElem
                    };
                    typedList.Add(elemObj);
                }
                return typedList;
            }
            else
            {
                var listType = typeof(List<>).MakeGenericType(listElementType);
                return Activator.CreateInstance(listType);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse Agtype list for property {Property}", propertyName);
        }
        return null;
    }

    private static object? ReadTypedAgtypeValue(Agtype agVal, Type targetType)
    {
        try
        {
            if (targetType == typeof(string)) return agVal.GetString();
            if (targetType == typeof(int) || targetType == typeof(int?)) return agVal.GetInt32();
            if (targetType == typeof(long) || targetType == typeof(long?)) return agVal.GetInt64();
            if (targetType == typeof(double) || targetType == typeof(double?)) return agVal.GetDouble();
            if (targetType == typeof(float) || targetType == typeof(float?)) return agVal.GetFloat();
            if (targetType == typeof(decimal) || targetType == typeof(decimal?)) return agVal.GetDecimal();
            if (targetType == typeof(bool) || targetType == typeof(bool?)) return agVal.GetBoolean();
            if (targetType == typeof(DateTime) || targetType == typeof(DateTime?))
            {
                var strVal = agVal.ToString()?.Trim('"', ' ', '\'');
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
        }
        catch { }
        return null;
    }

    private object? TryDeserializeComplexType(Agtype agVal, Type targetType, string propertyName)
    {
        try
        {
            var text = agVal.ToString();
            if (!string.IsNullOrEmpty(text) && text.TrimStart().StartsWith("{"))
            {
                var result = System.Text.Json.JsonSerializer.Deserialize(
                    text, targetType,
                    new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (result != null)
                {
                    _logger.LogDebug("Deserialized Agtype map to {Target} for property {Prop}",
                        targetType.Name, propertyName);
                    return result;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to deserialize complex property {Prop} to {Target}",
                propertyName, targetType.Name);
        }
        return null;
    }

    private static void GroupPathSegmentColumns(
        Dictionary<string, Property> complexProps,
        Type elementType)
    {
        var pathSegmentKeys = complexProps.Keys
            .Where(k => k.EndsWith("_src", StringComparison.Ordinal) ||
                        k.EndsWith("_r", StringComparison.Ordinal) ||
                        k.EndsWith("_tgt", StringComparison.Ordinal))
            .Select(k => k.Length > 4 ? k.Substring(0, k.Length - 4) : k)
            .Distinct()
            .ToList();

        foreach (var baseKey in pathSegmentKeys)
        {
            var srcKey = $"{baseKey}_src";
            var relKey = $"{baseKey}_r";
            var tgtKey = $"{baseKey}_tgt";

            if (complexProps.TryGetValue(srcKey, out var srcProp) &&
                complexProps.TryGetValue(tgtKey, out var tgtProp))
            {
                complexProps.Remove(srcKey);
                complexProps.Remove(tgtKey);

                var segmentSimpleProps = new Dictionary<string, Property>(StringComparer.Ordinal);
                var segmentComplexProps = new Dictionary<string, Property>(StringComparer.Ordinal);

                if (srcProp.Value is EntityInfo srcEntity)
                    segmentComplexProps["StartNode"] = new Property(null!, "StartNode", false, srcEntity);
                if (tgtProp.Value is EntityInfo tgtEntity)
                    segmentComplexProps["EndNode"] = new Property(null!, "EndNode", false, tgtEntity);

                if (complexProps.TryGetValue(relKey, out var relProp))
                {
                    complexProps.Remove(relKey);
                    if (relProp.Value is EntityInfo relEntity)
                        segmentComplexProps["Relationship"] = new Property(null!, "Relationship", false, relEntity);
                }

                var segmentTargetType = elementType.GetProperty(baseKey,
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.IgnoreCase)?.PropertyType
                    ?? typeof(object);

                var segmentEntityInfo = new EntityInfo(
                    segmentTargetType, string.Empty, Array.Empty<string>(),
                    segmentSimpleProps, segmentComplexProps);

                complexProps[baseKey] = new Property(null!, baseKey, false, segmentEntityInfo);
            }
        }
    }
}
