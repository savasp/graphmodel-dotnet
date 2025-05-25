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
using Cvoya.Graph.Model;
using Cvoya.Graph.Provider.Neo4j.Conversion;
using Cvoya.Graph.Provider.Neo4j.Query;
using Cvoya.Graph.Provider.Neo4j.Schema;
using Microsoft.Extensions.Logging;
using Neo4j.Driver;
using NLog.Targets;

namespace Cvoya.Graph.Provider.Neo4j.Entities;

/// <summary>
/// Manages Neo4j node operations.
/// </summary>
internal class Neo4jNodeManager : Neo4jEntityManagerBase
{
    private class ObjectTrackingInfo
    {
        public required global::Neo4j.Driver.INode Neo4jNode { get; set; }
        public required IList<(string RelationshipType, string TargetId)> ComplexProperties { get; set; }
        public object? NewObject { get; set; }
    }

    /// <summary>
    /// Initializes a new instance of the Neo4jNodeManager class.
    /// </summary>
    public Neo4jNodeManager(
        Neo4jQueryExecutor queryExecutor,
        Neo4jConstraintManager constraintManager,
        Neo4jEntityConverter entityConverter,
        Microsoft.Extensions.Logging.ILogger? logger = null)
        : base(queryExecutor, constraintManager, entityConverter, logger)
    {
    }

    /// <summary>
    /// Creates a node in Neo4j.
    /// </summary>
    /// <param name="parentId">Optional parent node ID for hierarchical relationships</param>
    /// <param name="node">The node to create</param>
    /// <param name="options">Graph operation options</param>
    /// <param name="tx">The transaction to use</param>
    /// <param name="propertyName">Optional property name when creating from a parent relationship</param>
    /// <returns>The ID of the created node</returns>
    public async Task<string> CreateNode(string? parentId, object node, GraphOperationOptions options, IAsyncTransaction tx, string? propertyName = null)
    {
        // Create a dictionary to track object instances if not already exists
        var objectTracker = new Dictionary<object, string>();

        return await CreateNodeInternal(parentId, node, options, tx, propertyName, objectTracker);
    }

    /// <summary>
    /// Updates a node with the given data.
    /// </summary>
    /// <param name="node">The node to update</param>
    /// <param name="options">Graph operation options</param>
    /// <param name="tx">The transaction to use</param>
    public async Task UpdateNode(Cvoya.Graph.Model.INode node, GraphOperationOptions options, IAsyncTransaction tx)
    {
        var (simpleProps, _) = GetSimpleAndComplexProperties(node);

        var cypher = $"MATCH (n) WHERE n.{nameof(Model.INode.Id)} = '{node.Id}' SET n += $props";
        await tx.RunAsync(cypher, new
        {
            props = ConvertPropertiesToNeo4j(simpleProps)
        });
    }

    /// <summary>
    /// Gets a node by its ID and type.
    /// </summary>
    /// <param name="id">The ID of the node</param>
    /// <param name="options">Graph operation options</param>
    /// <param name="tx">The transaction to use</param>
    /// <returns>The node instance</returns>
    /// <exception cref="GraphException">Thrown if the node is not found</exception>
    public async Task<T> GetNode<T>(string id, GraphOperationOptions options, IAsyncTransaction tx)
    where T : class, Cvoya.Graph.Model.INode, new()
    {
        var label = Neo4jTypeManager.GetLabel(typeof(T));

        var cypher = $@"
            MATCH path = (n:{label} {{ {nameof(Model.INode.Id)}: '{id}'}})-[*0..]-(target)
            WHERE ALL(rel IN relationships(path) WHERE
                type(rel) STARTS WITH '{Helpers.PropertyRelationshipTypeNamePrefix}' AND 
                type(rel) ENDS WITH '{Helpers.PropertyRelationshipTypeNameSuffix}')

            WITH DISTINCT target

            OPTIONAL MATCH (target)-[r]->(m)
            WHERE type(r) STARTS WITH '{Helpers.PropertyRelationshipTypeNamePrefix}' AND 
                type(r) ENDS WITH '{Helpers.PropertyRelationshipTypeNameSuffix}'

            WITH target, collect({{
                RelationshipType: type(r), 
                TargetId: elementId(m)
            }}) AS complexProperties

            RETURN {{
                Node: target,
                ComplexProperties: [c IN complexProperties WHERE c.RelationshipType IS NOT NULL AND c.TargetId IS NOT NULL]
            }} AS node";

        var result = await tx.RunAsync(cypher, new { id });
        var records = await result.ToListAsync();

        if (records.Count == 0)
        {
            var ex = new KeyNotFoundException($"Node with ID '{id}' not found");
            throw new GraphException(ex.Message, ex);
        }

        // We count on the fact that the query returns the records in order of traversal.
        // This means that we can always discover the .NET type of the object that we need
        // to create in order to assign to a complex property.

        // Track the objects we created using the neoj4 element ID
        var objectTracker = records
            .Select(r => r["node"].As<IDictionary<string, object>>())
            .Select(r => new ObjectTrackingInfo
            {
                Neo4jNode = r["Node"].As<global::Neo4j.Driver.INode>(),
                ComplexProperties = r["ComplexProperties"].As<IList<IDictionary<string, object>>>().Select(kv => (kv["RelationshipType"].As<string>(), kv["TargetId"].As<string>())).ToList(),
                NewObject = (object?)null
            }).ToDictionary(r => r.Neo4jNode.ElementId, r => r);

        // The first record is the node we are deserializing.

        var firstRecord = objectTracker.Values.FirstOrDefault()
            ?? throw new GraphException($"Node with ID '{id}' not found");
        firstRecord.NewObject = await EntityConverter.DeserializeNode<T>(firstRecord.Neo4jNode);

        // Now, traverse the graph, creating objects along the way and tracking them with the objectTracker.

        await TraverseGraphForComplexProperties(objectTracker, firstRecord);

        return firstRecord.NewObject as T ??
            throw new GraphException($"Node with ID '{id}' could not be constructed");
    }

