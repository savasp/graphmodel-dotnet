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
        Microsoft.Extensions.Logging.ILogger? logger = null)
        : base(queryExecutor, constraintManager, entityConverter, logger)
    {
    }

    /// <summary>
    /// Creates a relationship between two nodes in Neo4j.
    /// </summary>
    /// <param name="relationship">The relationship to create</param>
    /// <param name="options">Graph operation options</param>
    /// <param name="tx">The transaction to use</param>
    public async Task CreateRelationship(Cvoya.Graph.Model.IRelationship relationship, GraphOperationOptions options, IAsyncTransaction tx)
    {
        var type = relationship.GetType();
        var label = Neo4jTypeManager.GetLabel(type);
        var (simpleProps, _) = GetSimpleAndComplexProperties(relationship);

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
    /// <param name="options">Graph operation options</param>
    /// <param name="tx">The transaction to use</param>
    public async Task UpdateRelationship(Cvoya.Graph.Model.IRelationship relationship, GraphOperationOptions options, IAsyncTransaction tx)
    {
        var (simpleProps, _) = GetSimpleAndComplexProperties(relationship);

        var cypher = $"MATCH ()-[r]->() WHERE r.{nameof(Model.IRelationship.Id)} = '{relationship.Id}' SET r += $props";
        await tx.RunAsync(cypher, new
        {
            props = ConvertPropertiesToNeo4j(simpleProps),
        });
    }

    /// <summary>
    /// Gets a relationship by its ID and type.
    /// </summary>
    /// <param name="id">The ID of the relationship</param>
    /// <param name="options">Graph operation options</param>
    /// <param name="tx">The transaction to use</param>
    /// <returns>The relationship instance</returns>
    /// <exception cref="GraphException">Thrown if the relationship is not found</exception>
    public async Task<T> GetRelationship<T>(string id, GraphOperationOptions options, IAsyncTransaction tx)
        where T : class, Cvoya.Graph.Model.IRelationship, new()
    {
        var label = Neo4jTypeManager.GetLabel(typeof(T));
        var cypher = $"MATCH ()-[r:{label}]->() WHERE r.{nameof(Model.IRelationship.Id)} = $id RETURN r";

        var result = await tx.RunAsync(cypher, new { id });
        var records = await result.ToListAsync();

        if (records.Count == 0)
        {
            var ex = new KeyNotFoundException($"Relationship with ID '{id}' not found");
            throw new GraphException(ex.Message, ex);
        }

        var relationship = new T();

        relationship = await EntityConverter.DeserializeRelationship<T>(records[0]["r"].As<global::Neo4j.Driver.IRelationship>());

        return relationship;
    }

    /// <summary>
    /// Gets multiple relationships by their IDs.
    /// </summary>
    /// <param name="ids">The IDs of the relationships</param>
    /// <param name="options">Graph operation options</param>
    /// <param name="tx">The transaction to use</param>
    /// <returns>A collection of relationships</returns>
    public async Task<IEnumerable<T>> GetRelationships<T>(IEnumerable<string> ids, GraphOperationOptions options, IAsyncTransaction tx)
        where T : class, Cvoya.Graph.Model.IRelationship, new()
    {
        if (!ids.Any())
        {
            return [];
        }

        var label = Neo4jTypeManager.GetLabel(typeof(T));

        // Create a parameterized query to get all nodes in one go
        var cypher = $"MATCH ()-[r:{label}]->() WHERE r.{nameof(Model.IRelationship.Id)} IN $ids RETURN r";
        var cursor = await tx.RunAsync(cypher, new { ids });
        var records = await cursor.ToListAsync();

        // Track which IDs were found
        var foundIds = new HashSet<string>();

        var result = new List<T>();

        foreach (var record in records)
        {
            var neo4jRelationship = record.As<global::Neo4j.Driver.IRelationship>();
            var relationship = await EntityConverter.DeserializeRelationship<T>(neo4jRelationship);
            result.Add(relationship);
            foundIds.Add(relationship.Id);
        }

        // Check for missing nodes
        var missingIds = ids.Except(foundIds).ToList();
        if (missingIds.Count != 0)
        {
            throw new KeyNotFoundException($"Node(s) with ID(s) '{string.Join("', '", missingIds)}' not found");
        }

        return result;
    }

    /// <summary>
    /// Deletes a relationship by its ID.
    /// </summary>
    /// <param name="relationshipId">The ID of the relationship to delete</param>
    /// <param name="options">Graph operation options</param>
    /// <param name="tx">The transaction to use</param>
    public static async Task DeleteRelationship(string relationshipId, GraphOperationOptions options, IAsyncTransaction tx)
    {
        var cypher = $"MATCH ()-[r]->() WHERE r.{nameof(Model.IRelationship.Id)} = $relationshipId DELETE r";
        await tx.RunAsync(cypher, new { relationshipId });
    }
}