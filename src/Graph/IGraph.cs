// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph;

/// <summary>
/// Interface for the Graph client. Provides CRUD operations for nodes and relationships, querying, and transaction management.
/// All methods throw <see cref="GraphException"/> for underlying graph errors.
/// </summary>
/// <remarks>
/// Graph instances do not own provider resources. Dispose the provider store that created the
/// graph (for example, <c>Neo4jGraphStore</c>) to release provider-owned resources.
/// </remarks>
public interface IGraph
{
    /// <summary>
    /// Gets the schema registry for the graph.
    /// This registry is used to manage schema information for nodes and relationships.
    /// </summary>
    SchemaRegistry SchemaRegistry { get; }

    /// <summary>
    /// Gets a new transaction that can be used for multiple operations
    /// </summary>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>An <see cref="IGraphTransaction"/> instance</returns>
    Task<IGraphTransaction> GetTransactionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a queryable interface to dynamic nodes in the graph. Building the queryable performs
    /// no I/O; any transaction/session acquisition happens when the query is executed (e.g. via
    /// <c>ToListAsync</c> or <c>await foreach</c>).
    /// </summary>
    /// <param name="transaction">The transaction to use.
    /// If null, a new transaction will be automatically created and used at execution time.</param>
    /// <returns>A queryable interface to the dynamic nodes</returns>
    IGraphQueryable<DynamicNode> DynamicNodes(IGraphTransaction? transaction = null);

    /// <summary>
    /// Gets a queryable interface to dynamic relationships in the graph. Building the queryable
    /// performs no I/O; any transaction/session acquisition happens when the query is executed.
    /// </summary>
    /// <param name="transaction">The transaction to use.
    /// If null, a new transaction will be automatically created and used at execution time.</param>
    /// <returns>A queryable interface to the dynamic relationships</returns>
    IGraphQueryable<DynamicRelationship> DynamicRelationships(IGraphTransaction? transaction = null);

