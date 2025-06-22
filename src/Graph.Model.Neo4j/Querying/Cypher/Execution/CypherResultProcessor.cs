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

using Cvoya.Graph.Model.Neo4j.Querying.Linq.Queryables;
using Cvoya.Graph.Model.Neo4j.Serialization;
using Cvoya.Graph.Model.Serialization;
using global::Neo4j.Driver;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

internal sealed class CypherResultProcessor
{
    private readonly EntityFactory _entityFactory;
    private readonly ILogger<CypherResultProcessor> _logger;

    private record ComplexProperty(
        INode ParentNode,
        IRelationship Relationship,
        INode Property);
    private record NodeResult(INode Node, List<ComplexProperty> ComplexProperties);
    private record PathSegmentResult(
        NodeResult StartNode,
        IRelationship Relationship,
        NodeResult EndNode);

    public CypherResultProcessor(EntityFactory entityFactory, ILoggerFactory? loggerFactory = null)
    {
        _entityFactory = entityFactory ?? throw new ArgumentNullException(nameof(entityFactory));
        _logger = loggerFactory?.CreateLogger<CypherResultProcessor>() ?? NullLogger<CypherResultProcessor>.Instance;
    }

    public Task<List<EntityInfo>> ProcessAsync(
        List<IRecord> records,
        Type targetType,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Processing records for target type: {TargetType}", targetType.Name);

        if (cancellationToken.IsCancellationRequested)
        {
            _logger.LogDebug("Processing cancelled.");
            return Task.FromResult(new List<EntityInfo>());
        }

        // Handle path segments specially
        if (targetType.IsGenericType && targetType.GetGenericTypeDefinition() == typeof(IGraphPathSegment<,,>))
        {
            return Task.FromResult(ProcessPathSegments(records, targetType));
        }

        // Handle nodes
        if (typeof(Model.INode).IsAssignableFrom(targetType))
        {
            return Task.FromResult(ProcessNodes(records, targetType));
        }

        // Handle relationships
        if (typeof(Model.IRelationship).IsAssignableFrom(targetType))
        {
            return Task.FromResult(ProcessRelationships(records, targetType));
        }

        // Handle projections (EntityInfo)
        return Task.FromResult(ProcessProjections(records, targetType));
    }

    private PathSegmentResult? DeserializePathSegment(IReadOnlyDictionary<string, object> pathSegmentRecord)
    {
        // Extract the relevant properties from the dictionary
        if (!pathSegmentRecord.TryGetValue("StartNode", out var startNodeObj) ||
            !pathSegmentRecord.TryGetValue("Relationship", out var relationshipObj) ||
            !pathSegmentRecord.TryGetValue("EndNode", out var endNodeObj))
        {
            return null;
        }

        var startNode = DeserializeNode(startNodeObj.As<Dictionary<string, object>>())
                ?? throw new GraphException("Failed to deserialize start node from path segment record.");
        var relationship = relationshipObj.As<IRelationship>()
            ?? throw new GraphException("Failed to deserialize relationship from path segment record.");
        var endNode = DeserializeNode(endNodeObj.As<Dictionary<string, object>>())
            ?? throw new GraphException("Failed to deserialize end node from path segment record.");

        return new PathSegmentResult(startNode, relationship, endNode);
    }

    private static NodeResult? DeserializeNode(IReadOnlyDictionary<string, object> nodeRecord)
    {
        // Extract the relevant properties from the dictionary
        if (!nodeRecord.TryGetValue("Node", out var nodeObj) ||
            !nodeRecord.TryGetValue("ComplexProperties", out var complexPropsObj))
        {
            return null;
        }

        var node = nodeObj.As<INode>();
        var list = complexPropsObj as List<object> ?? [];
        var complexProperties = list
            .OfType<Dictionary<string, object>>()
            .Select(dict => new ComplexProperty(
                ParentNode: dict["ParentNode"].As<INode>(),
                Relationship: dict["Relationship"].As<IRelationship>(),
                Property: dict["Property"].As<INode>()
        )).ToList();

        return new NodeResult(node, complexProperties);
    }

