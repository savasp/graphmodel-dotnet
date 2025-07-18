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

namespace Cvoya.Graph.Model;

/// <summary>
/// Interface for the Graph client. Provides CRUD operations for nodes and relationships, querying, and transaction management.
/// All methods throw <see cref="GraphException"/> for underlying graph errors.
/// </summary>
public interface IGraph : IAsyncDisposable
{
    /// <summary>
    /// Gets the schema registry for the graph.
    /// This registry is used to manage schema information for nodes and relationships.
    /// </summary>
    SchemaRegistry SchemaRegistry { get; }

    /// <summary>
    /// Gets a new transaction that can be used for multiple operations
    /// </summary>
    /// <returns>An <see cref="IGraphTransaction"/> instance</returns>
    Task<IGraphTransaction> GetTransactionAsync();

    /// <summary>
    /// Gets a queryable interface to dynamic nodes in the graph
    /// </summary>
    /// <param name="transaction">The transaction to use.
    /// If null, a new transaction will be automatically created and used.</param>
    /// <returns>A queryable interface to the dynamic nodes</returns>
    /// <exception cref="GraphException">Thrown when the query fails</exception>
    IGraphNodeQueryable<DynamicNode> DynamicNodes(IGraphTransaction? transaction = null);

    /// <summary>
    /// Gets a queryable interface to dynamic relationships in the graph
    /// </summary>
    /// <param name="transaction">The transaction to use.
    /// If null, a new transaction will be automatically created and used.</param>
    /// <returns>A queryable interface to the dynamic relationships</returns>
    /// <exception cref="GraphException">Thrown when the query fails</exception>
    IGraphRelationshipQueryable<DynamicRelationship> DynamicRelationships(IGraphTransaction? transaction = null);

