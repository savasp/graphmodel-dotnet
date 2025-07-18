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
        int SequenceNumber,
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

        // Handle IEntity - polymorphic entity search results
        if (targetType == typeof(Model.IEntity))
        {
            return Task.FromResult(ProcessMixedEntities(records));
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
                SequenceNumber: dict["SequenceNumber"].As<int>(),
                Property: dict["Property"].As<INode>()
        ))
        .OrderBy(cp => cp.SequenceNumber)
        .ToList();

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
        if (targetType.IsAssignableTo(typeof(DynamicNode)))
        {
            // For dynamic nodes
            return DeserializeComplexPropertiesForDynamicNode(nodeResult.Node, nodeResult.ComplexProperties, typeof(DynamicNode));
        }

        // For strongly-typed nodes
        return DeserializeComplexPropertiesForTypedNode(nodeResult.Node, nodeResult.ComplexProperties, targetType);
    }

    /// <summary>
    /// Recursively reconstructs the object graph for a node and its complex properties.
    /// </summary>
    private EntityInfo DeserializeComplexPropertiesForTypedNode(
        INode node,
        List<ComplexProperty> allComplexProperties,
        Type nodeType)
    {
        // Create the base entity info for this node
        var actualType = DiscoverActualNodeType(node, nodeType);
        Dictionary<string, Property> simpleProperties;
        var label = node.Labels.FirstOrDefault() ?? actualType.Name;

        // Use dynamic extraction for dynamic nodes (including complex property nodes)
        if (typeof(Model.DynamicNode).IsAssignableFrom(actualType))
        {
            simpleProperties = ExtractAllSimplePropertiesForDynamicNode(node.Properties);
            if (!simpleProperties.ContainsKey(nameof(Model.IEntity.Id)) && node.Properties.TryGetValue(nameof(Model.IEntity.Id), out var idValue))
            {
                simpleProperties[nameof(Model.IEntity.Id)] = new Property(
                    PropertyInfo: default!,
                    Label: nameof(Model.IEntity.Id),
                    IsNullable: false,
                    Value: new SimpleValue(idValue ?? string.Empty, typeof(string))
                );
            }
        }
        else
        {
            simpleProperties = ExtractSimpleProperties(node.Properties, actualType);
        }

        var entityInfo = new EntityInfo(
            ActualType: actualType,
            Label: label,
            ActualLabels: node.Labels.ToList(),
            SimpleProperties: simpleProperties,
            ComplexProperties: new Dictionary<string, Property>()
        );

        // Find all complex properties where this node is the parent
        var directComplexProps = allComplexProperties
            .Where(cp => cp.ParentNode.ElementId == node.ElementId)
            .ToList();

        if (directComplexProps.Count == 0)
            return entityInfo;

        var schema = _entityFactory.GetSchema(nodeType);
        if (schema == null)
        {
            _logger.LogWarning("No schema found for node type {NodeType}. Cannot deserialize complex properties.", nodeType.Name);
            return entityInfo;
        }

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
                    .Select(cp => DeserializeComplexPropertiesForTypedNode(cp.Property, allComplexProperties, childType))
                    .ToList();

                entityInfo.ComplexProperties[propertyName] = new Property(
                    propertySchema.PropertyInfo,
                    propertySchema.Neo4jPropertyName,
                    propertySchema.IsNullable,
                    new EntityCollection(propertySchema.ElementType!, children));
            }
            else if (propertySchema.PropertyType == PropertyType.Complex)
            {
                var child = DeserializeComplexPropertiesForTypedNode(matchingProps[0].Property, allComplexProperties, childType);

                entityInfo.ComplexProperties[propertyName] = new Property(
                    propertySchema.PropertyInfo,
                    propertySchema.Neo4jPropertyName,
                    propertySchema.IsNullable,
                    child);
            }
        }

        return entityInfo;
    }

    /// <summary>
    /// Recursively reconstructs the object graph for a node and its complex properties.
    /// </summary>
    private EntityInfo DeserializeComplexPropertiesForDynamicNode(
        INode node,
        List<ComplexProperty> allComplexProperties,
        Type nodeType)
    {
        // Create the base entity info for this node
        Dictionary<string, Property> simpleProperties;

        simpleProperties = ExtractAllSimplePropertiesForDynamicNode(node.Properties);

        var entityInfo = new EntityInfo(
            ActualType: nodeType,
            Label: node.Labels.FirstOrDefault() ?? "",
            ActualLabels: node.Labels.ToList(),
            SimpleProperties: simpleProperties,
            ComplexProperties: new Dictionary<string, Property>()
        );

        var directComplexProps = allComplexProperties
            .Where(cp => cp.ParentNode.ElementId == node.ElementId)
            .ToList();

        // For dynamic nodes, attach all direct complex properties using the property name derived from the relationship type
        foreach (var cp in directComplexProps)
        {
            var propertyName = cp.Relationship.Type;
            // Remove __PROPERTY__ prefix and __ suffix if present
            if (propertyName.StartsWith(GraphDataModel.PropertyRelationshipTypeNamePrefix) && propertyName.EndsWith(GraphDataModel.PropertyRelationshipTypeNameSuffix))
            {
                propertyName = propertyName.Substring(GraphDataModel.PropertyRelationshipTypeNamePrefix.Length, propertyName.Length - GraphDataModel.PropertyRelationshipTypeNamePrefix.Length - GraphDataModel.PropertyRelationshipTypeNameSuffix.Length);
            }
            var childEntity = DeserializeComplexPropertiesForDynamicNode(cp.Property, allComplexProperties, typeof(object));
            entityInfo.ComplexProperties[propertyName] = new Property(
                PropertyInfo: null!,
                Label: propertyName,
                IsNullable: true,
                Value: childEntity
            );
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
        EnhanceRelationshipEntityInfo(entityInfo, relationship, targetType, startNodeId, endNodeId);
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
        var complexProperties = new Dictionary<string, Property>();

        // Extract all values from the record as simple or complex properties
        foreach (var key in record.Keys)
        {
            var value = record[key];

            // Handle Neo4j nodes specially - convert them to EntityInfo
            if (value is INode n)
            {
                // Create EntityInfo for the node and store as complex property
                var nodeEntityInfo = CreateEntityInfoFromNode(n, typeof(Model.INode));
                complexProperties[key] = new Property(
                    PropertyInfo: null!, // We'll handle this differently for projections
                    Label: key,
                    IsNullable: true, // Assume nullable for projections
                    Value: nodeEntityInfo
                );
                continue;
            }

            // Handle Neo4j relationships specially - convert them to EntityInfo
            if (value is IRelationship rel)
            {
                // Create EntityInfo for the relationship and store as complex property
                var relEntityInfo = CreateEntityInfoFromRelationship(rel, typeof(Model.IRelationship));
                complexProperties[key] = new Property(
                    PropertyInfo: null!, // We'll handle this differently for projections
                    Label: key,
                    IsNullable: true, // Assume nullable for projections
                    Value: relEntityInfo
                );
                continue;
            }

            // Handle complex property structures with Node and ComplexProperties (e.g., { Node: src, ComplexProperties: [...] })
            if (value is IReadOnlyDictionary<string, object> complexPropStructure &&
                complexPropStructure.ContainsKey("Node") &&
                complexPropStructure.ContainsKey("ComplexProperties"))
            {
                // This is a complex property structure - deserialize it properly
                if (complexPropStructure["Node"] is INode node)
                {
                    // Debug: Log the node properties to see what we have
                    _logger.LogDebug("Complex property structure node has {PropertyCount} properties: [{Properties}]",
                        node.Properties.Count,
                        string.Join(", ", node.Properties.Select(kv => $"{kv.Key}={kv.Value}")));

                    // Create the base EntityInfo from the node
                    var nodeEntityInfo = CreateEntityInfoFromNode(node, typeof(Model.INode));

                    // Debug: Log the created EntityInfo
                    _logger.LogDebug("Created EntityInfo with {SimpleCount} simple properties: [{SimpleProps}]",
                        nodeEntityInfo.SimpleProperties.Count,
                        string.Join(", ", nodeEntityInfo.SimpleProperties.Select(kv => $"{kv.Key}={kv.Value.Value}")));

                    // Add complex properties if they exist
                    if (complexPropStructure["ComplexProperties"] is IList<object> complexProps && complexProps.Count > 0)
                    {
                        // TODO: Process the complex properties from the flat list
                        // For now, we'll just use the base node EntityInfo
                        _logger.LogDebug("Complex properties found: {Count} items", complexProps.Count);
                    }

                    complexProperties[key] = new Property(
                        PropertyInfo: null!, // We'll handle this differently for projections
                        Label: key,
                        IsNullable: true, // Assume nullable for projections
                        Value: nodeEntityInfo
                    );
                    continue;
                }
            }

            // Handle structured path segment objects (e.g., { StartNode: src, Relationship: r, EndNode: tgt })
            if (value is IReadOnlyDictionary<string, object> structuredObject &&
                structuredObject.ContainsKey("StartNode") &&
                structuredObject.ContainsKey("Relationship") &&
                structuredObject.ContainsKey("EndNode"))
            {
                // Check if this is a complex path segment with nested structures
                var startNodeObj = structuredObject["StartNode"];
                var relationshipObj = structuredObject["Relationship"];
                var endNodeObj = structuredObject["EndNode"];

                // Handle complex property structures within path segments
                INode? startNode = null;
                IRelationship? relationship = null;
                INode? endNode = null;

                // Extract start node (could be a complex structure or direct node)
                if (startNodeObj is IReadOnlyDictionary<string, object> startNodeStruct &&
                    startNodeStruct.ContainsKey("Node"))
                {
                    startNode = startNodeStruct["Node"] as INode;
                }
                else if (startNodeObj is INode directStartNode)
                {
                    startNode = directStartNode;
                }

                // Extract relationship
                relationship = relationshipObj as IRelationship;

                // Extract end node (could be a complex structure or direct node)
                if (endNodeObj is IReadOnlyDictionary<string, object> endNodeStruct &&
                    endNodeStruct.ContainsKey("Node"))
                {
                    endNode = endNodeStruct["Node"] as INode;
                }
                else if (endNodeObj is INode directEndNode)
                {
                    endNode = directEndNode;
                }

                if (startNode != null && relationship != null && endNode != null)
                {
                    var startNodeEntityInfo = CreateEntityInfoFromNode(startNode, typeof(Model.INode));
                    var relEntityInfo = CreateEntityInfoFromRelationship(relationship, typeof(Model.IRelationship));
                    var endNodeEntityInfo = CreateEntityInfoFromNode(endNode, typeof(Model.INode));

                    var pathSegmentEntityInfo = CreatePathSegmentEntityInfo(
                        startNodeEntityInfo,
                        relEntityInfo,
                        endNodeEntityInfo,
                        typeof(IGraphPathSegment<Model.INode, Model.IRelationship, Model.INode>)
                    );

                    complexProperties[key] = new Property(
                        PropertyInfo: null!, // We'll handle this differently for projections
                        Label: key,
                        IsNullable: true, // Assume nullable for projections
                        Value: pathSegmentEntityInfo
                    );
                    continue;
                }
            }

            // Handle regular values
            try
            {
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
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to convert value for property '{key}' of type {value?.GetType().Name ?? "null"}: {ex.Message}", ex);
            }
        }

        return new EntityInfo(
            ActualType: targetType,
            Label: targetType.Name,
            ActualLabels: [],
            SimpleProperties: simpleProperties,
            ComplexProperties: complexProperties
        );
    }

    private void EnhanceRelationshipEntityInfo(EntityInfo entityInfo, IRelationship relationship, Type targetType, string startNodeId, string endNodeId)
    {
        // Add StartNodeId as a simple property
        var startNodeIdProperty = targetType.IsInterface
            ? targetType.GetInterface(typeof(Model.IRelationship).Name)?.GetProperty(nameof(Model.IRelationship.StartNodeId))
            : targetType.GetProperty(nameof(Model.IRelationship.StartNodeId));
        if (startNodeIdProperty != null)
        {
            entityInfo.SimpleProperties[nameof(Model.IRelationship.StartNodeId)] = new Property(
                PropertyInfo: startNodeIdProperty,
                Label: nameof(Model.IRelationship.StartNodeId),
                IsNullable: false,
                Value: new SimpleValue(startNodeId, typeof(string))
            );
        }

        // Add EndNodeId as a simple property
        var endNodeIdProperty = targetType.IsInterface
            ? targetType.GetInterface(typeof(Model.IRelationship).Name)?.GetProperty(nameof(Model.IRelationship.EndNodeId))
            : targetType.GetProperty(nameof(Model.IRelationship.EndNodeId));
        if (endNodeIdProperty != null)
        {
            entityInfo.SimpleProperties[nameof(Model.IRelationship.EndNodeId)] = new Property(
                PropertyInfo: endNodeIdProperty,
                Label: nameof(Model.IRelationship.EndNodeId),
                IsNullable: false,
                Value: new SimpleValue(endNodeId, typeof(string))
            );
        }

        // Add Direction
        var directionProperty = targetType.IsInterface
            ? targetType.GetInterface(typeof(Model.IRelationship).Name)?.GetProperty(nameof(Model.IRelationship.Direction))
            : targetType.GetProperty(nameof(Model.IRelationship.Direction));
        if (directionProperty != null)
        {
            var direction = GetRelationshipDirection(relationship, targetType);
            entityInfo.SimpleProperties[nameof(Model.IRelationship.Direction)] = new Property(
                PropertyInfo: directionProperty,
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

    private static RelationshipDirection GetRelationshipDirection(IRelationship relationship, Type targetType)
    {
        // Try to get the direction from the relationship properties
        if (relationship.Properties.TryGetValue(nameof(Model.IRelationship.Direction), out var directionValue))
        {
            if (directionValue is RelationshipDirection direction)
            {
                return direction;
            }

            // Try to parse if it's stored as a string or number
            if (Enum.TryParse<RelationshipDirection>(directionValue.ToString(), out var parsedDirection))
            {
                return parsedDirection;
            }
        }

        // Default to Outgoing if no direction is found
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
            ActualLabels: [],
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

    // Add this method to extract all properties for dynamic nodes
    private Dictionary<string, Property> ExtractAllSimplePropertiesForDynamicNode(IReadOnlyDictionary<string, object> properties)
    {
        var result = new Dictionary<string, Property>();
        foreach (var (key, value) in properties)
        {
            if (key == SerializationBridge.MetadataPropertyName)
                continue;
            // Use SerializationBridge to convert Neo4j values to .NET types
            object? convertedValue = value;
            if (value is List<object> listValue)
            {
                // Try to infer element type from contents (string, int, etc.)
                var elementType = listValue.FirstOrDefault()?.GetType() ?? typeof(object);
                // If all elements are string, treat as List<string>
                if (listValue.All(x => x is string || x == null))
                {
                    convertedValue = listValue.Cast<string?>().ToList();
                }
                else
                {
                    // Fallback: use SerializationBridge to convert to List<object>
                    convertedValue = SerializationBridge.FromNeo4jValue(listValue, typeof(List<object>));
                }
            }
            else
            {
                convertedValue = SerializationBridge.FromNeo4jValue(value, value?.GetType() ?? typeof(object));
            }
            result[key] = new Property(
                PropertyInfo: null!,
                Label: key,
                IsNullable: value == null,
                Value: new SimpleValue(convertedValue ?? string.Empty, convertedValue?.GetType() ?? typeof(object))
            );
        }
        return result;
    }

    // In CreateEntityInfoFromNode, use this for dynamic nodes
    private EntityInfo CreateEntityInfoFromNode(INode node, Type targetType)
    {
        // Discover the actual type from metadata, labels, or fall back to target type
        var actualType = DiscoverActualNodeType(node, targetType);

        Dictionary<string, Property> simpleProperties;
        var label = node.Labels.FirstOrDefault() ?? actualType.Name;

        // Handle dynamic nodes differently
        if (typeof(Model.DynamicNode).IsAssignableFrom(actualType))
        {
            simpleProperties = ExtractAllSimplePropertiesForDynamicNode(node.Properties);
            // Add Id property if not present
            if (!simpleProperties.ContainsKey(nameof(Model.IEntity.Id)) && node.Properties.TryGetValue(nameof(Model.IEntity.Id), out var idValue))
            {
                simpleProperties[nameof(Model.IEntity.Id)] = new Property(
                    PropertyInfo: default!, // null is expected for dynamic
                    Label: nameof(Model.IEntity.Id),
                    IsNullable: false,
                    Value: new SimpleValue(idValue ?? string.Empty, typeof(string))
                );
            }
        }
        else
        {
            simpleProperties = ExtractSimpleProperties(node.Properties, actualType);
        }

        return new EntityInfo(
            ActualType: actualType,
            Label: label,
            ActualLabels: node.Labels.ToList(),
            SimpleProperties: simpleProperties,
            ComplexProperties: new Dictionary<string, Property>()
        );
    }

    private EntityInfo CreateEntityInfoFromRelationship(IRelationship relationship, Type targetType)
    {
        // Discover the actual type from metadata, type, or fall back to target type
        var actualType = DiscoverActualRelationshipType(relationship, targetType);

        var label = relationship.Type;
        if (string.IsNullOrEmpty(label))
        {
            label = actualType.Name;
        }
        Dictionary<string, Property> simpleProperties;

        // Handle dynamic relationships differently
        if (typeof(Model.DynamicRelationship).IsAssignableFrom(actualType))
        {
            simpleProperties = new Dictionary<string, Property>();
            foreach (var (key, value) in relationship.Properties)
            {
                // Skip metadata properties
                if (SerializationBridge.MetadataPropertyName == key)
                    continue;
                // Store all properties as SimpleValue
                simpleProperties[key] = new Property(
                    PropertyInfo: default!, // null is expected for dynamic
                    Label: key,
                    IsNullable: value == null,
                    Value: new SimpleValue(value ?? string.Empty, value?.GetType() ?? typeof(object))
                );
            }
            // Add Id property if not present
            if (!simpleProperties.ContainsKey(nameof(Model.IEntity.Id)) && relationship.Properties.TryGetValue(nameof(Model.IEntity.Id), out var idValue))
            {
                simpleProperties[nameof(Model.IEntity.Id)] = new Property(
                    PropertyInfo: default!, // null is expected for dynamic
                    Label: nameof(Model.IEntity.Id),
                    IsNullable: false,
                    Value: new SimpleValue(idValue ?? string.Empty, typeof(string))
                );
            }
        }
        else
        {
            simpleProperties = ExtractSimpleProperties(relationship.Properties, actualType);
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
        }

        return new EntityInfo(
            ActualType: actualType,
            Label: label,
            ActualLabels: [label],
            SimpleProperties: simpleProperties,
            ComplexProperties: new Dictionary<string, Property>()
        );
    }

    /// <summary>
    /// Discovers the actual type of a node using metadata, labels, and compatibility checks.
    /// </summary>
    private Type DiscoverActualNodeType(INode node, Type targetType)
    {
        // Special handling for dynamic entities
        if (targetType == typeof(Model.DynamicNode))
        {
            _logger.LogDebug("Using label-based type DynamicNode for target type {TargetType}",
                targetType.Name);
            return typeof(Model.DynamicNode);
        }

        // Step 1: Try to get type from stored metadata
        var metadataType = SerializationBridge.GetTypeFromMetadata(node.Properties);
        if (metadataType != null && IsCompatibleType(metadataType, targetType))
        {
            _logger.LogDebug("Using metadata type {MetadataType} for target type {TargetType}",
                metadataType.Name, targetType.Name);
            return metadataType;
        }

        // Step 2: Try to discover from node labels
        var labelType = DiscoverTypeFromNodeLabels(node.Labels, targetType);
        if (labelType != null)
        {
            _logger.LogDebug("Using label-based type {LabelType} for target type {TargetType}",
                labelType.Name, targetType.Name);
            return labelType;
        }

        // Step 3: Fall back to target type
        _logger.LogDebug("Falling back to target type {TargetType} for node with labels [{Labels}]",
            targetType.Name, string.Join(", ", node.Labels));
        return targetType;
    }

    /// <summary>
    /// Discovers the actual type of a relationship using metadata, type, and compatibility checks.
    /// </summary>
    private Type DiscoverActualRelationshipType(IRelationship relationship, Type targetType)
    {
        // Special handling for dynamic entities
        if (targetType == typeof(Model.DynamicRelationship))
        {
            _logger.LogDebug("Using relationship type-based type DynamicRelationship for target type {TargetType}",
                targetType.Name);
            return typeof(Model.DynamicRelationship);
        }

        // Step 1: Try to get type from stored metadata
        var metadataType = SerializationBridge.GetTypeFromMetadata(relationship.Properties);
        if (metadataType != null && IsCompatibleType(metadataType, targetType))
        {
            _logger.LogDebug("Using metadata type {MetadataType} for target type {TargetType}",
                metadataType.Name, targetType.Name);
            return metadataType;
        }

        // Step 2: Try to discover from relationship type/label
        var labelType = DiscoverTypeFromRelationshipType(relationship.Type, targetType);
        if (labelType != null)
        {
            _logger.LogDebug("Using relationship type-based type {LabelType} for target type {TargetType}",
                labelType.Name, targetType.Name);
            return labelType;
        }

        // Step 3: Fall back to target type
        _logger.LogDebug("Falling back to target type {TargetType} for relationship with type {RelType}",
            targetType.Name, relationship.Type ?? "null");
        return targetType;
    }

    /// <summary>
    /// Checks if a discovered type is compatible with the target type (same or derived).
    /// </summary>
    private static bool IsCompatibleType(Type discoveredType, Type targetType)
    {
        // The discovered type must be assignable to the target type
        // This means discoveredType is the same as targetType or derives from it
        return targetType.IsAssignableFrom(discoveredType);
    }

    /// <summary>
    /// Attempts to discover a more specific type from node labels.
    /// </summary>
    private Type? DiscoverTypeFromNodeLabels(IReadOnlyList<string> labels, Type targetType)
    {
        if (!labels.Any())
            return null;

        // Get all types that are assignable to the target type
        var candidateTypes = GetKnownNodeTypes()
            .Where(t => targetType.IsAssignableFrom(t))
            .ToList();

        // Try to find a type that matches one of the labels
        foreach (var label in labels)
        {
            var matchingType = candidateTypes.FirstOrDefault(t =>
                string.Equals(t.Name, label, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(GetTypeLabel(t), label, StringComparison.OrdinalIgnoreCase));

            if (matchingType != null)
                return matchingType;
        }

        return null;
    }

    /// <summary>
    /// Attempts to discover a more specific type from relationship type.
    /// </summary>
    private Type? DiscoverTypeFromRelationshipType(string? relationshipType, Type targetType)
    {
        if (string.IsNullOrEmpty(relationshipType))
            return null;

        // Get all types that are assignable to the target type
        var candidateTypes = GetKnownRelationshipTypes()
            .Where(t => targetType.IsAssignableFrom(t))
            .ToList();

        // Try to find a type that matches the relationship type
        var matchingType = candidateTypes.FirstOrDefault(t =>
            string.Equals(GetTypeLabel(t), relationshipType, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(t.Name, relationshipType, StringComparison.OrdinalIgnoreCase));

        return matchingType;
    }

    /// <summary>
    /// Gets the label for a type, checking for attributes first.
    /// </summary>
    private static string GetTypeLabel(Type type)
    {
        // Check for NodeAttribute or RelationshipAttribute
        var nodeAttr = type.GetCustomAttribute<NodeAttribute>();
        if (nodeAttr != null)
            return nodeAttr.Label ?? type.Name;

        var relAttr = type.GetCustomAttribute<RelationshipAttribute>();
        if (relAttr != null)
            return relAttr.Label ?? type.Name;

        return type.Name;
    }

    /// <summary>
    /// Gets all known node types from loaded assemblies.
    /// </summary>
    private IEnumerable<Type> GetKnownNodeTypes()
    {
        // Get types from the current app domain that implement INode
        return AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(assembly =>
            {
                try
                {
                    return assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException ex)
                {
                    // Handle cases where some types can't be loaded
                    return ex.Types.Where(t => t != null).Cast<Type>();
                }
                catch
                {
                    return Enumerable.Empty<Type>();
                }
            })
            .Where(t => t.IsClass && !t.IsAbstract && typeof(Model.INode).IsAssignableFrom(t));
    }

    /// <summary>
    /// Gets all known relationship types from loaded assemblies.
    /// </summary>
    private IEnumerable<Type> GetKnownRelationshipTypes()
    {
        // Get types from the current app domain that implement IRelationship
        return AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(assembly =>
            {
                try
                {
                    return assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException ex)
                {
                    // Handle cases where some types can't be loaded
                    return ex.Types.Where(t => t != null).Cast<Type>();
                }
                catch
                {
                    return Enumerable.Empty<Type>();
                }
            })
            .Where(t => t.IsClass && !t.IsAbstract && typeof(Model.IRelationship).IsAssignableFrom(t));
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

    private List<EntityInfo> ProcessMixedEntities(List<IRecord> records)
    {
        var results = new List<EntityInfo>();

        foreach (var record in records)
        {
            // Entity search returns records with 'entity' column that can be either nodes or PathSegment maps
            if (record.TryGetValue("entity", out var entityValue))
            {
                if (entityValue is INode node)
                {
                    // Process as a node
                    var nodeEntityInfo = CreateEntityInfoFromNode(node, typeof(Model.INode));
                    results.Add(nodeEntityInfo);
                }
                else if (entityValue is IReadOnlyDictionary<string, object> pathSegmentMap)
                {
                    // This is a PathSegment map, process as a relationship
                    var pathSegment = DeserializePathSegment(pathSegmentMap);
                    if (pathSegment != null)
                    {
                        var relationshipEntityInfo = CreateEntityInfoFromRelationship(pathSegment.Relationship, typeof(Model.IRelationship));
                        results.Add(relationshipEntityInfo);
                    }
                }
            }
        }

        return results;
    }
}

