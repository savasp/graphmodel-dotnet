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

/// <summary>
///  All methods assume that there is already a transaction in progress.
/// </summary>
/// <param name="context"></param>
internal sealed class Neo4jRelationshipManager(GraphContext context)
{
    private readonly ILogger<Neo4jRelationshipManager>? _logger = context.LoggerFactory?.CreateLogger<Neo4jRelationshipManager>();
    private readonly GraphEntitySerializer _serializer = new GraphEntitySerializer(context);

    public async Task<TRelationship?> GetRelationshipAsync<TRelationship>(
        string relationshipId,
        GraphTransaction transaction,
        CancellationToken cancellationToken = default)
        where TRelationship : IRelationship
    {
        ArgumentException.ThrowIfNullOrEmpty(relationshipId);

        _logger?.LogDebug("Getting relationship of type {RelationshipType} with ID {RelationshipId}", typeof(TRelationship).Name, relationshipId);

        try
        {
            // Use LINQ with the relationship queryable
            var query = context.Graph.Relationships<TRelationship>()
                .Where(r => r.Id == relationshipId);

            query = query.WithTransaction(transaction);

            return await query.FirstOrDefaultAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error retrieving relationship {RelationshipId} of type {RelationshipType}", relationshipId, typeof(TRelationship).Name);
            throw new GraphException($"Failed to retrieve relationship: {ex.Message}", ex);
        }
    }

    public async Task CreateRelationshipAsync<TRelationship>(
        TRelationship relationship,
        GraphTransaction transaction,
        CancellationToken cancellationToken = default)
        where TRelationship : IRelationship
    {
        ArgumentNullException.ThrowIfNull(relationship);

        _logger?.LogDebug("Creating relationship of type {RelationshipType} from {SourceId} to {TargetId}",
            typeof(TRelationship).Name, relationship.StartNodeId, relationship.EndNodeId);

        try
        {
            // Validate no reference cycles
            GraphDataModel.EnsureNoReferenceCycle(relationship);

            // Serialize the relationship
            var serializedRelationship = _serializer.SerializeRelationship(relationship);

            // Build the Cypher query
            var cypher = $@"
                MATCH (source {{Id: $sourceId}})
                MATCH (target {{Id: $targetId}})
                CREATE (source)-[r:{serializedRelationship.Type} $props]->(target)
                RETURN r";

            _logger?.LogDebug("Cypher query: {CypherQuery}", cypher);
            _logger?.LogDebug("Parameters: SourceId={SourceId}, TargetId={TargetId}, Properties={Properties}",
                serializedRelationship.SourceId, serializedRelationship.TargetId, serializedRelationship.Properties);
            var result = await transaction.Transaction.RunAsync(cypher, new
            {
                sourceId = serializedRelationship.SourceId,
                targetId = serializedRelationship.TargetId,
                props = serializedRelationship.Properties
            });

            if (await result.CountAsync(cancellationToken) == 0)
            {
                _logger?.LogWarning($"Failed to create relationship of type {typeof(TRelationship).Name} from {relationship.StartNodeId} to {relationship.EndNodeId}");
                throw new GraphException($"Failed to create relationship of type {typeof(TRelationship).Name} from {relationship.StartNodeId} to {relationship.EndNodeId}");
            }

            _logger?.LogInformation("Created relationship of type {RelationshipType} with ID {RelationshipId}",
                typeof(TRelationship).Name, relationship.Id);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error creating relationship of type {RelationshipType}", typeof(TRelationship).Name);
            throw new GraphException($"Failed to create relationship: {ex.Message}", ex);
        }
    }

    public async Task<bool> UpdateRelationshipAsync<TRelationship>(
        TRelationship relationship,
        GraphTransaction transaction,
        CancellationToken cancellationToken = default)
        where TRelationship : IRelationship
    {
        ArgumentNullException.ThrowIfNull(relationship);

        _logger?.LogDebug("Updating relationship of type {RelationshipType} with ID {RelationshipId}",
            typeof(TRelationship).Name, relationship.Id);

        try
        {
            // Validate no reference cycles
            GraphDataModel.EnsureNoReferenceCycle(relationship);

            // Serialize the relationship
            var result = _serializer.SerializeRelationship(relationship);

            var cypher = "MATCH ()-[r {Id: $relId}]->() SET r = $props RETURN r";
            var relResult = await transaction.Transaction.RunAsync(cypher, new { relId = relationship.Id, props = result.Properties });
            var count = await relResult.CountAsync(cancellationToken);

            if (count == 0)
            {
                _logger?.LogWarning($"Relationship with ID {relationship.Id} not found for update");
                throw new KeyNotFoundException($"Relationship with ID {relationship.Id} not found for update.");
            }

            _logger?.LogInformation($"Updated relationship of type {typeof(TRelationship).Name} with ID {relationship.Id}");
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, $"Error updating relationship {relationship.Id} of type {typeof(TRelationship).Name}");
            throw new GraphException($"Failed to update relationship: {ex.Message}", ex);
        }
    }

    public async Task<bool> DeleteRelationshipAsync(
        string relationshipId,
        GraphTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(relationshipId);

        _logger?.LogDebug($"Deleting relationship with ID {relationshipId}");

        try
        {
            var cypher = @"
                MATCH ()-[r {Id: $relId}]->()
                WITH COUNT(r) AS count, r
                DELETE r
                RETURN count > 0 AS wasDeleted";

            var result = await transaction.Transaction.RunAsync(cypher, new { relId = relationshipId });

            _logger?.LogInformation($"Deleted relationship with ID {relationshipId}");

            // Check if the relationship was deleted
            var record = await result.SingleAsync(cancellationToken);
            var wasDeleted = record["wasDeleted"].As<bool>();

            if (!wasDeleted)
            {
                _logger?.LogWarning($"Relationship with ID {relationshipId} not found for deletion");
                throw new KeyNotFoundException($"Relationship with ID {relationshipId} not found for deletion.");
            }
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, $"Error deleting relationship with ID {relationshipId}");
            throw new GraphException($"Failed to delete relationship: {ex.Message}", ex);
        }
    }
}