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
using Neo4j.Driver;

namespace Cvoya.Graph.Model.Neo4j;

internal sealed class Neo4jNodeManager(GraphContext context)
{
    private readonly ILogger<Neo4jNodeManager>? _logger = context.LoggerFactory?.CreateLogger<Neo4jNodeManager>();
    private readonly GraphEntitySerializer _serializer = new GraphEntitySerializer(context);

    public async Task<TNode?> GetNodeAsync<TNode>(
        string nodeId,
        GraphTransaction transaction,
        CancellationToken cancellationToken = default)
        where TNode : INode
    {
        ArgumentException.ThrowIfNullOrEmpty(nodeId);

        _logger?.LogDebug("Getting node of type {NodeType} with ID {NodeId}", typeof(TNode).Name, nodeId);

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
            _logger?.LogError(ex, "Error retrieving node {NodeId} of type {NodeType}", nodeId, typeof(TNode).Name);
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

        _logger?.LogDebug("Creating node of type {NodeType} with ID {NodeId}", typeof(TNode).Name, node.Id);

        try
        {
            // Validate no reference cycles
            GraphDataModel.EnsureNoReferenceCycle(node);

            // Serialize the node
            var serializationResult = _serializer.SerializeNode(node);

            // Build the Cypher query
            var cypher = $"CREATE (n:{serializationResult.Label} $props) RETURN n";

            _logger?.LogDebug("Cypher query for creating node: {Cypher}", cypher);

            var simpleProperties = serializationResult.SerializedEntity
                .Where(kv => GraphDataModel.IsSimple(kv.Value.PropertyInfo.PropertyType) ||
                             GraphDataModel.IsCollectionOfSimple(kv.Value.PropertyInfo.PropertyType))
                .ToDictionary(kv => kv.Key, kv => kv.Value.Value);

            // Create the main node
            var nodeResult = await transaction.Transaction.RunAsync(cypher, new { props = simpleProperties });

            var nodeCreated = await nodeResult.CountAsync(cancellationToken) != 0;
            var complexPropertiesCreated = true;

            var complexProperties = serializationResult.SerializedEntity.ComplexProperties;

            _logger?.LogDebug("Node creation result: {NodeCreated}, Complex properties: {ComplexPropertiesCount}", nodeCreated, complexProperties.Count);
            // Create complex properties if any
            if (complexProperties.Any())
            {
                _logger?.LogDebug("Creating complex properties for node of type {NodeType} with ID {NodeId}", typeof(TNode).Name, node.Id);
                complexPropertiesCreated = await CreateComplexPropertiesAsync(transaction.Transaction, node.Id, complexProperties, 0, cancellationToken);
            }

            if (!nodeCreated || !complexPropertiesCreated)
            {
                _logger?.LogWarning("Node of type {NodeType} with ID {NodeId} was not created", typeof(TNode).Name, node.Id);
                throw new GraphException($"Failed to create node of type {typeof(TNode).Name} with ID {node.Id}");
            }

            _logger?.LogInformation("Created node of type {NodeType} with ID {NodeId}", typeof(TNode).Name, node.Id);
            return node;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error creating node of type {NodeType}", typeof(TNode).Name);
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

        _logger?.LogDebug("Updating node of type {NodeType} with ID {NodeId}", typeof(TNode).Name, node.Id);

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
                _logger?.LogWarning("Node with ID {NodeId} not found for update", node.Id);
                throw new KeyNotFoundException($"Node with ID {node.Id} not found for update");
            }

            // Update complex properties (delete old ones and create new ones)
            var complexPropertiesUpdated = await UpdateComplexPropertiesAsync(transaction.Transaction, node.Id, serializationResult.SerializedEntity.ComplexProperties, cancellationToken);

            if (!complexPropertiesUpdated && serializationResult.SerializedEntity.ComplexProperties.Count > 0)
            {
                _logger?.LogWarning("No complex properties were updated for node with ID {NodeId}", node.Id);
                throw new GraphException($"Failed to update the node's complex properties of type {typeof(TNode).Name} with ID {node.Id}");
            }

            if (!updated && !complexPropertiesUpdated)
            {
                _logger?.LogWarning("Node of type {NodeType} with ID {NodeId} was not updated", typeof(TNode).Name, node.Id);
                throw new GraphException($"Failed to update node of type {typeof(TNode).Name} with ID {node.Id}");
            }

            _logger?.LogInformation("Updated node of type {NodeType} with ID {NodeId}", typeof(TNode).Name, node.Id);
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error updating node {NodeId} of type {NodeType}", node.Id, typeof(TNode).Name);
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

        _logger?.LogDebug("Deleting node with ID: {NodeId}", nodeId);

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
                _logger?.LogWarning($"Node with ID {nodeId} not found for deletion");
                throw new KeyNotFoundException($"Node with ID {nodeId} not found for deletion");
            }

