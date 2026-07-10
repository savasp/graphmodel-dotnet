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

namespace Cvoya.Graph.Neo4j.Entities;

using Cvoya.Graph.Neo4j.Core;
using Cvoya.Graph.Neo4j.Querying.Cypher;
using Cvoya.Graph.Neo4j.Serialization;
using Cvoya.Graph.Serialization;
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

    public async Task CreateComplexPropertiesAsync(
        IAsyncTransaction transaction,
        string parentId,
        EntityInfo entity,
        CancellationToken cancellationToken = default)
    {
        await CreateComplexPropertiesAsync(
            transaction, parentId, entity, depth: 0, cancellationToken).ConfigureAwait(false);
    }

    private async Task CreateComplexPropertiesAsync(
        IAsyncTransaction transaction,
        string parentId,
        EntityInfo entity,
        int depth,
        CancellationToken cancellationToken)
    {
        if (entity.ComplexProperties.Count == 0)
            return;

        if (depth >= GraphDataModel.DefaultDepthAllowed &&
            entity.ComplexProperties.Values.Any(property => property.Value is not null))
        {
            throw new GraphException(
                $"Complex properties cannot exceed {GraphDataModel.DefaultDepthAllowed} levels of depth.");
        }

        foreach (var (propertyName, complexProperty) in entity.ComplexProperties)
        {
            switch (complexProperty.Value)
            {
                case EntityInfo childEntity:
                    await CreateSingleComplexPropertyAsync(
                        transaction, parentId, propertyName, complexProperty, childEntity, 0, depth, cancellationToken)
                        .ConfigureAwait(false);
                    break;

                case EntityCollection collection:
                    await CreateComplexPropertyCollectionAsync(
                        transaction, parentId, propertyName, complexProperty, collection, depth, cancellationToken)
                        .ConfigureAwait(false);
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
    }

    public async Task UpdateComplexPropertiesAsync(
        IAsyncTransaction transaction,
        string parentId,
        EntityInfo entity,
        CancellationToken cancellationToken = default)
    {
        // First, delete all existing complex property relationships
        await DeleteExistingComplexPropertiesAsync(transaction, parentId, cancellationToken).ConfigureAwait(false);

        // Then create the new ones
        await CreateComplexPropertiesAsync(transaction, parentId, entity, cancellationToken).ConfigureAwait(false);
    }

    private async Task CreateSingleComplexPropertyAsync(
        IAsyncTransaction transaction,
        string parentId,
        string propertyName,
        Property property,
        EntityInfo entity,
        int sequenceNumber,
        int depth,
        CancellationToken cancellationToken)
    {
        var relationshipType = property.RelationshipType ?? (property.PropertyInfo is null
            ? GraphDataModel.PropertyNameToRelationshipTypeName(propertyName)
            : GraphDataModel.GetComplexPropertyRelationshipType(property.PropertyInfo));
        var escapedRelationshipType = CypherIdentifier.Escape(relationshipType, "complex-property relationship type");
        var escapedLabel = CypherIdentifier.Escape(entity.Label, "complex-property node label");

        var cypher = @$"
            MATCH (parent)
            WHERE elementId(parent) = $parentId
            CREATE (parent)-[r:{escapedRelationshipType} $relProps]->(complex:{escapedLabel} $props)
            RETURN elementId(complex) as nodeId";

        var nodeProps = SerializationHelpers.SerializeSimpleProperties(entity);
        nodeProps[nameof(Model.IEntity.Id)] = Guid.NewGuid().ToString("D");
        nodeProps[SerializationBridge.EntityKindPropertyName] = SerializationBridge.NodeEntityKind;
        var relProps = new Dictionary<string, object>
        {
            [nameof(Model.IEntity.Id)] = Guid.NewGuid().ToString("D"),
            ["SequenceNumber"] = sequenceNumber,
            [ComplexPropertyStorage.RelationshipMarkerProperty] = true
        };

        var result = await transaction.RunAsync(cypher, new
        {
            parentId,
            props = nodeProps,
            relProps
        }).ConfigureAwait(false);

        var record = await GetSingleRecordAsync(result, cancellationToken).ConfigureAwait(false);
        var complexNodeId = record["nodeId"].As<string>()
            ?? throw new GraphException($"Failed to create complex property node");

        logger.LogDebug(
            "Created complex property node {NodeId} for property {PropertyName} on parent {ParentId}",
            complexNodeId, propertyName, parentId);

        // Recursively create nested complex properties
        await CreateComplexPropertiesAsync(transaction, complexNodeId, entity, depth + 1, cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task CreateComplexPropertyCollectionAsync(
        IAsyncTransaction transaction,
        string parentId,
        string propertyName,
        Property property,
        EntityCollection collection,
        int depth,
        CancellationToken cancellationToken)
    {
        var index = 0;
        foreach (var entity in collection.Entities)
        {
            await CreateSingleComplexPropertyAsync(
                transaction, parentId, propertyName, property, entity, index++, depth, cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private async Task DeleteExistingComplexPropertiesAsync(
        IAsyncTransaction transaction,
        string parentId,
        CancellationToken cancellationToken)
    {
        var cypher = $@"
            MATCH (parent)
            WHERE elementId(parent) = $parentId
            OPTIONAL MATCH (parent)-[propertyRels*1..{GraphDataModel.DefaultDepthAllowed}]->(propertyNode)
            WHERE ALL(rel IN propertyRels WHERE rel.{ComplexPropertyStorage.RelationshipMarkerProperty} = true)
            WITH [node IN collect(DISTINCT propertyNode) WHERE node IS NOT NULL] AS propertyNodes
            FOREACH (propertyNode IN propertyNodes | DETACH DELETE propertyNode)
            RETURN size(propertyNodes) AS deletedCount";

        var result = await transaction.RunAsync(cypher, new { parentId }).ConfigureAwait(false);

        var deletedCount = (await GetFirstRecordAsync(result, cancellationToken).ConfigureAwait(false))["deletedCount"].As<int>();

        logger.LogDebug(
            "Deleted {DeletedCount} complex property relationships for parent {ParentId}",
            deletedCount, parentId);
    }

    private static async Task<IRecord> GetSingleRecordAsync(IResultCursor result, CancellationToken cancellationToken)
    {
        return await result.SingleAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task<IRecord> GetFirstRecordAsync(IResultCursor result, CancellationToken cancellationToken)
    {
        return await result.FirstAsync(cancellationToken).ConfigureAwait(false);
    }
}