    /// <summary>
    /// Gets a dynamic node by ID
    /// </summary>
    /// <param name="id">The ID of the dynamic node</param>
    /// <param name="transaction">The transaction to use.
    /// If null, a new transaction will be automatically created and used.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>The dynamic node with the specified ID</returns>
    /// <exception cref="KeyNotFoundException">Thrown when the dynamic node is not found</exception>
    /// <exception cref="GraphException">Thrown when the dynamic node cannot be retrieved or there is another issue</exception>
    Task<DynamicNode> GetDynamicNodeAsync(string id, IGraphTransaction? transaction = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a dynamic relationship by ID
    /// </summary>
    /// <param name="id">The ID of the dynamic relationship</param>
    /// <param name="transaction">The transaction to use.
    /// If null, a new transaction will be automatically created and used.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>The dynamic relationship with the specified ID</returns>
    /// <exception cref="KeyNotFoundException">Thrown when the dynamic relationship is not found</exception>
    /// <exception cref="GraphException">Thrown when the dynamic relationship cannot be retrieved or there is another issue</exception>
    Task<DynamicRelationship> GetDynamicRelationshipAsync(string id, IGraphTransaction? transaction = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a queryable interface to nodes in the graph with options for relationship loading
    /// </summary>
    /// <param name="transaction">The transaction to use.
    /// If null, a new transaction will be automatically created and used.</param>
    /// <typeparam name="N">The type of the nodes to query</typeparam>
    /// <returns>A queryable interface to the nodes</returns>
    /// <exception cref="GraphException">Thrown when the query fails</exception>
    IGraphNodeQueryable<N> Nodes<N>(IGraphTransaction? transaction = null)
        where N : INode;

    /// <summary>
    /// Gets a queryable interface to relationships in the graph with options for node loading
    /// </summary>
    /// <param name="transaction">The transaction to use.
    /// If null, a new transaction will be automatically created and used.</param>
    /// <typeparam name="R">The type of the relationships to query</typeparam>
    /// <returns>A queryable interface to the relationships</returns>
    /// <exception cref="GraphException">Thrown when the query fails</exception>
    IGraphRelationshipQueryable<R> Relationships<R>(IGraphTransaction? transaction = null)
        where R : IRelationship;

    /// <summary>
    /// Gets a node by ID with options for relationship loading
    /// </summary>
    /// <param name="id">The ID of the node</param>
    /// <param name="transaction">The transaction to use.
    /// If null, a new transaction will be automatically created and used.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <typeparam name="N">The type of the node</typeparam>
    /// <returns>The node with the specified ID</returns>
    /// <exception cref="KeyNotFoundException">Thrown when the node is not found</exception>
    /// <exception cref="GraphException">Thrown when the node cannot be retrieved or there is another issue</exception>
    Task<N> GetNodeAsync<N>(string id, IGraphTransaction? transaction = null, CancellationToken cancellationToken = default)
        where N : INode;

    /// <summary>
    /// Gets a relationship by ID with options for node loading
    /// </summary>
    /// <param name="id">The ID of the relationship</param>
    /// <param name="transaction">The transaction to use.
    /// If null, a new transaction will be automatically created and used.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <typeparam name="R">The type of the relationship</typeparam>
    /// <returns>The relationship with the specified ID</returns>
    /// <exception cref="KeyNotFoundException">Thrown when any of the relationship is not found</exception>
    /// <exception cref="GraphException">Thrown when the relationship cannot be retrieved or there is another issue</exception>
    Task<R> GetRelationshipAsync<R>(string id, IGraphTransaction? transaction = null, CancellationToken cancellationToken = default)
        where R : IRelationship;

    /// <summary>
    /// Creates a new node in the graph with options for relationship handling
    /// </summary>
    /// <param name="node"> The node to create</param>
    /// <param name="transaction">The transaction to use.
    /// If null, a new transaction will be automatically created and used.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <typeparam name="N">The type of the node</typeparam>
    /// <exception cref="GraphException">Thrown when the node cannot be created or there is another issue</exception>
    Task CreateNodeAsync<N>(N node, IGraphTransaction? transaction = null, CancellationToken cancellationToken = default)
        where N : INode;

    /// <summary>
    /// Creates a new relationship in the graph with options for node handling
    /// </summary>
    /// <typeparam name="R">The type of the relationship</typeparam>
    /// <param name="relationship">The relationship to create</param>
    /// <param name="transaction">The transaction to use.
    /// If null, a new transaction will be automatically created and used.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <exception cref="GraphException">Thrown when the relationship cannot be created or there is another issue</exception>
    Task CreateRelationshipAsync<R>(R relationship, IGraphTransaction? transaction = null, CancellationToken cancellationToken = default)
        where R : IRelationship;

    /// <summary>
    /// Updates an existing node in the graph with options for relationship handling
    /// </summary>
    /// <typeparam name="N">The type of the node</typeparam>
    /// <param name="node">The node to update</param>
    /// <param name="transaction">The transaction to use.
    /// If null, a new transaction will be automatically created and used.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <exception cref="GraphException">Thrown when the update cannot be performed or there is another issue</exception>
    Task UpdateNodeAsync<N>(N node, IGraphTransaction? transaction = null, CancellationToken cancellationToken = default)
        where N : INode;

    /// <summary>
    /// Updates an existing relationship in the graph with options for node handling
    /// </summary>
    /// <typeparam name="R">The type of the relationship</typeparam>
    /// <param name="relationship">The relationship to update</param>
    /// <param name="transaction">The transaction to use.
    /// If null, a new transaction will be automatically created and used.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <exception cref="GraphException">Thrown when the relationship cannot be updated or there is another issue</exception>
    Task UpdateRelationshipAsync<R>(R relationship, IGraphTransaction? transaction = null, CancellationToken cancellationToken = default)
        where R : IRelationship;

    /// <summary>
    /// Deletes a node from the graph by ID
    /// </summary>
    /// <param name="id">The ID of the node to delete</param>
    /// <param name="cascadeDelete">Whether to cascade delete related nodes and relationships. The default is false.</param>
    /// <param name="transaction">The transaction to use.
    /// If null, a new transaction will be automatically created and used.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation</returns>
    /// <exception cref="GraphException">Thrown when the node cannot be deleted or there is another issue</exception>
    Task DeleteNodeAsync(string id, bool cascadeDelete = false, IGraphTransaction? transaction = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a relationship from the graph by ID
    /// </summary>
    /// <param name="id">The ID of the relationship to delete</param>
    /// <param name="transaction">The transaction to use.
    /// If null, a new transaction will be automatically created and used.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation</returns>
    /// <exception cref="GraphException">Thrown when the relationship cannot be deleted or there is another issue</exception>
    Task DeleteRelationshipAsync(string id, IGraphTransaction? transaction = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Performs a full text search across all entities (nodes and relationships) in the graph
    /// </summary>
    /// <param name="query">The search query string</param>
    /// <param name="transaction">The transaction to use.
    /// If null, a new transaction will be automatically created and used.</param>
    /// <returns>A queryable interface to the search results</returns>
    /// <exception cref="GraphException">Thrown when the search fails</exception>
    IGraphQueryable<IEntity> Search(string query, IGraphTransaction? transaction = null);

    /// <summary>
    /// Performs a full text search across all nodes in the graph
    /// </summary>
    /// <param name="query">The search query string</param>
    /// <param name="transaction">The transaction to use.
    /// If null, a new transaction will be automatically created and used.</param>
    /// <returns>A queryable interface to the node search results</returns>
    /// <exception cref="GraphException">Thrown when the search fails</exception>
    IGraphNodeQueryable<INode> SearchNodes(string query, IGraphTransaction? transaction = null);

    /// <summary>
    /// Performs a full text search across all relationships in the graph
    /// </summary>
    /// <param name="query">The search query string</param>
    /// <param name="transaction">The transaction to use.
    /// If null, a new transaction will be automatically created and used.</param>
    /// <returns>A queryable interface to the relationship search results</returns>
    /// <exception cref="GraphException">Thrown when the search fails</exception>
    IGraphRelationshipQueryable<IRelationship> SearchRelationships(string query, IGraphTransaction? transaction = null);

    /// <summary>
    /// Performs a full text search across nodes of a specific type in the graph
    /// </summary>
    /// <typeparam name="T">The type of the nodes to search</typeparam>
    /// <param name="query">The search query string</param>
    /// <param name="transaction">The transaction to use.
    /// If null, a new transaction will be automatically created and used.</param>
    /// <returns>A queryable interface to the typed node search results</returns>
    /// <exception cref="GraphException">Thrown when the search fails</exception>
    IGraphNodeQueryable<T> SearchNodes<T>(string query, IGraphTransaction? transaction = null) where T : INode;

    /// <summary>
    /// Performs a full text search across relationships of a specific type in the graph
    /// </summary>
    /// <typeparam name="T">The type of the relationships to search</typeparam>
    /// <param name="query">The search query string</param>
    /// <param name="transaction">The transaction to use.
    /// If null, a new transaction will be automatically created and used.</param>
    /// <returns>A queryable interface to the typed relationship search results</returns>
    /// <exception cref="GraphException">Thrown when the search fails</exception>
    IGraphRelationshipQueryable<T> SearchRelationships<T>(string query, IGraphTransaction? transaction = null) where T : IRelationship;

    /// <summary>
    /// Recreates all indexes in the graph database.
    /// This method will drop existing indexes and recreate them based on the current schema.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <exception cref="GraphException">Thrown when index recreation fails.</exception>
    Task RecreateIndexesAsync(CancellationToken cancellationToken = default);
}