            _logger?.LogInformation($"Deleted node with ID {nodeId}");
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, $"Error deleting node with ID: {nodeId}");
            throw new GraphException($"Failed to delete node: {ex.Message}", ex);
        }
    }

    private async Task<bool> CreateComplexPropertiesAsync(
        IAsyncTransaction tx,
        string parentId,
        IReadOnlyDictionary<string, IntermediateRepresentation> complexProperties,
        int index = 0,
        CancellationToken cancellationToken = default)
    {
        var allCreated = true;

        foreach (var complexProp in complexProperties.Values)
        {
            // Create the complex property node
            var relationshipLabel = GraphDataModel.PropertyNameToRelationshipTypeName(complexProp.PropertyInfo.Name);
            var complexNodeLabel = Labels.GetLabelFromType(complexProp.PropertyInfo.PropertyType);

            var relProps = new Dictionary<string, object> { { "SequenceNumber", index } };

            var cypher = $@"MATCH (parent {{Id: $parentId}})
                CREATE (parent)-[r:{relationshipLabel} $relProps]->(complex:{complexNodeLabel} $props)
                RETURN complex";

            _logger?.LogDebug($"Cypher query for creating complex property: {cypher}");

            var nodeProps = (complexProp.Value as IReadOnlyDictionary<string, IntermediateRepresentation>)!
                .SimpleProperties
                .ToDictionary(kv => kv.Key, kv => kv.Value.Value);

            var result = await tx.RunAsync(cypher, new
            {
                parentId,
                props = nodeProps,
                relProps
            });

            allCreated &= await result.CountAsync(cancellationToken) != 0;

            if (allCreated && !complexProp.IsCollection)
            {
                // We need the ID from the created node
                parentId = nodeProps["Id"] as string
                    ?? throw new GraphException(
                        $"Complex property {complexProp.PropertyInfo.Name} was created but ID is missing");

                foreach (var cp in complexProp.Value as IEnumerable<IReadOnlyDictionary<string, IntermediateRepresentation>> ?? throw new GraphException(
                    $"Complex property {complexProp.PropertyInfo.Name} is a collection but value is not a dictionary"))
                {
                    // Recursively create each item in the collection
                    allCreated &= await CreateComplexPropertiesAsync(tx, parentId, cp, index + 1, cancellationToken);
                }
            }
        }

        return allCreated;
    }

    private async Task<bool> UpdateComplexPropertiesAsync(
        IAsyncTransaction tx,
        string parentId,
        IReadOnlyDictionary<string, IntermediateRepresentation> complexProperties,
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

        _logger?.LogDebug("Deleted {DeletedCount} complex property relationships for parent ID {ParentId}", deletedCount, parentId);

        var updatedComplexProperties = !complexProperties.Any() || await CreateComplexPropertiesAsync(tx, parentId, complexProperties, 0, cancellationToken);

        return updatedComplexProperties;
    }
}