    private List<EntityInfo> ProcessPathSegments(List<IRecord> records, Type pathSegmentType)
    {
        var results = new List<EntityInfo>();
        var genericArgs = pathSegmentType.GetGenericArguments();
        var sourceType = genericArgs[0];  // Person
        var relType = genericArgs[1];     // WorksFor
        var targetType = genericArgs[2];  // Company

        foreach (var record in records)
        {
            var pathSegment = DeserializePathSegment(record["PathSegment"].As<Dictionary<string, object>>())
                ?? throw new GraphException("Failed to deserialize path segment from record.");

            // Process each component
            var startNodeEntityInfo = ProcessSingleNodeResult(pathSegment.StartNode, sourceType);
            var relEntityInfo = ProcessSingleRelationshipFromPathSegment(
                pathSegment.Relationship, relType,
                GetNodeId(pathSegment.StartNode.Node),
                GetNodeId(pathSegment.EndNode.Node));
            var endNodeEntityInfo = ProcessSingleNodeResult(pathSegment.EndNode, targetType);

            // Create the composite path segment EntityInfo
            var pathSegmentEntityInfo = CreatePathSegmentEntityInfo(
                startNodeEntityInfo, relEntityInfo, endNodeEntityInfo, pathSegmentType);

            results.Add(pathSegmentEntityInfo);
        }

        return results;
    }

    private List<EntityInfo> ProcessRelationships(List<IRecord> records, Type targetType)
    {
        var results = new List<EntityInfo>();

        foreach (var record in records)
        {
            // Both old and new formats now go through the same path segment deserialization
            if (record.Keys.Contains("PathSegment"))
            {
                var pathSegment = DeserializePathSegment(record["PathSegment"].As<Dictionary<string, object>>())
                    ?? throw new GraphException("Failed to deserialize relationship from record.");

                // For relationships, we only care about the relationship part
                var relationshipEntityInfo = ProcessSingleRelationshipFromPathSegment(
                    pathSegment.Relationship,
                    targetType,
                    pathSegment.StartNode.Node.Properties[nameof(Model.IEntity.Id)].As<string>(),
                    pathSegment.EndNode.Node.Properties[nameof(Model.IEntity.Id)].As<string>());

                results.Add(relationshipEntityInfo);
            }
            else
            {
                // Fallback for any old format queries that might still be around
                throw new GraphException("Legacy relationship format no longer supported. Please use the unified path segment format.");
            }
        }

        return results;
    }

    private EntityInfo ProcessSingleNodeResult(NodeResult nodeResult, Type targetType)
    {
        // Use the new recursive deserializer for complex properties
        return DeserializeComplexPropertiesForNode(nodeResult.Node, nodeResult.ComplexProperties, targetType);
    }

    /// <summary>
    /// Recursively reconstructs the object graph for a node and its complex properties.
    /// </summary>
    private EntityInfo DeserializeComplexPropertiesForNode(
        INode node,
        List<ComplexProperty> allComplexProperties,
        Type nodeType)
    {
        // Create the base entity info for this node
        var entityInfo = CreateEntityInfoFromNode(node, nodeType);

        // Find all complex properties where this node is the parent
        var directComplexProps = allComplexProperties
            .Where(cp => cp.ParentNode.ElementId == node.ElementId)
            .ToList();

        if (directComplexProps.Count == 0)
            return entityInfo;

        var schema = _entityFactory.GetSchema(nodeType);
        if (schema?.ComplexProperties == null)
            return entityInfo;

        foreach (var (propertyName, propertySchema) in schema.ComplexProperties)
        {
            var expectedRelType = GraphDataModel.PropertyNameToRelationshipTypeName(propertyName);

            // Find all complex properties for this property
            var matchingProps = directComplexProps
                .Where(cp => cp.Relationship.Type == expectedRelType)
                .ToList();

            if (matchingProps.Count == 0)
                continue;

            var childType = propertySchema.PropertyInfo.PropertyType.IsGenericType
                ? propertySchema.PropertyInfo.PropertyType.GetGenericArguments()[0]
                : propertySchema.PropertyInfo.PropertyType;

            if (propertySchema.PropertyType == PropertyType.ComplexCollection)
            {
                var children = matchingProps
                    .Select(cp => DeserializeComplexPropertiesForNode(cp.Property, allComplexProperties, childType))
                    .ToList();

                entityInfo.ComplexProperties[propertyName] = new Property(
                    propertySchema.PropertyInfo,
                    propertySchema.Neo4jPropertyName,
                    propertySchema.IsNullable,
                    new EntityCollection(propertySchema.ElementType!, children));
            }
            else if (propertySchema.PropertyType == PropertyType.Complex)
            {
                var child = DeserializeComplexPropertiesForNode(matchingProps[0].Property, allComplexProperties, childType);

                entityInfo.ComplexProperties[propertyName] = new Property(
                    propertySchema.PropertyInfo,
                    propertySchema.Neo4jPropertyName,
                    propertySchema.IsNullable,
                    child);
            }
        }

        return entityInfo;
    }

    private EntityInfo ProcessSingleRelationshipFromPathSegment(
        IRelationship relationship,
        Type targetType,
        string startNodeId,
        string endNodeId)
    {
        var entityInfo = CreateEntityInfoFromRelationship(relationship, targetType);
        EnhanceRelationshipEntityInfo(entityInfo, targetType, startNodeId, endNodeId);
        return entityInfo;
    }

