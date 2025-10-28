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

using System.Linq.Expressions;
using System.Reflection;
using Cvoya.Graph.Model.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Npgsql.Age.Types;

/// <summary>
/// Age-specific result processor that converts raw NpgsqlDataReader/Agtype results to EntityInfo structures.
/// This component handles the database-specific aspects of result processing, including projections,
/// path segments, scalar values, and complex properties, while delegating to shared materialization.
/// </summary>
internal sealed class AgeResultProcessor
{
    private readonly EntityFactory _entityFactory;
    private readonly AgeEntityMapper _entityMapper;
    private readonly ILogger<AgeResultProcessor> _logger;

    /// <summary>
    /// Initializes a new instance of the AgeResultProcessor with the specified dependencies.
    /// </summary>
    /// <param name="entityFactory">Factory for entity operations and type information</param>
    /// <param name="entityMapper">Mapper for converting AGE vertices/edges to EntityInfo</param>
    /// <param name="loggerFactory">Optional logger factory for debugging and diagnostics</param>
    public AgeResultProcessor(EntityFactory entityFactory, AgeEntityMapper entityMapper, ILoggerFactory? loggerFactory = null)
    {
        _entityFactory = entityFactory ?? throw new ArgumentNullException(nameof(entityFactory));
        _entityMapper = entityMapper ?? throw new ArgumentNullException(nameof(entityMapper));
        _logger = loggerFactory?.CreateLogger<AgeResultProcessor>() ?? NullLogger<AgeResultProcessor>.Instance;
    }

    /// <summary>
    /// Processes NpgsqlDataReader results into EntityInfo structures for the shared materialization pipeline.
    /// Handles various result types including entities, projections, path segments, and scalar values.
    /// </summary>
    /// <param name="reader">The NpgsqlDataReader containing Agtype results</param>
    /// <param name="elementType">The target element type for materialization</param>
    /// <param name="cancellationToken">Cancellation token for async operations</param>
    /// <param name="projectionExpression">Optional projection expression for projected queries</param>
    /// <param name="projectionResultType">Optional result type for projections</param>
    /// <param name="aggregationType">Optional aggregation type for handling empty set behavior</param>
    /// <returns>List of EntityInfo objects ready for shared materialization</returns>
    public async Task<List<EntityInfo>> ProcessAsync(
        NpgsqlDataReader reader,
        Type elementType,
        CancellationToken cancellationToken,
        LambdaExpression? projectionExpression = null,
        Type? projectionResultType = null,
        string? aggregationType = null)
    {
        var results = new List<EntityInfo>();
        var hasProjection = projectionExpression != null;
        var complexProps = GetComplexProperties(elementType);
        var hasComplexProps = complexProps.Count > 0;

        _logger.LogDebug("ProcessAsync: ElementType={ElementType}, HasProjection={HasProjection}, ComplexProps={ComplexPropsCount}",
            elementType.Name, hasProjection, complexProps.Count);

        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            try
            {
                var entityInfo = await ProcessSingleRecord(reader, elementType, hasProjection, projectionExpression, 
                    projectionResultType, complexProps, hasComplexProps, aggregationType);
                
                if (entityInfo != null)
                {
                    results.Add(entityInfo);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing record for element type {ElementType}", elementType.Name);
                throw;
            }
        }

        _logger.LogDebug("ProcessAsync: Processed {ResultCount} records into EntityInfo objects", results.Count);
        return results;
    }

    /// <summary>
    /// Processes a single record from the data reader into an EntityInfo.
    /// </summary>
    private async Task<EntityInfo?> ProcessSingleRecord(
        NpgsqlDataReader reader,
        Type elementType,
        bool hasProjection,
        LambdaExpression? projectionExpression,
        Type? projectionResultType,
        List<PropertyInfo> complexProps,
        bool hasComplexProps,
        string? aggregationType = null)
    {
        await Task.CompletedTask; // Keep async signature for consistency

        // Handle projections first
        if (hasProjection && projectionExpression != null && projectionResultType != null)
        {
            return ProcessProjectionRecord(reader, projectionExpression, projectionResultType);
        }

        // Handle path segments (3 columns: src, rel, tgt)
        if (reader.FieldCount == 3 && IsPathSegmentType(elementType))
        {
            return ProcessPathSegmentRecord(reader, elementType);
        }

        // Handle scalar results (single column)
        if (reader.FieldCount == 1)
        {
            return ProcessScalarRecord(reader, elementType, aggregationType);
        }

        // Handle entity results (nodes/relationships)
        return ProcessEntityRecord(reader, elementType, complexProps, hasComplexProps);
    }

