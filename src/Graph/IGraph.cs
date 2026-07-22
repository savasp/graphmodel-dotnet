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
    /// Creates two new endpoint nodes and the relationship connecting them as one atomic operation.
    /// </summary>
    /// <remarks>
    /// The complete subgraph, including complex-property value nodes, is created in the supplied
    /// transaction or one owned write transaction. Any validation or persistence failure rolls back
    /// the complete operation.
    /// </remarks>
    /// <typeparam name="TSource">The new source node type.</typeparam>
    /// <typeparam name="TRelationship">The new relationship type.</typeparam>
    /// <typeparam name="TTarget">The new target node type.</typeparam>
    /// <param name="source">The new source node.</param>
    /// <param name="relationship">The new relationship.</param>
    /// <param name="target">The new target node.</param>
    /// <param name="direction">The direction in which to create the relationship.</param>
    /// <param name="transaction">The transaction to use, or <see langword="null"/> to use an owned transaction.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    Task CreateAsync<TSource, TRelationship, TTarget>(
        TSource source,
        TRelationship relationship,
        TTarget target,
        RelationshipDirection direction = RelationshipDirection.Outgoing,
        IGraphTransaction? transaction = null,
        CancellationToken cancellationToken = default)
        where TSource : class, INode
        where TRelationship : class, IRelationship
        where TTarget : class, INode;

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
    /// Recreates the index artifacts that the active provider can positively identify as managed
    /// by CVOYA Graph. Indexes whose ownership cannot be proven and indexes owned by database
    /// constraints are preserved.
    /// </summary>
    /// <remarks>
    /// Managed-index ownership is provider-specific. A provider with no managed index artifacts
    /// completes successfully without issuing schema DDL. Successful completion means every
    /// configured managed index is usable. Cancellation or a provider failure may leave a managed
    /// artifact absent or not yet usable until this method is retried, but must never broaden the
    /// operation to an index whose ownership is unproven.
    /// </remarks>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <exception cref="GraphException">Thrown when managed index recreation fails.</exception>
    Task RecreateManagedIndexesAsync(CancellationToken cancellationToken = default);
}
