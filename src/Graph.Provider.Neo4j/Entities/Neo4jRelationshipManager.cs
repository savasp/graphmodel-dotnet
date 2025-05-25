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

using System.Reflection;
using Cvoya.Graph.Model;
using Cvoya.Graph.Provider.Neo4j.Conversion;
using Cvoya.Graph.Provider.Neo4j.Query;
using Cvoya.Graph.Provider.Neo4j.Schema;
using Microsoft.Extensions.Logging;
using Neo4j.Driver;

namespace Cvoya.Graph.Provider.Neo4j.Entities;

/// <summary>
/// Manages Neo4j relationship operations.
/// </summary>
internal class Neo4jRelationshipManager : Neo4jEntityManagerBase
{
    /// <summary>
    /// Initializes a new instance of the Neo4jRelationshipManager class.
    /// </summary>
    public Neo4jRelationshipManager(
        Neo4jQueryExecutor queryExecutor,
        Neo4jConstraintManager constraintManager, 
        Neo4jEntityConverter entityConverter,
        ILogger? logger = null)
        : base(queryExecutor, constraintManager, entityConverter, logger)
    {
    }

    /// <summary>
    /// Creates a relationship between two nodes in Neo4j.
    /// </summary>
    /// <param name="relationship">The relationship to create</param>
    /// <param name="tx">The transaction to use</param>
    public async Task CreateRelationship(IRelationship relationship, IAsyncTransaction tx)
    {
        var type = relationship.GetType();
        var label = Neo4jTypeManager.GetLabel(type);
        var (simpleProps, complexProps) = GetSimpleAndComplexProperties(relationship);

        CheckRelationshipProperties(complexProps);

        var cypher = $"""
            MATCH (a), (b) 
            WHERE a.{nameof(Model.INode.Id)} = '{relationship.SourceId}' 
                AND b.{nameof(Model.INode.Id)} = '{relationship.TargetId}'
            CREATE (a)-[r:{label} $props]->(b)
            RETURN elementId(r) AS relId
            """;

        await tx.RunAsync(cypher, new
        {
            props = ConvertPropertiesToNeo4j(simpleProps),
        });
    }

    /// <summary>
    /// Updates a relationship with the given data.
    /// </summary>
    /// <param name="relationship">The relationship to update</param>
    /// <param name="tx">The transaction to use</param>
    public async Task UpdateRelationship(IRelationship relationship, IAsyncTransaction tx)
    {
        var (simpleProps, complexProps) = GetSimpleAndComplexProperties(relationship);
        CheckRelationshipProperties(complexProps);

        var cypher = $"MATCH ()-[r]->() WHERE r.{nameof(Model.IRelationship.Id)} = '{relationship.Id}' SET r += $props";
        await tx.RunAsync(cypher, new
        {
            props = ConvertPropertiesToNeo4j(simpleProps),
        });
    }

    /// <summary>
    /// Gets a relationship by its ID and type.
    /// </summary>
    /// <param name="relationshipType">The type of the relationship</param>
    /// <param name="id">The ID of the relationship</param>
    /// <param name="tx">The transaction to use</param>
    /// <returns>The relationship instance</returns>
    /// <exception cref="GraphException">Thrown if the relationship is not found</exception>
    public async Task<IRelationship> GetRelationship(Type relationshipType, string id, IAsyncTransaction tx)
    {
        var label = Neo4jTypeManager.GetLabel(relationshipType);
        var cypher = $"MATCH ()-[r:{label}]->() WHERE r.{nameof(Model.IRelationship.Id)} = $id RETURN r";

        var result = await tx.RunAsync(cypher, new { id });
        var records = await result.ToListAsync();

        if (!records.Any())
        {
            var ex = new KeyNotFoundException($"Relationship with ID '{id}' not found");
            throw new GraphException(ex.Message, ex);
        }

        var relationship = Activator.CreateInstance(relationshipType) as IRelationship;
        if (relationship is null)
        {
            throw new GraphException($"Failed to create instance of type {relationshipType.Name}");
        }

        EntityConverter.PopulateRelationshipEntity(
            relationship, 
            records[0]["r"].As<IRelationship>());
        
        return relationship;
    }

    /// <summary>
    /// Loads the nodes connected to a relationship.
    /// </summary>
    /// <param name="relationship">The relationship to load nodes for</param>
    /// <param name="options">Graph operation options</param>
    /// <param name="tx">The transaction to use</param>
    /// <param name="nodeManager">The node manager for fetching nodes</param>
    public async Task LoadRelationshipNodes(
        IRelationship relationship, 
        GraphOperationOptions options,
        IAsyncTransaction tx,
        Neo4jNodeManager nodeManager)
    {
        var relType = relationship.GetType();
        var genericInterface = relType.GetInterfaces()
            .FirstOrDefault(i => i.IsGenericType &&
                              i.GetGenericTypeDefinition() == typeof(Model.IRelationship<,>));

        if (genericInterface == null) return;

        var sourceType = genericInterface.GetGenericArguments()[0];
        var targetType = genericInterface.GetGenericArguments()[1];
        var sourceProp = relType.GetProperty("Source");
        var targetProp = relType.GetProperty("Target");

        if (sourceProp != null && !string.IsNullOrEmpty(relationship.SourceId))
        {
            var source = await nodeManager.GetNode(sourceType, relationship.SourceId, tx);
            sourceProp.SetValue(relationship, source);

            // Load relationships for source node if depth allows
            if (options.TraversalDepth > 1 || options.TraversalDepth == -1)
            {
                var processedNodes = new HashSet<string>();
                // To be implemented: Load node relationships
            }
        }

        if (targetProp != null && !string.IsNullOrEmpty(relationship.TargetId))
        {
            var target = await nodeManager.GetNode(targetType, relationship.TargetId, tx);
            targetProp.SetValue(relationship, target);

            // Load relationships for target node if depth allows
            if (options.TraversalDepth > 1 || options.TraversalDepth == -1)
            {
                var processedNodes = new HashSet<string>();
                // To be implemented: Load node relationships  
            }
        }
    }

    /// <summary>
    /// Checks relationship properties to ensure they're valid for Neo4j.
    /// </summary>
    /// <param name="complexProps">Complex properties to check</param>
    /// <exception cref="GraphException">Thrown if invalid properties are found</exception>
    private void CheckRelationshipProperties(Dictionary<PropertyInfo, object?> complexProps)
    {
        List<string> relationshipPropertyNames = ["Source", "Target"];
        var check = complexProps
            .Select(p => p.Key)
            .Where(p => !(relationshipPropertyNames.Contains(p.Name) && p.PropertyType.IsAssignableTo(typeof(Model.INode))));

        if (check.Any())
        {
            throw new GraphException("Complex properties are not supported for relationships.");
        }
    }
}