    private List<EntityInfo> ProcessProjections(List<IRecord> records, Type targetType)
    {
        var results = new List<EntityInfo>();

        foreach (var record in records)
        {
            var entityInfo = CreateEntityInfoFromProjection(record, targetType);
            results.Add(entityInfo);
        }

        return results;
    }

    private EntityInfo CreateEntityInfoFromProjection(IRecord record, Type targetType)
    {
        var simpleProperties = new Dictionary<string, Property>();

        // Extract all values from the record as simple properties
        foreach (var key in record.Keys)
        {
            var value = record[key];
            // Do all the value conversion here once, not again in the materializer
            var convertedValue = SerializationBridge.FromNeo4jValue(value, typeof(object))
                ?? throw new InvalidOperationException($"Failed to convert value for property '{key}'");

            // Create a simple property info (we don't have real PropertyInfo for projections)
            simpleProperties[key] = new Property(
                PropertyInfo: null!, // We'll handle this differently for projections
                Label: key,
                IsNullable: true, // Assume nullable for projections
                Value: new SimpleValue(convertedValue, convertedValue.GetType())
            );
        }

        return new EntityInfo(
            ActualType: targetType,
            Label: targetType.Name,
            SimpleProperties: simpleProperties,
            ComplexProperties: new Dictionary<string, Property>()
        );
    }

    private void EnhanceRelationshipEntityInfo(EntityInfo entityInfo, Type targetType, string startNodeId, string endNodeId)
    {
        // Add StartNodeId as a simple property
        if (targetType.GetProperty(nameof(Model.IRelationship.StartNodeId)) != null)
        {
            entityInfo.SimpleProperties[nameof(Model.IRelationship.StartNodeId)] = new Property(
                PropertyInfo: targetType.GetProperty(nameof(Model.IRelationship.StartNodeId))!,
                Label: nameof(Model.IRelationship.StartNodeId),
                IsNullable: false,
                Value: new SimpleValue(startNodeId, typeof(string))
            );
        }

        // Add EndNodeId as a simple property  
        if (targetType.GetProperty(nameof(Model.IRelationship.EndNodeId)) != null)
        {
            entityInfo.SimpleProperties[nameof(Model.IRelationship.EndNodeId)] = new Property(
                PropertyInfo: targetType.GetProperty(nameof(Model.IRelationship.EndNodeId))!,
                Label: nameof(Model.IRelationship.EndNodeId),
                IsNullable: false,
                Value: new SimpleValue(endNodeId, typeof(string))
            );
        }

        // Add Direction
        if (targetType.GetProperty(nameof(Model.IRelationship.Direction)) != null)
        {
            var direction = GetRelationshipDirection(targetType);
            entityInfo.SimpleProperties[nameof(Model.IRelationship.Direction)] = new Property(
                PropertyInfo: targetType.GetProperty(nameof(Model.IRelationship.Direction))!,
                Label: nameof(Model.IRelationship.Direction),
                IsNullable: false,
                Value: new SimpleValue(direction, typeof(RelationshipDirection))
            );
        }
    }

    private static string GetNodeId(INode node)
    {
        // TODO: Throughout this code, we use nameof(<interface>.Id) where <interface> is IEntity, IRelationship, INode.
        // This is wrong. We should be using the label instead.

        // Try to get the Id property from the node
        if (node.Properties.TryGetValue(nameof(Model.IEntity.Id), out var idValue))
        {
            return idValue.As<string>();
        }

        // Fallback to ElementId if no Id property
        return node.ElementId;
    }

    private static RelationshipDirection GetRelationshipDirection(Type targetType)
    {
        // TODO: We are treating all relationships as outgoing by default for now. We may need to fix this.
        return RelationshipDirection.Outgoing;
    }

    private EntityInfo CreatePathSegmentEntityInfo(
        EntityInfo sourceEntity,
        EntityInfo relEntity,
        EntityInfo targetEntity,
        Type pathSegmentType)
    {
        // Store the three components as properties in the path segment EntityInfo
        var complexProperties = new Dictionary<string, Property>
        {
            [nameof(IGraphPathSegment.StartNode)] = new Property(
                PropertyInfo: null!,
                Label: nameof(IGraphPathSegment.StartNode),
                IsNullable: false,
                Value: sourceEntity
            ),
            [nameof(IGraphPathSegment.Relationship)] = new Property(
                PropertyInfo: null!,
                Label: nameof(IGraphPathSegment.Relationship),
                IsNullable: false,
                Value: relEntity
            ),
            [nameof(IGraphPathSegment.EndNode)] = new Property(
                PropertyInfo: null!,
                Label: nameof(IGraphPathSegment.EndNode),
                IsNullable: false,
                Value: targetEntity
            )
        };

        return new EntityInfo(
            ActualType: pathSegmentType,
            Label: typeof(GraphPathSegment<,,>).Name,
            SimpleProperties: new Dictionary<string, Property>(),
            ComplexProperties: complexProperties
        );
    }

