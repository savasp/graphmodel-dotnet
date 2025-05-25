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
using Cvoya.Graph.Provider.Neo4j.Entities;
using Cvoya.Graph.Provider.Neo4j.Linq;
using Cvoya.Graph.Provider.Neo4j.Query;
using Cvoya.Graph.Provider.Neo4j.Schema;
using Microsoft.Extensions.Logging;
using Neo4j.Driver;

namespace Cvoya.Graph.Provider.Neo4j;

/// <summary>
/// Neo4j implementation of the IGraph interface.
/// </summary>
public class Neo4jGraphProviderModular : IGraph
{
    private readonly Microsoft.Extensions.Logging.ILogger? _logger;
    private readonly IDriver _driver;
    private readonly string _databaseName;

    // Component services
    private readonly Neo4jQueryExecutor _queryExecutor;
    private readonly Neo4jConstraintManager _constraintManager;
    private readonly Neo4jEntityConverter _entityConverter;
    private readonly Neo4jNodeManager _nodeManager;
    private readonly Neo4jRelationshipManager _relationshipManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="Neo4jGraphProviderModular"/> class.
    /// </summary>
    /// <param name="uri">The URI of the Neo4j database.</param>
    /// <param name="username">The username for authentication.</param>
    /// <param name="password">The password for authentication.</param>
    /// <param name="databaseName">The name of the database.</param>
    /// <param name="logger">The logger instance.</param>
    /// <remarks>
    /// The default value for <see cref="databaseName"/> is "neo4j".
    /// </remarks>
    public Neo4jGraphProviderModular(
        string? uri = null,
        string? username = null,
        string? password = null,
        string? databaseName = null,
        Microsoft.Extensions.Logging.ILogger? logger = null)
    {
        _logger = logger;
        uri ??= Environment.GetEnvironmentVariable("NEO4J_URI") ?? "bolt://localhost:7687";
        username ??= Environment.GetEnvironmentVariable("NEO4J_USER") ?? "neo4j";
        password ??= Environment.GetEnvironmentVariable("NEO4J_PASSWORD") ?? "password";
        _databaseName = databaseName ?? Environment.GetEnvironmentVariable("NEO4J_DATABASE") ?? "neo4j";

        _driver = username is null
            ? GraphDatabase.Driver(uri)
            : GraphDatabase.Driver(uri, AuthTokens.Basic(username, password));

        // Initialize component services
        _entityConverter = new Neo4jEntityConverter(logger);
        _queryExecutor = new Neo4jQueryExecutor(_driver, _databaseName, logger);
        _constraintManager = new Neo4jConstraintManager(_driver, _databaseName, logger);
        _nodeManager = new Neo4jNodeManager(_queryExecutor, _constraintManager, _entityConverter, logger);
        _relationshipManager = new Neo4jRelationshipManager(_queryExecutor, _constraintManager, _entityConverter, logger);
    }

    /// <summary>
    /// Begins a new transaction.
    /// </summary>
    /// <returns>A new graph transaction</returns>
    public async Task<IGraphTransaction> BeginTransaction()
    {
        var session = _driver.AsyncSession(builder => builder.WithDatabase(_databaseName));
        var transaction = await session.BeginTransactionAsync();
        return new Neo4jGraphTransaction(session, transaction);
    }

    /// <inheritdoc />
    public async Task<string> CreateNode(Cvoya.Graph.Model.INode node, GraphOperationOptions options = default, IGraphTransaction? transaction = null)
    {
        // Validate the node
        if (node == null) throw new ArgumentNullException(nameof(node));
        node.EnsureNoReferenceCycle();

        if (string.IsNullOrEmpty(node.Id))
        {
            node.Id = Guid.NewGuid().ToString(); // Generate new ID
        }
        else if (!Guid.TryParse(node.Id, out _))
        {
            throw new ArgumentException($"Node ID '{node.Id}' is not a valid GUID");
        }

        // Execute the operation
        var (session, tx) = await _queryExecutor.GetOrCreateTransaction(transaction);
        try
        {
            // Create the node
            var nodeId = await _nodeManager.CreateNode(parentId: null, node: node, tx: tx);

            // Commit if no external transaction
            if (transaction == null)
            {
                await tx.CommitAsync();
            }

            return nodeId;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to create node");
            if (transaction == null)
            {
                await tx.RollbackAsync();
            }
            throw new GraphException("Failed to create node", ex);
        }
        finally
        {
            if (transaction == null)
            {
                await session.CloseAsync();
            }
        }
    }

