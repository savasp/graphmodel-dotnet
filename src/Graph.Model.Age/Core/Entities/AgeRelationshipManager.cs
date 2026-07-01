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
using Cvoya.Graph.Model.Age.Querying.Cypher.Visitors.Core;

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

        await AgeEntityAttributeValidator.ValidateRelationshipAsync(relationship, context, transaction, isUpdate: false, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        var entity = entityFactory.Serialize(relationship);
        if (entity.ComplexProperties.Count > 0)
            throw new GraphException("Relationships cannot have complex properties in the AGE provider");

        var actualType = relationship.GetType();
        var baseLabel = Labels.GetBaseTypeLabel(actualType);
        var inheritanceHierarchy = Labels.GetInheritanceHierarchy(actualType);

        var allInterfaces = actualType.GetInterfaces()
            .Where(i => typeof(IRelationship).IsAssignableFrom(i))
            .Select(Labels.GetLabelFromType).ToArray();

        var allLabels = inheritanceHierarchy.Concat(allInterfaces).Distinct().ToArray();
        var properties = AgeSerializationBridge.SerializeSimpleProperties(entity);

        if (allLabels.Length > 1)
            properties["inheritance_labels"] = allLabels;

        var setStatements = properties.Select((kvp, idx) => $"r.{ExpressionTranslationHelper.QuotePropertyName(MapPropertyNameForAge(kvp.Key))} = $prop{idx}").ToList();
        var cypher = $$"""
            MATCH (src {user_id: $startId}), (tgt {user_id: $endId})
            CREATE (src)-[r:{{baseLabel}}]->(tgt)
            SET {{string.Join(", ", setStatements)}}
            RETURN r
            """;

        var parameters = new Dictionary<string, object?> { ["startId"] = relationship.StartNodeId, ["endId"] = relationship.EndNodeId };
        var propIndex = 0;
        foreach (var (key, value) in properties) { parameters[$"prop{propIndex}"] = value; propIndex++; }

        await ExecuteSingleEdgeAsync(transaction, cypher, parameters, cancellationToken).ConfigureAwait(false);
        return relationship;
    }

    public async Task<bool> UpdateRelationshipAsync<TRelationship>(TRelationship relationship, AgeGraphTransaction transaction, CancellationToken cancellationToken)
        where TRelationship : IRelationship
    {
        ArgumentNullException.ThrowIfNull(relationship);
        GraphDataModel.EnforceGraphConstraintsForRelationship(relationship);
        await AgeEntityAttributeValidator.ValidateRelationshipAsync(relationship, context, transaction, isUpdate: true, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        var entity = entityFactory.Serialize(relationship);
        if (entity.ComplexProperties.Count > 0)
            throw new GraphException("Relationships cannot have complex properties in the AGE provider");

        var properties = AgeSerializationBridge.SerializeSimpleProperties(entity);
        var setStatements = properties.Select((kvp, idx) => $"r.{ExpressionTranslationHelper.QuotePropertyName(MapPropertyNameForAge(kvp.Key))} = $prop{idx}").ToList();
        var cypher = $$"""MATCH ()-[r {user_id: $id}]->() SET {{string.Join(", ", setStatements)}} RETURN r""";

        var parameters = new Dictionary<string, object?> { ["id"] = relationship.Id };
        var propIndex = 0;
        foreach (var (key, value) in properties) { parameters[$"prop{propIndex}"] = value; propIndex++; }

        await ExecuteSingleEdgeAsync(transaction, cypher, parameters, cancellationToken).ConfigureAwait(false);
        return true;
    }

    public async Task<bool> DeleteRelationshipAsync(string relationshipId, AgeGraphTransaction transaction, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relationshipId);
        var cypher = """MATCH ()-[r {user_id: $id}]->() WITH r, 1 AS found DELETE r RETURN found""";
        var parameters = new Dictionary<string, object?> { ["id"] = relationshipId };

        await using var command = context.Connection.CreateCypherCommand(context.GraphName, cypher, parameters);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false)) return false;
        return (long)reader.GetFieldValue<Agtype>(0) == 1;
    }

    public async Task<TRelationship> GetRelationshipAsync<TRelationship>(string id, AgeGraphTransaction transaction, CancellationToken cancellationToken)
        where TRelationship : IRelationship
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        var cypher = """MATCH ()-[r {user_id: $id}]->() RETURN r""";
        var parameters = new Dictionary<string, object?> { ["id"] = id };

        await using var command = context.Connection.CreateCypherCommand(context.GraphName, cypher, parameters);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            throw new GraphException($"Relationship {id} not found");

        var agtype = reader.GetFieldValue<Agtype>(0);
        if (!agtype.IsEdge)
            throw new GraphException("Query did not return an edge");

        var edge = agtype.GetEdge();

        // Use MapEdge for property conversion and type resolution (class hierarchy support).
        // MapEdge resolves the most derived type from inheritance_labels and converts
        // AGE property types to the correct C# types.
        var entityInfo = entityMapper.MapEdge(edge, typeof(TRelationship));
        return entityFactory.Deserialize<TRelationship>(entityInfo);
    }

    private async Task<Edge> ExecuteSingleEdgeAsync(AgeGraphTransaction transaction, string cypher, Dictionary<string, object?> parameters, CancellationToken cancellationToken)
    {
        await using var command = context.Connection.CreateCypherCommand(context.GraphName, cypher, parameters);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            throw new GraphException("Query returned no results");
        var agtype = reader.GetFieldValue<Agtype>(0);
        if (!agtype.IsEdge)
            throw new GraphException("Query did not return an edge");
        return agtype.GetEdge();
    }

    private static string MapPropertyNameForAge(string csharpPropertyName)
        => ExpressionTranslationHelper.MapPropertyName(csharpPropertyName);
}