    private List<EntityInfo> ProcessNodes(List<IRecord> records, Type targetType)
    {
        var results = new List<EntityInfo>();

        foreach (var record in records)
        {
            NodeResult? nodeResult = null;

            // Check if this is the new structured format
            if (record.Keys.Contains("Node"))
            {
                nodeResult = DeserializeNode(record["Node"].As<Dictionary<string, object>>())
                    ?? throw new GraphException("Failed to deserialize node from structured record.");
            }
            else
            {
                throw new GraphException("Unable to find node data in record.");
            }

            var entityInfo = ProcessSingleNodeResult(nodeResult, targetType);
            results.Add(entityInfo);
        }

        return results;
    }

    private EntityInfo CreateEntityInfoFromNode(INode node, Type actualType)
    {
        var label = node.Labels.FirstOrDefault() ?? actualType.Name;
        var simpleProperties = ExtractSimpleProperties(node.Properties, actualType);

        // Add ElementId as a system property if the type has an Id property
        if (actualType.GetProperty(nameof(Model.IEntity.Id)) != null)
        {
            if (node.Properties.TryGetValue("Id", out var idValue))
            {
                simpleProperties[nameof(Model.IEntity.Id)] = new Property(
                    PropertyInfo: actualType.GetProperty(nameof(Model.IEntity.Id))!,
                    Label: nameof(Model.IEntity.Id),
                    IsNullable: false,
                    Value: new SimpleValue(idValue, typeof(string))
                );
            }
        }

        return new EntityInfo(
            ActualType: actualType,
            Label: label,
            SimpleProperties: simpleProperties,
            ComplexProperties: new Dictionary<string, Property>()
        );
    }

    private EntityInfo CreateEntityInfoFromRelationship(IRelationship relationship, Type actualType)
    {
        var label = relationship.Type ?? actualType.Name;
        var simpleProperties = ExtractSimpleProperties(relationship.Properties, actualType);

        // Add ElementId as the Id property
        if (actualType.GetProperty(nameof(Model.IEntity.Id)) != null)
        {
            simpleProperties[nameof(Model.IEntity.Id)] = new Property(
                PropertyInfo: actualType.GetProperty(nameof(Model.IEntity.Id))!,
                Label: nameof(Model.IEntity.Id),
                IsNullable: false,
                Value: new SimpleValue(relationship.Properties[nameof(Model.IEntity.Id)], typeof(string))
            );
        }

        return new EntityInfo(
            ActualType: actualType,
            Label: label,
            SimpleProperties: simpleProperties,
            ComplexProperties: new Dictionary<string, Property>()
        );
    }

    private Dictionary<string, Property> ExtractSimpleProperties(
        IReadOnlyDictionary<string, object> properties,
        Type entityType)
    {
        var result = new Dictionary<string, Property>();

        if (!_entityFactory.CanDeserialize(entityType))
            return result;

        var schema = _entityFactory.GetSchema(entityType);
        if (schema?.SimpleProperties == null)
            return result;

        foreach (var (key, value) in properties)
        {
            // Skip metadata properties
            if (key == SerializationBridge.MetadataPropertyName)
                continue;

            // Check if this property is in the schema
            if (schema.SimpleProperties.TryGetValue(key, out var propertySchema))
            {
                var convertedValue = SerializationBridge.FromNeo4jValue(value, propertySchema.PropertyInfo.PropertyType)
                    ?? throw new InvalidOperationException($"Failed to convert value for property '{key}' of type '{propertySchema.PropertyInfo.PropertyType}'");

                Property property = new(
                    propertySchema.PropertyInfo,
                    propertySchema.Neo4jPropertyName,
                    propertySchema.IsNullable,
                    propertySchema.PropertyType == PropertyType.SimpleCollection
                        ? CreateSimpleCollection(convertedValue, propertySchema.ElementType!)
                        : new SimpleValue(convertedValue, propertySchema.PropertyInfo.PropertyType));

                result[key] = property;
            }
        }

        return result;
    }

    private SimpleCollection CreateSimpleCollection(object convertedValue, Type elementType)
    {
        var items = new List<SimpleValue>();
        if (convertedValue is IEnumerable<object> enumerable)
        {
            foreach (var item in enumerable)
            {
                items.Add(new SimpleValue(item, elementType));
            }
        }

        return new SimpleCollection(items, elementType);
    }
}

