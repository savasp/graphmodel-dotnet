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

using System.Collections;
using System.Reflection;
using Microsoft.Extensions.Logging;
using Neo4j.Driver;

namespace Cvoya.Graph.Model.Neo4j.Serialization;

internal class GraphEntitySerializer(GraphContext context)
{
    private readonly ILogger<GraphEntitySerializer>? _logger = context.LoggerFactory?.CreateLogger<GraphEntitySerializer>();
    private readonly ValueConverter _valueConverter = new ValueConverter();
    private readonly EntityFactory _entityFactory = new EntityFactory(context.LoggerFactory?.CreateLogger<EntityFactory>());

    public async Task<NodeSerializationResult> SerializeNodeAsync(INode node, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(node);

        var nodeType = node.GetType();
        var label = Labels.GetLabelFromType(nodeType);

        // Try to use generated serializer for simple properties
        Dictionary<string, object?> simpleProps;
        var serializer = EntitySerializerRegistry.GetSerializer(nodeType);
        if (serializer is not null)
        {
            // Generated serializer handles all the simple property extraction
            simpleProps = serializer.Serialize(node);
        }
        else
        {
            // Fallback to reflection-based approach
            simpleProps = ExtractSimplePropertiesFromObject(node);
        }

        // Now handle complex properties - this is where the graph magic happens
        var complexProps = await ExtractComplexPropertiesAsync(node, cancellationToken);

        return new NodeSerializationResult
        {
            SimpleProperties = simpleProps,
            ComplexProperties = complexProps,
            Label = label
        };
    }

    public async Task<T> DeserializeNodeAsync<T>(
        string nodeId,
        GraphTransaction transaction,
        bool useMostDerivedType = true,
        CancellationToken cancellationToken = default)
        where T : INode
    {
        ArgumentException.ThrowIfNullOrEmpty(nodeId);
        ArgumentNullException.ThrowIfNull(transaction);

        // First, fetch just the node to determine its type if needed
        Type targetType = typeof(T);

        if (useMostDerivedType)
        {
            var label = Labels.GetLabelFromType(typeof(T));

            // Try to resolve the most derived type from labels
            var resolvedType = Labels.GetTypeFromLabel(label);
            if (resolvedType != null)
            {
                targetType = resolvedType;
                _logger?.LogDebug("Resolved type {ResolvedType} from label {Label} for requested type {RequestedType}",
                    resolvedType.Name, label, typeof(T).Name);
            }
        }

        // Now fetch the full node data WITH the graph traversal
        var cypher = @"
        MATCH path = (n {Id: $nodeId})-[r*0..]-(target)
        WHERE ALL(rel IN relationships(path) WHERE type(rel) STARTS WITH $propertyPrefix)
        WITH n, relationships(path) AS rels, nodes(path) AS nodes
        WHERE size(nodes) = size(rels) + 1
        WITH n, 
            [i IN range(0, size(rels)-1) | 
                {
                    Node: nodes[i+1], 
                    RelType: type(rels[i]), 
                    RelationshipProperties: properties(rels[i])
                }
            ] AS relatedNodes
        RETURN n, relatedNodes";

        var result = await transaction.Session.ExecuteReadAsync(tx => tx.RunAsync(cypher, new
        {
            nodeId,
            propertyPrefix = GraphDataModel.PropertyRelationshipTypeNamePrefix
        }));

        var record = await result.SingleAsync();
        var neo4jNode = record["n"].As<global::Neo4j.Driver.INode>();
        var relatedNodes = record["relatedNodes"].As<List<object>>();

        // Try to use generated serializer first for the main entity
        var entity = await CreateEntityAsync(targetType, neo4jNode);

        // Ensure we can cast to the requested type
        if (entity is not T typedEntity)
        {
            throw new GraphException($"Cannot cast entity of type {entity.GetType()} to requested type {typeof(T)}");
        }

        // Populate complex properties recursively
        await PopulateComplexPropertiesAsync(entity, relatedNodes, useMostDerivedType, cancellationToken);

        return typedEntity;
    }

