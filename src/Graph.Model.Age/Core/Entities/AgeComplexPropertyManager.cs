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

namespace Cvoya.Graph.Model.Age.Core.Entities;

using Cvoya.Graph.Model.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql.Age;
using Npgsql.Age.Types;

/// <summary>
/// Handles storage of complex properties as separate nodes connected via property relationships.
/// </summary>
internal sealed class AgeComplexPropertyManager
{
    private readonly AgeGraphContext context;
    private readonly ILogger logger;

    public AgeComplexPropertyManager(AgeGraphContext context, ILoggerFactory? loggerFactory)
    {
        this.context = context;
        logger = loggerFactory?.CreateLogger<AgeComplexPropertyManager>() ?? NullLogger<AgeComplexPropertyManager>.Instance;
    }

    public async Task<bool> CreateComplexPropertiesAsync(
        AgeGraphTransaction transaction,
        string parentNodeId,
        EntityInfo entity,
        CancellationToken cancellationToken)
    {
        if (entity.ComplexProperties.Count == 0)
        {
            return true;
        }

        foreach (var (propertyName, property) in entity.ComplexProperties)
        {
            switch (property.Value)
            {
                case EntityInfo child:
                    await CreateSingleComplexPropertyAsync(transaction, parentNodeId, propertyName, child, 0, cancellationToken).ConfigureAwait(false);
                    break;
                case EntityCollection collection:
                    var index = 0;
                    foreach (var childEntity in collection.Entities)
                    {
                        await CreateSingleComplexPropertyAsync(transaction, parentNodeId, propertyName, childEntity, index++, cancellationToken).ConfigureAwait(false);
                    }

                    break;
            }
        }

        return true;
    }

    public async Task<bool> UpdateComplexPropertiesAsync(
        AgeGraphTransaction transaction,
        string parentNodeId,
        EntityInfo entity,
        CancellationToken cancellationToken)
    {
        await DeleteExistingComplexPropertiesAsync(transaction, parentNodeId, cancellationToken).ConfigureAwait(false);
        return await CreateComplexPropertiesAsync(transaction, parentNodeId, entity, cancellationToken).ConfigureAwait(false);
    }

    public async Task<Dictionary<string, Property>> LoadComplexPropertiesAsync(
        AgeGraphTransaction transaction,
        string parentNodeId,
        CancellationToken cancellationToken)
    {
        // TODO: Implement complex property loading for AGE
        // For now, complex properties are handled by the query infrastructure (AgeCypherEngine)
        // which returns them as part of the MATCH query result
        await Task.CompletedTask;
        return new Dictionary<string, Property>(StringComparer.Ordinal);
    }

    private async Task CreateSingleComplexPropertyAsync(
        AgeGraphTransaction transaction,
        string parentNodeId,
        string propertyName,
        EntityInfo entity,
        int sequenceNumber,
        CancellationToken cancellationToken)
    {
        var relationshipType = GraphDataModel.PropertyNameToRelationshipTypeName(propertyName);
        var labels = entity.ActualLabels.Count > 0 ? entity.ActualLabels : [entity.Label];
        var serialized = AgeSerializationBridge.SerializeSimpleProperties(entity);
        serialized[nameof(IEntity.Id)] = Guid.NewGuid().ToString("N");
        
        // Remove Labels from properties - it's handled via the CREATE (complex:Label) syntax
        serialized.Remove(nameof(INode.Labels));

        // Build property assignments for SET clause (AGE requires individual property assignments)
        var setStatements = serialized.Select((kvp, idx) => $"complex.{kvp.Key} = $prop{idx}").ToList();
        
        var parameters = new Dictionary<string, object?>
        {
            ["parentId"] = parentNodeId,
            ["sequenceNumber"] = sequenceNumber
        };
        
        var propIndex = 0;
        foreach (var (key, value) in serialized)
        {
            parameters[$"prop{propIndex}"] = value;
            propIndex++;
        }

        var escapedLabels = string.Join(":", labels.Select(label => label.Replace("`", "``")));
        var cypher = $$"""
            MATCH (parent {Id: $parentId})
            CREATE (complex:{{escapedLabels}})
            SET {{string.Join(", ", setStatements)}}
            CREATE (parent)-[rel:{{relationshipType}} {SequenceNumber: $sequenceNumber}]->(complex)
            RETURN complex
            """;

        await ExecuteNonQueryAsync(transaction, cypher, parameters, cancellationToken).ConfigureAwait(false);

        if (entity.ComplexProperties.Count > 0)
        {
            await CreateComplexPropertiesAsync(transaction, (string)serialized[nameof(IEntity.Id)]!, entity, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task DeleteExistingComplexPropertiesAsync(
        AgeGraphTransaction transaction,
        string parentNodeId,
        CancellationToken cancellationToken)
    {
        var cypher = """
            MATCH (parent {Id: $parentId})-[rel]->(complex)
            WHERE type(rel) STARTS WITH $prefix
            DELETE rel
            DETACH DELETE complex
            RETURN 1
            """;

        var parameters = new Dictionary<string, object?>
        {
            ["parentId"] = parentNodeId,
            ["prefix"] = GraphDataModel.PropertyRelationshipTypeNamePrefix
        };

        await ExecuteNonQueryAsync(transaction, cypher, parameters, cancellationToken).ConfigureAwait(false);
    }

    private async Task ExecuteNonQueryAsync(
        AgeGraphTransaction transaction,
        string cypher,
        IReadOnlyDictionary<string, object?> parameters,
        CancellationToken cancellationToken)
    {
        await using var command = context.Connection.CreateCypherCommand(context.GraphName, cypher, new Dictionary<string, object?>(parameters));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            _ = reader.GetValue(0);
        }
    }
}
