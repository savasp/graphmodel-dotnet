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
            var result = await _serializer.SerializeNodeAsync(node, cancellationToken);

            // Build the Cypher query
            var cypher = $"CREATE (n:{result.Label} $props) RETURN n";

            // Create the main node
            var nodeResult = await transaction.Transaction.RunAsync(cypher, new { props = result.SimpleProperties });
            var record = await nodeResult.SingleAsync();

            // Create complex properties if any
            if (result.ComplexProperties.Count > 0)
            {
                await CreateComplexPropertiesAsync(transaction.Transaction, node.Id, result.ComplexProperties, cancellationToken);
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

            // Check if node exists using LINQ
            var exists = await context.Graph.Nodes<TNode>()
                .Where(n => n.Id == node.Id)
                .AnyAsync(cancellationToken);

            if (!exists)
            {
                _logger?.LogWarning("Node {NodeId} not found for update", node.Id);
                return false;
            }

            // Serialize the node
            var result = await _serializer.SerializeNodeAsync(node, cancellationToken);

            // Update the node properties
            var cypher = "MATCH (n {Id: $nodeId}) SET n = $props RETURN n";
            var nodeResult = await transaction.Transaction.RunAsync(cypher, new { nodeId = node.Id, props = result.SimpleProperties });
            await nodeResult.ConsumeAsync();

            // Update complex properties (delete old ones and create new ones)
            await UpdateComplexPropertiesAsync(transaction.Transaction, node.Id, result.ComplexProperties, cancellationToken);

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
            var record = await result.SingleAsync();
            var wasDeleted = record["wasDeleted"].As<bool>();

            if (!wasDeleted)
            {
                _logger?.LogWarning("Node with ID {NodeId} not found for deletion", nodeId);
                return false;
            }

            _logger?.LogInformation("Deleted node with ID {NodeId}", nodeId);
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error deleting node with ID: {NodeId}", nodeId);
            throw new GraphException($"Failed to delete node: {ex.Message}", ex);
        }
    }

    private async Task CreateComplexPropertiesAsync(
        IAsyncTransaction tx,
        string parentId,
        List<ComplexPropertyInfo> complexProperties,
        CancellationToken cancellationToken)
    {
        foreach (var complexProp in complexProperties)
        {
            // Create the complex property node
            var labels = complexProp.SerializedNode.Label;

            // Build relationship properties for collections
            var relProps = new Dictionary<string, object>();
            if (complexProp.CollectionIndex.HasValue)
            {
                relProps["SequenceNumber"] = complexProp.CollectionIndex.Value;
            }

            var cypher = relProps.Count > 0
                ? $@"MATCH (parent {{Id: $parentId}})
                CREATE (parent)-[r:{complexProp.RelationshipType} $relProps]->(complex:{labels} $props)
                RETURN complex"
                : $@"MATCH (parent {{Id: $parentId}})
                CREATE (parent)-[:{complexProp.RelationshipType}]->(complex:{labels} $props)
                RETURN complex";

            var result = await tx.RunAsync(cypher, new
            {
                parentId,
                props = complexProp.SerializedNode.SimpleProperties,
                relProps
            });

            await result.ConsumeAsync();

            // Recursively create nested complex properties if any
            if (complexProp.SerializedNode.ComplexProperties.Count > 0)
            {
                // We need the ID from the created node
                var nodeId = complexProp.SerializedNode.SimpleProperties["Id"]?.ToString()
                    ?? throw new GraphException("Complex property node must have an Id");

                await CreateComplexPropertiesAsync(tx, nodeId, complexProp.SerializedNode.ComplexProperties, cancellationToken);
            }
        }
    }

    private async Task UpdateComplexPropertiesAsync(
        IAsyncTransaction tx,
        string parentId,
        List<ComplexPropertyInfo> complexProperties,
        CancellationToken cancellationToken)
    {
        // First, delete all existing complex property relationships
        var deleteCypher = @"
            MATCH (n {Id: $parentId})-[r]->(complex)
            WHERE type(r) STARTS WITH $propertyPrefix
            DETACH DELETE complex";

        await tx.RunAsync(deleteCypher, new
        {
            parentId,
            propertyPrefix = GraphDataModel.PropertyRelationshipTypeNamePrefix
        });

        // Then create the new ones
        await CreateComplexPropertiesAsync(tx, parentId, complexProperties, cancellationToken);
    }
}