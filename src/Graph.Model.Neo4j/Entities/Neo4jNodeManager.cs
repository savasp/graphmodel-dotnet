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

namespace Cvoya.Graph.Model.Neo4j.Entities;

using Cvoya.Graph.Model.Neo4j.Core;
using Cvoya.Graph.Model.Serialization;
using global::Neo4j.Driver;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;


internal sealed class Neo4jNodeManager(GraphContext context)
{
    private readonly ILogger<Neo4jNodeManager> _logger = context.LoggerFactory?.CreateLogger<Neo4jNodeManager>()
        ?? NullLogger<Neo4jNodeManager>.Instance;
    private readonly EntityFactory _serializer = new EntityFactory();
    private readonly ComplexPropertyManager _complexPropertyManager = new(context);

    public async Task<TNode> GetNodeAsync<TNode>(
        string nodeId,
        GraphTransaction transaction,
        CancellationToken cancellationToken = default)
        where TNode : Model.INode
    {
        ArgumentException.ThrowIfNullOrEmpty(nodeId);

        _logger.LogDebug("Getting node of type {NodeType} with ID {NodeId}", typeof(TNode).Name, nodeId);

        try
        {
            // Just use LINQ! The visitor pattern handles everything including complex properties
            var query = context.Graph.Nodes<TNode>(transaction)
                .Where(n => n.Id == nodeId);

            return await query.FirstOrDefaultAsync(cancellationToken)
                ?? throw new KeyNotFoundException($"Node with ID {nodeId} not found");
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
        where TNode : Model.INode
    {
        ArgumentNullException.ThrowIfNull(node);

        _logger.LogDebug("Creating node of type {NodeType} with ID {NodeId}", typeof(TNode).Name, node.Id);

        try
        {
            // Validate no reference cycles
            GraphDataModel.EnsureNoReferenceCycle(node);

            // Ensure we have the constraints for the node type
            await context.ConstraintManager.EnsureConstraintsForType(node);

            // Serialize the node
            var entity = _serializer.Serialize(node);

            // Create the main node
            var nodeId = await CreateMainNodeAsync(entity, transaction.Transaction, cancellationToken);

            // Create complex properties
            var success = await _complexPropertyManager.CreateComplexPropertiesAsync(
                transaction.Transaction, nodeId, entity, cancellationToken);

            if (!success)
            {
                _logger.LogWarning("Failed to create all complex properties for node {NodeId}", node.Id);
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
        where TNode : Model.INode
    {
        ArgumentNullException.ThrowIfNull(node);

        _logger.LogDebug("Updating node of type {NodeType} with ID {NodeId}", typeof(TNode).Name, node.Id);

        try
        {
            // Validate no reference cycles
            GraphDataModel.EnsureNoReferenceCycle(node);

            // Serialize the node
            var entity = _serializer.Serialize(node);

            // Update the node properties
            var updated = await UpdateMainNodeAsync(node.Id, entity, transaction.Transaction, cancellationToken);

            if (!updated)
            {
                _logger.LogWarning("Node with ID {NodeId} not found for update", node.Id);
                throw new KeyNotFoundException($"Node with ID {node.Id} not found for update");
            }

            // Update complex properties
            var complexPropertiesUpdated = await _complexPropertyManager.UpdateComplexPropertiesAsync(
                transaction.Transaction, node.Id, entity, cancellationToken);

            if (!complexPropertiesUpdated && entity.ComplexProperties.Count > 0)
            {
                _logger.LogWarning("Failed to update complex properties for node {NodeId}", node.Id);
                throw new GraphException($"Failed to update the node's complex properties");
            }

            _logger.LogInformation("Updated node of type {NodeType} with ID {NodeId}", typeof(TNode).Name, node.Id);
            return true;
        }
        catch (Exception ex) when (ex is not GraphException and not KeyNotFoundException)
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

        _logger.LogDebug("Deleting node with ID: {NodeId}, cascade: {CascadeDelete}", nodeId, cascadeDelete);

        try
        {
            if (!cascadeDelete)
            {
                // First check if the node has any business relationships (non-complex properties)
                var checkCypher = @"
                    MATCH (n {Id: $nodeId})
                    OPTIONAL MATCH (n)-[r]-()
                    WHERE NOT type(r) STARTS WITH $propertyPrefix
                    RETURN COUNT(r) AS businessRelationshipCount";

                var checkResult = await transaction.Transaction.RunAsync(checkCypher, new
                {
                    nodeId,
                    propertyPrefix = GraphDataModel.PropertyRelationshipTypeNamePrefix
                });

                var checkRecord = await checkResult.SingleAsync(cancellationToken);
                var businessRelationshipCount = checkRecord["businessRelationshipCount"].As<int>();

                if (businessRelationshipCount > 0)
                {
                    throw new GraphException(
                        $"Cannot delete node {nodeId} because it has {businessRelationshipCount} relationship(s). " +
                        "Use cascadeDelete=true to force deletion or delete the relationships first.");
                }
            }

            // Now perform the deletion
            var cypher = cascadeDelete
                ? @"MATCH (n {Id: $nodeId})
                    OPTIONAL MATCH (n)--(connected)
                    DETACH DELETE connected
                    WITH n
                    DETACH DELETE n
                    RETURN n IS NOT NULL AS wasDeleted"
                : @"MATCH (n {Id: $nodeId})
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

            var record = await result.SingleAsync(cancellationToken);
            var wasDeleted = record["wasDeleted"].As<bool>();

            if (!wasDeleted)
            {
                _logger.LogWarning("Node with ID {NodeId} not found for deletion", nodeId);
                throw new KeyNotFoundException($"Node with ID {nodeId} not found for deletion");
            }

            _logger.LogInformation("Deleted node with ID {NodeId}", nodeId);
            return true;
        }
        catch (Exception ex) when (ex is not KeyNotFoundException && ex is not GraphException)
        {
            _logger.LogError(ex, "Error deleting node with ID: {NodeId}", nodeId);
            throw new GraphException($"Failed to delete node: {ex.Message}", ex);
        }
    }

    private async Task<string> CreateMainNodeAsync(
        EntityInfo entity,
        IAsyncTransaction transaction,
        CancellationToken cancellationToken)
    {
        var cypher = $"CREATE (n:{entity.Label} $props) RETURN elementId(n) AS nodeId";

        var simpleProperties = SerializationHelpers.SerializeSimpleProperties(entity);

        var result = await transaction.RunAsync(cypher, new { props = simpleProperties });
        var record = await result.SingleAsync(cancellationToken);

        return record["nodeId"].As<string>()
            ?? throw new GraphException("Failed to create node - no ID returned");
    }

    private async Task<bool> UpdateMainNodeAsync(
        string nodeId,
        EntityInfo entity,
        IAsyncTransaction transaction,
        CancellationToken cancellationToken)
    {
        var cypher = "MATCH (n {Id: $nodeId}) SET n = $props RETURN n";

        var simpleProperties = SerializationHelpers.SerializeSimpleProperties(entity);

        var result = await transaction.RunAsync(cypher, new { nodeId, props = simpleProperties });
        return await result.CountAsync(cancellationToken) > 0;
    }
}