    /// <summary>
    /// Gets a node by its ID and type.
    /// </summary>
    /// <param name="ids">The IDs for the nodes to retrieve</param>
    /// <param name="options">Graph operation options</param>
    /// <param name="tx">The transaction to use</param>
    /// <returns>The node instance</returns>
    /// <exception cref="GraphException">Thrown if the node is not found</exception>
    public async Task<IEnumerable<T>> GetNodes<T>(IEnumerable<string> ids, GraphOperationOptions options, IAsyncTransaction tx)
        where T : class, Cvoya.Graph.Model.INode, new()
    {
        if (!ids.Any())
        {
            return Enumerable.Empty<T>();
        }

        var label = Neo4jTypeManager.GetLabel(typeof(T));
        var idList = string.Join(",", ids.Select(id => $"'{id}'"));
        var cypher = $"MATCH (n:{label}) WHERE n.{nameof(Model.INode.Id)} IN [{idList}] RETURN n, elementId(n) as neo4jNodeId";

        var result = await tx.RunAsync(cypher);
        var records = await result.ToListAsync();

        if (!records.Any())
        {
            return Enumerable.Empty<T>();
        }

        var nodes = new List<T>();
        foreach (var record in records)
        {
            var node = await EntityConverter.DeserializeNode<T>(record["n"].As<global::Neo4j.Driver.INode>());
            nodes.Add(node);
        }

        return nodes;
    }

    /// <summary>
    /// Deletes a node by its ID.
    /// </summary>
    /// <param name="nodeId">The ID of the node to delete</param>
    /// <param name="options">Graph operation options</param>
    /// <param name="tx">The transaction to use</param>
    public async Task DeleteNode(string nodeId, GraphOperationOptions options, IAsyncTransaction tx)
    {
        var cypher = options.CascadeDelete
            ? $"MATCH (n) WHERE n.{nameof(Model.INode.Id)} = $nodeId DETACH DELETE n"
            : $"MATCH (n) WHERE n.{nameof(Model.INode.Id)} = $nodeId DELETE n";

        await tx.RunAsync(cypher, new { nodeId });
    }



