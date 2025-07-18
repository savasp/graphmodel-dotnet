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

            // Validate property constraints at application level
            ValidateNodeProperties(node);

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

            // Validate property constraints at application level
            ValidateNodeProperties(node);

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
        string cypher;

        // For dynamic nodes, use the actual labels from ActualLabels
        if (entity.ActualType.IsAssignableTo(typeof(Model.DynamicNode)))
        {
            if (entity.ActualLabels != null && entity.ActualLabels.Count > 0)
            {
                var labels = string.Join(":", entity.ActualLabels);
                cypher = $"CREATE (n:{labels} $props) RETURN elementId(n) AS nodeId";
            }
            else
            {
                // For dynamic nodes with no labels, create without any labels
                cypher = "CREATE (n $props) RETURN elementId(n) AS nodeId";
            }
        }
        else
        {
            cypher = $"CREATE (n:{entity.Label} $props) RETURN elementId(n) AS nodeId";
        }

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
        string cypher;
        var simpleProperties = SerializationHelpers.SerializeSimpleProperties(entity);

        // For dynamic nodes, update both properties and labels
        if (entity.ActualType == typeof(Model.DynamicNode) && entity.ActualLabels != null && entity.ActualLabels.Count > 0)
        {
            // First, get the current labels to remove them
            var getLabelsCypher = "MATCH (n {Id: $nodeId}) RETURN labels(n) AS currentLabels";
            var getLabelsResult = await transaction.RunAsync(getLabelsCypher, new { nodeId });
            var getLabelsRecord = await getLabelsResult.SingleAsync(cancellationToken);
            var currentLabels = getLabelsRecord["currentLabels"].As<List<string>>() ?? new List<string>();

            // Build the REMOVE clause for current labels
            var removeLabelsClause = currentLabels.Count > 0
                ? $"REMOVE n:{string.Join(":n:", currentLabels)} "
                : "";

            // Build the SET clause for new labels
            var newLabels = string.Join(":", entity.ActualLabels);
            var setLabelsClause = $"SET n:{newLabels} ";

            cypher = $"MATCH (n {{Id: $nodeId}}) {removeLabelsClause}SET n = $props {setLabelsClause}RETURN n";
        }
        else
        {
            // For non-dynamic nodes, just update properties
            cypher = "MATCH (n {Id: $nodeId}) SET n = $props RETURN n";
        }

        var result = await transaction.RunAsync(cypher, new { nodeId, props = simpleProperties });
        return await result.CountAsync(cancellationToken) > 0;
    }

    private void ValidateNodeProperties<TNode>(TNode node) where TNode : Model.INode
    {
        var label = Labels.GetLabelFromType(node.GetType());
        var config = context.SchemaManager.GetRegistry().GetNodeConfiguration(label);

        if (config == null) return;

        foreach (var (propertyName, propertyConfig) in config.Properties)
        {
            if (propertyConfig.Validation == null) continue;

            var property = node.GetType().GetProperty(propertyName);
            if (property == null) continue;

            var value = property.GetValue(node);
            if (value == null) continue;

            ValidatePropertyValue(propertyName, value, propertyConfig.Validation, label);
        }
    }

    private void ValidatePropertyValue(string propertyName, object value, PropertyValidation validation, string entityLabel)
    {
        // MinValue validation
        if (validation.MinValue is not null)
        {
            if (value is IComparable comparable)
            {
                if (comparable.CompareTo(validation.MinValue) < 0)
                {
                    throw new GraphException($"Property '{propertyName}' on {entityLabel} must be greater than or equal to {validation.MinValue}. Current value: {value}");
                }
            }
        }

        // MaxValue validation
        if (validation.MaxValue is not null)
        {
            if (value is IComparable comparable)
            {
                if (comparable.CompareTo(validation.MaxValue) > 0)
                {
                    throw new GraphException($"Property '{propertyName}' on {entityLabel} must be less than or equal to {validation.MaxValue}. Current value: {value}");
                }
            }
        }

        // MinLength validation
        if (validation.MinLength is not null)
        {
            if (value is string stringValue)
            {
                if (stringValue.Length < validation.MinLength)
                {
                    throw new GraphException($"Property '{propertyName}' on {entityLabel} must have a minimum length of {validation.MinLength}. Current length: {stringValue.Length}");
                }
            }
        }

        // MaxLength validation
        if (validation.MaxLength is not null)
        {
            if (value is string stringValue)
            {
                if (stringValue.Length > validation.MaxLength)
                {
                    throw new GraphException($"Property '{propertyName}' on {entityLabel} must have a maximum length of {validation.MaxLength}. Current length: {stringValue.Length}");
                }
            }
        }

        // Pattern validation
        if (!string.IsNullOrEmpty(validation.Pattern))
        {
            if (value is string stringValue)
            {
                if (!System.Text.RegularExpressions.Regex.IsMatch(stringValue, validation.Pattern))
                {
                    throw new GraphException($"Property '{propertyName}' on {entityLabel} must match the pattern '{validation.Pattern}'. Current value: {stringValue}");
                }
            }
        }
    }
}