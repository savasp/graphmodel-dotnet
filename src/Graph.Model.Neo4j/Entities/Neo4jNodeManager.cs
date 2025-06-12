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

using Cvoya.Graph.Model.Neo4j.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Neo4j.Driver;

namespace Cvoya.Graph.Model.Neo4j;

internal sealed class Neo4jNodeManager(GraphContext context)
{
    private readonly ILogger<Neo4jNodeManager> _logger = context.LoggerFactory?.CreateLogger<Neo4jNodeManager>()
        ?? NullLogger<Neo4jNodeManager>.Instance;
    private readonly GraphEntitySerializer _serializer = new GraphEntitySerializer(context);

    public async Task<TNode?> GetNodeAsync<TNode>(
        string nodeId,
        GraphTransaction transaction,
        CancellationToken cancellationToken = default)
        where TNode : INode
    {
        ArgumentException.ThrowIfNullOrEmpty(nodeId);

        _logger.LogDebug("Getting node of type {NodeType} with ID {NodeId}", typeof(TNode).Name, nodeId);

        try
        {
            // Just use LINQ! The visitor pattern handles everything including complex properties
            var query = context.Graph.Nodes<TNode>()
                .Where(n => n.Id == nodeId)
                .WithTransaction(transaction);

            return await query.FirstOrDefaultAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving node {NodeId} of type {NodeType}", nodeId, typeof(TNode).Name);
            throw new GraphException($"Failed to retrieve node: {ex.Message}", ex);
        }
    }

    public async Task<TNode> CreateNodeAsync<TNode>(
        TNode node,
        GraphTransaction transaction,
        CancellationToken cancellationToken = default)
        where TNode : INode
    {
        ArgumentNullException.ThrowIfNull(node);

        _logger.LogDebug("Creating node of type {NodeType} with ID {NodeId}", typeof(TNode).Name, node.Id);

        try
        {
            // Validate no reference cycles
            GraphDataModel.EnsureNoReferenceCycle(node);

            // Serialize the node
            var serializationResult = _serializer.SerializeNode(node);

            // Build the Cypher query
            var cypher = $"CREATE (n:{serializationResult.Label} $props) RETURN elementId(n) AS nodeId";

            _logger.LogDebug("Cypher query for creating node: {Cypher}", cypher);

            var simpleProperties = serializationResult.SerializedEntity.SimpleProperties
                .Where(kv => kv.Value.Value is not null)
                .ToDictionary(
                    kv => kv.Key,
                    kv => kv.Value.Value switch
                    {
                        SimpleValue simpleValue => simpleValue.Object,
                        SimpleCollection simpleCollection => simpleCollection.Values.Select(v => v.Object),
                        _ => throw new GraphException($"This is a simple property so there should be no other Serialized type")
                    });

            // Create the main node
            var nodeResult = await transaction.Transaction.RunAsync(cypher, new { props = simpleProperties });
            var record = await nodeResult.SingleAsync(cancellationToken);
            var parentId = record["nodeId"].As<string>();

            _logger.LogDebug("Created node with ID: {NodeId}", parentId);

            // Yes, that's true by default. The entity being stored might not have any complex properties,
            var complexPropertiesCreated = true;
            var complexProperties = serializationResult.SerializedEntity.ComplexProperties;

            // Create complex properties if any
            foreach (var cp in complexProperties)
            {
                var label = cp.Value.Label;
                switch (cp.Value.Value)
                {
                    case Entity entity:
                        // Create complex properties recursively
                        // Note: The index is used to maintain the order in the collection
                        complexPropertiesCreated = await CreateComplexGraphAsync(transaction.Transaction, parentId, label, entity, 0, cancellationToken);
                        break;

                    case EntityCollection entityCollection:
                        // Create complex properties recursively for each entity in the collection
                        int index = 0;
                        foreach (var entityItem in entityCollection.Entities)
                        {
                            complexPropertiesCreated &= await CreateComplexGraphAsync(transaction.Transaction, parentId, label, entityItem, index++, cancellationToken);
                        }
                        break;
                    case null:
                        // If the complex property is null, we skip it
                        continue;
                    default:
                        _logger.LogWarning("Unsupported complex property type: {PropertyType} for property {PropertyName}", cp.Value.Value.GetType().Name, cp.Key);
                        throw new GraphException($"Unsupported complex property type: {cp.Value.Value.GetType().Name} for property {cp.Key}");
                }
            }

            if (!complexPropertiesCreated)
            {
                _logger.LogWarning("Node of type {NodeType} with ID {NodeId} was not created", typeof(TNode).Name, node.Id);
                throw new GraphException($"Failed to create node of type {typeof(TNode).Name} with ID {node.Id}");
            }

            _logger.LogInformation("Created node of type {NodeType} with ID {NodeId}", typeof(TNode).Name, node.Id);
            return node;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating node of type {NodeType}", typeof(TNode).Name);
            throw new GraphException($"Failed to create node: {ex.Message}", ex);
        }
    }

