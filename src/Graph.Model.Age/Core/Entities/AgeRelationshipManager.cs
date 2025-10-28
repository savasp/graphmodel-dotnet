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

using System.Linq;

using Cvoya.Graph.Model;
using Cvoya.Graph.Model.Serialization;
using Npgsql.Age;
using Npgsql.Age.Types;

/// <summary>
/// Handles CRUD operations for AGE relationships.
/// </summary>
internal sealed class AgeRelationshipManager
{
    private readonly AgeGraphContext context;
    private readonly EntityFactory entityFactory;
    private readonly AgeEntityMapper entityMapper;

    public AgeRelationshipManager(AgeGraphContext context)
    {
        this.context = context;
        entityFactory = new EntityFactory(context.LoggerFactory);
        entityMapper = new AgeEntityMapper(entityFactory, context.LoggerFactory);
    }

    public async Task<TRelationship> CreateRelationshipAsync<TRelationship>(TRelationship relationship, AgeGraphTransaction transaction, CancellationToken cancellationToken)
        where TRelationship : IRelationship
    {
        ArgumentNullException.ThrowIfNull(relationship);
        GraphDataModel.EnforceGraphConstraintsForRelationship(relationship);

        var entity = entityFactory.Serialize(relationship);
        if (entity.ComplexProperties.Count > 0)
        {
            throw new GraphException("Relationships cannot have complex properties in the AGE provider");
        }

        var properties = AgeSerializationBridge.SerializeSimpleProperties(entity);
        // Don't remove StartNodeId and EndNodeId - we want to store them as properties in AGE
        // so they can be retrieved later (AGE's internal IDs are different from our node IDs)

        var labels = entity.ActualLabels.Count > 0 ? entity.ActualLabels : [entity.Label];
        var relationshipType = string.Join(":", labels.Select(label => label.Replace("`", "``")));

        // Build property assignments for SET clause (AGE requires individual property assignments)
        // Apply the same property name mapping that we use in queries
        var setStatements = properties.Select((kvp, idx) => $"r.{MapPropertyNameForAge(kvp.Key)} = $prop{idx}").ToList();
        var cypher = $$"""
            MATCH (src {user_id: $startId}), (tgt {user_id: $endId})
            CREATE (src)-[r:{{relationshipType}}]->(tgt)
            SET {{string.Join(", ", setStatements)}}
            RETURN r
            """;

        var parameters = new Dictionary<string, object?>
        {
            ["startId"] = relationship.StartNodeId,
            ["endId"] = relationship.EndNodeId
        };
        var propIndex = 0;
        foreach (var (key, value) in properties)
        {
            parameters[$"prop{propIndex}"] = value;
            propIndex++;
        }

        var edge = await ExecuteSingleEdgeAsync(transaction, cypher, parameters, relationshipType, cancellationToken).ConfigureAwait(false);
        var entityInfo = entityMapper.MapEdge(edge, typeof(TRelationship));
        return entityFactory.Deserialize<TRelationship>(entityInfo);
    }

    public async Task<bool> UpdateRelationshipAsync<TRelationship>(TRelationship relationship, AgeGraphTransaction transaction, CancellationToken cancellationToken)
        where TRelationship : IRelationship
    {
        ArgumentNullException.ThrowIfNull(relationship);
        GraphDataModel.EnforceGraphConstraintsForRelationship(relationship);

        var entity = entityFactory.Serialize(relationship);
        if (entity.ComplexProperties.Count > 0)
        {
            throw new GraphException("Relationships cannot have complex properties in the AGE provider");
        }

        var properties = AgeSerializationBridge.SerializeSimpleProperties(entity);
        // Don't remove StartNodeId and EndNodeId - keep them as properties

        // Build property assignments for SET clause (AGE requires individual property assignments)
        // Apply the same property name mapping that we use in queries
        var setStatements = properties.Select((kvp, idx) => $"r.{MapPropertyNameForAge(kvp.Key)} = $prop{idx}").ToList();
        var cypher = $$"""
            MATCH ()-[r {user_id: $id}]->()
            SET {{string.Join(", ", setStatements)}}
            RETURN r
            """;

        var parameters = new Dictionary<string, object?>
        {
            ["id"] = relationship.Id
        };
        var propIndex = 0;
        foreach (var (key, value) in properties)
        {
            parameters[$"prop{propIndex}"] = value;
            propIndex++;
        }

        await ExecuteSingleEdgeAsync(transaction, cypher, parameters, null, cancellationToken).ConfigureAwait(false);
        return true;
    }

    public async Task<bool> DeleteRelationshipAsync(string relationshipId, AgeGraphTransaction transaction, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relationshipId);

        var cypher = """
            MATCH ()-[r {user_id: $id}]->()
            WITH r, 1 AS found
            DELETE r
            RETURN found
            """;

        var parameters = new Dictionary<string, object?>
        {
            ["id"] = relationshipId
        };

        await using var command = context.Connection.CreateCypherCommand(context.GraphName, cypher, parameters);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return false;
        }
        
        // Read the result - AGE returns integers wrapped in Agtype
        // Cast to long to extract the integer value
        var intValue = (long)reader.GetFieldValue<Agtype>(0);
        return intValue == 1;
    }

    public async Task<TRelationship> GetRelationshipAsync<TRelationship>(string id, AgeGraphTransaction transaction, CancellationToken cancellationToken)
        where TRelationship : IRelationship
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        // Use the same property mapping as in creation - Id maps to user_id
        var cypher = """
            MATCH ()-[r {user_id: $id}]->()
            RETURN r
            """;

        var parameters = new Dictionary<string, object?>
        {
            ["id"] = id
        };

        var edge = await ExecuteSingleEdgeAsync(transaction, cypher, parameters, null, cancellationToken).ConfigureAwait(false);
        var entityInfo = entityMapper.MapEdge(edge, typeof(TRelationship));
        return entityFactory.Deserialize<TRelationship>(entityInfo);
    }

    private async Task<Edge> ExecuteSingleEdgeAsync(
        AgeGraphTransaction transaction,
        string cypher,
        IReadOnlyDictionary<string, object?> parameters,
        string? relationshipType,
        CancellationToken cancellationToken)
    {
        var finalCypher = relationshipType is null ? cypher : cypher.Replace("{relationshipType}", relationshipType);

        await using var command = context.Connection.CreateCypherCommand(context.GraphName, finalCypher, new Dictionary<string, object?>(parameters));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            throw new GraphException("Query returned no results");
        }

        var agtype = reader.GetFieldValue<Agtype>(0);
        if (!agtype.IsEdge)
        {
            throw new GraphException("Query did not return an edge");
        }

        return agtype.GetEdge();
    }

    /// <summary>
    /// Maps C# property names to AGE property names.
    /// </summary>
    private static string MapPropertyNameForAge(string csharpPropertyName)
    {
        return csharpPropertyName switch
        {
            // Map C# "Id" property to our prefixed "user_id" field to avoid conflict with PostgreSQL internal "Id"
            // This ensures we always use our application-controlled IDs, not PostgreSQL internal IDs
            "Id" => "user_id",
            
            // For all other properties, keep the same name
            _ => csharpPropertyName
        };
    }
}