    public async Task<object> DeserializeNodeFromNeo4jNodeAsync(
            global::Neo4j.Driver.INode neo4jNode,
            Type targetType,
            bool useMostDerivedType = true,
            CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(neo4jNode);
        ArgumentNullException.ThrowIfNull(targetType);

        // Resolve the most derived type if requested
        if (useMostDerivedType && neo4jNode.Labels.Any())
        {
            // Use the labels from the Neo4j node to find the most derived type
            var label = neo4jNode.Labels[0]; // Use the first label for resolution
            var resolvedType = Labels.GetMostDerivedType(targetType, label);

            if (resolvedType == null)
            {
                throw new GraphException($"No type found for label '{label}' that is assignable to {targetType.Name}. " +
                    "Ensure the label matches a registered type in the GraphDataModel.");
            }

            if (resolvedType != targetType)
            {
                targetType = resolvedType;
                _logger?.LogDebug("Resolved type {ResolvedType} from labels {Labels} for requested type {RequestedType}",
                    resolvedType.Name, string.Join(",", neo4jNode.Labels), targetType.Name);
            }
        }

        // Use the existing CreateEntityAsync method to avoid duplication
        return await CreateEntityAsync(targetType, neo4jNode);
    }

    public Task<RelationshipSerializationResult> SerializeRelationshipAsync(IRelationship relationship, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(relationship);

        var type = relationship.GetType();
        var relType = Labels.GetLabelFromType(type);

        var props = new Dictionary<string, object?>();

        foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (prop.GetIndexParameters().Length > 0) continue;

            var value = prop.GetValue(relationship);
            if (value == null) continue;

            var propType = prop.PropertyType;

            // Relationships can only have simple properties
            if (GraphDataModel.IsSimple(propType) || GraphDataModel.IsCollectionOfSimple(propType))
            {
                props[prop.Name] = _valueConverter.ConvertToNeo4j(value);
            }
            else
            {
                throw new GraphException($"Relationship property '{prop.Name}' of type '{propType}' is not allowed. Relationships can only have simple properties.");
            }
        }

