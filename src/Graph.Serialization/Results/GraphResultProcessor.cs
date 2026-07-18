// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Serialization.Results;

using System.Collections;
using System.Reflection;
using Cvoya.Graph.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;


/// <summary>
/// Reassembles provider-neutral graph records into serialization representations for CLR materialization.
/// </summary>
public sealed class GraphResultProcessor
{
    private readonly EntityFactory _entityFactory;
    private readonly ILogger<GraphResultProcessor> _logger;

    private sealed record ComplexProperty(
        GraphValue ParentNode,
        GraphValue Relationship,
        int SequenceNumber,
        GraphValue Property);
    private sealed record NodeResult(GraphValue Node, List<ComplexProperty> ComplexProperties);
    private sealed record PathSegmentResult(
        NodeResult StartNode,
        GraphValue Relationship,
        NodeResult EndNode);

    /// <summary>Initializes a shared graph-result processor.</summary>
    public GraphResultProcessor(EntityFactory entityFactory, ILoggerFactory? loggerFactory = null)
    {
        _entityFactory = entityFactory ?? throw new ArgumentNullException(nameof(entityFactory));
        _logger = loggerFactory?.CreateLogger<GraphResultProcessor>() ?? NullLogger<GraphResultProcessor>.Instance;
    }

    /// <summary>Processes result records for a requested CLR target type.</summary>
    public Task<List<EntityInfo>> ProcessAsync(
        IReadOnlyList<GraphRecord> records,
        Type targetType,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebugGraphResultProcessor44(targetType.Name);

        cancellationToken.ThrowIfCancellationRequested();

        // Handle path segments specially
        if (targetType.IsGenericType && targetType.GetGenericTypeDefinition() == typeof(IGraphPathSegment<,,>))
        {
            return Task.FromResult(ProcessPathSegments(records, targetType));
        }

        // Handle nodes
        if (typeof(Graph.INode).IsAssignableFrom(targetType))
        {
            return Task.FromResult(ProcessNodes(records, targetType));
        }

        // Handle relationships
        if (typeof(Graph.IRelationship).IsAssignableFrom(targetType))
        {
            return Task.FromResult(ProcessRelationships(records, targetType));
        }

        // Handle IEntity - polymorphic entity search results
        if (targetType == typeof(Graph.IEntity))
        {
            return Task.FromResult(ProcessMixedEntities(records));
        }

        // Handle projections (EntityInfo)
        return Task.FromResult(ProcessProjections(records, targetType));
    }

    private static PathSegmentResult? DeserializePathSegment(IReadOnlyDictionary<string, object> pathSegmentRecord)
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
        var relationship = relationshipObj.As<GraphValue>()
            ?? throw new GraphException("Failed to deserialize relationship from path segment record.");
        var endNode = DeserializeNode(endNodeObj.As<Dictionary<string, object>>())
            ?? throw new GraphException("Failed to deserialize end node from path segment record.");

