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

using System.Runtime.CompilerServices;
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
            typeof(TRelationship).Name, relationship.SourceId, relationship.TargetId);

        try
        {
            // Serialize the relationship
            var result = _serializer.SerializeRelationship(relationship);

            // Build the Cypher query
            var cypher = $@"
                MATCH (source {{Id: $sourceId}})
                MATCH (target {{Id: $targetId}})
                CREATE (source)-[r:{result.Type} $props]->(target)
                RETURN r";

            _logger?.LogDebug("Cypher query: {CypherQuery}", cypher);
            _logger?.LogDebug("Parameters: SourceId={SourceId}, TargetId={TargetId}, Properties={Properties}",
                result.SourceId, result.TargetId, result.Properties);
            await transaction.Transaction.RunAsync(cypher, new
            {
                sourceId = result.SourceId,
                targetId = result.TargetId,
                props = result.Properties
            });

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
            // Check if relationship exists using LINQ
            var exists = await context.Graph.Relationships<TRelationship>()
                .Where(r => r.Id == relationship.Id)
                .AnyAsync(cancellationToken);

            if (!exists)
            {
                _logger?.LogWarning("Relationship {RelationshipId} not found for update", relationship.Id);
                return false;
            }

            // Serialize the relationship
            var result = _serializer.SerializeRelationship(relationship);

            var cypher = "MATCH ()-[r {Id: $relId}]->() SET r = $props RETURN r";
            var relResult = await transaction.Transaction.RunAsync(cypher, new { relId = relationship.Id, props = result.Properties });
            await relResult.ConsumeAsync();

            _logger?.LogInformation("Updated relationship of type {RelationshipType} with ID {RelationshipId}",
                typeof(TRelationship).Name, relationship.Id);
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error updating relationship {RelationshipId} of type {RelationshipType}",
                relationship.Id, typeof(TRelationship).Name);
            throw new GraphException($"Failed to update relationship: {ex.Message}", ex);
        }
    }

    public async Task<bool> DeleteRelationshipAsync(
        string relationshipId,
        GraphTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(relationshipId);

        _logger?.LogDebug("Deleting relationship with ID {RelationshipId}",
            relationshipId);

        try
        {
            var cypher = "MATCH ()-[r {Id: $relId}]->() DELETE r";
            var result = await transaction.Transaction.RunAsync(cypher, new { relId = relationshipId });

            _logger?.LogInformation("Deleted relationship with ID {RelationshipId}",
                relationshipId);

            // Check if the relationship was deleted
            var record = await result.SingleAsync();
            var wasDeleted = record["wasDeleted"].As<bool>();

            if (!wasDeleted)
            {
                _logger?.LogWarning("Relationship with ID {RelationshipId} not found for deletion", relationshipId);
                return false;
            }
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error deleting relationship with ID {RelationshipId}",
                relationshipId);
            throw new GraphException($"Failed to delete relationship: {ex.Message}", ex);
        }
    }
}