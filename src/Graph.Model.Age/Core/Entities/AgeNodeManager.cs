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

/// <summary>
/// Handles CRUD operations for node entities within Apache AGE.
/// </summary>
internal sealed class AgeNodeManager
{
    private readonly AgeGraphContext context;
    private readonly EntityFactory entityFactory;
    private readonly AgeEntityMapper entityMapper;
    private readonly ILogger<AgeNodeManager> logger;
    private readonly AgeComplexPropertyManager complexPropertyManager;

    public AgeNodeManager(AgeGraphContext context)
    {
        this.context = context;
        entityFactory = new EntityFactory(context.LoggerFactory);
        entityMapper = new AgeEntityMapper(entityFactory, context.LoggerFactory);
        complexPropertyManager = new AgeComplexPropertyManager(context, context.LoggerFactory);
        logger = context.LoggerFactory.CreateLogger<AgeNodeManager>();
    }

    public async Task<TNode> CreateNodeAsync<TNode>(TNode node, AgeGraphTransaction transaction, CancellationToken cancellationToken)
        where TNode : INode
    {
        ArgumentNullException.ThrowIfNull(node);
        GraphDataModel.EnforceGraphConstraintsForNode(node);

        var entity = entityFactory.Serialize(node);
        
        // For AGE inheritance support: use base type label and store hierarchy in properties
        var baseLabel = Labels.GetBaseTypeLabel(typeof(TNode));
        var inheritanceHierarchy = Labels.GetInheritanceHierarchy(typeof(TNode));
        
        var properties = AgeSerializationBridge.SerializeSimpleProperties(entity);

        // Remove Labels from properties - it's handled via the CREATE (n:Label) syntax
        // We'll add it back when reading from AGE since the deserialization layer expects it
        properties.Remove(nameof(INode.Labels));
        
        // Add inheritance hierarchy to properties for AGE inheritance support
        if (inheritanceHierarchy.Length > 1)
        {
            properties["inheritance_labels"] = inheritanceHierarchy;
        }

        // Build property assignments for SET clause (AGE requires individual property assignments)
        // Apply the same property name mapping that we use in queries
        var setStatements = properties.Select((kvp, idx) => $"n.{MapPropertyNameForAge(kvp.Key)} = $prop{idx}").ToList();
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

        var vertex = await ExecuteSingleVertexAsync(transaction, cypher, parameters, [baseLabel], cancellationToken).ConfigureAwait(false);
        await complexPropertyManager.CreateComplexPropertiesAsync(transaction, vertex.Properties[MapPropertyNameForAge(nameof(IEntity.Id))]?.ToString() ?? node.Id, entity, cancellationToken).ConfigureAwait(false);

        // Return the original node object with our custom ID (not PostgreSQL internal ID)
        return node;
    }

    public async Task<bool> UpdateNodeAsync<TNode>(TNode node, AgeGraphTransaction transaction, CancellationToken cancellationToken)
        where TNode : INode
    {
        ArgumentNullException.ThrowIfNull(node);
        GraphDataModel.EnforceGraphConstraintsForNode(node);

        var entity = entityFactory.Serialize(node);
        var properties = AgeSerializationBridge.SerializeSimpleProperties(entity);

        // Remove Labels from properties - it's part of the node structure, not a regular property
        properties.Remove(nameof(INode.Labels));
        
        // Remove Id from properties - in AGE, ID is handled by internal AGE IDs, not as a property
        properties.Remove(nameof(INode.Id));

        // Build property assignments for SET clause (AGE requires individual property assignments)
        // Apply the same property name mapping that we use in queries
        var setStatements = properties.Select((kvp, idx) => $"n.{MapPropertyNameForAge(kvp.Key)} = $prop{idx}").ToList();
        var cypher = $$"""
            MATCH (n {user_id: $id})
            SET {{string.Join(", ", setStatements)}}
            RETURN n
            """;

        var parameters = new Dictionary<string, object?>
        {
            ["id"] = node.Id
        };
        var propIndex = 0;
        foreach (var (key, value) in properties)
        {
            parameters[$"prop{propIndex}"] = value;
            propIndex++;
        }

        var vertex = await ExecuteSingleVertexAsync(transaction, cypher, parameters, [], cancellationToken).ConfigureAwait(false);
        await complexPropertyManager.UpdateComplexPropertiesAsync(transaction, vertex.Id.Value.ToString(), entity, cancellationToken).ConfigureAwait(false);
        return true;
    }

    public async Task<bool> DeleteNodeAsync(string nodeId, AgeGraphTransaction transaction, bool cascadeDelete, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nodeId);

        var finalCypher = cascadeDelete
            ? """
                MATCH (n {Id: $id})
                WITH n, 1 AS found
                DETACH DELETE n
                RETURN found
                """
            : """
                MATCH (n {Id: $id})
                WHERE NOT EXISTS((n)--())
                WITH n, 1 AS found
                DETACH DELETE n
                RETURN found
                """;

        var parameters = new Dictionary<string, object?>
        {
            ["id"] = nodeId
        };

        await using var command = context.Connection.CreateCypherCommand(context.GraphName, finalCypher, parameters);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            if (!cascadeDelete)
            {
                throw new GraphException($"Cannot delete node {nodeId} because it has relationships. Use cascadeDelete=true to force deletion.");
            }

            return false;
        }

        // Read the result - AGE returns integers wrapped in Agtype
        // Cast to long to extract the integer value
        var intValue = (long)reader.GetFieldValue<Agtype>(0);
        return intValue == 1;
    }

    public async Task<TNode> GetNodeAsync<TNode>(string id, AgeGraphTransaction transaction, CancellationToken cancellationToken)
        where TNode : INode
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        var cypher = """
            MATCH (n {Id: $id})
            RETURN n
            """;

        var parameters = new Dictionary<string, object?>
        {
            ["id"] = id
        };

        var vertex = await ExecuteSingleVertexAsync(transaction, cypher, parameters, [], cancellationToken).ConfigureAwait(false);
        var entityInfo = entityMapper.MapVertex(vertex, typeof(TNode));
        return entityFactory.Deserialize<TNode>(entityInfo);
    }

    private async Task<Vertex> ExecuteSingleVertexAsync(
        AgeGraphTransaction transaction,
        string cypher,
        IReadOnlyDictionary<string, object?> parameters,
        IReadOnlyList<string> labels,
        CancellationToken cancellationToken)
    {
        var finalCypher = labels.Count == 0 ? cypher : cypher.Replace("{labels}", string.Join(":", labels.Select(label => label.Replace("`", "``"))));
        logger.LogDebug("Executing Cypher query: {Cypher}", finalCypher);
        await using var command = context.Connection.CreateCypherCommand(context.GraphName, finalCypher, new Dictionary<string, object?>(parameters));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            throw new GraphException("Query returned no results");
        }

        var agtype = reader.GetFieldValue<Agtype>(0);
        if (!agtype.IsVertex)
        {
            throw new GraphException("Query did not return a vertex");
        }

        return agtype.GetVertex();
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
