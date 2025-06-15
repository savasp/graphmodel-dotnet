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

namespace Cvoya.Graph.Model.Neo4j.Querying.Cypher.Execution;

using System.Reflection;
using Cvoya.Graph.Model.Neo4j.Querying.Cypher.Serialization;
using Cvoya.Graph.Model.Serialization;
using global::Neo4j.Driver;
using Microsoft.Extensions.Logging;

/// <summary>
/// Processes Cypher query results and converts them to strongly-typed objects.
/// </summary>
internal sealed class CypherResultProcessor
{
    private readonly EntityFactory _entityFactory;
    private readonly ILogger<CypherResultProcessor> _logger;

    public CypherResultProcessor(EntityFactory entityFactory, ILogger<CypherResultProcessor> logger)
    {
        _entityFactory = entityFactory ?? throw new ArgumentNullException(nameof(entityFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Processes a single record and converts it to the target type.
    /// </summary>
    public T? ProcessRecord<T>(IRecord record)
    {
        if (record == null)
            return default;

        var targetType = typeof(T);

        // Single value result
        if (record.Values.Count == 1)
        {
            return ConvertValue<T>(record.Values.First().Value);
        }

        // Multiple values - check if it's an entity or projection
        if (typeof(INode).IsAssignableFrom(targetType) || typeof(IRelationship).IsAssignableFrom(targetType))
        {
            // For entities, we expect the first value to be the node/relationship
            return ConvertValue<T>(record.Values.First().Value);
        }

        // For projections (anonymous types or DTOs), use reflection
        return DeserializeProjection<T>(record);
    }

    /// <summary>
    /// Processes records that include complex properties.
    /// </summary>
    public T? ProcessRecordsWithComplexProperties<T>(IEnumerable<IRecord> records) where T : INode
    {
        var recordList = records.ToList();
        if (!recordList.Any())
            return default;

        // Get the main node from the first record
        var firstRecord = recordList.First();
        if (!firstRecord.TryGet("n", out var mainNodeValue) || mainNodeValue is not INode mainNode)
            return default;

        // Create EntityInfo for the main node
        var mainNodeInfo = CreateEntityInfoFromNode(mainNode, typeof(T));

        // Process complex properties from paths
        var complexProperties = new Dictionary<string, Property>();

        foreach (var record in recordList)
        {
            // Skip if no path
            if (!record.TryGet("path", out var pathValue) || pathValue is not IPath path)
                continue;

            // Get the relationship to determine property name
            var relationships = path.Relationships.ToList();
            if (relationships.Count == 0)
                continue;

            var relationship = relationships.First();
            var propertyName = GraphDataModel.RelationshipTypeNameToPropertyName(relationship.Type);

            // Get the complex property node
            if (path.Nodes.Count() < 2)
                continue;

            var complexNode = path.Nodes.Last();
            var complexValue = DeserializeComplexPropertyNode(complexNode);

            if (complexValue != null)
            {
                // Check if this property is a collection
                var schema = _entityFactory.GetSchema(typeof(T));
                var propInfo = schema.ComplexProperties.GetValueOrDefault(propertyName);

                if (propInfo != null && GraphDataModel.IsCollectionOfComplex(propInfo.Type))
                {
                    // Add to collection
                    if (!complexProperties.TryGetValue(propertyName, out var existing))
                    {
                        existing = new ComplexCollection(new List<object?>(), propInfo.Type);
                        complexProperties[propertyName] = existing;
                    }

                    if (existing is ComplexCollection collection)
                    {
                        ((List<object?>)collection.Values).Add(complexValue);
                    }
                }
                else
                {
                    // Single value
                    complexProperties[propertyName] = new ComplexValue(complexValue, complexValue.GetType());
                }
            }
        }

        // Merge complex properties into the main entity info
        var finalEntityInfo = new EntityInfo(
            Type: mainNodeInfo.Type,
            SimpleProperties: mainNodeInfo.SimpleProperties,
            ComplexProperties: complexProperties
        );

        return (T)_entityFactory.Deserialize(finalEntityInfo);
    }

    private T? ConvertValue<T>(object? value)
    {
        if (value == null)
            return default;

        var targetType = typeof(T);

        return value switch
        {
            INode node when typeof(INode).IsAssignableFrom(targetType) =>
                (T)DeserializeNode(node, targetType),

            IRelationship relationship when typeof(IRelationship).IsAssignableFrom(targetType) =>
                (T)DeserializeRelationship(relationship, targetType),

            _ => (T?)SerializationBridge.FromNeo4jValue(value, targetType)
        };
    }

    private object DeserializeNode(INode node, Type expectedType)
    {
        _logger.LogDebug("Deserializing node {ElementId} with labels {Labels}", node.ElementId, node.Labels);

        // Try to get the actual type from metadata
        var actualType = SerializationBridge.GetTypeFromMetadata(node.Properties) ?? expectedType;

        // Ensure the actual type is compatible with expected type
        if (!expectedType.IsAssignableFrom(actualType))
        {
            _logger.LogWarning(
                "Type mismatch: expected {Expected} but metadata indicates {Actual}. Using expected type.",
                expectedType.Name, actualType.Name);
            actualType = expectedType;
        }

        var entityInfo = CreateEntityInfoFromNode(node, actualType);
        return _entityFactory.Deserialize(entityInfo);
    }

    private object DeserializeRelationship(IRelationship relationship, Type expectedType)
    {
        _logger.LogDebug("Deserializing relationship {ElementId} of type {Type}", relationship.ElementId, relationship.Type);

        var actualType = SerializationBridge.GetTypeFromMetadata(relationship.Properties) ?? expectedType;

        if (!expectedType.IsAssignableFrom(actualType))
        {
            actualType = expectedType;
        }

        var entityInfo = CreateEntityInfoFromRelationship(relationship, actualType);
        var instance = _entityFactory.Deserialize(entityInfo);

        // Set tracking info if applicable
        if (instance is ITrackedRelationship trackedRel)
        {
            trackedRel.Id = new Neo4jElementId(relationship.ElementId);
            trackedRel.SourceId = new Neo4jElementId(relationship.StartNodeElementId);
            trackedRel.TargetId = new Neo4jElementId(relationship.EndNodeElementId);
        }

        return instance;
    }

    private EntityInfo CreateEntityInfoFromNode(INode node, Type type)
    {
        var schema = _entityFactory.GetSchema(type);
        var simpleProperties = new Dictionary<string, Property>();

        // Set the ID if it's a tracked entity
        if (typeof(ITrackedNode).IsAssignableFrom(type))
        {
            simpleProperties["Id"] = new SimpleValue(new Neo4jElementId(node.ElementId), typeof(Neo4jElementId));
        }

        // Extract simple properties
        foreach (var (key, value) in node.Properties)
        {
            if (key == SerializationBridge.MetadataPropertyName)
                continue;

            var propertyInfo = schema.SimpleProperties.GetValueOrDefault(key);
            if (propertyInfo != null)
            {
                var convertedValue = SerializationBridge.FromNeo4jValue(value, propertyInfo.Type);

                if (GraphDataModel.IsCollectionOfSimple(propertyInfo.Type))
                {
                    // It's a collection
                    simpleProperties[key] = new SimpleCollection(
                        convertedValue as IEnumerable<object?> ?? new List<object?>(),
                        propertyInfo.Type);
                }
                else
                {
                    // Single value
                    simpleProperties[key] = new SimpleValue(convertedValue, propertyInfo.Type);
                }
            }
        }

        return new EntityInfo(
            Type: type,
            SimpleProperties: simpleProperties,
            ComplexProperties: new Dictionary<string, Property>() // Complex properties handled separately
        );
    }

    private EntityInfo CreateEntityInfoFromRelationship(IRelationship relationship, Type type)
    {
        var schema = _entityFactory.GetSchema(type);
        var simpleProperties = new Dictionary<string, Property>();

        // Extract simple properties
        foreach (var (key, value) in relationship.Properties)
        {
            if (key == SerializationBridge.MetadataPropertyName)
                continue;

            var propertyInfo = schema.SimpleProperties.GetValueOrDefault(key);
            if (propertyInfo != null)
            {
                var convertedValue = SerializationBridge.FromNeo4jValue(value, propertyInfo.Type);

                if (GraphDataModel.IsCollectionOfSimple(propertyInfo.Type))
                {
                    simpleProperties[key] = new SimpleCollection(
                        convertedValue as IEnumerable<object?> ?? new List<object?>(),
                        propertyInfo.Type);
                }
                else
                {
                    simpleProperties[key] = new SimpleValue(convertedValue, propertyInfo.Type);
                }
            }
        }

        return new EntityInfo(
            Type: type,
            SimpleProperties: simpleProperties,
            ComplexProperties: new Dictionary<string, Property>()
        );
    }

    private object? DeserializeComplexPropertyNode(INode node)
    {
        try
        {
            // Get the type from metadata
            var type = SerializationBridge.GetTypeFromMetadata(node.Properties);
            if (type == null)
            {
                _logger.LogWarning("No type metadata found for complex property node");
                return null;
            }

            return DeserializeNode(node, type);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to deserialize complex property node");
            return null;
        }
    }

    private T DeserializeProjection<T>(IRecord record)
    {
        var targetType = typeof(T);

        // For anonymous types or projections, match constructor parameters
        var constructor = targetType.GetConstructors().FirstOrDefault();
        if (constructor == null)
            throw new InvalidOperationException($"No constructor found for type {targetType}");

        var parameters = constructor.GetParameters();
        var values = new object?[parameters.Length];

        for (int i = 0; i < parameters.Length; i++)
        {
            var param = parameters[i];

            // Try to find matching value by name
            if (record.TryGet(param.Name!, out var value))
            {
                values[i] = ConvertValueNonGeneric(value, param.ParameterType);
            }
            else if (i < record.Values.Count)
            {
                // Fall back to positional matching
                values[i] = ConvertValueNonGeneric(record.Values[i].Value, param.ParameterType);
            }
        }

        return (T)constructor.Invoke(values);
    }

    private object? ConvertValueNonGeneric(object? value, Type targetType)
    {
        var method = GetType()
            .GetMethod(nameof(ConvertValue), BindingFlags.NonPublic | BindingFlags.Instance)!
            .MakeGenericMethod(targetType);

        return method.Invoke(this, new[] { value });
    }
}