        return new PathSegmentResult(startNode, relationship, endNode);
    }

    private static NodeResult? DeserializeNode(Dictionary<string, object> nodeRecord)
    {
        // Extract the relevant properties from the dictionary
        if (!nodeRecord.TryGetValue("Node", out var nodeObj) ||
            !nodeRecord.TryGetValue("ComplexProperties", out var complexPropsObj))
        {
            return null;
        }

        var node = nodeObj.As<GraphValue>();
        var list = complexPropsObj as List<object> ?? [];
        var complexProperties = list
            .OfType<Dictionary<string, object>>()
            .Select(dict => new ComplexProperty(
                ParentNode: dict["ParentNode"].As<GraphValue>(),
                Relationship: dict["Relationship"].As<GraphValue>(),
                SequenceNumber: dict["SequenceNumber"].As<int>(),
                Property: dict["Property"].As<GraphValue>()
        ))
        .GroupBy(cp => cp.Relationship.ElementId, StringComparer.Ordinal)
        .Select(group => group.First())
        .OrderBy(cp => cp.SequenceNumber)
        .ToList();

        return new NodeResult(node, complexProperties);
    }

    private List<EntityInfo> ProcessPathSegments(IReadOnlyList<GraphRecord> records, Type pathSegmentType)
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
            var startNodeEntityInfo = ProcessSingleNodeResult(
                pathSegment.StartNode,
                sourceType,
                preserveInterfaceShape: true);
            var relEntityInfo = ProcessSingleRelationshipFromPathSegment(
                pathSegment.Relationship, relType,
                pathSegment.StartNode.Node,
                pathSegment.EndNode.Node);
            var endNodeEntityInfo = ProcessSingleNodeResult(
                pathSegment.EndNode,
                targetType,
                preserveInterfaceShape: true);

            // Create the composite path segment EntityInfo
            var pathSegmentEntityInfo = CreatePathSegmentEntityInfo(
                startNodeEntityInfo, relEntityInfo, endNodeEntityInfo, pathSegmentType);

            results.Add(pathSegmentEntityInfo);
        }

        return results;
    }

    private List<EntityInfo> ProcessRelationships(IReadOnlyList<GraphRecord> records, Type targetType)
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
                    pathSegment.StartNode.Node,
                    pathSegment.EndNode.Node);

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

    private EntityInfo ProcessSingleNodeResult(
        NodeResult nodeResult,
        Type targetType,
        bool preserveInterfaceShape = false)
    {
        // Use the new recursive deserializer for complex properties
        if (typeof(Graph.DynamicNode).IsAssignableFrom(targetType) ||
            preserveInterfaceShape && targetType == typeof(Graph.INode))
        {
            // An interface-only read shape intentionally omits declared properties, so keep it
            // dynamic instead of rediscovering a concrete type whose required complex properties
            // were not requested by the projection.
            return DeserializeComplexPropertiesForDynamicNode(nodeResult.Node, nodeResult.ComplexProperties, typeof(DynamicNode));
        }

        // For strongly-typed nodes
        return DeserializeComplexPropertiesForTypedNode(nodeResult.Node, nodeResult.ComplexProperties, targetType);
    }

    /// <summary>
    /// Recursively reconstructs the object graph for a node and its complex properties.
    /// </summary>
    private EntityInfo DeserializeComplexPropertiesForTypedNode(
        GraphValue node,
        List<ComplexProperty> allComplexProperties,
        Type nodeType,
        int depth = 0,
        HashSet<string>? visitedNodeIds = null)
    {
        if (depth > GraphDataModel.DefaultDepthAllowed)
        {
            throw new GraphException(
                $"Complex properties cannot exceed {GraphDataModel.DefaultDepthAllowed} levels of depth.");
        }

        visitedNodeIds ??= new HashSet<string>(StringComparer.Ordinal);
        if (!visitedNodeIds.Add(node.ElementId!))
            throw new GraphException("A cycle or shared node was detected in the persisted complex-property graph.");

        // Create the base entity info for this node
        var actualType = DiscoverActualNodeType(node, nodeType);
        Dictionary<string, Property> simpleProperties;
        var label = node.Labels.Count == 0 ? actualType.Name : node.Labels[0];

        // Use dynamic extraction for dynamic nodes (including complex property nodes)
        if (typeof(Graph.DynamicNode).IsAssignableFrom(actualType))
        {
            simpleProperties = ExtractAllSimplePropertiesForDynamicEntity(node.Properties);
            if (!simpleProperties.ContainsKey(nameof(Graph.IEntity.Id)) && node.Properties.TryGetValue(nameof(Graph.IEntity.Id), out var idValue))
            {
                simpleProperties[nameof(Graph.IEntity.Id)] = new Property(
                    PropertyInfo: default!,
                    Label: nameof(Graph.IEntity.Id),
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

        // Resolve the schema from the DISCOVERED concrete type, not the declared/target type.
        // For a base-typed complex property holding a derived instance (e.g. a PoliceDogDescription
        // in a List<AnimalDescription>), nodeType is the base (AnimalDescription) whose schema has
        // none of the derived-only complex properties (Handler); actualType is the concrete type
        // whose schema does. Simple properties already use actualType (see ExtractSimpleProperties
        // above) - this keeps complex-property reconstruction consistent, so derived-only nested
        // complex properties survive the round trip (see #146).
        var schema = _entityFactory.GetSchema(actualType);
        if (schema == null)
        {
            _logger.LogWarningGraphResultProcessor280(actualType.Name);
            return entityInfo;
        }

        foreach (var (propertyName, propertySchema) in schema.ComplexProperties)
        {
            var expectedRelType = propertySchema.RelationshipType ??
                GraphDataModel.GetComplexPropertyRelationshipType(propertySchema.PropertyInfo);

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
                    .OrderBy(cp => cp.SequenceNumber)
                    .Select(cp => DeserializeComplexPropertiesForTypedNode(
                        cp.Property, allComplexProperties, childType, depth + 1, visitedNodeIds))
                    .ToList();

                entityInfo.ComplexProperties[propertyName] = new Property(
                    propertySchema.PropertyInfo,
                    propertySchema.PropertyName,
                    propertySchema.IsNullable,
                    new EntityCollection(propertySchema.ElementType!, children));
            }
            else if (propertySchema.PropertyType == PropertyType.Complex)
            {
                var child = DeserializeComplexPropertiesForTypedNode(
                    matchingProps[0].Property, allComplexProperties, childType, depth + 1, visitedNodeIds);

                entityInfo.ComplexProperties[propertyName] = new Property(
                    propertySchema.PropertyInfo,
                    propertySchema.PropertyName,
                    propertySchema.IsNullable,
                    child);
            }
        }

        return entityInfo;
    }

    /// <summary>
    /// Recursively reconstructs the object graph for a node and its complex properties.
    /// </summary>
    private static EntityInfo DeserializeComplexPropertiesForDynamicNode(
        GraphValue node,
        List<ComplexProperty> allComplexProperties,
        Type nodeType,
        int depth = 0,
        HashSet<string>? visitedNodeIds = null)
    {
        if (depth > GraphDataModel.DefaultDepthAllowed)
        {
            throw new GraphException(
                $"Complex properties cannot exceed {GraphDataModel.DefaultDepthAllowed} levels of depth.");
        }

        visitedNodeIds ??= new HashSet<string>(StringComparer.Ordinal);
        if (!visitedNodeIds.Add(node.ElementId!))
            throw new GraphException("A cycle or shared node was detected in the persisted complex-property graph.");

        // Create the base entity info for this node
        Dictionary<string, Property> simpleProperties;

        simpleProperties = ExtractAllSimplePropertiesForDynamicEntity(node.Properties);
        if (depth > 0)
        {
            RemoveDynamicComplexValueStructuralProperties(simpleProperties);
        }

        var entityInfo = new EntityInfo(
            ActualType: nodeType,
            Label: node.Labels.Count == 0 ? "" : node.Labels[0],
            ActualLabels: node.Labels.ToList(),
            SimpleProperties: simpleProperties,
            ComplexProperties: new Dictionary<string, Property>()
        );

        var directComplexProps = allComplexProperties
            .Where(cp => cp.ParentNode.ElementId == node.ElementId)
            .ToList();

        // For dynamic nodes, attach all direct complex properties using the property name derived
        // from the relationship type. Multiple relationships of the same type are a stored
        // collection and must materialize as one - assigning them to a single slot would keep only
        // the last item. (A one-item collection is indistinguishable from a single value without a
        // schema and materializes as a single value.)
        foreach (var group in directComplexProps.GroupBy(cp => cp.Relationship.Type))
        {
            var propertyName = group.Key;
            var children = group
                .OrderBy(cp => cp.SequenceNumber)
                .Select(cp => DeserializeComplexPropertiesForDynamicNode(
                    cp.Property, allComplexProperties, typeof(object), depth + 1, visitedNodeIds))
                .ToList();

            entityInfo.ComplexProperties[propertyName] = new Property(
                PropertyInfo: null!,
                Label: propertyName,
                IsNullable: true,
                Value: children.Count == 1
                    ? children[0]
                    : new EntityCollection(typeof(object), children)
            );
        }

        return entityInfo;
    }

    private EntityInfo ProcessSingleRelationshipFromPathSegment(
        GraphValue relationship,
        Type targetType,
        GraphValue startNode,
        GraphValue endNode)
    {
        return CreateEnhancedRelationshipEntityInfo(relationship, targetType, startNode, endNode);
    }

    /// <summary>
    /// A single decomposed hop of a <c>TraversePaths</c> result: which path and hop position it
    /// belongs to, and the (already-deserialized-to-<see cref="EntityInfo"/>) start node,
    /// relationship, and end node for that hop.
    /// </summary>
    public sealed record GraphPathHop(
        int PathIndex,
        int HopIndex,
        EntityInfo StartNode,
        EntityInfo Relationship,
        EntityInfo EndNode);

    /// <summary>
    /// Processes the rows produced by <c>CypherQueryVisitor.HandleTraversePaths</c> - one row per
    /// hop, each carrying <c>pathIndex</c>, <c>hopIndex</c>, and a <c>PathSegment</c> column shaped
    /// like the single-hop path-segment projection - into per-hop <see cref="EntityInfo"/> triples,
    /// reusing the same node/relationship deserialization as the single-hop <c>PathSegments</c> path.
    /// </summary>
    public List<GraphPathHop> ProcessGraphPathHops(IReadOnlyList<GraphRecord> records, Type sourceType, Type relationshipType, Type targetType)
    {
        var results = new List<GraphPathHop>(records.Count);

        foreach (var record in records)
        {
            results.Add(ProcessGraphPathHop(record, sourceType, relationshipType, targetType));
        }

        return results;
    }

    /// <summary>Processes one decomposed graph-path hop.</summary>
    public GraphPathHop ProcessGraphPathHop(GraphRecord record, Type sourceType, Type relationshipType, Type targetType)
    {
        var pathIndex = record["pathIndex"].As<int>();
        var hopIndex = record["hopIndex"].As<int>();

        var pathSegment = DeserializePathSegment(record["PathSegment"].As<Dictionary<string, object>>())
            ?? throw new GraphException("Failed to deserialize path segment hop from record.");

        var startNodeEntityInfo = ProcessSingleNodeResult(
            pathSegment.StartNode,
            sourceType,
            preserveInterfaceShape: true);
        var relEntityInfo = ProcessSingleRelationshipFromPathSegment(
            pathSegment.Relationship, relationshipType,
            pathSegment.StartNode.Node,
            pathSegment.EndNode.Node);
        var endNodeEntityInfo = ProcessSingleNodeResult(
            pathSegment.EndNode,
            targetType,
            preserveInterfaceShape: true);

        return new GraphPathHop(pathIndex, hopIndex, startNodeEntityInfo, relEntityInfo, endNodeEntityInfo);
    }

    private List<EntityInfo> ProcessProjections(IReadOnlyList<GraphRecord> records, Type targetType)
    {
        var results = new List<EntityInfo>();

        foreach (var record in records)
        {
            var entityInfo = CreateEntityInfoFromProjection(record, targetType);
            results.Add(entityInfo);
        }

        return results;
    }

    private EntityInfo CreateEntityInfoFromProjection(GraphRecord record, Type targetType)
    {
        return BuildProjectionEntityInfo(
            record.Keys.Select(key => new KeyValuePair<string, object?>(key, record[key].ToObject())),
            targetType);
    }

    private EntityInfo BuildProjectionEntityInfo(
        IEnumerable<KeyValuePair<string, object?>> columns,
        Type targetType)
    {
        var simpleProperties = new Dictionary<string, Property>();
        var complexProperties = new Dictionary<string, Property>();

        // Extract all values from the record as simple or complex properties
        foreach (var (key, value) in columns)
        {
            // A correlated collection projection (pattern comprehension) yields a list whose
            // elements are themselves projected structures (maps); build a nested entity per
            // element so the materializer can bind each field to the target element type.
            if (value is System.Collections.IEnumerable and not string &&
                TryBuildProjectionEntityCollection(value, out var projectedCollection))
            {
                complexProperties[key] = new Property(
                    PropertyInfo: null!,
                    Label: key,
                    IsNullable: true,
                    Value: projectedCollection);
                continue;
            }

            // Handle node values specially - convert them to EntityInfo.
            if (value is GraphValue { Kind: GraphValueKind.Node } n)
            {
                // Create EntityInfo for the node and store as complex property
                var nodeEntityInfo = CreateEntityInfoFromNode(n, typeof(Graph.INode));
                complexProperties[key] = new Property(
                    PropertyInfo: null!, // We'll handle this differently for projections
                    Label: key,
                    IsNullable: true, // Assume nullable for projections
                    Value: nodeEntityInfo
                );
                continue;
            }

            // A bare relationship omits the endpoints needed for public endpoint IDs.
            if (value is GraphValue { Kind: GraphValueKind.Relationship })
            {
                throw new GraphException(
                    $"Relationship projection '{key}' did not include endpoint node data. " +
                    "StartNodeId and EndNodeId cannot be reconstructed from a bare relationship value.");
            }

            // Handle relationship-shaped projection structures with endpoint nodes.
            if (TryDeserializeRelationshipProjection(value, targetType, key, out var relationshipProjection))
            {
                var (relationship, startNode, endNode, projectionType) = relationshipProjection;
                var relEntityInfo = CreateEnhancedRelationshipEntityInfo(relationship, projectionType, startNode, endNode);
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
                if (complexPropStructure["Node"] is GraphValue { Kind: GraphValueKind.Node } node)
                {
                    if (_logger.IsEnabled(LogLevel.Debug))
                    {
                        _logger.LogDebugGraphResultProcessor528(node.Properties.Count, string.Join(", ", node.Properties.Keys));
                    }

                    // Create the base EntityInfo from the node
                    EntityInfo nodeEntityInfo;

                    // Discover the actual node type from the node labels
                    var actualNodeType = DiscoverActualNodeType(node, typeof(Graph.INode));

                    // Add complex properties if they exist
                    if (complexPropStructure["ComplexProperties"] is IList<object> complexProps && complexProps.Count > 0)
                    {
                        _logger.LogDebugGraphResultProcessor542(complexProps.Count);

                        // Convert the complex properties list to ComplexProperty objects
                        var complexPropertyList = complexProps
                            .OfType<Dictionary<string, object>>()
                            .Select(dict => new ComplexProperty(
                                ParentNode: dict["ParentNode"].As<GraphValue>(),
                                Relationship: dict["Relationship"].As<GraphValue>(),
                                SequenceNumber: dict["SequenceNumber"].As<int>(),
                                Property: dict["Property"].As<GraphValue>()
                            ))
                            .GroupBy(cp => cp.Relationship.ElementId, StringComparer.Ordinal)
                            .Select(group => group.First())
                            .OrderBy(cp => cp.SequenceNumber)
                            .ToList();

                        // Create EntityInfo with complex properties properly deserialized using the actual type
                        nodeEntityInfo = DeserializeComplexPropertiesForTypedNode(node, complexPropertyList, actualNodeType);
                    }
                    else
                    {
                        // No complex properties, just create the base EntityInfo from the node
                        nodeEntityInfo = CreateEntityInfoFromNode(node, actualNodeType);
                    }

                    if (_logger.IsEnabled(LogLevel.Debug))
                    {
                        _logger.LogDebugGraphResultProcessor569(nodeEntityInfo.SimpleProperties.Count, string.Join(", ", nodeEntityInfo.SimpleProperties.Keys), nodeEntityInfo.ComplexProperties.Count);
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
                GraphValue? startNode = null;
                GraphValue? relationship = null;
                GraphValue? endNode = null;

                // Extract start node (could be a complex structure or direct node)
                if (startNodeObj is IReadOnlyDictionary<string, object> startNodeStruct &&
                    startNodeStruct.ContainsKey("Node"))
                {
                    startNode = startNodeStruct["Node"] as GraphValue;
                }
                else if (startNodeObj is GraphValue { Kind: GraphValueKind.Node } directStartNode)
                {
                    startNode = directStartNode;
                }

                // Extract relationship
                relationship = relationshipObj is GraphValue { Kind: GraphValueKind.Relationship } relationshipValue
                    ? relationshipValue
                    : null;

                // Extract end node (could be a complex structure or direct node)
                if (endNodeObj is IReadOnlyDictionary<string, object> endNodeStruct &&
                    endNodeStruct.ContainsKey("Node"))
                {
                    endNode = endNodeStruct["Node"] as GraphValue;
                }
                else if (endNodeObj is GraphValue { Kind: GraphValueKind.Node } directEndNode)
                {
                    endNode = directEndNode;
                }

                if (startNode != null && relationship != null && endNode != null)
                {
                    var startNodeEntityInfo = CreateEntityInfoFromNode(startNode, typeof(Graph.INode));
                    var relEntityInfo = CreateEnhancedRelationshipEntityInfo(relationship, typeof(Graph.IRelationship), startNode, endNode);
                    var endNodeEntityInfo = CreateEntityInfoFromNode(endNode, typeof(Graph.INode));

                    var pathSegmentEntityInfo = CreatePathSegmentEntityInfo(
                        startNodeEntityInfo,
                        relEntityInfo,
                        endNodeEntityInfo,
                        typeof(IGraphPathSegment<Graph.INode, Graph.IRelationship, Graph.INode>)
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
                var convertedValue = GraphValueConverter.ConvertTo(value, typeof(object));
                if (convertedValue is null && value is not null)
                {
                    throw new InvalidOperationException($"Failed to convert value for property '{key}'");
                }

                // Create a simple property info (we don't have real PropertyInfo for projections)
                simpleProperties[key] = new Property(
                    PropertyInfo: null!, // We'll handle this differently for projections
                    Label: key,
                    IsNullable: true, // Assume nullable for projections
                    Value: new SimpleValue(convertedValue!, convertedValue?.GetType() ?? typeof(object))
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

    /// <summary>
    /// Builds an <see cref="EntityCollection"/> for a projected list whose every element is itself a
    /// projected structure (a map), such as a pattern comprehension producing a list of anonymous
    /// objects. Each element becomes a nested projection <see cref="EntityInfo"/> whose fields the
    /// materializer binds to the target element type. Lists of scalars (or empty lists) are declined
    /// so the scalar collection path handles them.
    /// </summary>
    private bool TryBuildProjectionEntityCollection(object? value, out EntityCollection collection)
    {
        collection = null!;
        if (value is not System.Collections.IEnumerable sequence || value is string)
        {
            return false;
        }

        var entities = new List<EntityInfo>();
        foreach (var element in sequence)
        {
            if (element is not IReadOnlyDictionary<string, object> map)
            {
                return false;
            }

            // ActualType is left null so the materializer binds each element against the target
            // collection's element type (unknown here, since projection properties carry no
            // PropertyInfo) rather than the placeholder object type.
            var elementInfo = BuildProjectionEntityInfo(
                map.Select(pair => new KeyValuePair<string, object?>(pair.Key, pair.Value)),
                typeof(object)) with
            { ActualType = null! };
            entities.Add(elementInfo);
        }

        if (entities.Count == 0)
        {
            return false;
        }

        collection = new EntityCollection(typeof(object), entities);
        return true;
    }

    private static bool TryDeserializeRelationshipProjection(
        object? value,
        Type targetType,
        string projectionName,
        out (GraphValue Relationship, GraphValue StartNode, GraphValue EndNode, Type ProjectionType) result)
    {
        result = default;

        if (value is not IReadOnlyDictionary<string, object> structuredObject ||
            !structuredObject.TryGetValue("StartNode", out var startNodeValue) ||
            !structuredObject.TryGetValue("Relationship", out var relationshipValue) ||
            !structuredObject.TryGetValue("EndNode", out var endNodeValue))
        {
            return false;
        }

        var projectionType = GetProjectionMemberType(targetType, projectionName);
        if (projectionType == null || !typeof(Graph.IRelationship).IsAssignableFrom(projectionType))
        {
            return false;
        }

        var startNode = ExtractProjectedNode(startNodeValue);
        var relationship = relationshipValue as GraphValue;
        var endNode = ExtractProjectedNode(endNodeValue);

        if (startNode == null || relationship == null || endNode == null)
        {
            throw new GraphException(
                $"Relationship projection '{projectionName}' did not include endpoint node data. " +
                "StartNodeId and EndNodeId cannot be reconstructed from a bare relationship value.");
        }

        result = (relationship, startNode, endNode, projectionType);
        return true;
    }

    private static Type? GetProjectionMemberType(Type targetType, string projectionName)
    {
        return targetType.GetProperty(projectionName)?.PropertyType
            ?? targetType.GetConstructors()
                .SelectMany(c => c.GetParameters())
                .FirstOrDefault(p => string.Equals(p.Name, projectionName, StringComparison.OrdinalIgnoreCase))
                ?.ParameterType;
    }

    private static GraphValue? ExtractProjectedNode(object value)
    {
        if (value is GraphValue { Kind: GraphValueKind.Node } node)
        {
            return node;
        }

        if (value is IReadOnlyDictionary<string, object> nodeStructure &&
            nodeStructure.TryGetValue("Node", out var nodeObject) &&
            nodeObject is GraphValue { Kind: GraphValueKind.Node } structuredNode)
        {
            return structuredNode;
        }

        return null;
    }

    private EntityInfo CreateEnhancedRelationshipEntityInfo(
        GraphValue relationship,
        Type targetType,
        GraphValue startNode,
        GraphValue endNode)
    {
        var entityInfo = CreateEntityInfoFromRelationship(relationship, targetType);
        EnhanceRelationshipEntityInfo(entityInfo, relationship, targetType, startNode, endNode);
        return entityInfo;
    }

    private static void EnhanceRelationshipEntityInfo(EntityInfo entityInfo, GraphValue relationship, Type targetType, GraphValue pathStartNode, GraphValue pathEndNode)
    {
        var direction = GetRelationshipDirection(relationship);
        var (startNodeId, endNodeId) = GetLogicalRelationshipNodeIds(relationship, pathStartNode, pathEndNode, direction);

        // Add StartNodeId as a simple property
        var startNodeIdProperty = targetType.IsInterface
            ? targetType.GetInterface(typeof(Graph.IRelationship).Name)?.GetProperty(nameof(Graph.IRelationship.StartNodeId))
            : targetType.GetProperty(nameof(Graph.IRelationship.StartNodeId));
        if (startNodeIdProperty != null)
        {
            entityInfo.SimpleProperties[nameof(Graph.IRelationship.StartNodeId)] = new Property(
                PropertyInfo: startNodeIdProperty,
                Label: nameof(Graph.IRelationship.StartNodeId),
                IsNullable: false,
                Value: new SimpleValue(startNodeId, typeof(string))
            );
        }

        // Add EndNodeId as a simple property
        var endNodeIdProperty = targetType.IsInterface
            ? targetType.GetInterface(typeof(Graph.IRelationship).Name)?.GetProperty(nameof(Graph.IRelationship.EndNodeId))
            : targetType.GetProperty(nameof(Graph.IRelationship.EndNodeId));
        if (endNodeIdProperty != null)
        {
            entityInfo.SimpleProperties[nameof(Graph.IRelationship.EndNodeId)] = new Property(
                PropertyInfo: endNodeIdProperty,
                Label: nameof(Graph.IRelationship.EndNodeId),
                IsNullable: false,
                Value: new SimpleValue(endNodeId, typeof(string))
            );
        }

        // Add Direction
        var directionProperty = targetType.IsInterface
            ? targetType.GetInterface(typeof(Graph.IRelationship).Name)?.GetProperty(nameof(Graph.IRelationship.Direction))
            : targetType.GetProperty(nameof(Graph.IRelationship.Direction));
        if (directionProperty != null)
        {
            entityInfo.SimpleProperties[nameof(Graph.IRelationship.Direction)] = new Property(
                PropertyInfo: directionProperty,
                Label: nameof(Graph.IRelationship.Direction),
                IsNullable: false,
                Value: new SimpleValue(direction, typeof(RelationshipDirection))
            );
        }
    }

    private static string GetNodeId(GraphValue node)
    {
        // TODO: Throughout this code, we use nameof(<interface>.Id) where <interface> is IEntity, INode, IRelationship.
        // This is wrong. We should be using the label instead.

        // Try to get the Id property from the node
        if (node.Properties.TryGetValue(nameof(Graph.IEntity.Id), out var idValue))
        {
            return idValue.As<string>();
        }

        // Fallback to ElementId if no Id property
        return node.ElementId!;
    }

    private static (string StartNodeId, string EndNodeId) GetLogicalRelationshipNodeIds(
        GraphValue relationship,
        GraphValue pathStartNode,
        GraphValue pathEndNode,
        RelationshipDirection direction)
    {
        var pathStartNodeId = GetNodeId(pathStartNode);
        var pathEndNodeId = GetNodeId(pathEndNode);

        var endpointsMatchPath =
            relationship.StartNodeElementId == pathStartNode.ElementId &&
            relationship.EndNodeElementId == pathEndNode.ElementId;
        var endpointsMatchReversePath =
            relationship.StartNodeElementId == pathEndNode.ElementId &&
            relationship.EndNodeElementId == pathStartNode.ElementId;

        if (!endpointsMatchPath && !endpointsMatchReversePath)
        {
            // This should be unreachable for well-formed single-hop projections; fail fast
            // rather than fabricating logical endpoint IDs from an unrelated row shape.
            throw new GraphException(
                "Relationship endpoint element IDs do not match the projected endpoint nodes. " +
                "StartNodeId and EndNodeId cannot be reconstructed.");
        }

        var (physicalStartNodeId, physicalEndNodeId) =
            endpointsMatchPath
                ? (pathStartNodeId, pathEndNodeId)
                : (pathEndNodeId, pathStartNodeId);

        return direction switch
        {
            RelationshipDirection.Outgoing => (physicalStartNodeId, physicalEndNodeId),
            RelationshipDirection.Incoming => (physicalEndNodeId, physicalStartNodeId),
            _ => (physicalStartNodeId, physicalEndNodeId)
        };
    }

    private static RelationshipDirection GetRelationshipDirection(GraphValue relationship)
    {
        // Try to get the direction from the relationship properties
        if (relationship.Properties.TryGetValue(nameof(Graph.IRelationship.Direction), out var directionValue))
        {
            if (directionValue is RelationshipDirection direction && Enum.IsDefined(direction))
            {
                return direction;
            }

            // Try to parse if it's stored as a string or number
            if (Enum.TryParse<RelationshipDirection>(directionValue.ToString(), out var parsedDirection) &&
                Enum.IsDefined(parsedDirection))
            {
                return parsedDirection;
            }
        }

        // Default to Outgoing if no direction is found
        return RelationshipDirection.Outgoing;
    }

    private static EntityInfo CreatePathSegmentEntityInfo(
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
            Label: typeof(IGraphPathSegment<,,>).Name,
            ActualLabels: [],
            SimpleProperties: new Dictionary<string, Property>(),
            ComplexProperties: complexProperties
        );
    }

    private List<EntityInfo> ProcessNodes(IReadOnlyList<GraphRecord> records, Type targetType)
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

    private static Dictionary<string, Property> ExtractAllSimplePropertiesForDynamicEntity(
        IReadOnlyDictionary<string, object> properties)
    {
        var result = new Dictionary<string, Property>();
        foreach (var (key, value) in properties)
        {
            if (key is GraphValueConverter.MetadataPropertyName or GraphValueConverter.EntityKindPropertyName)
                continue;

            Serialized serializedValue;
            // Dictionaries (wire maps) are not simple collections; they pass through as scalar values.
            if (value is IEnumerable enumerable and not string and not byte[] and not IDictionary)
            {
                var items = enumerable.Cast<object?>().ToList();
                var elementType = InferDynamicCollectionElementType(items);
                serializedValue = CreateSimpleCollection(key, items, elementType);
            }
            else
            {
                // Driver-specific scalar values have already been normalized by the wire adapter.
                var convertedValue = GraphValueConverter.ConvertTo(value, value?.GetType() ?? typeof(object));
                serializedValue = new SimpleValue(convertedValue!, convertedValue?.GetType() ?? typeof(object));
            }

            result[key] = new Property(
                PropertyInfo: null!,
                Label: key,
                IsNullable: value == null,
                Value: serializedValue
            );
        }
        return result;
    }

    private static void RemoveDynamicComplexValueStructuralProperties(
        Dictionary<string, Property> simpleProperties)
    {
        simpleProperties.Remove(nameof(Graph.IEntity.Id));
        simpleProperties.Remove(nameof(Graph.INode.Labels));
    }

    private static Type InferDynamicCollectionElementType(IReadOnlyList<object?> items)
    {
        var elementTypes = items
            .Where(item => item is not null)
            .Select(item => item!.GetType())
            .Distinct()
            .ToList();

        if (elementTypes.Count != 1)
        {
            return typeof(object);
        }

        var elementType = elementTypes[0];
        if (items.Any(item => item is null) &&
            elementType.IsValueType &&
            Nullable.GetUnderlyingType(elementType) is null)
        {
            return typeof(Nullable<>).MakeGenericType(elementType);
        }

        return elementType;
    }

    private EntityInfo CreateEntityInfoFromNode(GraphValue node, Type targetType)
    {
        // Discover the actual type from metadata, labels, or fall back to target type
        var actualType = DiscoverActualNodeType(node, targetType);

        Dictionary<string, Property> simpleProperties;
        var label = node.Labels.Count == 0 ? actualType.Name : node.Labels[0];

        // Handle dynamic nodes differently
        if (typeof(Graph.DynamicNode).IsAssignableFrom(actualType))
        {
            simpleProperties = ExtractAllSimplePropertiesForDynamicEntity(node.Properties);
            // Add Id property if not present
            if (!simpleProperties.ContainsKey(nameof(Graph.IEntity.Id)) && node.Properties.TryGetValue(nameof(Graph.IEntity.Id), out var idValue))
            {
                simpleProperties[nameof(Graph.IEntity.Id)] = new Property(
                    PropertyInfo: default!, // null is expected for dynamic
                    Label: nameof(Graph.IEntity.Id),
                    IsNullable: false,
                    Value: new SimpleValue(idValue ?? string.Empty, typeof(string))
                );
            }
        }
        else
        {
            simpleProperties = ExtractSimpleProperties(node.Properties, actualType);
        }

        // Add Labels property for all nodes (both dynamic and typed)
        // This enables filtering by labels in LINQ queries
        var labelsProperty = actualType.GetProperty(nameof(Graph.INode.Labels));
        if (labelsProperty != null)
        {
            var labelsValue = node.Labels.ToList();
            simpleProperties[nameof(Graph.INode.Labels)] = new Property(
                PropertyInfo: labelsProperty,
                Label: nameof(Graph.INode.Labels),
                IsNullable: false,
                Value: new SimpleCollection(
                    labelsValue.Select(l => new SimpleValue(l, typeof(string))).ToList(),
                    typeof(string))
            );
        }

        return new EntityInfo(
            ActualType: actualType,
            Label: label,
            ActualLabels: node.Labels.ToList(),
            SimpleProperties: simpleProperties,
            ComplexProperties: new Dictionary<string, Property>()
        );
    }

    private EntityInfo CreateEntityInfoFromRelationship(GraphValue relationship, Type targetType)
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
        if (typeof(Graph.DynamicRelationship).IsAssignableFrom(actualType))
        {
            simpleProperties = ExtractAllSimplePropertiesForDynamicEntity(relationship.Properties);
            // Add Id property if not present
            if (!simpleProperties.ContainsKey(nameof(Graph.IEntity.Id)) && relationship.Properties.TryGetValue(nameof(Graph.IEntity.Id), out var idValue))
            {
                simpleProperties[nameof(Graph.IEntity.Id)] = new Property(
                    PropertyInfo: default!, // null is expected for dynamic
                    Label: nameof(Graph.IEntity.Id),
                    IsNullable: false,
                    Value: new SimpleValue(idValue ?? string.Empty, typeof(string))
                );
            }
        }
        else
        {
            simpleProperties = ExtractSimpleProperties(relationship.Properties, actualType);
            // Add ElementId as the Id property
            if (actualType.GetProperty(nameof(Graph.IEntity.Id)) != null)
            {
                simpleProperties[nameof(Graph.IEntity.Id)] = new Property(
                    PropertyInfo: actualType.GetProperty(nameof(Graph.IEntity.Id))!,
                    Label: nameof(Graph.IEntity.Id),
                    IsNullable: false,
                    Value: new SimpleValue(relationship.Properties[nameof(Graph.IEntity.Id)], typeof(string))
                );
            }
        }

        // Add Type property for all relationships (both dynamic and typed)
        // This enables filtering by type in LINQ queries
        var typeProperty = actualType.GetProperty(nameof(Graph.IRelationship.Type));
        if (typeProperty != null)
        {
            simpleProperties[nameof(Graph.IRelationship.Type)] = new Property(
                PropertyInfo: typeProperty,
                Label: nameof(Graph.IRelationship.Type),
                IsNullable: false,
                Value: new SimpleValue(label, typeof(string))
            );
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
    private Type DiscoverActualNodeType(GraphValue node, Type targetType)
    {
        // Special handling for dynamic entities
        if (targetType == typeof(Graph.DynamicNode))
        {
            _logger.LogDebugGraphResultProcessor1107(targetType.Name);
            return typeof(Graph.DynamicNode);
        }

        // Step 1: Try to get type from stored metadata
        var metadataType = GraphValueConverter.GetTypeFromMetadata(node.Properties);
        if (metadataType != null && IsCompatibleType(metadataType, targetType))
        {
            _logger.LogDebugGraphResultProcessor1116(metadataType.Name, targetType.Name);
            return metadataType;
        }

        // Step 2: Try to discover from node labels
        var labelType = DiscoverTypeFromNodeLabels(node.Labels, targetType);
        if (labelType != null)
        {
            _logger.LogDebugGraphResultProcessor1125(labelType.Name, targetType.Name);
            return labelType;
        }

        // Step 3: Fall back to target type
        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebugGraphResultProcessor1133(targetType.Name, string.Join(", ", node.Labels));
        }
        return targetType;
    }

    /// <summary>
    /// Discovers the actual type of a relationship using metadata, type, and compatibility checks.
    /// </summary>
    private Type DiscoverActualRelationshipType(GraphValue relationship, Type targetType)
    {
        // Special handling for dynamic entities
        if (targetType == typeof(Graph.DynamicRelationship))
        {
            _logger.LogDebugGraphResultProcessor1147(targetType.Name);
            return typeof(Graph.DynamicRelationship);
        }

        // Step 1: Try to get type from stored metadata
        var metadataType = GraphValueConverter.GetTypeFromMetadata(relationship.Properties);
        if (metadataType != null && IsCompatibleType(metadataType, targetType))
        {
            _logger.LogDebugGraphResultProcessor1156(metadataType.Name, targetType.Name);
            return metadataType;
        }

        // Step 2: Try to discover from relationship type/label
        var labelType = DiscoverTypeFromRelationshipType(relationship.Type, targetType);
        if (labelType != null)
        {
            _logger.LogDebugGraphResultProcessor1165(labelType.Name, targetType.Name);
            return labelType;
        }

        // Step 3: Fall back to target type
        _logger.LogDebugGraphResultProcessor1171(targetType.Name, relationship.Type ?? "null");
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
    private static Type? DiscoverTypeFromNodeLabels(IReadOnlyList<string> labels, Type targetType)
    {
        if (!labels.Any())
            return null;

        // Get all types that are assignable to the target type. This must NOT be scoped to
        // INode types: targetType here can be a plain complex-property POCO (e.g. the element
        // type of a List<T> complex property) that never implements INode, yet its concrete
        // subtype is exactly what the stored label encodes and what we need to recover.
        var candidateTypes = GetKnownConcreteTypes()
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
    private static Type? DiscoverTypeFromRelationshipType(string? relationshipType, Type targetType)
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
    /// Gets all known concrete classes from loaded assemblies that could be the actual type
    /// behind a node label: both real graph node types (INode) and the plain POCO types used
    /// as complex-property values (which are never INode themselves - see
    /// <see cref="DiscoverTypeFromNodeLabels"/>). The caller narrows this down via an
    /// assignability check against the specific target type it is resolving.
    /// </summary>
    private static IEnumerable<Type> GetKnownConcreteTypes()
    {
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
            .Where(t => t.IsClass && !t.IsAbstract)
            .Where(t => t != typeof(Graph.DynamicNode) && t != typeof(Graph.DynamicRelationship)); // Exclude dynamic entities from schema discovery
    }

    /// <summary>
    /// Gets all known relationship types from loaded assemblies.
    /// </summary>
    private static IEnumerable<Type> GetKnownRelationshipTypes()
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
            .Where(t => t.IsClass && !t.IsAbstract && typeof(Graph.IRelationship).IsAssignableFrom(t))
            .Where(t => t != typeof(Graph.DynamicRelationship)); // Exclude DynamicRelationship from schema discovery
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
            if (key == GraphValueConverter.MetadataPropertyName)
                continue;

            // Check if this property is in the schema
            if (schema.SimpleProperties.TryGetValue(key, out var propertySchema))
            {
                object convertedValue;
                try
                {
                    convertedValue = GraphValueConverter.ConvertTo(value, propertySchema.PropertyInfo.PropertyType)
                        ?? throw new GraphException(
                            $"Failed to convert value for property '{key}' to '{propertySchema.PropertyInfo.PropertyType}'.");
                }
                catch (GraphException)
                {
                    throw;
                }
                catch (Exception exception) when (exception is ArgumentException or InvalidCastException or FormatException or OverflowException)
                {
                    throw new GraphException(
                        $"Failed to convert value for property '{key}' to '{propertySchema.PropertyInfo.PropertyType}'.",
                        exception);
                }

                Property property = new(
                    propertySchema.PropertyInfo,
                    propertySchema.PropertyName,
                    propertySchema.IsNullable,
                    propertySchema.PropertyType == PropertyType.SimpleCollection
                        ? CreateSimpleCollection(key, convertedValue, propertySchema.ElementType!)
                        : new SimpleValue(convertedValue, propertySchema.PropertyInfo.PropertyType));

                result[key] = property;
            }
        }

        return result;
    }

    private static SimpleCollection CreateSimpleCollection(string propertyName, object convertedValue, Type elementType)
    {
        if (convertedValue is string || convertedValue is not IEnumerable enumerable)
        {
            throw new GraphException(
                $"Expected an enumerable value for simple collection property '{propertyName}' with element type " +
                $"'{elementType}', but received '{convertedValue.GetType()}'.");
        }

        var items = new List<SimpleValue>();
        foreach (var item in enumerable)
        {
            items.Add(new SimpleValue(item!, elementType));
        }

        return new SimpleCollection(items, elementType);
    }

    private List<EntityInfo> ProcessMixedEntities(IReadOnlyList<GraphRecord> records)
    {
        var results = new List<EntityInfo>();

        foreach (var record in records)
        {
            // Entity search returns records with 'entity' column that can be either nodes or PathSegment maps
            if (record.Values.TryGetValue("entity", out var wireEntityValue))
            {
                var entityValue = wireEntityValue.ToObject();
                if (entityValue is GraphValue { Kind: GraphValueKind.Node } node)
                {
                    // Process as a node
                    var nodeEntityInfo = CreateEntityInfoFromNode(node, typeof(Graph.INode));
                    results.Add(nodeEntityInfo);
                }
                else if (entityValue is IReadOnlyDictionary<string, object> pathSegmentMap)
                {
                    // This is a PathSegment map, process as a relationship
                    var pathSegment = DeserializePathSegment(pathSegmentMap);
                    if (pathSegment != null)
                    {
                        var relationshipEntityInfo = CreateEnhancedRelationshipEntityInfo(
                            pathSegment.Relationship,
                            typeof(Graph.IRelationship),
                            pathSegment.StartNode.Node,
                            pathSegment.EndNode.Node);
                        results.Add(relationshipEntityInfo);
                    }
                }
            }
        }

        return results;
    }
}
