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
using Microsoft.Extensions.Logging;
using Npgsql.Age;
using Npgsql.Age.Types;
using Cvoya.Graph.Model.Age.Querying.Cypher.Visitors.Core;

/// <summary>
/// Handles CRUD operations for node entities within Apache AGE.
/// </summary>
internal sealed class AgeNodeManager
{
    private readonly AgeGraphContext context;
    private readonly EntityFactory entityFactory;
    private readonly AgeEntityMapper entityMapper;
    private readonly ILogger<AgeNodeManager> logger;

    public AgeNodeManager(AgeGraphContext context)
    {
        this.context = context;
        entityFactory = new EntityFactory(context.LoggerFactory);
        entityMapper = new AgeEntityMapper(entityFactory, context.LoggerFactory);
        logger = context.LoggerFactory.CreateLogger<AgeNodeManager>();
    }

    public async Task<TNode> CreateNodeAsync<TNode>(TNode node, AgeGraphTransaction transaction, CancellationToken cancellationToken)
        where TNode : INode
    {
        ArgumentNullException.ThrowIfNull(node);
        GraphDataModel.EnforceGraphConstraintsForNode(node);

        await AgeEntityAttributeValidator.ValidateNodeAsync(
                node,
                context,
                transaction,
                isUpdate: false,
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        var entity = entityFactory.Serialize(node);
        var actualType = node.GetType();
        var baseLabel = Labels.GetBaseTypeLabel(actualType);
        var inheritanceHierarchy = Labels.GetInheritanceHierarchy(actualType);
        var properties = AgeSerializationBridge.SerializeAllProperties(entity);

        properties.Remove(nameof(INode.Labels));

        if (inheritanceHierarchy.Length > 1)
        {
            properties["inheritance_labels"] = inheritanceHierarchy;
        }

        // Check for duplicate node ID first
        if (await NodeExistsAsync(node.Id, transaction, cancellationToken).ConfigureAwait(false))
        {
            throw new GraphException($"Node with ID '{node.Id}' already exists.");
        }

        var setStatements = properties.Select((kvp, idx) => $"n.{ExpressionTranslationHelper.QuotePropertyName(MapPropertyNameForAge(kvp.Key))} = $prop{idx}").ToList();
        var cypher = $$"""
            CREATE (n:{{baseLabel}})
            SET {{string.Join(", ", setStatements)}}
            RETURN n
            """;

        var parameters = new Dictionary<string, object?>();
        var propIndex = 0;
        foreach (var (key, value) in properties)
        {
            parameters[$"prop{propIndex}"] = value;
            propIndex++;
        }

        await ExecuteSingleVertexAsync(transaction, cypher, parameters, [baseLabel], cancellationToken).ConfigureAwait(false);
        return node;
    }

    public async Task<bool> UpdateNodeAsync<TNode>(TNode node, AgeGraphTransaction transaction, CancellationToken cancellationToken)
        where TNode : INode
    {
        ArgumentNullException.ThrowIfNull(node);
        GraphDataModel.EnforceGraphConstraintsForNode(node);

        await AgeEntityAttributeValidator.ValidateNodeAsync(
                node,
                context,
                transaction,
                isUpdate: true,
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        var entity = entityFactory.Serialize(node);
        var properties = AgeSerializationBridge.SerializeAllProperties(entity);
        properties.Remove(nameof(INode.Labels));
        properties.Remove(nameof(INode.Id));

        var setStatements = properties.Select((kvp, idx) => $"n.{ExpressionTranslationHelper.QuotePropertyName(MapPropertyNameForAge(kvp.Key))} = $prop{idx}").ToList();
        var cypher = $$"""
            MATCH (n {user_id: $id})
            SET {{string.Join(", ", setStatements)}}
            RETURN n
            """;

        var parameters = new Dictionary<string, object?> { ["id"] = node.Id };
        var propIndex = 0;
        foreach (var (key, value) in properties)
        {
            parameters[$"prop{propIndex}"] = value;
            propIndex++;
        }

        await ExecuteSingleVertexAsync(transaction, cypher, parameters, [], cancellationToken).ConfigureAwait(false);
        return true;
    }

    public async Task<bool> DeleteNodeAsync(string nodeId, AgeGraphTransaction transaction, bool cascadeDelete, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nodeId);

        var finalCypher = cascadeDelete
            ? """MATCH (n {user_id: $id}) WITH n, 1 AS found DETACH DELETE n RETURN found"""
            : """MATCH (n {user_id: $id}) WHERE NOT EXISTS((n)--()) WITH n, 1 AS found DETACH DELETE n RETURN found""";

        var parameters = new Dictionary<string, object?> { ["id"] = nodeId };
        logger.LogTrace("Executing Cypher query: {Cypher}", finalCypher);
        await using var command = context.Connection.CreateCypherCommand(context.GraphName, finalCypher, parameters);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            if (!cascadeDelete)
                throw new GraphException($"Cannot delete node {nodeId} because it has relationships. Use cascadeDelete=true to force deletion.");
            return false;
        }

        return (long)reader.GetFieldValue<Agtype>(0) == 1;
    }