    public async Task<bool> UpdateNodeAsync<TNode>(
        TNode node,
        GraphTransaction transaction,
        CancellationToken cancellationToken = default)
        where TNode : INode
    {
        ArgumentNullException.ThrowIfNull(node);

        _logger.LogDebug("Updating node of type {NodeType} with ID {NodeId}", typeof(TNode).Name, node.Id);

        try
        {
            // Validate no reference cycles
            GraphDataModel.EnsureNoReferenceCycle(node);

            // Serialize the node
            var serializationResult = _serializer.SerializeNode(node);

            // Update the node properties
            var cypher = "MATCH (n {Id: $nodeId}) SET n = $props RETURN n";
            var result = await transaction.Transaction.RunAsync(cypher, new { nodeId = node.Id, props = serializationResult.SerializedEntity.SimpleProperties });

            var updated = await result.CountAsync(cancellationToken) != 0;
            if (!updated)
            {
                _logger.LogWarning("Node with ID {NodeId} not found for update", node.Id);
                throw new KeyNotFoundException($"Node with ID {node.Id} not found for update");
            }

            // Update complex properties (delete old ones and create new ones)
            var complexPropertiesUpdated = await UpdateComplexPropertiesAsync(
                transaction.Transaction,
                node.Id,
                serializationResult.SerializedEntity,
                cancellationToken);

            if (!complexPropertiesUpdated && serializationResult.SerializedEntity.ComplexProperties.Count > 0)
            {
                _logger.LogWarning("No complex properties were updated for node with ID {NodeId}", node.Id);
                throw new GraphException($"Failed to update the node's complex properties of type {typeof(TNode).Name} with ID {node.Id}");
            }

            if (!updated && !complexPropertiesUpdated)
            {
                _logger.LogWarning("Node of type {NodeType} with ID {NodeId} was not updated", typeof(TNode).Name, node.Id);
                throw new GraphException($"Failed to update node of type {typeof(TNode).Name} with ID {node.Id}");
            }

            _logger.LogInformation("Updated node of type {NodeType} with ID {NodeId}", typeof(TNode).Name, node.Id);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating node {NodeId} of type {NodeType}", node.Id, typeof(TNode).Name);
            throw new GraphException($"Failed to update node: {ex.Message}", ex);
        }
    }

    public async Task<bool> DeleteNodeAsync(
        string nodeId,
        GraphTransaction transaction,
        bool cascadeDelete = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(nodeId);

        _logger.LogDebug("Deleting node with ID: {NodeId}", nodeId);

        try
        {
            // Delete complex properties and the node itself
            // or delete node and all its relationships and the connected nodes.
            var cypher = cascadeDelete ?
            @"MATCH (n {Id: $nodeId})
                OPTIONAL MATCH (n)--(connected)
                DETACH DELETE connected
                WITH n
                DETACH DELETE n
                RETURN n IS NOT NULL AS wasDeleted" :
            @"MATCH (n {Id: $nodeId})
                OPTIONAL MATCH (n)-[r]->(complex)
                WHERE type(r) STARTS WITH $propertyPrefix
                DETACH DELETE complex
                WITH n
                DETACH DELETE n
                RETURN n IS NOT NULL AS wasDeleted";

            var result = await transaction.Transaction.RunAsync(cypher, new
            {
                nodeId,
                propertyPrefix = GraphDataModel.PropertyRelationshipTypeNamePrefix
            });

            // Check if the node was deleted
            var record = await result.SingleAsync(cancellationToken);
            var wasDeleted = record["wasDeleted"].As<bool>();

            if (!wasDeleted)
            {
                _logger.LogWarning($"Node with ID {nodeId} not found for deletion");
                throw new KeyNotFoundException($"Node with ID {nodeId} not found for deletion");
            }

            _logger.LogInformation($"Deleted node with ID {nodeId}");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error deleting node with ID: {nodeId}");
            throw new GraphException($"Failed to delete node: {ex.Message}", ex);
        }
    }