    /// <summary>
    /// Gets a dynamic node by ID
    /// </summary>
    /// <param name="id">The ID of the dynamic node</param>
    /// <param name="transaction">The transaction to use.
    /// If null, a new transaction will be automatically created and used.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>The dynamic node with the specified ID</returns>
    /// <exception cref="EntityNotFoundException">Thrown when the dynamic node is not found</exception>
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
    /// <exception cref="EntityNotFoundException">Thrown when the dynamic relationship is not found</exception>
    /// <exception cref="GraphException">Thrown when the dynamic relationship cannot be retrieved or there is another issue</exception>
    Task<DynamicRelationship> GetDynamicRelationshipAsync(string id, IGraphTransaction? transaction = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a queryable interface to nodes in the graph. Building the queryable performs no I/O;
    /// any transaction/session acquisition happens when the query is executed (e.g. via
    /// <c>ToListAsync</c> or <c>await foreach</c>).
    /// </summary>
    /// <param name="transaction">The transaction to use.
    /// If null, a new transaction will be automatically created and used at execution time.</param>
    /// <typeparam name="TNode">The type of the nodes to query</typeparam>
    /// <returns>A queryable interface to the nodes</returns>
    IGraphQueryable<TNode> Nodes<TNode>(IGraphTransaction? transaction = null)
        where TNode : class, INode;

    /// <summary>
    /// Gets a queryable interface to relationships in the graph. Building the queryable performs
    /// no I/O; any transaction/session acquisition happens when the query is executed.
    /// </summary>
    /// <param name="transaction">The transaction to use.
    /// If null, a new transaction will be automatically created and used at execution time.</param>
    /// <typeparam name="TRelationship">The type of the relationships to query</typeparam>
    /// <returns>A queryable interface to the relationships</returns>
    IGraphQueryable<TRelationship> Relationships<TRelationship>(IGraphTransaction? transaction = null)
        where TRelationship : class, IRelationship;

    /// <summary>
    /// Gets a node by ID with options for relationship loading
    /// </summary>
    /// <param name="id">The ID of the node</param>
    /// <param name="transaction">The transaction to use.
    /// If null, a new transaction will be automatically created and used.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <typeparam name="TNode">The type of the node</typeparam>
    /// <returns>The node with the specified ID</returns>
    /// <exception cref="EntityNotFoundException">Thrown when the node is not found</exception>
    /// <exception cref="GraphException">Thrown when the node cannot be retrieved or there is another issue</exception>
    Task<TNode> GetNodeAsync<TNode>(string id, IGraphTransaction? transaction = null, CancellationToken cancellationToken = default)
        where TNode : class, INode;

    /// <summary>
    /// Gets a relationship by ID with options for node loading
    /// </summary>
    /// <param name="id">The ID of the relationship</param>
    /// <param name="transaction">The transaction to use.
    /// If null, a new transaction will be automatically created and used.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <typeparam name="TRelationship">The type of the relationship</typeparam>
    /// <returns>The relationship with the specified ID</returns>
    /// <exception cref="EntityNotFoundException">Thrown when the relationship is not found</exception>
    /// <exception cref="GraphException">Thrown when the relationship cannot be retrieved or there is another issue</exception>
    Task<TRelationship> GetRelationshipAsync<TRelationship>(string id, IGraphTransaction? transaction = null, CancellationToken cancellationToken = default)
        where TRelationship : class, IRelationship;

    /// <summary>
    /// Creates a new node in the graph with options for relationship handling
    /// </summary>
    /// <param name="node"> The node to create</param>
    /// <param name="transaction">The transaction to use.
    /// If null, a new transaction will be automatically created and used.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <typeparam name="TNode">The type of the node</typeparam>
    /// <exception cref="GraphException">Thrown when the node cannot be created or there is another issue</exception>
    Task CreateNodeAsync<TNode>(TNode node, IGraphTransaction? transaction = null, CancellationToken cancellationToken = default)
        where TNode : class, INode;

    /// <summary>
    /// Creates a new relationship in the graph with options for node handling
    /// </summary>
    /// <typeparam name="TRelationship">The type of the relationship</typeparam>
    /// <param name="relationship">The relationship to create</param>
    /// <param name="transaction">The transaction to use.
    /// If null, a new transaction will be automatically created and used.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <exception cref="GraphException">Thrown when the relationship cannot be created or there is another issue</exception>
    Task CreateRelationshipAsync<TRelationship>(TRelationship relationship, IGraphTransaction? transaction = null, CancellationToken cancellationToken = default)
        where TRelationship : class, IRelationship;

    /// <summary>
    /// Creates a node–relationship–node subgraph — both endpoint nodes and the relationship that
    /// connects them — as a single atomic operation.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The whole subgraph (both endpoint nodes, all of their complex-property value-node subtrees,
    /// and the edge) is created as one transactional unit: if any part fails, nothing is created.
    /// The transitional relationship model's <see cref="Relationship.Direction"/> is honored for the stored edge.
    /// </para>
    /// <para>
    /// <paramref name="relationship"/>'s <see cref="IRelationship.StartNodeId"/> and
    /// <see cref="IRelationship.EndNodeId"/> must equal <paramref name="source"/>'s and
    /// <paramref name="target"/>'s <see cref="IEntity.Id"/> respectively; otherwise an
    /// <see cref="ArgumentException"/> is thrown.
    /// </para>
    /// <para>
    /// By default both endpoint nodes are created and the operation fails atomically if an endpoint
    /// id already exists. Set <see cref="GraphOperationOptions.CreateMissingEndpoints"/> to
    /// <see langword="true"/> to instead merge each endpoint by id — an existing node is reused
    /// entirely as-is (both its simple properties and its existing complex-property subtrees are left
    /// untouched, and the passed-in endpoint object's properties are ignored for it), while a missing
    /// endpoint is created with its full properties and complex-property subtree. The edge is always
    /// created.
    /// </para>
    /// </remarks>
    /// <typeparam name="TSource">The type of the source (start) node</typeparam>
    /// <typeparam name="TRelationship">The type of the relationship</typeparam>
    /// <typeparam name="TTarget">The type of the target (end) node</typeparam>
    /// <param name="source">The source (start) node</param>
    /// <param name="relationship">The relationship connecting the two nodes</param>
    /// <param name="target">The target (end) node</param>
    /// <param name="options">Options controlling how the endpoint nodes are handled.
    /// If null, the default (create both endpoints) is used.</param>
    /// <param name="transaction">The transaction to use.
    /// If null, a new transaction will be automatically created and used.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <exception cref="ArgumentException">Thrown when an argument is null, or when the relationship
    /// endpoints do not match the source and target node ids.</exception>
    /// <exception cref="GraphException">Thrown when the subgraph cannot be created or there is another issue</exception>
    Task CreateAsync<TSource, TRelationship, TTarget>(
        TSource source,
        TRelationship relationship,
        TTarget target,
        GraphOperationOptions? options = null,
        IGraphTransaction? transaction = null,
        CancellationToken cancellationToken = default)
        where TSource : class, INode
        where TRelationship : class, IRelationship
        where TTarget : class, INode;

    /// <summary>
    /// Updates an existing node in the graph with options for relationship handling
    /// </summary>
    /// <typeparam name="TNode">The type of the node</typeparam>
    /// <param name="node">The node to update</param>
    /// <param name="transaction">The transaction to use.
    /// If null, a new transaction will be automatically created and used.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <exception cref="EntityNotFoundException">Thrown when the node is not found.</exception>
    /// <exception cref="GraphException">Thrown when the update cannot be performed or there is another issue</exception>
    Task UpdateNodeAsync<TNode>(TNode node, IGraphTransaction? transaction = null, CancellationToken cancellationToken = default)
        where TNode : class, INode;

    /// <summary>
    /// Updates an existing relationship in the graph with options for node handling
    /// </summary>
    /// <typeparam name="TRelationship">The type of the relationship</typeparam>
    /// <param name="relationship">The relationship to update</param>
    /// <param name="transaction">The transaction to use.
    /// If null, a new transaction will be automatically created and used.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <exception cref="EntityNotFoundException">Thrown when the relationship is not found.</exception>
    /// <exception cref="GraphException">
    /// Thrown when the relationship cannot be updated, including when its persisted relationship
    /// type, concrete CLR type, or direction differs from the incoming relationship.
    /// </exception>
    Task UpdateRelationshipAsync<TRelationship>(TRelationship relationship, IGraphTransaction? transaction = null, CancellationToken cancellationToken = default)
        where TRelationship : class, IRelationship;

    /// <summary>
    /// Deletes a node from the graph by ID.
    /// </summary>
    /// <param name="id">The ID of the node to delete</param>
    /// <param name="cascadeDelete">
    /// Whether to delete the node when it has user-defined relationships.
    /// When false, deleting a node with user-defined relationships throws a <see cref="GraphException"/>.
    /// When true, those relationships are deleted with the node, but the related user nodes are left intact.
    /// Complex-property nodes owned by the deleted node are always deleted.
    /// </param>
    /// <param name="transaction">The transaction to use.
    /// If null, a new transaction will be automatically created and used.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation</returns>
    /// <exception cref="EntityNotFoundException">Thrown when the node is not found.</exception>
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
    /// <exception cref="EntityNotFoundException">Thrown when the relationship is not found.</exception>
    /// <exception cref="GraphException">Thrown when the relationship cannot be deleted or there is another issue</exception>
    Task DeleteRelationshipAsync(string id, IGraphTransaction? transaction = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a queryable interface that performs a full text search across all entities (nodes and
    /// relationships) in the graph. This is a thin convenience over the <c>Search</c> LINQ
    /// operator; building the queryable performs no I/O.
    /// </summary>
    /// <param name="query">The search query string</param>
    /// <param name="transaction">The transaction to use.
    /// If null, a new transaction will be automatically created and used at execution time.</param>
    /// <returns>A queryable interface to the search results</returns>
    IGraphQueryable<IEntity> Search(string query, IGraphTransaction? transaction = null);

    /// <summary>
    /// Gets a queryable interface that performs a full text search across all nodes in the graph.
    /// This is a thin convenience over the <c>Search</c> LINQ operator, equivalent to
    /// <c>graph.Nodes&lt;INode&gt;(transaction).Search(query)</c>; building the queryable
    /// performs no I/O.
    /// </summary>
    /// <param name="query">The search query string</param>
    /// <param name="transaction">The transaction to use.
    /// If null, a new transaction will be automatically created and used at execution time.</param>
    /// <returns>A queryable interface to the node search results</returns>
    IGraphQueryable<INode> SearchNodes(string query, IGraphTransaction? transaction = null);

    /// <summary>
    /// Gets a queryable interface that performs a full text search across all relationships in
    /// the graph. This is a thin convenience over the <c>Search</c> LINQ operator, equivalent to
    /// <c>graph.Relationships&lt;IRelationship&gt;(transaction).Search(query)</c>; building the
    /// queryable performs no I/O.
    /// </summary>
    /// <param name="query">The search query string</param>
    /// <param name="transaction">The transaction to use.
    /// If null, a new transaction will be automatically created and used at execution time.</param>
    /// <returns>A queryable interface to the relationship search results</returns>
    IGraphQueryable<IRelationship> SearchRelationships(string query, IGraphTransaction? transaction = null);

    /// <summary>
    /// Gets a queryable interface that performs a full text search across nodes of the specified
    /// type in the graph. This is a thin convenience over the <c>Search</c> LINQ operator,
    /// equivalent to <c>graph.Nodes&lt;T&gt;(transaction).Search(query)</c>; building the
    /// queryable performs no I/O.
    /// </summary>
    /// <typeparam name="T">The type of the nodes to search</typeparam>
    /// <param name="query">The search query string</param>
    /// <param name="transaction">The transaction to use.
    /// If null, a new transaction will be automatically created and used at execution time.</param>
    /// <returns>A queryable interface to the typed node search results</returns>
    IGraphQueryable<T> SearchNodes<T>(string query, IGraphTransaction? transaction = null) where T : class, INode;

    /// <summary>
    /// Gets a queryable interface that performs a full text search across relationships of the
    /// specified type in the graph. This is a thin convenience over the <c>Search</c> LINQ
    /// operator, equivalent to <c>graph.Relationships&lt;T&gt;(transaction).Search(query)</c>;
    /// building the queryable performs no I/O.
    /// </summary>
    /// <typeparam name="T">The type of the relationships to search</typeparam>
    /// <param name="query">The search query string</param>
    /// <param name="transaction">The transaction to use.
    /// If null, a new transaction will be automatically created and used at execution time.</param>
    /// <returns>A queryable interface to the typed relationship search results</returns>
    IGraphQueryable<T> SearchRelationships<T>(string query, IGraphTransaction? transaction = null) where T : class, IRelationship;

    /// <summary>
    /// Recreates all indexes in the graph database.
    /// This method will drop existing indexes and recreate them based on the current schema.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <exception cref="GraphException">Thrown when index recreation fails.</exception>
    Task RecreateIndexesAsync(CancellationToken cancellationToken = default);
}