    public async Task<TNode> GetNodeAsync<TNode>(string id, AgeGraphTransaction transaction, CancellationToken cancellationToken)
        where TNode : INode
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        var vertex = await FetchVertexAsync(id, transaction, cancellationToken).ConfigureAwait(false);
        var entityInfo = entityMapper.MapVertex(vertex, typeof(TNode));

        var instance = entityFactory.Deserialize<TNode>(entityInfo);

        // Force-set Labels using reflection — the generated serializer can produce
        // empty Labels for record types in some framework configurations.
        var labelsProp = typeof(TNode).GetProperty(nameof(INode.Labels));
        if (labelsProp != null)
        {
            var labels = new List<string>();
            if (!string.IsNullOrWhiteSpace(vertex.Label))
                labels.Add(vertex.Label);
            labelsProp.SetValue(instance, labels);
        }

        return instance;
    }

    private async Task<Vertex> FetchVertexAsync(string id, AgeGraphTransaction transaction, CancellationToken cancellationToken)
    {
        var cypher = """MATCH (n {user_id: $id}) RETURN n""";
        var parameters = new Dictionary<string, object?> { ["id"] = id };
        return await ExecuteSingleVertexAsync(transaction, cypher, parameters, [], cancellationToken).ConfigureAwait(false);
    }

    private async Task<bool> NodeExistsAsync(string id, AgeGraphTransaction transaction, CancellationToken cancellationToken)
    {
        // Use MATCH with LIMIT 1 — if a row is returned, the node exists
        var cypher = """MATCH (n {user_id: $id}) RETURN n LIMIT 1""";
        await using var cmd = context.Connection.CreateCypherCommand(context.GraphName, cypher, new Dictionary<string, object?> { ["id"] = id });
        cmd.Transaction = transaction.Transaction;
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<Vertex> ExecuteSingleVertexAsync(
        AgeGraphTransaction transaction,
        string cypher,
        IReadOnlyDictionary<string, object?> parameters,
        IReadOnlyList<string> labels,
        CancellationToken cancellationToken)
    {
        logger.LogDebug("Executing Cypher query: {Cypher}", cypher);
        await using var command = context.Connection.CreateCypherCommand(context.GraphName, cypher, new Dictionary<string, object?>(parameters));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            throw new GraphException("Query returned no results");

        var agtype = reader.GetFieldValue<Agtype>(0);
        if (!agtype.IsVertex)
            throw new GraphException("Query did not return a vertex");

        return agtype.GetVertex();
    }

    private static string MapPropertyNameForAge(string csharpPropertyName)
        => ExpressionTranslationHelper.MapPropertyName(csharpPropertyName);
}
