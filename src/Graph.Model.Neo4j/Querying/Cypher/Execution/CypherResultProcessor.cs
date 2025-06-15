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

    // Remove the generic overload since it was just calling the non-generic one anyway
    public async Task<List<EntityInfo>> ProcessAsync(
        List<IRecord> records,
        Type targetType,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Processing records for target type: {TargetType}", targetType.Name);

        // Check if we're dealing with graph entities
        if (typeof(Model.INode).IsAssignableFrom(targetType))
        {
            return await ProcessNodesAsync(records, targetType, cancellationToken);
        }

        if (typeof(Model.IRelationship).IsAssignableFrom(targetType))
        {
            return ProcessRelationships(records, targetType);
        }

        // For projections, convert to EntityInfo using reflection
        return ProcessProjections(records, targetType);
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
            if (!record.TryGet<object>("n", out var nodeObj) || nodeObj is not INode node)
                continue;

            var entityInfo = CreateEntityInfoFromNode(node, targetType);
            results.Add(entityInfo);
        }

        return results;
    }

    private async Task<List<EntityInfo>> ProcessNodesWithComplexPropertiesAsync(
        List<IRecord> records,
        Type targetType,
        CancellationToken cancellationToken)
    {
        var results = new List<EntityInfo>();

        foreach (var record in records)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!record.TryGet<object>("n", out var nodeObj) || nodeObj is not INode mainNode)
                continue;

            // Start with the main node
            var entityInfo = CreateEntityInfoFromNode(mainNode, targetType);

            // Add complex properties from related nodes
            if (record.TryGet<object>("relatedNodes", out var relatedNodesObj) &&
                relatedNodesObj is IList<object> relatedNodesList)
            {
                await AddComplexPropertiesFromRelatedNodes(entityInfo, relatedNodesList, cancellationToken);
            }

            results.Add(entityInfo);
        }

        return results;
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

            var entityInfo = CreateEntityInfoFromRelationship(relationship, targetType);
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

