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
    /// Gets a queryable interface to nodes in the graph with options for relationship loading
    /// </summary>
    /// <param name="options">Options controlling relationship traversal when query is executed</param>
    /// <param name="transaction">The transaction to use.
    /// If null, a new transaction will be automatically created and used.</param>
    /// <typeparam name="N">The type of the nodes to query</typeparam>
    /// <returns>A queryable interface to the nodes</returns>
    /// <exception cref="GraphException">Thrown when the query fails</exception>
    /// <exception cref="GraphTransactionException">Thrown when there is an issue with the given or automatically created transaction</exception>
    IQueryable<N> Nodes<N>(GraphOperationOptions options = default, IGraphTransaction? transaction = null) where N : INode, new();

    /// <summary>
    /// Gets a queryable interface to relationships in the graph with options for node loading
    /// </summary>
    /// <param name="options">Options controlling node traversal when query is executed</param>
    /// <param name="transaction">The transaction to use.
    /// If null, a new transaction will be automatically created and used.</param>
    /// <typeparam name="R">The type of the relationships to query</typeparam>
    /// <returns>A queryable interface to the relationships</returns>
    /// <exception cref="GraphException">Thrown when the query fails</exception>
    /// <exception cref="GraphTransactionException">Thrown when there is an issue with the given or automatically created transaction</exception>
    IQueryable<R> Relationships<R>(GraphOperationOptions options = default, IGraphTransaction? transaction = null)
        where R : IRelationship, new();

    /// <summary>
    /// Gets a node by ID with options for relationship loading
    /// </summary>
    /// <param name="id">The ID of the node</param>
    /// <param name="options">Options controlling relationship traversal</param>
    /// <param name="transaction">The transaction to use.
    /// If null, a new transaction will be automatically created and used.</param>
    /// <typeparam name="T">The type of the node</typeparam>
    /// <returns>The node with the specified ID</returns>
    /// <exception cref="KeyNotFoundException">Thrown when the node is not found</exception>
    /// <exception cref="GraphException">Thrown when the node cannot be retrieved or there is another issue</exception>
    /// <exception cref="GraphTransactionException">Thrown when there is an issue with the given or automatically created transaction</exception>
    Task<T> GetNode<T>(string id, GraphOperationOptions options = default, IGraphTransaction? transaction = null)
        where T : INode, new();

    /// <summary>
    /// Gets multiple nodes by their IDs with options for relationship loading
    /// </summary>
    /// <typeparam name="T">The type of the nodes</typeparam>
    /// <param name="ids">The IDs of the nodes</param>
    /// <param name="options">Options controlling relationship traversal</param>
    /// <param name="transaction">The transaction to use.
    /// If null, a new transaction will be automatically created and used.</param>
    /// <returns>A list of nodes with the specified IDs</returns>
    /// <exception cref="KeyNotFoundException">Thrown when any of the nodes are not found</exception>
    /// <exception cref="GraphException">Thrown when the node cannot be retrieved or there is another issue</exception>
    /// <exception cref="GraphTransactionException">Thrown when there is an issue with the given or automatically created transaction</exception>
    Task<IEnumerable<T>> GetNodes<T>(IEnumerable<string> ids, GraphOperationOptions options = default, IGraphTransaction? transaction = null)
        where T : INode, new();

    /// <summary>
    /// Gets a relationship by ID with options for node loading
    /// </summary>
    /// <param name="id">The ID of the relationship</param>
    /// <param name="options">Options controlling node traversal</param>
    /// <param name="transaction">The transaction to use.
    /// If null, a new transaction will be automatically created and used.</param>
    /// <typeparam name="R">The type of the relationship</typeparam>
    /// <returns>The relationship with the specified ID</returns>
    /// <exception cref="KeyNotFoundException">Thrown when any of the relationship is not found</exception>
    /// <exception cref="GraphException">Thrown when the relationship cannot be retrieved or there is another issue</exception>
    /// <exception cref="GraphTransactionException">Thrown when there is an issue with the given or automatically created transaction</exception>
    Task<R> GetRelationship<R>(string id, GraphOperationOptions options = default, IGraphTransaction? transaction = null)
        where R : IRelationship, new();

    /// <summary>
    /// Gets multiple relationships by their IDs with options for node loading
    /// </summary>
    /// <typeparam name="T">The type of the relationships</typeparam>
    /// <param name="ids">The IDs of the relationships</param>
    /// <param name="options">Options controlling node traversal</param>
    /// <param name="transaction">The transaction to use.
    /// If null, a new transaction will be automatically created and used.</param>
    /// <returns>A list of relationships with the specified IDs</returns>
    /// <exception cref="KeyNotFoundException">Thrown when any of the relationships are not found</exception>
    /// <exception cref="GraphException">Thrown when any of the relationships cannot be retrieved or there is another issue</exception>
    /// <exception cref="GraphTransactionException">Thrown when there is an issue with the given or automatically created transaction</exception>
    Task<IEnumerable<T>> GetRelationships<T>(IEnumerable<string> ids, GraphOperationOptions options = default, IGraphTransaction? transaction = null)
        where T : IRelationship, new();

    /// <summary>
    /// Creates a new node in the graph with options for relationship handling
    /// </summary>
    /// <param name="node"> The node to create</param>
    /// <param name="options">Options for the operation</param>
    /// <param name="transaction">The transaction to use.
    /// If null, a new transaction will be automatically created and used.</param>
    /// <typeparam name="T">The type of the node</typeparam>
    /// <exception cref="GraphException">Thrown when the node cannot be created or there is another issue</exception>
    /// <exception cref="GraphTransactionException">Thrown when there is an issue with the given or automatically created transaction</exception>
    Task CreateNode<T>(T node, GraphOperationOptions options = default, IGraphTransaction? transaction = null)
        where T : INode, new();

    /// <summary>
    /// Creates a new relationship in the graph with options for node handling
    /// </summary>
    /// <typeparam name="R">The type of the relationship</typeparam>
    /// <param name="relationship">The relationship to create</param>
    /// <param name="options">Options for the operation</param>
    /// <param name="transaction">The transaction to use.
    /// If null, a new transaction will be automatically created and used.</param>
    /// <exception cref="GraphException">Thrown when the relationship cannot be created or there is another issue</exception>
    /// <exception cref="GraphTransactionException">Thrown when there is an issue with the given or automatically created transaction</exception>
    Task CreateRelationship<R>(R relationship, GraphOperationOptions options = default, IGraphTransaction? transaction = null)
        where R : IRelationship, new();

    /// <summary>
    /// Updates an existing node in the graph with options for relationship handling
    /// </summary>
    /// <typeparam name="T">The type of the node</typeparam>
    /// <param name="node">The node to update</param>
    /// <param name="options">Options for the operation</param>
    /// <param name="transaction">The transaction to use.
    /// If null, a new transaction will be automatically created and used.</param>
    /// <exception cref="GraphException">Thrown when the update cannot be performed or there is another issue</exception>
    /// <exception cref="GraphTransactionException">Thrown when there is an issue with the given or automatically created transaction</exception>
    Task UpdateNode<T>(T node, GraphOperationOptions options = default, IGraphTransaction? transaction = null)
        where T : INode, new();

    /// <summary>
    /// Updates an existing relationship in the graph with options for node handling
    /// </summary>
    /// <typeparam name="R">The type of the relationship</typeparam>
    /// <param name="relationship">The relationship to update</param>
    /// <param name="options">Options for the operation</param>
    /// <param name="transaction">The transaction to use.
    /// If null, a new transaction will be automatically created and used.</param>
    /// <exception cref="GraphException">Thrown when the relationship cannot be updated or there is another issue</exception>
    /// <exception cref="GraphTransactionException">Thrown when there is an issue with the given or automatically created transaction</exception>
    Task UpdateRelationship<R>(R relationship, GraphOperationOptions options = default, IGraphTransaction? transaction = null)
        where R : IRelationship, new();

    /// <summary>
    /// Begins a new transaction
    /// </summary>
    /// <returns>A transaction object</returns>
    /// <exception cref="GraphTransactionException">Thrown when the transaction cannot be created</exception>
    Task<IGraphTransaction> BeginTransaction();

    /// <summary>
    /// Deletes a node from the graph by ID
    /// </summary>
    /// <param name="id">The ID of the node to delete</param>
    /// <param name="transaction">The transaction to use.
    /// If null, a new transaction will be automatically created and used.</param>
    /// <returns>A task representing the asynchronous operation</returns>
    /// <exception cref="GraphException">Thrown when the node cannot be deleted or there is another issue</exception>
    /// <exception cref="GraphTransactionException">Thrown when there is an issue with the given or automatically created transaction</exception>
    Task DeleteNode(string id, IGraphTransaction? transaction = null);

    /// <summary>
    /// Deletes a relationship from the graph by ID
    /// </summary>
    /// <param name="id">The ID of the relationship to delete</param>
    /// <param name="transaction">The transaction to use.
    /// If null, a new transaction will be automatically created and used.</param>
    /// <returns>A task representing the asynchronous operation</returns>
    /// <exception cref="GraphException">Thrown when the relationship cannot be deleted or there is another issue</exception>
    /// <exception cref="GraphTransactionException">Thrown when there is an issue with the given or automatically created transaction</exception>
    Task DeleteRelationship(string id, IGraphTransaction? transaction = null);
}