    /// <summary>
    /// Processes a projection record (Select with anonymous types or property selections).
    /// </summary>
    private EntityInfo ProcessProjectionRecord(NpgsqlDataReader reader, LambdaExpression projectionExpression, Type projectionResultType)
    {
        _logger.LogDebug("ProcessProjectionRecord: Processing projection for type {ProjectionType}", projectionResultType.Name);
        
        var body = projectionExpression.Body;
        var simpleProperties = new Dictionary<string, Property>(StringComparer.Ordinal);

        // Handle simple property projection: Select(p => p.FirstName)
        if (body is MemberExpression && reader.FieldCount == 1)
        {
            var agtype = reader.GetFieldValue<Agtype>(0);
            var value = ExtractScalarValue(agtype, projectionResultType);
            var propertyName = ((MemberExpression)body).Member.Name;
            
            simpleProperties[propertyName] = new Property(null!, propertyName, false, new SimpleValue(value ?? string.Empty, projectionResultType));
        }
        // Handle anonymous type projection: Select(p => new { p.FirstName, p.LastName })
        else if (body is NewExpression newExpr)
        {
            for (int i = 0; i < newExpr.Arguments.Count && i < reader.FieldCount; i++)
            {
                var agtype = reader.GetFieldValue<Agtype>(i);
                var member = newExpr.Members?[i];
                var propertyName = member?.Name ?? $"Field{i}";
                var propertyType = member switch
                {
                    PropertyInfo pi => pi.PropertyType,
                    FieldInfo fi => fi.FieldType,
                    _ => typeof(object)
                };
                
                var value = ExtractScalarValue(agtype, propertyType);
                simpleProperties[propertyName] = new Property(null!, propertyName, false, new SimpleValue(value ?? string.Empty, propertyType));
            }
        }

        return new EntityInfo(
            projectionResultType,
            "Projection",
            Array.Empty<string>(),
            simpleProperties,
            new Dictionary<string, Property>(StringComparer.Ordinal));
    }

    /// <summary>
    /// Processes a path segment record with three components: source node, relationship, target node.
    /// </summary>
    private EntityInfo ProcessPathSegmentRecord(NpgsqlDataReader reader, Type elementType)
    {
        _logger.LogDebug("ProcessPathSegmentRecord: Processing path segment for type {ElementType}", elementType.Name);

        var genericArgs = elementType.GetGenericArguments();
        if (genericArgs.Length != 3)
        {
            throw new InvalidOperationException($"Path segment type {elementType.Name} must have exactly 3 generic arguments");
        }

        var sourceType = genericArgs[0];
        var relationshipType = genericArgs[1]; 
        var targetType = genericArgs[2];

        // Read the three components
        var srcAgtype = reader.GetFieldValue<Agtype>(0);
        var relAgtype = reader.GetFieldValue<Agtype>(1);
        var tgtAgtype = reader.GetFieldValue<Agtype>(2);

        // Convert each component to EntityInfo
        var sourceEntityInfo = ConvertAgtypeToEntityInfo(srcAgtype, sourceType, "StartNode");
        var relationshipEntityInfo = ConvertAgtypeToEntityInfo(relAgtype, relationshipType, "Relationship");
        var targetEntityInfo = ConvertAgtypeToEntityInfo(tgtAgtype, targetType, "EndNode");

        // Create the path segment EntityInfo with the three components as complex properties
        var complexProperties = new Dictionary<string, Property>(StringComparer.Ordinal)
        {
            ["StartNode"] = new Property(null!, "StartNode", false, sourceEntityInfo),
            ["Relationship"] = new Property(null!, "Relationship", false, relationshipEntityInfo),
            ["EndNode"] = new Property(null!, "EndNode", false, targetEntityInfo)
        };

        return new EntityInfo(
            elementType,
            "PathSegment",
            Array.Empty<string>(),
            new Dictionary<string, Property>(StringComparer.Ordinal),
            complexProperties);
    }

