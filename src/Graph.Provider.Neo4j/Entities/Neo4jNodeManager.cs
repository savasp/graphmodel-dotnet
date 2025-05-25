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

using Cvoya.Graph.Model;
using Cvoya.Graph.Provider.Neo4j.Conversion;
using Cvoya.Graph.Provider.Neo4j.Query;
using Cvoya.Graph.Provider.Neo4j.Schema;
using Microsoft.Extensions.Logging;
using Neo4j.Driver;

namespace Cvoya.Graph.Provider.Neo4j.Entities;

/// <summary>
/// Manages Neo4j node operations.
/// </summary>
internal class Neo4jNodeManager : Neo4jEntityManagerBase
{
    /// <summary>
    /// Initializes a new instance of the Neo4jNodeManager class.
    /// </summary>
    public Neo4jNodeManager(
        Neo4jQueryExecutor queryExecutor,
        Neo4jConstraintManager constraintManager,
        Neo4jEntityConverter entityConverter,
        ILogger? logger = null)
        : base(queryExecutor, constraintManager, entityConverter, logger)
    {
    }

    /// <summary>
    /// Creates a node in Neo4j.
    /// </summary>
    /// <param name="parentId">Optional parent node ID for hierarchical relationships</param>
    /// <param name="node">The node to create</param>
    /// <param name="tx">The transaction to use</param>
    /// <param name="propertyName">Optional property name when creating from a parent relationship</param>
    /// <returns>The ID of the created node</returns>
    public async Task<string> CreateNode(string? parentId, object node, IAsyncTransaction tx, string? propertyName = null)
    {
        var type = node.GetType();
        var label = Neo4jTypeManager.GetLabel(type);
        var (simpleProps, complexProps) = GetSimpleAndComplexProperties(node);

        CheckNodeProperties(complexProps);

        // Ensure constraints for the node
        await ConstraintManager.EnsureConstraintsForLabel(label, simpleProps.Select(p => p.Key));

        // Create the node with appropriate cypher query
        var cypher = parentId == null ?
            $"CREATE (b:{label} $props) RETURN elementId(b) as nodeId" :
            $"MATCH (a) WHERE elementId(a) = '{parentId}' CREATE (a)-[:{propertyName}]->(b:{label} $props) RETURN elementId(b) as nodeId";

        var record = await tx.RunAsync(cypher, new
        {
            props = ConvertPropertiesToNeo4j(simpleProps),
        });
        
        var createdNode = await record.SingleAsync();
        var nodeId = createdNode["nodeId"].ToString() ?? 
            throw new GraphException($"Failed to create node of type '{label}'");

        // Handle complex properties recursively
        foreach (var prop in complexProps)
        {
            if (prop.Value == null) continue;
            await CreateNode(nodeId, prop.Value, tx, prop.Key.Name);
        }

        return nodeId;
    }

    /// <summary>
    /// Updates a node with the given data.
    /// </summary>
    /// <param name="node">The node to update</param>
    /// <param name="tx">The transaction to use</param>
    public async Task UpdateNode(INode node, IAsyncTransaction tx)
    {
        var (simpleProps, complexProps) = GetSimpleAndComplexProperties(node);
        CheckNodeProperties(complexProps);

        var cypher = $"MATCH (n) WHERE n.{nameof(Model.INode.Id)} = '{node.Id}' SET n += $props";
        await tx.RunAsync(cypher, new
        {
            props = ConvertPropertiesToNeo4j(simpleProps)
        });
    }

    /// <summary>
    /// Gets a node by its ID and type.
    /// </summary>
    /// <param name="nodeType">The type of the node</param>
    /// <param name="id">The ID of the node</param>
    /// <param name="tx">The transaction to use</param>
    /// <returns>The node instance</returns>
    /// <exception cref="GraphException">Thrown if the node is not found</exception>
    public async Task<INode> GetNode(Type nodeType, string id, IAsyncTransaction tx)
    {
        var label = Neo4jTypeManager.GetLabel(nodeType);
        var cypher = $"MATCH (n:{label}) WHERE n.{nameof(Model.INode.Id)} = $id RETURN n";

        var result = await tx.RunAsync(cypher, new { id });
        var records = await result.ToListAsync();

        if (!records.Any())
        {
            var ex = new KeyNotFoundException($"Node with ID '{id}' not found");
            throw new GraphException(ex.Message, ex);
        }

        var node = Activator.CreateInstance(nodeType) as INode;
        if (node == null)
        {
            throw new GraphException($"Failed to create instance of type {nodeType.Name}");
        }

        EntityConverter.PopulateNodeEntity(node, records[0]["n"].As<INode>());
        return node;
    }

    /// <summary>
    /// Checks node properties to ensure they're valid for Neo4j.
    /// </summary>
    /// <param name="complexProps">Complex properties to check</param>
    /// <exception cref="GraphException">Thrown if invalid properties are found</exception>
    private void CheckNodeProperties(Dictionary<System.Reflection.PropertyInfo, object?> complexProps)
    {
        var check = complexProps
            .Select(p => p.Key)
            .Where(p => p.PropertyType.IsAssignableTo(typeof(Model.INode)));

        if (check.Any())
        {
            throw new GraphException($"Properties of type '{typeof(Model.INode).Name}' are not supported for nodes.");
        }
    }
}