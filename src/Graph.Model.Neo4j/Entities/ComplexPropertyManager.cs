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
/// Handles the creation and management of complex properties as separate nodes in the graph.
/// </summary>
internal sealed class ComplexPropertyManager(GraphContext context)
{
    private readonly ILogger<ComplexPropertyManager> logger = context.LoggerFactory?.CreateLogger<ComplexPropertyManager>()
        ?? NullLogger<ComplexPropertyManager>.Instance;

    public async Task<bool> CreateComplexPropertiesAsync(
        IAsyncTransaction transaction,
        string parentId,
        EntityInfo entity,
        CancellationToken cancellationToken = default)
    {
        if (entity.ComplexProperties.Count == 0)
            return true;

        var allCreated = true;

        foreach (var (propertyName, complexProperty) in entity.ComplexProperties)
        {
            switch (complexProperty.Value)
            {
                case EntityInfo childEntity:
                    allCreated &= await CreateSingleComplexPropertyAsync(
                        transaction, parentId, propertyName, childEntity, 0, cancellationToken);
                    break;

                case EntityCollection collection:
                    allCreated &= await CreateComplexPropertyCollectionAsync(
                        transaction, parentId, propertyName, collection, cancellationToken);
                    break;

                case null:
                    logger.LogDebug("Skipping null complex property {PropertyName}", propertyName);
                    continue;

                default:
                    logger.LogWarning(
                        "Unsupported complex property type: {PropertyType} for property {PropertyName}",
                        complexProperty.Value.GetType().Name,
                        propertyName);
                    throw new GraphException(
                        $"Unsupported complex property type: {complexProperty.Value.GetType().Name} for property {propertyName}");
            }
        }

        return allCreated;
    }

    public async Task<bool> UpdateComplexPropertiesAsync(
        IAsyncTransaction transaction,
        string parentId,
        EntityInfo entity,
        CancellationToken cancellationToken = default)
    {
        // First, delete all existing complex property relationships
        await DeleteExistingComplexPropertiesAsync(transaction, parentId, cancellationToken);

        // Then create the new ones
        return await CreateComplexPropertiesAsync(transaction, parentId, entity, cancellationToken);
    }

    private async Task<bool> CreateSingleComplexPropertyAsync(
        IAsyncTransaction transaction,
        string parentId,
        string propertyName,
        EntityInfo entity,
        int sequenceNumber,
        CancellationToken cancellationToken)
    {
        var relationshipType = GraphDataModel.PropertyNameToRelationshipTypeName(propertyName);

        var cypher = @"
            MATCH (parent)
            WHERE elementId(parent) = $parentId
            CREATE (parent)-[r:$relType $relProps]->(complex:$label $props)
            RETURN elementId(complex) as nodeId";

        var nodeProps = SerializeSimpleProperties(entity);
        var relProps = new Dictionary<string, object> { ["SequenceNumber"] = sequenceNumber };

        var result = await transaction.RunAsync(cypher, new
        {
            parentId,
            relType = relationshipType,
            label = entity.Label,
            props = nodeProps,
            relProps
        });

        var record = await result.SingleAsync(cancellationToken);
        var complexNodeId = record["nodeId"].As<string>()
            ?? throw new GraphException($"Failed to create complex property node");

        logger.LogDebug(
            "Created complex property node {NodeId} for property {PropertyName} on parent {ParentId}",
            complexNodeId, propertyName, parentId);

        // Recursively create nested complex properties
        return await CreateComplexPropertiesAsync(transaction, complexNodeId, entity, cancellationToken);
    }

    private async Task<bool> CreateComplexPropertyCollectionAsync(
        IAsyncTransaction transaction,
        string parentId,
        string propertyName,
        EntityCollection collection,
        CancellationToken cancellationToken)
    {
        var allCreated = true;
        var index = 0;

        foreach (var entity in collection.Entities)
        {
            allCreated &= await CreateSingleComplexPropertyAsync(
                transaction, parentId, propertyName, entity, index++, cancellationToken);
        }

        return allCreated;
    }

    private async Task DeleteExistingComplexPropertiesAsync(
        IAsyncTransaction transaction,
        string parentId,
        CancellationToken cancellationToken)
    {
        var cypher = @"
            MATCH (n {Id: $parentId})-[r]->(complex)
            WHERE type(r) STARTS WITH $propertyPrefix
            DETACH DELETE complex
            DELETE r
            RETURN COUNT(r) AS deletedCount";

        var result = await transaction.RunAsync(cypher, new
        {
            parentId,
            propertyPrefix = GraphDataModel.PropertyRelationshipTypeNamePrefix
        });

        var deletedCount = (await result.FirstAsync(cancellationToken))["deletedCount"].As<int>();

        logger.LogDebug(
            "Deleted {DeletedCount} complex property relationships for parent {ParentId}",
            deletedCount, parentId);
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