    /// <summary>
    /// Processes a scalar record (single value like count, boolean, etc.).
    /// </summary>
    private EntityInfo? ProcessScalarRecord(NpgsqlDataReader reader, Type elementType, string? aggregationType = null)
    {
        _logger.LogDebug("ProcessScalarRecord: Processing scalar for type {ElementType}", elementType.Name);

        // Handle NULL aggregation results (empty sets)
        if (reader.IsDBNull(0))
        {
            _logger.LogDebug("ProcessScalarRecord: NULL result for aggregation type {AggregationType}", aggregationType ?? "unknown");
            
            // Handle different aggregation types with appropriate empty set behavior
            switch (aggregationType)
            {
                case "Average":
                    // Average of empty set should throw InvalidOperationException
                    throw new InvalidOperationException("Sequence contains no elements");
                    
                case "Min":
                case "Max":
                    // Min/Max of empty set should throw InvalidOperationException
                    throw new InvalidOperationException("Sequence contains no elements");
                    
                case "Sum":
                case "Count":
                    // Sum and Count of empty set should return 0
                    return CreateScalarEntityInfo(0, elementType);
                    
                default:
                    // For unknown aggregation types, return DBNull and let the value converter handle it
                    return CreateScalarEntityInfo(DBNull.Value, elementType);
            }
        }

        var agtype = reader.GetFieldValue<Agtype>(0);

        // If it's a vertex or edge in a scalar context, convert to entity
        if (agtype.IsVertex && typeof(INode).IsAssignableFrom(elementType))
        {
            var vertex = agtype.GetVertex();
            return _entityMapper.MapVertex(vertex, elementType);
        }

        if (agtype.IsEdge && typeof(IRelationship).IsAssignableFrom(elementType))
        {
            var edge = agtype.GetEdge();
            return _entityMapper.MapEdge(edge, elementType);
        }

        // Otherwise, it's a scalar value
        var scalarValue = ExtractScalarValue(agtype, elementType);
        return CreateScalarEntityInfo(scalarValue, elementType);
    }

    /// <summary>
    /// Processes an entity record (node or relationship with optional complex properties).
    /// </summary>
    private EntityInfo? ProcessEntityRecord(NpgsqlDataReader reader, Type elementType, List<PropertyInfo> complexProps, bool hasComplexProps)
    {
        _logger.LogDebug("ProcessEntityRecord: Processing entity for type {ElementType}", elementType.Name);

        if (typeof(INode).IsAssignableFrom(elementType))
        {
            return ProcessNodeRecord(reader, elementType, complexProps, hasComplexProps);
        }

        if (typeof(IRelationship).IsAssignableFrom(elementType))
        {
            return ProcessRelationshipRecord(reader, elementType);
        }

        _logger.LogWarning("ProcessEntityRecord: Unsupported element type {ElementType}", elementType.Name);
        return null;
    }

    /// <summary>
    /// Processes a node record with optional complex properties from additional columns.
    /// </summary>
    private EntityInfo ProcessNodeRecord(NpgsqlDataReader reader, Type elementType, List<PropertyInfo> complexProps, bool hasComplexProps)
    {
        // Read the main node (first column)
        var nodeAgtype = reader.GetFieldValue<Agtype>(0);
        
        if (!nodeAgtype.IsVertex)
        {
            throw new InvalidOperationException("Expected vertex but got different Agtype");
        }

        var vertex = nodeAgtype.GetVertex();
        var entityInfo = _entityMapper.MapVertex(vertex, elementType);

        // If we have complex properties, read them from additional columns
        if (hasComplexProps)
        {
            var complexPropertyDict = new Dictionary<string, Property>(StringComparer.Ordinal);

            for (int i = 0; i < complexProps.Count; i++)
            {
                var prop = complexProps[i];
                var columnIndex = i + 1; // +1 because first column is the node
                
                if (columnIndex >= reader.FieldCount)
                {
                    _logger.LogDebug("ProcessNodeRecord: Column index {ColumnIndex} exceeds field count {FieldCount}", columnIndex, reader.FieldCount);
                    break;
                }

                try
                {
                    if (reader.IsDBNull(columnIndex))
                    {
                        // NULL complex property (e.g., OPTIONAL MATCH that didn't find a match)
                        continue;
                    }

                    var propAgtype = reader.GetFieldValue<Agtype>(columnIndex);
                    
                    // Check if it's a vertex (complex property node)
                    if (propAgtype.IsVertex)
                    {
                        var complexVertex = propAgtype.GetVertex();
                        var complexEntityInfo = _entityMapper.MapVertex(complexVertex, prop.PropertyType);
                        
                        // Single complex property
                        complexPropertyDict[prop.Name] = new Property(prop, prop.Name, false, complexEntityInfo);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "ProcessNodeRecord: Failed to read complex property {PropertyName} from column {ColumnIndex}", prop.Name, columnIndex);
                    // If reading complex property fails, just skip it
                    // This is expected for OPTIONAL MATCH that doesn't find a match
                    continue;
                }
            }

            // Merge complex properties into entity info
            if (complexPropertyDict.Count > 0)
            {
                var allComplexProps = new Dictionary<string, Property>(entityInfo.ComplexProperties, StringComparer.Ordinal);
                foreach (var kvp in complexPropertyDict)
                {
                    allComplexProps[kvp.Key] = kvp.Value;
                }

                entityInfo = new EntityInfo(
                    entityInfo.ActualType,
                    entityInfo.Label,
                    entityInfo.ActualLabels,
                    entityInfo.SimpleProperties,
                    allComplexProps);
            }
        }

        return entityInfo;
    }