    private async Task<bool> CreateComplexGraphAsync(
        IAsyncTransaction tx,
        string parentId,
        string label,
        Entity entity,
        int index = 0,
        CancellationToken cancellationToken = default)
    {
        // Yes, we start with true.
        var allCreated = true;

        // Create the complex property node
        var complexNodeLabel = entity.Label;
        label = GraphDataModel.PropertyNameToRelationshipTypeName(label);

        var cypher = $@"MATCH (parent)
                WHERE elementId(parent) = $parentId
                CREATE (parent)-[r:{label} $relProps]->(complex:{complexNodeLabel} $props)
                RETURN elementId(complex) as nodeId";
        _logger.LogDebug($"Cypher query for creating complex property: {cypher}");

        IReadOnlyDictionary<string, object>? nodeProps = null;

        // Serialize the entity's simple properties and then later we 
        // will recursively handle its complex properties
        nodeProps = entity.SimpleProperties
            .Where(kv => kv.Value.Value is not null)
            .ToDictionary(
                kv => kv.Key,
                kv => kv.Value.Value switch
                {
                    SimpleValue simpleValue => simpleValue.Object,
                    SimpleCollection simpleCollection => simpleCollection.Values.Select(v => v.Object),
                    _ => throw new GraphException($"This is a simple property so there should be no other Serialized type")
                });

        var relProps = new Dictionary<string, object> { { "SequenceNumber", index } };

        if (nodeProps == null || !nodeProps.Any())
        {
            nodeProps = new Dictionary<string, object>();
        }

        var result = await tx.RunAsync(cypher, new
        {
            parentId,
            props = nodeProps,
            relProps
        });

        var record = await result.SingleAsync(cancellationToken);
        var complexNodeId = record["nodeId"].As<string>()
            ?? throw new GraphException($"Failed to create entity {entity.Label} with parent ID {parentId}");

        // Recursively create complex properties for the entity
        foreach (var cp in entity.ComplexProperties)
        {
            label = cp.Value.Label;
            switch (cp.Value.Value)
            {
                case Entity e:
                    // Create complex properties recursively
                    // Note: The index is used to maintain the order in the collection
                    allCreated &= await CreateComplexGraphAsync(tx, complexNodeId, label, e, 0, cancellationToken);
                    break;

                case EntityCollection entityCollection:
                    // Create complex properties recursively for each entity in the collection
                    int i = 0;
                    foreach (var entityItem in entityCollection.Entities)
                    {
                        allCreated &= await CreateComplexGraphAsync(tx, complexNodeId, label, entityItem, i++, cancellationToken);
                    }
                    break;
                case null:
                    // If the complex property is null, we skip it
                    continue;
                default:
                    _logger.LogWarning("Unsupported complex property type: {PropertyType} for property {PropertyName}", cp.Value.Value.GetType().Name, cp.Key);
                    throw new GraphException($"Unsupported complex property type: {cp.Value.Value.GetType().Name} for property {cp.Key}");
            }
        }

        return allCreated;
    }

    private async Task<bool> UpdateComplexPropertiesAsync(
        IAsyncTransaction tx,
        string parentId,
        Entity entity,
        CancellationToken cancellationToken)
    {
        // First, delete all existing complex property relationships
        var deleteCypher = @"
            MATCH (n {Id: $parentId})-[r]->(complex)
            WHERE type(r) STARTS WITH $propertyPrefix
            DETACH DELETE complex
            DELETE r
            RETURN COUNT(r) AS deletedCount";

        var result = await tx.RunAsync(deleteCypher, new
        {
            parentId,
            propertyPrefix = GraphDataModel.PropertyRelationshipTypeNamePrefix
        });

        var deletedCount = (await result.FirstAsync(cancellationToken))["deletedCount"].As<int>();

        _logger.LogDebug("Deleted {DeletedCount} complex property relationships for parent ID {ParentId}", deletedCount, parentId);

        // Yes, that's true by default. The entity being stored might not have any complex properties,
        var updatedComplexProperties = true;

        // Create complex properties if any
        foreach (var cp in entity.ComplexProperties)
        {
            var label = cp.Value.Label;
            switch (cp.Value.Value)
            {
                case Entity e:
                    // Create complex properties recursively
                    // Note: The index is used to maintain the order in the collection
                    updatedComplexProperties = await CreateComplexGraphAsync(tx, parentId, label, entity, 0, cancellationToken);
                    break;

                case EntityCollection entityCollection:
                    // Create complex properties recursively for each entity in the collection
                    int index = 0;
                    foreach (var entityItem in entityCollection.Entities)
                    {
                        updatedComplexProperties &= await CreateComplexGraphAsync(tx, parentId, label, entityItem, index++, cancellationToken);
                    }
                    break;
                case null:
                    // If the complex property is null, we skip it
                    continue;
                default:
                    _logger.LogWarning("Unsupported complex property type: {PropertyType} for property {PropertyName}", cp.Value.Value.GetType().Name, cp.Key);
                    throw new GraphException($"Unsupported complex property type: {cp.Value.Value.GetType().Name} for property {cp.Key}");
            }
        }

        return updatedComplexProperties;
    }
}