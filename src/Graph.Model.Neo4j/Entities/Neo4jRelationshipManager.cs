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


/// <summary>
/// Manages CRUD operations for relationships in Neo4j.
/// All methods assume that there is already a transaction in progress.
/// </summary>
internal sealed class Neo4jRelationshipManager(GraphContext context)
{
    private readonly ILogger<Neo4jRelationshipManager> _logger = context.LoggerFactory?.CreateLogger<Neo4jRelationshipManager>()
        ?? NullLogger<Neo4jRelationshipManager>.Instance;
    private readonly EntityFactory _serializer = new();

    private static readonly string[] _ignoredProperties =
    [
        nameof(Model.IRelationship.StartNodeId),
        nameof(Model.IRelationship.EndNodeId),
        nameof(Model.IRelationship.Direction)
    ];

    public async Task<TRelationship?> GetRelationshipAsync<TRelationship>(
        string relationshipId,
        GraphTransaction transaction,
        CancellationToken cancellationToken = default)
        where TRelationship : Model.IRelationship
    {
        ArgumentException.ThrowIfNullOrEmpty(relationshipId);

        _logger.LogDebug("Getting relationship of type {RelationshipType} with ID {RelationshipId}",
            typeof(TRelationship).Name, relationshipId);

        try
        {
            // Use LINQ with the relationship queryable
            var query = context.Graph.Relationships<TRelationship>()
                .Where(r => r.Id == relationshipId)
                .WithTransaction(transaction);

            return await query.FirstOrDefaultAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving relationship {RelationshipId} of type {RelationshipType}",
                relationshipId, typeof(TRelationship).Name);
            throw new GraphException($"Failed to retrieve relationship: {ex.Message}", ex);
        }
    }

    public async Task<TRelationship> CreateRelationshipAsync<TRelationship>(
        TRelationship relationship,
        GraphTransaction transaction,
        CancellationToken cancellationToken = default)
        where TRelationship : Model.IRelationship
    {
        ArgumentNullException.ThrowIfNull(relationship);

        _logger.LogDebug("Creating relationship of type {RelationshipType} from {StartNodeId} to {EndNodeId}",
            typeof(TRelationship).Name, relationship.StartNodeId, relationship.EndNodeId);

        try
        {
            // Validate no reference cycles
            GraphDataModel.EnsureNoReferenceCycle(relationship);

            // Serialize the relationship
            var entity = _serializer.Serialize(relationship);

            // Validate that relationships don't have complex properties
            if (entity.ComplexProperties.Count > 0)
            {
                throw new GraphException(
                    $"Relationships cannot have complex properties. Found {entity.ComplexProperties.Count} complex properties on {typeof(TRelationship).Name}");
            }

            // Create the relationship
            var created = await CreateRelationshipInGraphAsync(
                entity,
                relationship.StartNodeId,
                relationship.EndNodeId,
                transaction.Transaction,
                cancellationToken);

            if (!created)
            {
                throw new GraphException(
                    $"Failed to create relationship of type {typeof(TRelationship).Name} from {relationship.StartNodeId} to {relationship.EndNodeId}. " +
                    "One or both nodes may not exist.");
            }

            _logger.LogInformation("Created relationship of type {RelationshipType} with ID {RelationshipId}",
                typeof(TRelationship).Name, relationship.Id);

            return relationship;
        }
        catch (Exception ex) when (ex is not GraphException)
        {
            _logger.LogError(ex, "Error creating relationship of type {RelationshipType}", typeof(TRelationship).Name);
            throw new GraphException($"Failed to create relationship: {ex.Message}", ex);
        }
    }

    public async Task<bool> UpdateRelationshipAsync<TRelationship>(
        TRelationship relationship,
        GraphTransaction transaction,
        CancellationToken cancellationToken = default)
        where TRelationship : Model.IRelationship
    {
        ArgumentNullException.ThrowIfNull(relationship);

        _logger.LogDebug("Updating relationship of type {RelationshipType} with ID {RelationshipId}",
            typeof(TRelationship).Name, relationship.Id);

        try
        {
            // Validate no reference cycles
            GraphDataModel.EnsureNoReferenceCycle(relationship);

            // Serialize the relationship
            var entity = _serializer.Serialize(relationship);

            // Validate that relationships don't have complex properties
            if (entity.ComplexProperties.Count > 0)
            {
                throw new GraphException(
                    $"Relationships cannot have complex properties. Found {entity.ComplexProperties.Count} complex properties on {typeof(TRelationship).Name}");
            }

            // Update the relationship properties
            var updated = await UpdateRelationshipPropertiesAsync(
                relationship.Id,
                entity,
                transaction.Transaction,
                cancellationToken);

            if (!updated)
            {
                _logger.LogWarning("Relationship with ID {RelationshipId} not found for update", relationship.Id);
                throw new KeyNotFoundException($"Relationship with ID {relationship.Id} not found for update");
            }

            _logger.LogInformation("Updated relationship of type {RelationshipType} with ID {RelationshipId}",
                typeof(TRelationship).Name, relationship.Id);

            return true;
        }
        catch (Exception ex) when (ex is not GraphException and not KeyNotFoundException)
        {
            _logger.LogError(ex, "Error updating relationship {RelationshipId} of type {RelationshipType}",
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

        _logger.LogDebug("Deleting relationship with ID {RelationshipId}", relationshipId);

        try
        {
            var cypher = "MATCH ()-[r {Id: $relId}]-() DELETE r RETURN COUNT(r) AS deletedCount";

            var result = await transaction.Transaction.RunAsync(
                cypher,
                new { relId = relationshipId });

            var record = await result.SingleAsync(cancellationToken);
            var deletedCount = record["deletedCount"].As<int>();

            if (deletedCount == 0)
            {
                _logger.LogWarning("Relationship with ID {RelationshipId} not found for deletion", relationshipId);
                throw new KeyNotFoundException($"Relationship with ID {relationshipId} not found for deletion");
            }

            _logger.LogInformation("Deleted relationship with ID {RelationshipId}", relationshipId);
            return true;
        }
        catch (Exception ex) when (ex is not KeyNotFoundException)
        {
            _logger.LogError(ex, "Error deleting relationship with ID {RelationshipId}", relationshipId);
            throw new GraphException($"Failed to delete relationship: {ex.Message}", ex);
        }
    }

    private async Task<bool> CreateRelationshipInGraphAsync(
        EntityInfo entity,
        string startNodeId,
        string endNodeId,
        IAsyncTransaction transaction,
        CancellationToken cancellationToken)
    {
        var cypher = $@"
            MATCH (source {{Id: $startNodeId}})
            MATCH (target {{Id: $endNodeId}})
            CREATE (source)-[r:{entity.Label} $props]->(target)
            RETURN r IS NOT NULL AS created";

        var properties = SerializeSimpleProperties(entity)
            .Where(kv => !_ignoredProperties.Contains(kv.Key))
            .ToDictionary(kv => kv.Key, kv => kv.Value);

        var result = await transaction.RunAsync(cypher, new
        {
            startNodeId,
            endNodeId,
            props = properties
        });

        var record = await result.SingleAsync(cancellationToken);
        return record["created"].As<bool>();
    }

    private async Task<bool> UpdateRelationshipPropertiesAsync(
        string relationshipId,
        EntityInfo entity,
        IAsyncTransaction transaction,
        CancellationToken cancellationToken)
    {
        var cypher = "MATCH ()-[r {Id: $relId}]->() SET r = $props RETURN r IS NOT NULL AS updated";

        var properties = SerializeSimpleProperties(entity)
            .Where(kv => !_ignoredProperties.Contains(kv.Key))
            .ToDictionary(kv => kv.Key, kv => kv.Value);

        var result = await transaction.RunAsync(cypher, new
        {
            relId = relationshipId,
            props = properties
        });

        var record = await result.SingleAsync(cancellationToken);
        return record["updated"].As<bool>();
    }

    private static Dictionary<string, object> SerializeSimpleProperties(EntityInfo entity)
    {
        return entity.SimpleProperties
            .Where(kv => kv.Value.Value is not null)
            .ToDictionary(
                kv => kv.Key,
                kv => kv.Value.Value switch
                {
                    SimpleValue simple => simple.Object,
                    SimpleCollection collection => collection.Values.Select(v => v.Object),
                    _ => throw new GraphException("Unexpected value type in simple properties")
                });
    }
}