    /// <summary>
    /// Processes a relationship record.
    /// </summary>
    private EntityInfo ProcessRelationshipRecord(NpgsqlDataReader reader, Type elementType)
    {
        var agtype = reader.GetFieldValue<Agtype>(0);
        
        if (!agtype.IsEdge)
        {
            throw new InvalidOperationException("Expected edge but got different Agtype");
        }

        var edge = agtype.GetEdge();
        return _entityMapper.MapEdge(edge, elementType);
    }

    /// <summary>
    /// Converts an Agtype value to EntityInfo based on its content type.
    /// </summary>
    private EntityInfo ConvertAgtypeToEntityInfo(Agtype agtype, Type targetType, string label)
    {
        if (agtype.IsVertex)
        {
            var vertex = agtype.GetVertex();
            return _entityMapper.MapVertex(vertex, targetType);
        }

        if (agtype.IsEdge)
        {
            var edge = agtype.GetEdge();
            return _entityMapper.MapEdge(edge, targetType);
        }

        // For scalar values, create a simple EntityInfo
        var value = ExtractScalarValue(agtype, targetType);
        var simpleProperties = new Dictionary<string, Property>(StringComparer.Ordinal)
        {
            ["Value"] = new Property(null!, "Value", false, new SimpleValue(value ?? string.Empty, targetType))
        };

        return new EntityInfo(
            targetType,
            label,
            Array.Empty<string>(),
            simpleProperties,
            new Dictionary<string, Property>(StringComparer.Ordinal));
    }

    /// <summary>
    /// Creates an EntityInfo for scalar values.
    /// </summary>
    private static EntityInfo CreateScalarEntityInfo(object? value, Type elementType)
    {
        var simpleProperties = new Dictionary<string, Property>(StringComparer.Ordinal)
        {
            ["Value"] = new Property(null!, "Value", value == null || value == DBNull.Value, new SimpleValue(value ?? DBNull.Value, elementType))
        };

        return new EntityInfo(
            elementType,
            "Scalar",
            Array.Empty<string>(),
            simpleProperties,
            new Dictionary<string, Property>(StringComparer.Ordinal));
    }

    /// <summary>
    /// Extracts a scalar value from an Agtype, converting it to the target type.
    /// </summary>
    private static object? ExtractScalarValue(Agtype agtype, Type targetType)
    {
        try
        {
            // Try as the target type first for optimal conversion
            if (targetType == typeof(long) || targetType == typeof(long?))
            {
                return (long)agtype;
            }

            if (targetType == typeof(int) || targetType == typeof(int?))
            {
                var longValue = (long)agtype;
                return (int)longValue;
            }

            if (targetType == typeof(bool) || targetType == typeof(bool?))
            {
                // AGE returns booleans as "true" or "false" strings
                var strValue = (string)agtype;
                return strValue == "true";
            }

            if (targetType == typeof(string))
            {
                return (string)agtype;
            }

            if (targetType == typeof(double) || targetType == typeof(double?))
            {
                return (double)agtype;
            }

            // Fallback: try the most common conversions
            try
            {
                return (long)agtype;
            }
            catch
            {
                try
                {
                    var strValue = (string)agtype;
                    
                    // Handle boolean strings
                    if (strValue == "true") return true;
                    if (strValue == "false") return false;
                    
                    return strValue;
                }
                catch
                {
                    // Last resort: return the agtype itself
                    return agtype;
                }
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to extract scalar value from Agtype for target type {targetType.Name}", ex);
        }
    }

    /// <summary>
    /// Gets complex properties (those with complex types) from the element type.
    /// </summary>
    private List<PropertyInfo> GetComplexProperties(Type elementType)
    {
        return elementType.GetProperties()
            .Where(p => !GraphDataModel.IsSimple(p.PropertyType))
            .ToList();
    }

    /// <summary>
    /// Determines if the type represents a path segment.
    /// </summary>
    private static bool IsPathSegmentType(Type type)
    {
        return type.IsGenericType && 
               (type.GetGenericTypeDefinition() == typeof(IGraphPathSegment<,,>) ||
                type.GetGenericTypeDefinition().Name.Contains("IGraphPathSegment"));
    }
}