    private async Task<string> CreateNodeInternal(string? parentId, object node, GraphOperationOptions options, IAsyncTransaction tx, string? propertyName, Dictionary<object, string> objectTracker)
    {
        // Check if we've already created this object instance
        if (objectTracker.TryGetValue(node, out var existingNodeId))
        {
            // If we have an existing node ID, it means that have already started traversing
            // an in-memory object graph from a complex property. This means that we
            // have a parentId and a propertyName.
            var relType = Helpers.PropertyNameToRelationshipTypeName(propertyName!);
            var c = $"MATCH (a), (b) WHERE elementId(a) = '{parentId!}' AND elementId(b) = '{existingNodeId}' CREATE (a)-[:{relType}]->(b)";
            await tx.RunAsync(c);
            return existingNodeId;
        }

        var type = node.GetType();
        var label = Neo4jTypeManager.GetLabel(type);
        var (simpleProps, complexProps) = GetSimpleAndComplexProperties(node);

        // Ensure constraints for the node
        await ConstraintManager.EnsureConstraintsForLabel(label, simpleProps.Select(p => p.Key));

        // Create the node with appropriate cypher query
        var relationshipType = Helpers.PropertyNameToRelationshipTypeName(propertyName!);
        var cypher = parentId == null ?
            $"CREATE (b:{label} $props) RETURN elementId(b) as nodeId" :
            $"MATCH (a) WHERE elementId(a) = '{parentId}' CREATE (a)-[:{relationshipType}]->(b:{label} $props) RETURN elementId(b) as nodeId";

        var record = await tx.RunAsync(cypher, new
        {
            props = ConvertPropertiesToNeo4j(simpleProps),
        });

        var createdNode = await record.SingleAsync();
        var nodeId = createdNode["nodeId"].ToString() ??
            throw new GraphException($"Failed to create node of type '{label}'");

        // Track this object instance
        objectTracker[node] = nodeId;

        // Handle complex properties recursively
        foreach (var prop in complexProps)
        {
            if (prop.Value == null) continue;
            if (prop.Key.PropertyType.IsAssignableTo(typeof(IEnumerable)))
            {
                foreach (var item in (IEnumerable)prop.Value)
                {
                    // Create a new for each object referenced
                    await CreateNodeInternal(nodeId, item, options, tx, prop.Key.Name, objectTracker);
                }
            }
            else
            {
                // For single complex properties, create the node directly
                await CreateNodeInternal(nodeId, prop.Value, options, tx, prop.Key.Name, objectTracker);
            }
        }

        return nodeId;
    }

    private async Task TraverseGraphForComplexProperties(
        Dictionary<string, ObjectTrackingInfo> objectTracker,
        ObjectTrackingInfo recordInfo)
    {
        foreach (var complexPropertyInfo in recordInfo.ComplexProperties)
        {
            // Get the property name
            var propertyName = Helpers.RelationshipTypeNameToPropertyName(complexPropertyInfo.RelationshipType);

            var property = recordInfo.NewObject!.GetType().GetProperty(propertyName)
                ?? throw new GraphException($"Property '{propertyName}' not found on type '{recordInfo.NewObject.GetType().FullName}'");

            // TODO: Check if this captures arrays

            if (property.PropertyType.IsAssignableTo(typeof(IEnumerable)))
            {
                // Create a new object for this complex property
                var newNode = await EntityConverter.DeserializeObjectFromNeo4jEntity(
                    GetCollectionElementType(property),
                    objectTracker[complexPropertyInfo.TargetId].Neo4jNode);

                if (property.GetValue(recordInfo.NewObject) is not null)
                {
                    // If the property already has a value, we need to add to it
                    var existingList = (IList)property.GetValue(recordInfo.NewObject)!;
                    existingList.Add(newNode);
                }
                else
                {
                    // If the property is a collection, we need to create a list and add the new node to it
                    var listType = typeof(List<>).MakeGenericType(property.PropertyType.GetGenericArguments());
                    var newList = (IList)Activator.CreateInstance(listType)!;
                    newList.Add(newNode);
                    property.SetValue(recordInfo.NewObject, newList);
                }
                objectTracker[complexPropertyInfo.TargetId].NewObject = newNode;
            }
            else
            {
                // Create a new object for this complex property
                var newNode = await EntityConverter.DeserializeObjectFromNeo4jEntity(
                    property.PropertyType,
                    objectTracker[complexPropertyInfo.TargetId].Neo4jNode);

                property.SetValue(recordInfo.NewObject, newNode);
                objectTracker[complexPropertyInfo.TargetId].NewObject = newNode;
            }
            await TraverseGraphForComplexProperties(objectTracker, objectTracker[complexPropertyInfo.TargetId]);
        }
    }

    private Type GetCollectionElementType(PropertyInfo property)
    {
        var type = property.PropertyType;

        // Handle arrays
        if (type.IsArray)
            return type.GetElementType()!;

        // Handle generic collections (List<T>, IList<T>, IEnumerable<T>, etc.)
        if (type.IsGenericType)
        {
            var genericArgs = type.GetGenericArguments();
            if (genericArgs.Length == 1)
                return genericArgs[0];
        }

        // Handle non-generic collections that implement IEnumerable<T>
        var enumerableType = type.GetInterfaces()
            .FirstOrDefault(t => t.IsGenericType &&
                                t.GetGenericTypeDefinition() == typeof(IEnumerable<>));

        if (enumerableType != null)
            return enumerableType.GetGenericArguments()[0];

        throw new InvalidOperationException($"Cannot determine element type for property {property.Name} of type {type}");
    }
}
