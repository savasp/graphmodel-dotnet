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

    public CypherResultProcessor(EntityFactory entityFactory, ILoggerFactory? loggerFactory = null)
    {
        _entityFactory = entityFactory ?? throw new ArgumentNullException(nameof(entityFactory));
        _logger = loggerFactory?.CreateLogger<CypherResultProcessor>() ?? NullLogger<CypherResultProcessor>.Instance;
    }

    public async Task<List<EntityInfo>> ProcessAsync(
        List<IRecord> records,
        Type targetType,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Processing records for target type: {TargetType}", targetType.Name);

        // Handle path segments specially
        if (targetType.IsGenericType && targetType.GetGenericTypeDefinition() == typeof(IGraphPathSegment<,,>))
        {
            return ProcessPathSegments(records, targetType);
        }

        // Handle regular entities
        if (typeof(Model.INode).IsAssignableFrom(targetType))
        {
            return await ProcessNodesAsync(records, targetType, cancellationToken);
        }

        if (typeof(Model.IRelationship).IsAssignableFrom(targetType))
        {
            return ProcessRelationships(records, targetType);
        }

        return ProcessProjections(records, targetType);
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
            // Extract values by their return column names
            var sourceNode = record["src"].As<INode>();  // The source node
            var relationship = record["r"].As<IRelationship>();  // The relationship
            var targetNode = record["tgt"].As<INode>();  // The target node

            // Process each component using existing logic
            var sourceEntityInfo = ProcessSingleNode(sourceNode, sourceType);

            // For relationships in PathSegments, we need to reconstruct the missing properties
            var relEntityInfo = ProcessSingleRelationshipWithContext(relationship, relType, GetNodeId(sourceNode), GetNodeId(targetNode));

            var targetEntityInfo = ProcessSingleNode(targetNode, targetType);

            // Create the composite path segment EntityInfo
            var pathSegmentEntityInfo = CreatePathSegmentEntityInfo(
                sourceEntityInfo, relEntityInfo, targetEntityInfo, pathSegmentType);

            results.Add(pathSegmentEntityInfo);
        }

        return results;
    }

    private EntityInfo ProcessSingleRelationshipWithContext(IRelationship relationship, Type targetType, string startNodeId, string endNodeId)
    {
        // Create the base EntityInfo from the relationship
        var entityInfo = CreateEntityInfoFromRelationship(relationship, targetType);

        // Add back the missing properties using the shared enhancement logic
        EnhanceRelationshipEntityInfo(entityInfo, targetType, startNodeId, endNodeId);

        return entityInfo;
    }

    private void EnhanceRelationshipEntityInfo(EntityInfo entityInfo, Type targetType, string startNodeId, string endNodeId)
    {
        // Add StartNodeId as a simple property
        entityInfo.SimpleProperties[nameof(Model.IRelationship.StartNodeId)] = new Property(
            PropertyInfo: targetType.GetProperty(nameof(Model.IRelationship.StartNodeId))!,
            Label: nameof(Model.IRelationship.StartNodeId),
            IsNullable: false,
            Value: new SimpleValue(startNodeId, typeof(string))
        );

        // Add EndNodeId as a simple property  
        entityInfo.SimpleProperties[nameof(Model.IRelationship.EndNodeId)] = new Property(
            PropertyInfo: targetType.GetProperty(nameof(Model.IRelationship.EndNodeId))!,
            Label: nameof(Model.IRelationship.EndNodeId),
            IsNullable: false,
            Value: new SimpleValue(endNodeId, typeof(string))
        );

        // Add Direction
        var direction = GetRelationshipDirection(targetType);
        entityInfo.SimpleProperties[nameof(Model.IRelationship.Direction)] = new Property(
            PropertyInfo: targetType.GetProperty(nameof(Model.IRelationship.Direction))!,
            Label: nameof(Model.IRelationship.Direction),
            IsNullable: false,
            Value: new SimpleValue(direction, typeof(RelationshipDirection))
        );
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
        // For records derived from Relationship, check if there's a default direction
        // Most relationship types will use Outgoing as the default
        return RelationshipDirection.Outgoing;
    }

    private EntityInfo ProcessSingleNode(object nodeValue, Type targetType)
    {
        if (nodeValue is not INode node)
        {
            throw new InvalidOperationException($"Expected INode, got {nodeValue?.GetType()}");
        }

        return CreateEntityInfoFromNode(node, targetType);
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

    private List<EntityInfo> ProcessSimpleNodes(List<IRecord> records, Type targetType)
    {
        var results = new List<EntityInfo>();

        foreach (var record in records)
        {
            if (!record.TryGet<object>("n", out var nodeObj))
                continue;

            // Reuse the extracted logic
            var entityInfo = ProcessSingleNode(nodeObj, targetType);
            results.Add(entityInfo);
        }

        return results;
    }

    private async Task<List<EntityInfo>> ProcessNodesWithComplexPropertiesAsync(
        List<IRecord> records,
        Type targetType,
        CancellationToken cancellationToken)
    {
        if (!records.Any())
            return [];

        // Create the base EntityInfo from the first record
        var baseNode = records[0]["n"].As<INode>();
        var entityInfo = CreateEntityInfoFromNode(baseNode, targetType);

        // Process all records to build a complete EntityInfo
        foreach (var record in records)
        {
            var relatedNodesList = record["relatedNodes"].As<IList<object>>();
            if (relatedNodesList != null && relatedNodesList.Any())
            {
                await AddComplexPropertiesFromRelatedNodes(entityInfo, relatedNodesList, cancellationToken);
            }
        }

        return [entityInfo];
    }

    private async Task AddComplexPropertiesFromRelatedNodes(
        EntityInfo entityInfo,
        IList<object> relatedNodesList,
        CancellationToken cancellationToken)
    {
        if (!_entityFactory.CanDeserialize(entityInfo.ActualType))
            return;

        var schema = _entityFactory.GetSchema(entityInfo.ActualType);
        if (schema?.ComplexProperties == null)
            return;

        // Group related nodes by relationship type
        var nodesByRelType = new Dictionary<string, List<RelatedNodeInfo>>();

        foreach (var relatedNodeObj in relatedNodesList)
        {
            if (relatedNodeObj is not IReadOnlyDictionary<string, object> relatedNodeDict)
                continue;

            var nodeInfo = ExtractRelatedNodeInfo(relatedNodeDict);
            if (nodeInfo != null)
            {
                if (!nodesByRelType.ContainsKey(nodeInfo.RelType))
                    nodesByRelType[nodeInfo.RelType] = [];

                nodesByRelType[nodeInfo.RelType].Add(nodeInfo);
            }
        }

        // Process each complex property
        foreach (var (propertyName, propertySchema) in schema.ComplexProperties)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var expectedRelType = GraphDataModel.PropertyNameToRelationshipTypeName(propertyName);

            if (!nodesByRelType.TryGetValue(expectedRelType, out var relatedNodes) || !relatedNodes.Any())
                continue;

            // Build the complex property
            var complexProperty = await BuildComplexPropertyFromRelatedNodes(
                relatedNodes, propertySchema, cancellationToken);

            if (complexProperty != null)
            {
                entityInfo.ComplexProperties[propertyName] = complexProperty;
            }
        }
    }

    private RelatedNodeInfo? ExtractRelatedNodeInfo(IReadOnlyDictionary<string, object> relatedNodeDict)
    {
        if (!relatedNodeDict.TryGetValue("Node", out var nodeObj) || nodeObj is not INode node)
            return null;

        if (!relatedNodeDict.TryGetValue("RelType", out var relTypeObj) || relTypeObj is not string relType)
            return null;

        var relationshipProperties = relatedNodeDict.TryGetValue("RelationshipProperties", out var relPropsObj)
            ? relPropsObj as IReadOnlyDictionary<string, object> ?? new Dictionary<string, object>()
            : new Dictionary<string, object>();

        return new RelatedNodeInfo(node, relType, relationshipProperties);
    }

    private Task<Property> BuildComplexPropertyFromRelatedNodes(
        List<RelatedNodeInfo> relatedNodes,
        PropertySchema propertySchema,
        CancellationToken cancellationToken)
    {
        var childEntityInfos = new List<EntityInfo>();
        var childType = propertySchema.PropertyInfo.PropertyType.IsGenericType
            ? propertySchema.PropertyInfo.PropertyType.GetGenericArguments()[0]
            : propertySchema.PropertyInfo.PropertyType;

        foreach (var relatedNode in relatedNodes)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var childEntityInfo = CreateEntityInfoFromNode(relatedNode.Node, childType);

            // Handle any relationship properties if needed
            if (relatedNode.RelationshipProperties.Any())
            {
                // For now, we'll log that we have relationship properties but don't process them
                _logger.LogDebug("Relationship properties found but not yet processed for {RelType}", relatedNode.RelType);
            }

            childEntityInfos.Add(childEntityInfo);
        }

        // Return appropriate property type
        if (propertySchema.PropertyType == PropertyType.ComplexCollection)
        {
            // Collection of simple or complex objects
            if (propertySchema.ElementType == null)
                throw new InvalidOperationException("Element type must be specified for collections.");

            Property property = new(
                propertySchema.PropertyInfo,
                propertySchema.Neo4jPropertyName,
                propertySchema.IsNullable,
                new EntityCollection(propertySchema.ElementType, childEntityInfos));

            return Task.FromResult(property);
        }
        else if (propertySchema.PropertyType == PropertyType.Complex)
        {
            // Single complex property
            var firstChild = childEntityInfos.FirstOrDefault()
                ?? throw new InvalidOperationException("No related nodes found for complex property.");
            Property property = new(
                propertySchema.PropertyInfo,
                propertySchema.Neo4jPropertyName,
                propertySchema.IsNullable,
                firstChild);
            return Task.FromResult(property);
        }

        throw new InvalidOperationException(
            $"Unsupported property type for complex property: {propertySchema.PropertyType}");
    }

    private List<EntityInfo> ProcessRelationships(List<IRecord> records, Type targetType)
    {
        var results = new List<EntityInfo>();

        foreach (var record in records)
        {
            if (!record.TryGet<object>("r", out var relObj) || relObj is not IRelationship relationship)
                continue;

            EntityInfo entityInfo;

            // Check if we have source and target context available (from PathSegment projections)
            if (record.TryGet<object>("src", out var srcObj) && srcObj is INode sourceNode &&
                record.TryGet<object>("tgt", out var tgtObj) && tgtObj is INode targetNode)
            {
                // We have full context
                entityInfo = ProcessSingleRelationshipWithContext(relationship, targetType, GetNodeId(sourceNode), GetNodeId(targetNode));
            }
            else if (record.TryGet<object>(nameof(Model.IRelationship.StartNodeId), out var startNodeId)
                  && record.TryGet<object>(nameof(Model.IRelationship.EndNodeId), out var endNodeId))
            {
                // We have start and end node IDs
                entityInfo = ProcessSingleRelationshipWithContext(relationship, targetType, startNodeId.ToString()!, endNodeId.ToString()!);
            }
            else
            {
                throw new GraphException(
                    "Cannot process relationship without source and target context. " +
                    "Ensure your query includes the necessary node IDs or nodes.");
            }

            results.Add(entityInfo);
        }

        return results;
    }

    private EntityInfo CreateEntityInfoFromNode(INode node, Type actualType)
    {
        var label = node.Labels.FirstOrDefault() ?? actualType.Name;
        var simpleProperties = ExtractSimpleProperties(node.Properties, actualType);

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

        return new EntityInfo(
            ActualType: actualType,
            Label: label,
            SimpleProperties: simpleProperties,
            ComplexProperties: new Dictionary<string, Property>() // Relationships can't have complex properties
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

    private async Task<List<EntityInfo>> ProcessNodesAsync(
        List<IRecord> records,
        Type targetType,
        CancellationToken cancellationToken)
    {
        // Check if this query includes complex properties (relatedNodes in the results)
        var hasComplexProperties = records.Any(r => r.Keys.Contains("relatedNodes"));

        if (hasComplexProperties)
        {
            return await ProcessNodesWithComplexPropertiesAsync(records, targetType, cancellationToken);
        }

        return ProcessSimpleNodes(records, targetType);
    }

    // Helper record for organizing related node data
    private record RelatedNodeInfo(
        INode Node,
        string RelType,
        IReadOnlyDictionary<string, object> RelationshipProperties);
}