    /// <inheritdoc />
    public async Task UpdateNode(Cvoya.Graph.Model.INode node, GraphOperationOptions options = default, IGraphTransaction? transaction = null)
    {
        // Validate the node
        if (node == null) throw new ArgumentNullException(nameof(node));
        node.EnsureNoReferenceCycle();

        if (string.IsNullOrEmpty(node.Id))
            throw new ArgumentException("Cannot update a node with no ID");

        // Execute the operation
        var (session, tx) = await _queryExecutor.GetOrCreateTransaction(transaction);
        try
        {
            await _nodeManager.UpdateNode(node, tx);

            // Commit if no external transaction
            if (transaction == null)
            {
                await tx.CommitAsync();
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to update node");
            if (transaction == null)
            {
                await tx.RollbackAsync();
            }
            throw new GraphException("Failed to update node", ex);
        }
        finally
        {
            if (transaction == null)
            {
                await session.CloseAsync();
            }
        }
    }

    /// <inheritdoc />
    public async Task DeleteNode(string nodeId, GraphOperationOptions options = default, IGraphTransaction? transaction = null)
    {
        if (string.IsNullOrEmpty(nodeId))
            throw new ArgumentException("Node ID cannot be null or empty");

        var (session, tx) = await _queryExecutor.GetOrCreateTransaction(transaction);
        try
        {
            var detachDelete = options.CascadeDelete;
            var cypher = detachDelete
                ? $"MATCH (n) WHERE n.{nameof(Model.INode.Id)} = $nodeId DETACH DELETE n"
                : $"MATCH (n) WHERE n.{nameof(Model.INode.Id)} = $nodeId DELETE n";

            await tx.RunAsync(cypher, new { nodeId });

            if (transaction == null)
            {
                await tx.CommitAsync();
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to delete node");
            if (transaction == null)
            {
                await tx.RollbackAsync();
            }
            throw new GraphException("Failed to delete node", ex);
        }
        finally
        {
            if (transaction == null)
            {
                await session.CloseAsync();
            }
        }
    }

    /// <inheritdoc />
    public async Task CreateRelationship(Cvoya.Graph.Model.IRelationship relationship, GraphOperationOptions options = default, IGraphTransaction? transaction = null)
    {
        if (relationship == null) throw new ArgumentNullException(nameof(relationship));
        relationship.EnsureNoReferenceCycle();

        if (string.IsNullOrEmpty(relationship.SourceId) || string.IsNullOrEmpty(relationship.TargetId))
            throw new ArgumentException("Relationship source and target IDs cannot be null or empty");

        if (string.IsNullOrEmpty(relationship.Id))
            relationship.Id = Guid.NewGuid().ToString();

        var (session, tx) = await _queryExecutor.GetOrCreateTransaction(transaction);
        try
        {
            await _relationshipManager.CreateRelationship(relationship, tx);

            if (transaction == null)
            {
                await tx.CommitAsync();
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to create relationship");
            if (transaction == null)
            {
                await tx.RollbackAsync();
            }
            throw new GraphException("Failed to create relationship", ex);
        }
        finally
        {
            if (transaction == null)
            {
                await session.CloseAsync();
            }
        }
    }

    /// <inheritdoc />
    public async Task UpdateRelationship(Cvoya.Graph.Model.IRelationship relationship, GraphOperationOptions options = default, IGraphTransaction? transaction = null)
    {
        if (relationship == null) throw new ArgumentNullException(nameof(relationship));
        relationship.EnsureNoReferenceCycle();

        if (string.IsNullOrEmpty(relationship.Id))
            throw new ArgumentException("Cannot update a relationship with no ID");

        var (session, tx) = await _queryExecutor.GetOrCreateTransaction(transaction);
        try
        {
            await _relationshipManager.UpdateRelationship(relationship, tx);

            if (transaction == null)
            {
                await tx.CommitAsync();
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to update relationship");
            if (transaction == null)
            {
                await tx.RollbackAsync();
            }
            throw new GraphException("Failed to update relationship", ex);
        }
        finally
        {
            if (transaction == null)
            {
                await session.CloseAsync();
            }
        }
    }

    /// <inheritdoc />
    public async Task DeleteRelationship(string relationshipId, GraphOperationOptions options = default, IGraphTransaction? transaction = null)
    {
        if (string.IsNullOrEmpty(relationshipId))
            throw new ArgumentException("Relationship ID cannot be null or empty");

        var (session, tx) = await _queryExecutor.GetOrCreateTransaction(transaction);
        try
        {
            var cypher = $"MATCH ()-[r]->() WHERE r.{nameof(Model.IRelationship.Id)} = $relationshipId DELETE r";
            await tx.RunAsync(cypher, new { relationshipId });

            if (transaction == null)
            {
                await tx.CommitAsync();
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to delete relationship");
            if (transaction == null)
            {
                await tx.RollbackAsync();
            }
            throw new GraphException("Failed to delete relationship", ex);
        }
        finally
        {
            if (transaction == null)
            {
                await session.CloseAsync();
            }
        }
    }

    /// <inheritdoc />
    public async Task<T> GetNode<T>(string id, GraphOperationOptions options = default, IGraphTransaction? transaction = null) 
        where T : Cvoya.Graph.Model.INode, new()
    {
        if (string.IsNullOrEmpty(id))
            throw new ArgumentException("Node ID cannot be null or empty");

        var (session, tx) = await _queryExecutor.GetOrCreateTransaction(transaction);
        try
        {
            var node = await _nodeManager.GetNode(typeof(T), id, tx) as T 
                ?? throw new GraphException($"Failed to cast node of ID '{id}' to type {typeof(T).Name}");
            
            if (options.TraversalDepth > 0)
            {
                await LoadNodeRelationships(node, options, tx, currentDepth: 0);
            }
            
            if (transaction == null)
            {
                await tx.CommitAsync();
            }
            
            return node;
        }
        catch (Exception ex) when (ex is not GraphException && ex is not KeyNotFoundException)
        {
            _logger?.LogError(ex, $"Failed to get node with ID '{id}'");
            if (transaction == null)
            {
                await tx.RollbackAsync();
            }
            throw new GraphException($"Failed to get node with ID '{id}'", ex);
        }
        finally
        {
            if (transaction == null)
            {
                await session.CloseAsync();
            }
        }
    }

    /// <inheritdoc />
    public async Task<IEnumerable<T>> GetNodes<T>(
        IEnumerable<string> ids, 
        GraphOperationOptions options = default, 
        IGraphTransaction? transaction = null) 
        where T : Cvoya.Graph.Model.INode, new()
    {
        if (ids == null)
            throw new ArgumentNullException(nameof(ids));
        
        var idList = ids.ToList();
        if (idList.Count == 0)
            return Enumerable.Empty<T>();
            
        var (session, tx) = await _queryExecutor.GetOrCreateTransaction(transaction);
        try
        {
            var result = new List<T>();
            var nodeType = typeof(T);
            var label = Neo4jTypeManager.GetLabel(nodeType);
            
            // Create a parameterized query to get all nodes in one go
            var parameters = new Dictionary<string, object>
            {
                ["ids"] = idList
            };
            
            var cypher = $"MATCH (n:{label}) WHERE n.{nameof(Model.INode.Id)} IN $ids RETURN n";
            var cursor = await tx.RunAsync(cypher, parameters);
            var records = await cursor.ToListAsync();
            
            // Track which IDs were found
            var foundIds = new HashSet<string>();
            
            foreach (var record in records)
            {
                var neo4jNode = record["n"].As<global::Neo4j.Driver.INode>();
                var idProperty = neo4jNode.Properties[nameof(Model.INode.Id)].As<string>();
                foundIds.Add(idProperty);
                
                var node = new T();
                _entityConverter.PopulateNodeEntity(node, neo4jNode);
                result.Add(node);
            }
            
            // Check for missing nodes
            var missingIds = idList.Except(foundIds).ToList();
            if (missingIds.Any())
            {
                throw new KeyNotFoundException($"Node(s) with ID(s) '{string.Join("', '", missingIds)}' not found");
            }
            
            // Load relationships if needed
            if (options.TraversalDepth > 0)
            {
                foreach (var node in result)
                {
                    await LoadNodeRelationships(node, options, tx, currentDepth: 0);
                }
            }
            
            if (transaction == null)
            {
                await tx.CommitAsync();
            }
            
            return result;
        }
        catch (Exception ex) when (ex is not GraphException && ex is not KeyNotFoundException)
        {
            _logger?.LogError(ex, "Failed to get nodes");
            if (transaction == null)
            {
                await tx.RollbackAsync();
            }
            throw new GraphException("Failed to get nodes", ex);
        }
        finally
        {
            if (transaction == null)
            {
                await session.CloseAsync();
            }
        }
    }

    /// <inheritdoc />
    public async Task<R> GetRelationship<R>(
        string id, 
        GraphOperationOptions options = default, 
        IGraphTransaction? transaction = null) 
        where R : Cvoya.Graph.Model.IRelationship, new()
    {
        if (string.IsNullOrEmpty(id))
            throw new ArgumentException("Relationship ID cannot be null or empty");

        var (session, tx) = await _queryExecutor.GetOrCreateTransaction(transaction);
        try
        {
            var relationship = await _relationshipManager.GetRelationship(typeof(R), id, tx) as R 
                ?? throw new GraphException($"Failed to cast relationship of ID '{id}' to type {typeof(R).Name}");
            
            if (options.TraversalDepth > 0)
            {
                await LoadRelationshipNodes(relationship, options, tx);
            }
            
            if (transaction == null)
            {
                await tx.CommitAsync();
            }
            
            return relationship;
        }
        catch (Exception ex) when (ex is not GraphException && ex is not KeyNotFoundException)
        {
            _logger?.LogError(ex, $"Failed to get relationship with ID '{id}'");
            if (transaction == null)
            {
                await tx.RollbackAsync();
            }
            throw new GraphException($"Failed to get relationship with ID '{id}'", ex);
        }
        finally
        {
            if (transaction == null)
            {
                await session.CloseAsync();
            }
        }
    }

    /// <inheritdoc />
    public async Task<IEnumerable<T>> GetRelationships<T>(
        IEnumerable<string> ids, 
        GraphOperationOptions options = default, 
        IGraphTransaction? transaction = null) 
        where T : Cvoya.Graph.Model.IRelationship, new()
    {
        if (ids == null)
            throw new ArgumentNullException(nameof(ids));
        
        var idList = ids.ToList();
        if (idList.Count == 0)
            return Enumerable.Empty<T>();
            
        var (session, tx) = await _queryExecutor.GetOrCreateTransaction(transaction);
        try
        {
            var result = new List<T>();
            var relType = typeof(T);
            var label = Neo4jTypeManager.GetLabel(relType);
            
            // Create a parameterized query to get all relationships in one go
            var parameters = new Dictionary<string, object>
            {
                ["ids"] = idList
            };
            
            var cypher = $"MATCH ()-[r:{label}]->() WHERE r.{nameof(Model.IRelationship.Id)} IN $ids RETURN r";
            var cursor = await tx.RunAsync(cypher, parameters);
            var records = await cursor.ToListAsync();
            
            // Track which IDs were found
            var foundIds = new HashSet<string>();
            
            foreach (var record in records)
            {
                var neo4jRel = record["r"].As<global::Neo4j.Driver.IRelationship>();
                var idProperty = neo4jRel.Properties[nameof(Model.IRelationship.Id)].As<string>();
                foundIds.Add(idProperty);
                
                var relationship = new T();
                _entityConverter.PopulateRelationshipEntity(relationship, neo4jRel);
                result.Add(relationship);
            }
            
            // Check for missing relationships
            var missingIds = idList.Except(foundIds).ToList();
            if (missingIds.Any())
            {
                throw new KeyNotFoundException($"Relationship(s) with ID(s) '{string.Join("', '", missingIds)}' not found");
            }
            
            // Load nodes if needed
            if (options.TraversalDepth > 0)
            {
                foreach (var relationship in result)
                {
                    await LoadRelationshipNodes(relationship, options, tx);
                }
            }
            
            if (transaction == null)
            {
                await tx.CommitAsync();
            }
            
            return result;
        }
        catch (Exception ex) when (ex is not GraphException && ex is not KeyNotFoundException)
        {
            _logger?.LogError(ex, "Failed to get relationships");
            if (transaction == null)
            {
                await tx.RollbackAsync();
            }
            throw new GraphException("Failed to get relationships", ex);
        }
        finally
        {
            if (transaction == null)
            {
                await session.CloseAsync();
            }
        }
    }

    /// <inheritdoc />
    public Task CreateNode<T>(T node, GraphOperationOptions options = default, IGraphTransaction? transaction = null) 
        where T : Cvoya.Graph.Model.INode, new()
    {
        return CreateNode((Cvoya.Graph.Model.INode)node, options, transaction);
    }

    /// <inheritdoc />
    public Task CreateRelationship<R>(R relationship, GraphOperationOptions options = default, IGraphTransaction? transaction = null) 
        where R : Cvoya.Graph.Model.IRelationship, new()
    {
        return CreateRelationship((Cvoya.Graph.Model.IRelationship)relationship, options, transaction);
    }

    /// <inheritdoc />
    public Task UpdateNode<T>(T node, GraphOperationOptions options = default, IGraphTransaction? transaction = null) 
        where T : Cvoya.Graph.Model.INode, new()
    {
        return UpdateNode((Cvoya.Graph.Model.INode)node, options, transaction);
    }

    /// <inheritdoc />
    public Task UpdateRelationship<R>(R relationship, GraphOperationOptions options = default, IGraphTransaction? transaction = null) 
        where R : Cvoya.Graph.Model.IRelationship, new()
    {
        return UpdateRelationship((Cvoya.Graph.Model.IRelationship)relationship, options, transaction);
    }

    /// <inheritdoc />
    public Task DeleteNode(string nodeId, IGraphTransaction? transaction = null)
    {
        return DeleteNode(nodeId, new GraphOperationOptions(), transaction);
    }

    /// <inheritdoc />
    public Task DeleteRelationship(string relationshipId, IGraphTransaction? transaction = null)
    {
        return DeleteRelationship(relationshipId, new GraphOperationOptions(), transaction);
    }

    /// <summary>
    /// Loads relationships for a node with the specified traversal depth.
    /// </summary>
    internal async Task LoadNodeRelationships(
        Cvoya.Graph.Model.INode node, 
        GraphOperationOptions options,
        IAsyncTransaction transaction,
        int currentDepth,
        HashSet<string>? processedNodes = null)
    {
        processedNodes ??= new HashSet<string>();
        
        // Skip if the node doesn't have an ID or has already been processed
        if (string.IsNullOrEmpty(node.Id) || !processedNodes.Add(node.Id))
            return;

        if (options.TraversalDepth > 0 && currentDepth >= options.TraversalDepth)
            return;

        // Get all relationship properties from the node type
        var nodeType = node.GetType();
        var relationshipProperties = nodeType.GetProperties()
            .Where(IsRelationshipProperty)
            .ToList();

        foreach (var property in relationshipProperties)
        {
            await LoadPropertyRelationships(node, property, options, transaction, currentDepth, processedNodes);
        }
    }

    private async Task LoadPropertyRelationships(
        Cvoya.Graph.Model.INode node,
        PropertyInfo property, 
        GraphOperationOptions options,
        IAsyncTransaction transaction,
        int currentDepth,
        HashSet<string> processedNodes)
    {
        var relType = property.PropertyType;
        
        // Handle collection of relationships (one-to-many)
        if (relType.IsGenericType && 
            typeof(IEnumerable).IsAssignableFrom(relType) && 
            !relType.IsAssignableTo(typeof(string)))
        {
            await LoadCollectionRelationships(node, property, options, transaction, currentDepth, processedNodes);
            return;
        }

        // Handle single relationship (one-to-one)
        if (typeof(Cvoya.Graph.Model.IRelationship).IsAssignableFrom(relType))
        {
            await LoadSingleRelationship(node, property, options, transaction, currentDepth, processedNodes);
            return;
        }
    }

    private async Task LoadSingleRelationship(
        Cvoya.Graph.Model.INode node,
        PropertyInfo property,
        GraphOperationOptions options,
        IAsyncTransaction transaction,
        int currentDepth,
        HashSet<string> processedNodes)
    {
        // Implementation details for loading a single relationship
        // This is a placeholder for the actual implementation
    }

    private async Task LoadCollectionRelationships(
        Cvoya.Graph.Model.INode node,
        PropertyInfo property,
        GraphOperationOptions options,
        IAsyncTransaction transaction,
        int currentDepth,
        HashSet<string> processedNodes)
    {
        // Implementation details for loading a collection of relationships
        // This is a placeholder for the actual implementation
    }

    private bool IsRelationshipProperty(PropertyInfo property)
    {
        var type = property.PropertyType;

        if (typeof(Cvoya.Graph.Model.IRelationship).IsAssignableFrom(type))
            return true;

        if (type.IsGenericType && 
            typeof(IEnumerable).IsAssignableFrom(type) && 
            !type.IsAssignableTo(typeof(string)))
        {
            var elementType = type.GetGenericArguments().FirstOrDefault();
            return elementType != null && typeof(Cvoya.Graph.Model.IRelationship).IsAssignableFrom(elementType);
        }

        return false;
    }

    /// <summary>
    /// Loads nodes for a relationship.
    /// </summary>
    internal async Task LoadRelationshipNodes(
        Cvoya.Graph.Model.IRelationship relationship, 
        GraphOperationOptions options,
        IAsyncTransaction transaction)
    {
        await _relationshipManager.LoadRelationshipNodes(relationship, options, transaction, _nodeManager);
    }

    /// <summary>
    /// Gets or creates a Neo4j transaction from a graph transaction.
    /// </summary>
    internal async Task<(IAsyncSession, IAsyncTransaction)> GetOrCreateTransaction(IGraphTransaction? transaction)
    {
        return await _queryExecutor.GetOrCreateTransaction(transaction);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        DisposeAsync().AsTask().GetAwaiter().GetResult();
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await _driver.CloseAsync();
    }
}