        return Task.FromResult(new RelationshipSerializationResult
        {
            Properties = props,
            Type = relType,
            SourceId = relationship.SourceId,
            TargetId = relationship.TargetId
        });
    }

    public async Task<T> DeserializeRelationshipAsync<T>(
        string relationshipId,
        IAsyncTransaction transaction,
        bool useMostDerivedType = true,
        CancellationToken cancellationToken = default)
        where T : IRelationship
    {
        ArgumentException.ThrowIfNullOrEmpty(relationshipId);
        ArgumentNullException.ThrowIfNull(transaction);

        var cypher = "MATCH ()-[r {Id: $relationshipId}]->() RETURN r, type(r) as relType";
        var result = await transaction.RunAsync(cypher, new { relationshipId });
        var record = await result.SingleAsync();
        var neo4jRel = record["r"].As<global::Neo4j.Driver.IRelationship>();
        var relType = record["relType"].As<string>();

        Type targetType = typeof(T);

        if (useMostDerivedType)
        {
            // Try to resolve the most derived type from relationship type
            var resolvedType = Labels.GetMostDerivedType(targetType, relType);
            if (resolvedType != null)
            {
                targetType = resolvedType;
                _logger?.LogDebug("Resolved relationship type {ResolvedType} from Neo4j type {RelType} for requested type {RequestedType}",
                    resolvedType.Name, relType, typeof(T).Name);
            }
        }

        var entity = _entityFactory.CreateInstance(targetType, neo4jRel);

        if (entity is not T typedEntity)
        {
            throw new GraphException($"Cannot cast entity of type {entity.GetType()} to requested type {typeof(T)}");
        }

        PopulateSimpleProperties(entity, neo4jRel);
        return typedEntity;
    }

    private void PopulateSimpleProperties(object entity, global::Neo4j.Driver.IEntity neo4jEntity)
    {
        var type = entity.GetType();

        foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!prop.CanWrite || prop.GetIndexParameters().Length > 0) continue;

            if (neo4jEntity.Properties.TryGetValue(prop.Name, out var value))
            {
                var convertedValue = _valueConverter.ConvertFromNeo4j(value, prop.PropertyType);
                prop.SetValue(entity, convertedValue);
            }
        }
    }

    private async Task PopulateComplexPropertiesAsync(
        object entity,
        List<object> relatedNodes,
        bool useMostDerivedType,
        CancellationToken cancellationToken)
    {
        var entityType = entity.GetType();
        var complexProperties = GraphDataModel.GetComplexProperties(entityType);

        // Group related nodes by property name and handle collections with sequence numbers
        var propertyGroups = new Dictionary<string, List<(global::Neo4j.Driver.INode node, int? sequenceNumber, IDictionary<string, object> relData)>>();

        foreach (var relatedNodeInfo in relatedNodes)
        {
            if (relatedNodeInfo is not IDictionary<string, object> relatedNodeData)
            {
                continue;
            }

            if (relatedNodeData["Node"] is not global::Neo4j.Driver.INode neo4jNode || relatedNodeData["RelType"] is not string relationshipType) continue;

            // Convert relationship type back to property name
            var propertyName = GraphDataModel.RelationshipTypeNameToPropertyName(relationshipType);

            if (!propertyGroups.ContainsKey(propertyName))
            {
                propertyGroups[propertyName] = [];
            }

            // Extract sequence number from relationship properties if it exists
            int? sequenceNumber = null;
            if (relatedNodeData.TryGetValue("RelationshipProperties", out var relProps) &&
                relProps is IDictionary<string, object> relPropsDict &&
                relPropsDict.TryGetValue("SequenceNumber", out var seqNum))
            {
                sequenceNumber = Convert.ToInt32(seqNum);
            }

            propertyGroups[propertyName].Add((neo4jNode, sequenceNumber, relatedNodeData));
        }

        // Now populate each property
        foreach (var prop in complexProperties)
        {
            if (!propertyGroups.TryGetValue(prop.Name, out var nodesForProperty))
            {
                continue;
            }

            if (GraphDataModel.IsCollectionOfComplex(prop.PropertyType))
            {
                // Handle collection properties - sort by sequence number!
                var elementType = prop.PropertyType.GetGenericArguments().FirstOrDefault()
                    ?? prop.PropertyType.GetElementType();

                if (elementType == null)
                {
                    continue;
                }

                var items = new List<object>();

                // Sort by sequence number to maintain original order
                foreach (var (neo4jNode, seqNum, _) in nodesForProperty.OrderBy(x => x.sequenceNumber ?? 0))
                {
                    var deserializedItem = await DeserializeComplexPropertyAsync(
                        neo4jNode,
                        elementType,
                        useMostDerivedType,
                        cancellationToken);

                    if (deserializedItem != null)
                    {
                        items.Add(deserializedItem);
                    }
                }

                // Create the appropriate collection type
                var collection = CreateCollection(prop.PropertyType, items);
                prop.SetValue(entity, collection);
            }
            else
            {
                // Handle single complex property
                if (nodesForProperty.Count > 0)
                {
                    var (neo4jNode, _, _) = nodesForProperty[0];

                    var deserializedValue = await DeserializeComplexPropertyAsync(
                        neo4jNode,
                        prop.PropertyType,
                        useMostDerivedType,
                        cancellationToken);

                    prop.SetValue(entity, deserializedValue);
                }
            }
        }
    }

    private Task<object?> DeserializeComplexPropertyAsync(
        global::Neo4j.Driver.INode neo4jNode,
        Type targetType,
        bool useMostDerivedType,
        CancellationToken cancellationToken)
    {
        // Apply most-derived type resolution if requested
        if (useMostDerivedType)
        {
            var label = Labels.GetLabelFromType(targetType);
            var resolvedType = Labels.GetMostDerivedType(targetType, label);
            if (resolvedType != null)
            {
                targetType = resolvedType;
                _logger?.LogDebug($"Resolved complex property to type {resolvedType.Name} from label {label} for target type {targetType.Name}");
            }
        }

        // Create instance of the complex type
        var instance = Activator.CreateInstance(targetType);
        if (instance == null)
        {
            return Task.FromResult<object?>(null);
        }

        // Populate its simple properties from the Neo4j node
        foreach (var prop in GraphDataModel.GetSimpleProperties(targetType))
        {
            if (neo4jNode.Properties.TryGetValue(prop.Name, out var value))
            {
                var convertedValue = _valueConverter.ConvertFromNeo4j(value, prop.PropertyType);
                prop.SetValue(instance, convertedValue);
            }
        }

        return Task.FromResult<object?>(instance);
    }

    private static object CreateCollection(Type collectionType, List<object> items)
    {
        var elementType = collectionType.IsArray
            ? collectionType.GetElementType()!
            : collectionType.GetGenericArguments()[0];

        if (collectionType.IsArray)
        {
            var array = Array.CreateInstance(elementType, items.Count);
            for (var i = 0; i < items.Count; i++)
            {
                array.SetValue(items[i], i);
            }
            return array;
        }

        // Handle List<T> and other collection types
        var listType = typeof(List<>).MakeGenericType(elementType);
        var list = (IList)Activator.CreateInstance(listType)!;
        foreach (var item in items)
        {
            list.Add(item);
        }

        // If the target type is exactly List<T>, return it
        if (collectionType.IsAssignableFrom(listType))
        {
            return list;
        }

        // Otherwise try to convert to the specific collection type
        try
        {
            return Activator.CreateInstance(collectionType, list)!;
        }
        catch
        {
            // If we can't create the specific type, return the list
            return list;
        }
    }

    public static object CreateCollection(Type collectionType, List<INode> items)
    {
        var elementType = collectionType.IsArray
            ? collectionType.GetElementType()!
            : collectionType.GetGenericArguments()[0];

        if (collectionType.IsArray)
        {
            var array = Array.CreateInstance(elementType, items.Count);
            for (var i = 0; i < items.Count; i++)
            {
                array.SetValue(items[i], i);
            }
            return array;
        }

        var listType = typeof(List<>).MakeGenericType(elementType);
        var list = (IList)Activator.CreateInstance(listType)!;
        foreach (var item in items)
        {
            list.Add(item);
        }

        return list;
    }

    private Task<List<ComplexPropertyInfo>> ExtractComplexPropertiesAsync(INode node, CancellationToken cancellationToken)
    {
        var complexProps = new List<ComplexPropertyInfo>();
        var nodeType = node.GetType();

        // Use GraphDataModel to get complex properties
        var complexProperties = GraphDataModel.GetComplexProperties(nodeType);

        foreach (var prop in complexProperties)
        {
            var value = prop.GetValue(node);
            if (value == null) continue;

            // Check if it's a single complex property (like Address)
            if (GraphDataModel.IsComplex(prop.PropertyType) && !GraphDataModel.IsCollectionOfComplex(prop.PropertyType))
            {
                // Extract the properties of the complex object
                var complexObjProps = ExtractSimplePropertiesFromObject(value);

                // Determine the label - use the actual runtime type for polymorphism
                var actualType = value.GetType();
                var label = Labels.GetLabelFromType(actualType);

                // Create a NodeSerializationResult for the complex property
                var serializedComplex = new NodeSerializationResult
                {
                    SimpleProperties = complexObjProps,
                    ComplexProperties = new List<ComplexPropertyInfo>(), // Complex properties can't have nested complex properties
                    Label = label
                };

                complexProps.Add(new ComplexPropertyInfo(
                    PropertyName: prop.Name,
                    PropertyValue: value,
                    SerializedNode: serializedComplex,
                    RelationshipType: GraphDataModel.PropertyNameToRelationshipTypeName(prop.Name),
                    CollectionIndex: null // No sequence number for single properties
                ));
            }
            // Check if it's a collection of complex properties
            else if (GraphDataModel.IsCollectionOfComplex(prop.PropertyType) && value is IEnumerable collection)
            {
                int sequenceNumber = 0;
                foreach (var item in collection)
                {
                    if (item != null)
                    {
                        var itemProps = ExtractSimplePropertiesFromObject(item);

                        // Use actual runtime type for the label
                        var actualType = item.GetType();
                        var label = Labels.GetLabelFromType(actualType);

                        var serializedComplex = new NodeSerializationResult
                        {
                            SimpleProperties = itemProps,
                            ComplexProperties = new List<ComplexPropertyInfo>(),
                            Label = label
                        };

                        complexProps.Add(new ComplexPropertyInfo(
                            PropertyName: prop.Name,
                            PropertyValue: item,
                            SerializedNode: serializedComplex,
                            RelationshipType: GraphDataModel.PropertyNameToRelationshipTypeName(prop.Name),
                            CollectionIndex: sequenceNumber++ // This maintains the order!
                        ));
                    }
                }
            }
        }

        return Task.FromResult(complexProps);
    }

    private Dictionary<string, object?> ExtractSimplePropertiesFromObject(object obj)
    {
        var props = new Dictionary<string, object?>();
        var type = obj.GetType();

        // Only extract simple properties - complex properties are handled separately
        foreach (var prop in GraphDataModel.GetSimpleProperties(type))
        {
            var value = prop.GetValue(obj);
            if (value != null)
            {
                // Convert to Neo4j-compatible value
                var convertedValue = _valueConverter.ConvertToNeo4j(value);
                if (convertedValue != null)
                {
                    props[prop.Name] = convertedValue;
                }
            }
        }

        return props;
    }

    private async Task<object> CreateEntityAsync(Type targetType, global::Neo4j.Driver.INode neo4jNode)
    {
        // First try generated serializer
        var serializer = EntitySerializerRegistry.GetSerializer(targetType);
        if (serializer != null)
        {
            return await serializer.DeserializeAsync(neo4jNode);
        }

        // Fallback to factory creation - use CreateInstance instead of Create
        var entity = _entityFactory.CreateInstance(targetType, neo4jNode);

        // Populate simple properties from the Neo4j node
        foreach (var prop in GraphDataModel.GetSimpleProperties(targetType))
        {
            if (neo4jNode.Properties.TryGetValue(prop.Name, out var value))
            {
                var convertedValue = _valueConverter.ConvertFromNeo4j(value, prop.PropertyType);
                prop.SetValue(entity, convertedValue);
            }
        }

        return entity;
    }
}