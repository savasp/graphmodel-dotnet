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

using Neo4j.Driver;

namespace Cvoya.Graph.Model.Neo4j;

/// <summary>
/// Manages Neo4j relationship operations.
/// </summary>
internal class Neo4jRelationshipManager : Neo4jEntityManagerBase
{
    /// <summary>
    /// Initializes a new instance of the Neo4jRelationshipManager class.
    /// </summary>
    public Neo4jRelationshipManager(GraphContext context) : base(context)
    {
    }

    /// <summary>
    /// Creates a relationship between two nodes in Neo4j.
    /// </summary>
    /// <param name="relationship">The relationship to create</param>
    /// <param name="tx">The transaction to use</param>
    public async Task CreateRelationship(Cvoya.Graph.Model.IRelationship relationship, IAsyncTransaction tx)
    {
        var type = relationship.GetType();
        var label = Labels.GetLabelFromType(type);
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
    /// <param name="tx">The transaction to use</param>
    public async Task UpdateRelationship(Cvoya.Graph.Model.IRelationship relationship, IAsyncTransaction tx)
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
    /// <param name="tx">The transaction to use</param>
    /// <returns>The relationship instance</returns>
    /// <exception cref="GraphException">Thrown if the relationship is not found</exception>
    public async Task<T> GetRelationship<T>(string id, IAsyncTransaction tx)
        where T : class, Cvoya.Graph.Model.IRelationship, new()
    {
        // First, find the relationship by ID without type restriction
        var findRelCypher = $"MATCH ()-[r]->() WHERE r.{nameof(Model.IRelationship.Id)} = $id RETURN r, type(r) as relType";
        var findResult = await tx.RunAsync(findRelCypher, new { id });
        var findRecords = await findResult.ToListAsync();

        if (findRecords.Count == 0)
        {
            var ex = new KeyNotFoundException($"Relationship with ID '{id}' not found");
            throw new GraphException(ex.Message, ex);
        }

        var foundRelationship = findRecords[0]["r"].As<global::Neo4j.Driver.IRelationship>();
        var relationshipType = findRecords[0]["relType"].As<string>();

        // Find the actual type that matches the relationship type and is assignable to T
        Type actualType;
        try
        {
            actualType = Labels.GetTypeFromLabel(relationshipType);
        }
        catch (GraphException)
        {
            // If no specific type matches, fallback to the requested type T
            actualType = typeof(T);
        }

        // Deserialize using the actual type and cast to T
        var relationship = await GraphContext.EntityConverter.DeserializeObjectFromNeo4jEntity(actualType, foundRelationship);

        return (T)relationship;
    }

    /// <summary>
    /// Gets multiple relationships by their IDs.
    /// </summary>
    /// <param name="ids">The IDs of the relationships</param>
    /// <param name="tx">The transaction to use</param>
    /// <returns>A collection of relationships</returns>
    public async Task<IEnumerable<T>> GetRelationships<T>(IEnumerable<string> ids, IAsyncTransaction tx)
        where T : class, Cvoya.Graph.Model.IRelationship, new()
    {
        if (!ids.Any())
        {
            return [];
        }

        var label = Labels.GetLabelFromType(typeof(T));

        // Create a parameterized query to get all nodes in one go
        var cypher = $@"
            MATCH ()-[r:{label}]->()
            WHERE r.{nameof(Model.IRelationship.Id)} IN $ids
            RETURN r";
        var cursor = await tx.RunAsync(cypher, new { ids });
        var records = await cursor.ToListAsync();

        // Track which IDs were found
        var foundIds = new HashSet<string>();

        var result = new List<T>();

        foreach (var record in records.Select(r => r["r"]))
        {
            var neo4jRelationship = record.As<global::Neo4j.Driver.IRelationship>();
            var relationship = await GraphContext.EntityConverter.DeserializeRelationship<T>(neo4jRelationship);
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
    /// <param name="cascadeDelete">Whether to cascade delete related entities</param>
    /// <param name="tx">The transaction to use</param>
    public async Task DeleteRelationship(string relationshipId, bool cascadeDelete, IAsyncTransaction tx)
    {
        var cypher = $"MATCH ()-[r]->() WHERE r.{nameof(Model.IRelationship.Id)} = $relationshipId DELETE r";
        await tx.RunAsync(cypher, new { relationshipId });
    }
}