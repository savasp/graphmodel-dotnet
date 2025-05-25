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

using System.Collections;
using System.Reflection;
using Cvoya.Graph.Model;
using Cvoya.Graph.Provider.Neo4j.Linq;
using Microsoft.Extensions.Logging;
using Neo4j.Driver;

namespace Cvoya.Graph.Provider.Neo4j;

/// <summary>
/// Neo4j implementation of the IGraphProvider interface.
/// </summary>
public class Neo4jGraphProvider : IGraph
{
    private readonly Microsoft.Extensions.Logging.ILogger? logger;

    private readonly IDriver driver;

    // Tracks labels/types for which constraints have been created
    private readonly HashSet<string> constrainedLabels = [];
    private readonly object constraintLock = new();
    private bool constraintsLoaded = false;

    private readonly string databaseName;

    /// <summary>
    /// Initializes a new instance of the <see cref="Neo4jGraphProvider"/> class.
    /// </summary>
    /// <param name="uri">The URI of the Neo4j database.</param>
    /// <param name="username">The username for authentication.</param>
    /// <param name="password">The password for authentication.</param>
    /// <param name="databaseName">The name of the database.</param>
    /// <param name="logger">The logger instance.</param>
    /// <remarks>
    /// The default value for <see cref="databaseName"/> is "neo4j".
    /// </remarks>
    public Neo4jGraphProvider(
        string? uri = null,
        string? username = null,
        string? password = null,
        string? databaseName = null,
        Microsoft.Extensions.Logging.ILogger? logger = null)
    {
        this.logger = logger;
        uri ??= Environment.GetEnvironmentVariable("NEO4J_URI") ?? "bolt://localhost:7687";
        username ??= Environment.GetEnvironmentVariable("NEO4J_USER") ?? "neo4j";
        password ??= Environment.GetEnvironmentVariable("NEO4J_PASSWORD") ?? "password";
        databaseName ??= Environment.GetEnvironmentVariable("NEO4J_DATABASE") ?? "neo4j";

        this.databaseName = databaseName;

        this.driver = username is null
            ? GraphDatabase.Driver(uri)
            : GraphDatabase.Driver(uri, AuthTokens.Basic(username, password));
    }

    /// <inheritdoc />
    public IQueryable<N> Nodes<N>(GraphOperationOptions options = default, IGraphTransaction? transaction = null)
           where N : Model.INode, new()
    {
        var provider = new Neo4jQueryProvider(this, options, this.logger, transaction, typeof(N));

        return new Neo4jQueryable<N>(provider, options, transaction);
    }

    /// <inheritdoc />
    public IQueryable<R> Relationships<R>(GraphOperationOptions options = default, IGraphTransaction? transaction = null)
         where R : Model.IRelationship, new()
    {
        var provider = new Neo4jQueryProvider(this, options, this.logger, transaction, typeof(R));

        return new Neo4jQueryable<R>(provider, options, transaction);
    }

    /// <inheritdoc />
    public async Task<T> GetNode<T>(string id, GraphOperationOptions options = default, IGraphTransaction? transaction = null)
        where T : Model.INode, new()
    {
        if (string.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));

        var result = await GetNodes<T>([id], options, transaction);
        return result.FirstOrDefault() ?? throw new GraphException($"Node with ID '{id}' not found.");
    }

    /// <inheritdoc />
    public async Task<IEnumerable<T>> GetNodes<T>(IEnumerable<string> ids, GraphOperationOptions options = default, IGraphTransaction? transaction = null)
        where T : Model.INode, new()
    {
        if (ids is null || !ids.Any()) throw new ArgumentException("IDs cannot be null or empty", nameof(ids));
        if (ids.Any(string.IsNullOrEmpty)) throw new ArgumentException("ID cannot be null or empty", nameof(ids));

        var (session, tx) = await GetOrCreateTransaction(transaction);
        try
        {
            var nodes = new List<T>();
            var processedNodes = new HashSet<string>();

            foreach (var id in ids)
            {
                var node = await GetNodeInternal<T>(id, tx);
                nodes.Add(node);

                // Load relationships based on options
                // Check for both positive depth and -1 (full graph)
                if (options.TraversalDepth > 0 || options.TraversalDepth == -1)
                {
                    await LoadNodeRelationships(node, options, tx, currentDepth: 0, processedNodes);
                }
            }

            return nodes;
        }
        catch (Exception ex)
        {
            throw new GraphException($"Error retrieving nodes of type '{typeof(T).Name}'", ex);
        }
        finally
        {
            if (transaction is null)
            {
                await session.CloseAsync();
            }
        }
    }

    /// <inheritdoc />
    public async Task<R> GetRelationship<R>(string id, GraphOperationOptions options = default, IGraphTransaction? transaction = null)
        where R : Model.IRelationship, new()
    {
        if (string.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));

        var result = await this.GetRelationships<R>([id], options, transaction);

        return result.FirstOrDefault() ?? throw new GraphException($"Relationship with ID '{id}' not found.");
    }

    /// <inheritdoc />
    public async Task<IEnumerable<R>> GetRelationships<R>(IEnumerable<string> ids, GraphOperationOptions options = default, IGraphTransaction? transaction = null)
        where R : Model.IRelationship, new()
    {
        if (ids is null || !ids.Any()) throw new ArgumentException("IDs cannot be null or empty", nameof(ids));
        if (ids.Any(string.IsNullOrEmpty)) throw new ArgumentException("ID cannot be null or empty", nameof(ids));

        var (session, tx) = await GetOrCreateTransaction(transaction);
        try
        {
            var relationships = new List<R>();

            foreach (var id in ids)
            {
                var relationship = await GetRelationshipInternal<R>(id, tx);
                relationships.Add(relationship);

                // Load connected nodes based on options
                // Check for both positive depth and -1 (full graph)
                if (options.TraversalDepth > 0 || options.TraversalDepth == -1)
                {
                    await LoadRelationshipNodes(relationship, options, tx);
                }
            }

            return relationships;
        }
        catch (Exception ex)
        {
            throw new GraphException($"Error retrieving relationships of type '{typeof(R).Name}'", ex);
        }
        finally
        {
            if (transaction is null)
            {
                await session.CloseAsync();
            }
        }
    }

    /// <inheritdoc />
    public async Task CreateNode<T>(T node, GraphOperationOptions options = default, IGraphTransaction? transaction = null)
        where T : Model.INode, new()
    {
        if (node is null) throw new ArgumentNullException(nameof(node));
        if (string.IsNullOrEmpty(node.Id)) throw new ArgumentNullException(nameof(node.Id));

        node.EnsureNoReferenceCycle();

        var (session, tx) = await GetOrCreateTransaction(transaction);

        try
        {
            // Create the main node
            await this.CreateNode(null, node, tx);

            // Handle relationships based on options
            // Check for both positive depth and -1 (full graph)
            if (options.TraversalDepth > 0 || options.TraversalDepth == -1)
            {
                await ProcessNodeRelationships(node, options, tx, currentDepth: 0, processedNodes: new HashSet<string>());
            }

            if (transaction is null)
            {
                // We created the transaction, so we need to commit it
                await tx.CommitAsync();
            }
        }
        catch (Exception ex)
        {
            throw new GraphException($"Error creating node of type '{typeof(T).Name}'", ex);
        }
        finally
        {
            if (transaction is null)
            {
                await session.CloseAsync();
            }
        }
    }

    /// <inheritdoc />
    public async Task CreateRelationship<R>(R relationship, GraphOperationOptions options = default, IGraphTransaction? transaction = null)
        where R : Model.IRelationship, new()
    {
        if (relationship is null) throw new ArgumentNullException(nameof(relationship));
        if (string.IsNullOrEmpty(relationship.Id)) throw new ArgumentNullException(nameof(relationship.Id));
        if (string.IsNullOrEmpty(relationship.SourceId)) throw new ArgumentNullException(nameof(relationship.SourceId));
        if (string.IsNullOrEmpty(relationship.TargetId)) throw new ArgumentNullException(nameof(relationship.TargetId));

        relationship.EnsureNoReferenceCycle();

        var (session, tx) = await GetOrCreateTransaction(transaction);

        try
        {
            // Handle source and target nodes based on options
            // Process nodes first if needed
            await ProcessRelationshipNodes(relationship, options, tx, isCreate: true);

            // Create the relationship
            await CreateRelationshipInternal(relationship, tx);

            if (transaction is null)
            {
                // We created the transaction, so we need to commit it
                await tx.CommitAsync();
            }
        }
        catch (Exception ex) when (ex is not GraphException)
        {
            throw new GraphException($"Error creating relationship of type '{typeof(R).Name}' with ID '{relationship.Id}'", ex);
        }
        finally
        {
            if (transaction is null)
            {
                await session.CloseAsync();
            }
        }
    }

    /// <inheritdoc />
    public async Task UpdateNode<T>(T node, GraphOperationOptions options = default, IGraphTransaction? transaction = null)
        where T : Model.INode, new()
    {
        if (node == null) throw new ArgumentNullException(nameof(node));
        if (string.IsNullOrEmpty(node.Id)) throw new ArgumentNullException(nameof(node.Id));

        node.EnsureNoReferenceCycle();

        var (simpleProps, complexProps) = SerializationExtensions.GetSimpleAndComplexProperties(node);

        CheckNodeProperties(complexProps);

        var cypher = $"MATCH (n) WHERE n.{nameof(Model.INode.Id)} = '{node.Id}' SET n += $props";
        var (session, tx) = await GetOrCreateTransaction(transaction);
        try
        {
            await tx.RunAsync(cypher, new
            {
                props = simpleProps.ToDictionary(kv => kv.Key.Name, kv => kv.Value.ConvertToNeo4jValue()),
            });

            // Handle relationships based on options
            // Check for both positive depth and -1 (full graph)
            if (options.TraversalDepth > 0 || options.TraversalDepth == -1)
            {
                await ProcessNodeRelationships(node, options, tx, currentDepth: 0, processedNodes: new HashSet<string>());
            }

            if (transaction == null)
            {
                // We created the transaction, so we need to commit it
                await tx.CommitAsync();
            }
        }
        catch (Exception ex)
        {
            throw new GraphException($"Error updating node of type '{typeof(T).Name}'", ex);
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
    public async Task UpdateRelationship<R>(R relationship, GraphOperationOptions options = default, IGraphTransaction? transaction = null)
        where R : Model.IRelationship, new()
    {
        if (relationship is null) throw new ArgumentNullException(nameof(relationship));
        if (string.IsNullOrEmpty(relationship.Id)) throw new ArgumentNullException(nameof(relationship.Id));

        relationship.EnsureNoReferenceCycle();

        var (session, tx) = await GetOrCreateTransaction(transaction);

        try
        {
            // Update the relationship properties first
            await UpdateRelationshipInternal(relationship, tx);

            // Handle source and target nodes based on options
            if (options.TraversalDepth > 0 && options.UpdateExistingNodes)
            {
                await ProcessRelationshipNodes(relationship, options, tx, isCreate: false);
            }

            if (transaction == null)
            {
                // We created the transaction, so we need to commit it
                await tx.CommitAsync();
            }
        }
        catch (Exception ex)
        {
            throw new GraphException($"Error updating relationship of type '{typeof(R).Name}'", ex);
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
    public async Task<IGraphTransaction> BeginTransaction()
    {
        var session = driver.AsyncSession(builder => builder.WithDatabase(this.databaseName));
        var tx = await session.BeginTransactionAsync();

        return new Neo4jGraphTransaction(session, tx);
    }

    /// <inheritdoc />
    public async Task DeleteNode(string id, IGraphTransaction? transaction = null)
    {
        if (string.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));

        var (session, tx) = await GetOrCreateTransaction(transaction);
        try
        {
            // TODO: Delete all nodes that represent complex properties of the node
            var cypher = "MATCH (n) WHERE n.Id = $id DETACH DELETE n";
            await tx.RunAsync(cypher, new { id });

            if (transaction == null)
            {
                // We created the transaction, so we need to commit it
                await tx.CommitAsync();
            }
        }
        catch (Exception ex)
        {
            throw new GraphException("Failed to delete node.", ex);
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
    public async Task DeleteRelationship(string id, IGraphTransaction? transaction = null)
    {
        if (string.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));

        var (session, tx) = await GetOrCreateTransaction(transaction);
        try
        {
            var cypher = "MATCH ()-[r]->() WHERE r.Id = $id DELETE r";
            await tx.RunAsync(cypher, new { id });

            if (transaction == null)
            {
                // We created the transaction, so we need to commit it
                await tx.CommitAsync();
            }
        }
        catch (Exception ex)
        {
            throw new GraphException("Failed to delete relationship.", ex);
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
    public async Task<IEnumerable<dynamic>> ExecuteCypher(string cypher, object? parameters = null, IGraphTransaction? transaction = null)
    {
        if (string.IsNullOrEmpty(cypher)) throw new ArgumentNullException(nameof(cypher));

        var (session, tx) = await GetOrCreateTransaction(transaction);
        try
        {
            return await ExecuteCypherInternal(tx, cypher, parameters);
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Failed to execute Cypher query.");
            throw new GraphException("Failed to execute Cypher query.", ex);
        }
        finally
        {
            if (transaction == null)
            {
                await session.CloseAsync();
            }
        }
    }

    internal async Task<IEnumerable<dynamic>> ExecuteCypherInternal(IAsyncTransaction transaction, string cypher, object? parameters = null)
    {
        if (string.IsNullOrEmpty(cypher)) throw new ArgumentNullException(nameof(cypher));

        var results = new List<dynamic>();
        var cursor = await transaction.RunAsync(cypher, parameters);
        while (await cursor.FetchAsync())
        {
            var record = cursor.Current;
            results.Add(record.Values);
        }
        return results;
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await driver.DisposeAsync();
    }


    ///
    /// Private helper methods -----------------
    /// 


    internal async Task<(IAsyncSession, IAsyncTransaction)> GetOrCreateTransaction(IGraphTransaction? transaction)
    {
        if (transaction is Neo4jGraphTransaction neo4jTx && neo4jTx.IsActive)
        {
            var tx = neo4jTx.GetTransaction() ?? throw new InvalidOperationException("Transaction is not active.");
            return (neo4jTx.Session, tx);
        }
        else if (transaction == null)
        {
            var session = driver.AsyncSession(builder => builder.WithDatabase(this.databaseName));
            var tx = await session.BeginTransactionAsync();
            return (session, tx);
        }
        else
        {
            throw new InvalidOperationException("Transaction is not active or not a Neo4j transaction.");
        }
    }

    private async Task LoadExistingConstraints()
    {
        if (constraintsLoaded) return;
        lock (constraintLock)
        {
            if (constraintsLoaded) return;
            constraintsLoaded = true;
        }

        var cypher = "SHOW CONSTRAINTS";
        var (session, tx) = await GetOrCreateTransaction(null);

        try
        {
            var cursor = await tx.RunAsync(cypher);
            while (await cursor.FetchAsync())
            {
                var record = cursor.Current;
                if (record.Values.TryGetValue("labelsOrTypes", out var labelsOrTypesObj) && labelsOrTypesObj is IEnumerable<object> labelsOrTypes)
                {
                    foreach (var label in labelsOrTypes)
                    {
                        if (label is string s)
                        {
                            constrainedLabels.Add(s);
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Log(l => l.LogError(ex, "Failed to load existing constraints from Neo4j."));
            throw new GraphException("Failed to load existing constraints from Neo4j.", ex);
        }
        finally
        {
            await session.CloseAsync();
        }
    }

    // TODO: Ensure that this runs within the context of a transaction
    private async Task EnsureConstraintsForLabel(string label, IEnumerable<PropertyInfo> properties)
    {
        await LoadExistingConstraints();

        lock (constraintLock)
        {
            if (constrainedLabels.Contains(label))
                return;
            constrainedLabels.Add(label);
        }

        var (session, tx) = await GetOrCreateTransaction(null);
        await using var _ = session;
        await using var __ = tx;

        // Always add unique constraint for the identifier property
        var cypher = $"CREATE CONSTRAINT IF NOT EXISTS FOR (n:`{label}`) REQUIRE n.{nameof(Model.INode.Id)} IS UNIQUE";
        await tx.RunAsync(cypher);

        foreach (var prop in properties)
        {
            if (prop.Name == nameof(Model.INode.Id)) continue;
            var name = prop.GetCustomAttribute<PropertyAttribute>()?.Label ?? prop.Name;
            var propCypher = $"CREATE CONSTRAINT IF NOT EXISTS FOR (n:`{label}`) REQUIRE n.{name} IS NOT NULL";
            await tx.RunAsync(propCypher);
        }

        await tx.CommitAsync();
        await session.CloseAsync();
    }

    // Get the label for a type (NodeAttribute/RelationshipAttribute or namespace-qualified name with dots replaced by underscores)
    internal static string GetLabel(Type type)
    {
        var nodeAttr = type.GetCustomAttribute<NodeAttribute>(inherit: false);
        if (nodeAttr?.Label is { Length: > 0 }) return nodeAttr.Label;

        var relAttr = type.GetCustomAttribute<RelationshipAttribute>(inherit: false);
        if (relAttr?.Label is { Length: > 0 }) return relAttr.Label;

        var propertyAttr = type.GetCustomAttribute<PropertyAttribute>(inherit: false);
        if (propertyAttr?.Label is { Length: > 0 }) return propertyAttr.Label;

        // Fall back to the namespace-qualified type name with dots replaced by underscores
        var label = type.Name.Replace("`", "");
        return label ?? throw new GraphException($"Type '{type}' does not have a valid FullName.");
    }

    // Find the .NET type for a given label that is assignable to baseType
    private static Type GetTypeForLabel(string label, Type baseType)
    {
        var match = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(a => a.GetTypes())
            .Where(baseType.IsAssignableFrom)
            .FirstOrDefault(t => GetLabel(t) == label);

        if (match is null)
            throw new GraphException($"No .NET type found for label '{label}' assignable to {baseType.FullName}");

        return match;
    }

    private void Log(Action<Microsoft.Extensions.Logging.ILogger> logAction)
    {
        if (logger != null)
        {
            logAction(logger);
        }
    }

    private async Task<string> CreateNode(
        string? parentId,
        object node,
        IAsyncTransaction transaction,
        string? propertyName = null)
    {
        var type = node.GetType();
        var label = GetLabel(type);
        var (simpleProps, complexProps) = SerializationExtensions.GetSimpleAndComplexProperties(node);

        CheckNodeProperties(complexProps);

        await EnsureConstraintsForLabel(label, simpleProps.Select(p => p.Key));

        var cypher = parentId == null ?
            $"CREATE (b:{label} $props) RETURN elementId(b) as nodeId" :
            $"MATCH (a) WHERE elementId(a) = '{parentId}' CREATE (a)-[:{propertyName}]->(b:{label} $props) RETURN elementId(b) as nodeId";

        var record = await transaction.RunAsync(cypher, new
        {
            props = simpleProps.ToDictionary(kv => kv.Key.Name, kv => kv.Value.ConvertToNeo4jValue()),
        });
        var createdNode = await record.SingleAsync();
        var nodeId = createdNode["nodeId"].ToString() ?? throw new GraphException($"Failed to create node of type '{label}'");

        // Handle complex properties
        foreach (var prop in complexProps)
        {
            if (prop.Value == null) continue;

            await this.CreateNode(nodeId, prop.Value, transaction, prop.Key.Name);
        }

        return nodeId;
    }

    private void CheckRelationshipProperties(Dictionary<PropertyInfo, object?> complexProps)
    {
        List<string> relationshipPropertyNames = ["Source", "Target"];
        var check = complexProps
            .Select(p => p.Key)
            .Where(p => !(relationshipPropertyNames.Contains(p.Name) && p.PropertyType.IsAssignableTo(typeof(Model.INode))));

        if (check.Any())
        {
            throw new GraphException($"Complex properties are not supported for relationships.");
        }
    }

    private void CheckNodeProperties(Dictionary<PropertyInfo, object?> complexProps)
    {
        var check = complexProps
            .Select(p => p.Key)
            .Where(p => p.PropertyType.IsAssignableTo(typeof(Model.INode)));

        if (check.Any())
        {
            throw new GraphException($"Properties of type '{typeof(Model.INode).Name}' are not supported for nodes.");
        }
    }

    private async Task ProcessNodeRelationships(
       Model.INode node,
       GraphOperationOptions options,
       IAsyncTransaction tx,
       int currentDepth,
       HashSet<string> processedNodes)
    {
        if (currentDepth >= options.TraversalDepth && options.TraversalDepth != -1)
            return;

        // Prevent infinite loops
        if (!processedNodes.Add(node.Id))
            return;

        var nodeType = node.GetType();
        var relationshipProperties = nodeType
            .GetProperties()
            .Where(p => p.PropertyType.IsGenericType &&
                       (p.PropertyType.GetGenericTypeDefinition() == typeof(List<>) ||
                        p.PropertyType.GetGenericTypeDefinition() == typeof(IList<>) ||
                        p.PropertyType.GetGenericTypeDefinition() == typeof(ICollection<>) ||
                        p.PropertyType.GetGenericTypeDefinition() == typeof(IEnumerable<>)))
            .Where(p =>
            {
                var elementType = p.PropertyType.GetGenericArguments()[0];
                return elementType.GetInterfaces().Any(i => i == typeof(Model.IRelationship));
            });

        foreach (var prop in relationshipProperties)
        {
            var relationships = prop.GetValue(node) as IEnumerable;
            if (relationships == null) continue;

            foreach (var rel in relationships)
            {
                if (rel is not Model.IRelationship relationship) continue;

                // Check if we should process this relationship type
                if (options.RelationshipTypes?.Any() == true)
                {
                    var relType = GetLabel(relationship.GetType());
                    if (!options.RelationshipTypes.Contains(relType))
                        continue;
                }

                await HandleRelationshipInTraversal(relationship, options, tx, currentDepth, processedNodes);
            }
        }
    }

    private async Task HandleRelationshipInTraversal(
        Model.IRelationship relationship,
        GraphOperationOptions options,
        IAsyncTransaction tx,
        int currentDepth,
        HashSet<string> processedNodes)
    {
        // Get the generic relationship interface to access Source and Target
        var relType = relationship.GetType();
        var genericInterface = relType.GetInterfaces()
            .FirstOrDefault(i => i.IsGenericType &&
                               i.GetGenericTypeDefinition() == typeof(Model.IRelationship<,>));

        if (genericInterface == null) return;

        var sourceProp = relType.GetProperty("Source");
        var targetProp = relType.GetProperty("Target");

        if (sourceProp == null || targetProp == null) return;

        var source = sourceProp.GetValue(relationship) as Model.INode;
        var target = targetProp.GetValue(relationship) as Model.INode;

        // Handle target node
        if (target != null && !string.IsNullOrEmpty(target.Id))
        {
            // Check if target exists
            var exists = await NodeExists(target.Id, tx);

            if (!exists && options.CreateMissingNodes)
            {
                await CreateNode(null, target, tx);
                // Continue traversal for the newly created node
                await ProcessNodeRelationships(target, options, tx, currentDepth + 1, processedNodes);
            }
            else if (exists && options.UpdateExistingNodes)
            {
                await UpdateNodeInternal(target, tx);
                // Continue traversal for the updated node
                await ProcessNodeRelationships(target, options, tx, currentDepth + 1, processedNodes);
            }
        }

        // Create or update the relationship
        var relationshipExists = await RelationshipExists(relationship.Id, tx);
        if (!relationshipExists)
        {
            await CreateRelationshipInternal(relationship, tx);
        }
        else if (options.UpdateExistingNodes)
        {
            await UpdateRelationshipInternal(relationship, tx);
        }
    }

    private async Task<bool> NodeExists(string nodeId, IAsyncTransaction tx)
    {
        var cypher = $"MATCH (n) WHERE n.{nameof(Model.INode.Id)} = '{nodeId}' RETURN COUNT(n) as count";
        var result = await tx.RunAsync(cypher);
        var record = await result.SingleAsync();
        return record["count"].As<long>() > 0;
    }

    private async Task<bool> RelationshipExists(string relId, IAsyncTransaction tx)
    {
        var cypher = $"MATCH ()-[r]->() WHERE r.{nameof(Model.IRelationship.Id)} = '{relId}' RETURN COUNT(r) as count";
        var result = await tx.RunAsync(cypher);
        var record = await result.SingleAsync();
        return record["count"].As<long>() > 0;
    }

    private async Task CreateRelationshipInternal(Model.IRelationship relationship, IAsyncTransaction tx)
    {
        var type = relationship.GetType();
        var label = GetLabel(type);
        var (simpleProps, complexProps) = SerializationExtensions.GetSimpleAndComplexProperties(relationship);

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
            props = simpleProps.ToDictionary(kv => kv.Key.Name, kv => kv.Value.ConvertToNeo4jValue()),
        });
    }

    private async Task UpdateNodeInternal(Model.INode node, IAsyncTransaction tx)
    {
        var (simpleProps, complexProps) = SerializationExtensions.GetSimpleAndComplexProperties(node);
        CheckNodeProperties(complexProps);

        var cypher = $"MATCH (n) WHERE n.{nameof(Model.INode.Id)} = '{node.Id}' SET n += $props";
        await tx.RunAsync(cypher, new
        {
            props = simpleProps.ToDictionary(kv => kv.Key.Name, kv => kv.Value.ConvertToNeo4jValue()),
        });
    }

    private async Task UpdateRelationshipInternal(Model.IRelationship relationship, IAsyncTransaction tx)
    {
        var (simpleProps, complexProps) = SerializationExtensions.GetSimpleAndComplexProperties(relationship);
        CheckRelationshipProperties(complexProps);

        var cypher = $"MATCH ()-[r]->() WHERE r.{nameof(Model.IRelationship.Id)} = '{relationship.Id}' SET r += $props";
        await tx.RunAsync(cypher, new
        {
            props = simpleProps.ToDictionary(kv => kv.Key.Name, kv => kv.Value.ConvertToNeo4jValue()),
        });
    }

    private async Task ProcessRelationshipNodes(
                Model.IRelationship relationship,
                GraphOperationOptions options,
                IAsyncTransaction tx,
                bool isCreate)
    {
        // First check if nodes should exist when not creating
        if (!options.CreateMissingNodes && isCreate)
        {
            // Verify nodes exist
            var sourceExists = await NodeExists(relationship.SourceId, tx);
            var targetExists = await NodeExists(relationship.TargetId, tx);

            if (!sourceExists || !targetExists)
            {
                throw new GraphException($"Cannot create relationship: source node '{relationship.SourceId}' or target node '{relationship.TargetId}' does not exist and CreateMissingNodes is false");
            }
            return; // No further processing needed
        }

        // Only process nodes if we have the generic interface with Source/Target properties
        var relType = relationship.GetType();
        var genericInterface = relType.GetInterfaces()
            .FirstOrDefault(i => i.IsGenericType &&
                               i.GetGenericTypeDefinition() == typeof(Model.IRelationship<,>));

        if (genericInterface == null)
        {
            // This relationship doesn't implement IRelationship<S,T>, so we can't access Source/Target
            // Just verify the nodes exist if CreateMissingNodes = false
            return;
        }

        var sourceProp = relType.GetProperty("Source");
        var targetProp = relType.GetProperty("Target");

        if (sourceProp == null || targetProp == null) return;

        var source = sourceProp.GetValue(relationship) as Model.INode;
        var target = targetProp.GetValue(relationship) as Model.INode;

        // Keep track of processed nodes to avoid cycles
        var processedNodes = new HashSet<string>();

        // Process source node and its relationships if needed
        if (source != null && !string.IsNullOrEmpty(source.Id))
        {
            var sourceExists = await NodeExists(source.Id, tx);

            if (isCreate && !sourceExists && options.CreateMissingNodes)
            {
                await CreateNode(null, source, tx);
                // Process source node's relationships if depth allows
                if (options.TraversalDepth > 0 || options.TraversalDepth == -1)
                {
                    await ProcessNodeRelationships(source, options, tx, currentDepth: 0, processedNodes);
                }
            }
            else if (!isCreate && sourceExists && options.UpdateExistingNodes)
            {
                await UpdateNodeInternal(source, tx);
                if (options.TraversalDepth > 0 || options.TraversalDepth == -1)
                {
                    await ProcessNodeRelationships(source, options, tx, currentDepth: 0, processedNodes);
                }
            }
            else if (isCreate && sourceExists && (options.TraversalDepth > 0 || options.TraversalDepth == -1))
            {
                // Source exists but we still need to process its relationships
                await ProcessNodeRelationships(source, options, tx, currentDepth: 0, processedNodes);
            }
        }

        // Process target node and its relationships if needed
        if (target != null && !string.IsNullOrEmpty(target.Id))
        {
            var targetExists = await NodeExists(target.Id, tx);

            if (isCreate && !targetExists && options.CreateMissingNodes)
            {
                await CreateNode(null, target, tx);
                // Process target node's relationships if depth allows
                if (options.TraversalDepth > 0 || options.TraversalDepth == -1)
                {
                    await ProcessNodeRelationships(target, options, tx, currentDepth: 0, processedNodes);
                }
            }
            else if (!isCreate && targetExists && options.UpdateExistingNodes)
            {
                await UpdateNodeInternal(target, tx);
                if (options.TraversalDepth > 0 || options.TraversalDepth == -1)
                {
                    await ProcessNodeRelationships(target, options, tx, currentDepth: 0, processedNodes);
                }
            }
            else if (isCreate && targetExists && (options.TraversalDepth > 0 || options.TraversalDepth == -1))
            {
                // Target exists but we still need to process its relationships
                await ProcessNodeRelationships(target, options, tx, currentDepth: 0, processedNodes);
            }
        }
    }

    internal async Task LoadNodeRelationships(
        Model.INode node,
        GraphOperationOptions options,
        IAsyncTransaction tx,
        int currentDepth,
        HashSet<string> processedNodes)
    {
        if (currentDepth >= options.TraversalDepth && options.TraversalDepth != -1)
            return;

        // Prevent infinite loops
        if (!processedNodes.Add(node.Id))
            return;

        var nodeType = node.GetType();

        // Find all properties that are collections of relationships
        var relationshipProperties = nodeType
            .GetProperties()
            .Where(p => p.PropertyType.IsGenericType &&
                       (p.PropertyType.GetGenericTypeDefinition() == typeof(List<>) ||
                        p.PropertyType.GetGenericTypeDefinition() == typeof(IList<>) ||
                        p.PropertyType.GetGenericTypeDefinition() == typeof(ICollection<>)))
            .Where(p =>
            {
                var elementType = p.PropertyType.GetGenericArguments()[0];
                return elementType.GetInterfaces().Any(i => i == typeof(Model.IRelationship));
            });

        foreach (var prop in relationshipProperties)
        {
            var elementType = prop.PropertyType.GetGenericArguments()[0];
            var relLabel = GetLabel(elementType);

            // Check if we should process this relationship type
            if (options.RelationshipTypes?.Any() == true && !options.RelationshipTypes.Contains(relLabel))
                continue;

            // Query for relationships of this type
            var cypher = $@"
                MATCH (n)-[r:{relLabel}]-(m)
                WHERE n.{nameof(Model.INode.Id)} = $nodeId
                    AND (startNode(r) = n OR r.IsBidirectional = true)
                RETURN r, m, startNode(r) = n AS isOutgoing";
            var result = await tx.RunAsync(cypher, new { nodeId = node.Id });
            var relationships = new List<Model.IRelationship>();

            // First, collect all the data from the cursor
            var records = await result.ToListAsync();

            // Then process each record
            foreach (var record in records)
            {
                var relNode = record["r"].As<global::Neo4j.Driver.IRelationship>();
                var otherNode = record["m"].As<global::Neo4j.Driver.INode>();
                var isOutgoing = record["isOutgoing"].As<bool>();

                var relationship = Activator.CreateInstance(elementType) as Model.IRelationship;
                if (relationship != null)
                {
                    // For bidirectional relationships viewed from the target node,
                    // we need to create a "flipped" view of the relationship
                    if (!isOutgoing && relNode.Properties.ContainsKey("IsBidirectional") &&
                        relNode.Properties["IsBidirectional"].As<bool>())
                    {
                        // This is a bidirectional relationship where current node is the target
                        // We need to populate it as if the current node is the source
                        PopulateEntity(relationship, relNode);

                        var sourceProp = elementType.GetProperty("Source");
                        if (sourceProp != null)
                        {
                            sourceProp.SetValue(relationship, node); // Current node (Bob) is source
                        }

                        var genericInterface = elementType.GetInterfaces()
                            .FirstOrDefault(i => i.IsGenericType &&
                                              i.GetGenericTypeDefinition() == typeof(Model.IRelationship<,>));

                        if (genericInterface != null)
                        {
                            var targetProp = elementType.GetProperty("Target");
                            if (targetProp != null)
                            {
                                var targetType = genericInterface.GetGenericArguments()[1];
                                var target = Activator.CreateInstance(targetType) as Model.INode;
                                if (target != null)
                                {
                                    PopulateEntity(target, otherNode); // Other node (Alice) is target
                                    targetProp.SetValue(relationship, target);

                                    if (currentDepth + 1 < options.TraversalDepth || options.TraversalDepth == -1)
                                    {
                                        await LoadNodeRelationships(target, options, tx, currentDepth + 1, processedNodes);
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        // Normal outgoing relationship or non-bidirectional incoming
                        PopulateEntity(relationship, relNode);

                        var sourceProp = elementType.GetProperty("Source");
                        if (sourceProp != null)
                        {
                            sourceProp.SetValue(relationship, isOutgoing ? node : null);
                        }

                        var genericInterface = elementType.GetInterfaces()
                            .FirstOrDefault(i => i.IsGenericType &&
                                              i.GetGenericTypeDefinition() == typeof(Model.IRelationship<,>));

                        if (genericInterface != null)
                        {
                            var targetProp = elementType.GetProperty("Target");
                            if (targetProp != null)
                            {
                                var targetType = genericInterface.GetGenericArguments()[isOutgoing ? 1 : 0];
                                var target = Activator.CreateInstance(targetType) as Model.INode;
                                if (target != null)
                                {
                                    PopulateEntity(target, otherNode);
                                    targetProp.SetValue(relationship, isOutgoing ? target : null);

                                    if (currentDepth + 1 < options.TraversalDepth || options.TraversalDepth == -1)
                                    {
                                        await LoadNodeRelationships(target, options, tx, currentDepth + 1, processedNodes);
                                    }
                                }
                            }
                        }
                    }

                    relationships.Add(relationship);
                }
            }

            // Set the collection property
            if (prop.PropertyType.GetGenericTypeDefinition() == typeof(List<>))
            {
                var list = Activator.CreateInstance(prop.PropertyType) as IList;
                foreach (var rel in relationships)
                {
                    list?.Add(rel);
                }
                prop.SetValue(node, list);
            }
        }

        // Also check for incoming relationships if the relationship is bidirectional
        foreach (var prop in relationshipProperties)
        {
            var elementType = prop.PropertyType.GetGenericArguments()[0];
            var relLabel = GetLabel(elementType);

            // Check if we should process this relationship type
            if (options.RelationshipTypes?.Any() == true && !options.RelationshipTypes.Contains(relLabel))
                continue;

            // Query for incoming relationships of this type
            var cypher = $@"
                MATCH (n)<-[r:{relLabel}]-(m)
                WHERE n.{nameof(Model.INode.Id)} = $nodeId
                RETURN r, m";

            var result = await tx.RunAsync(cypher, new { nodeId = node.Id });

            // First, collect all the data from the cursor
            var records = await result.ToListAsync();

            // Then process each record
            foreach (var record in records)
            {
                var relNode = record["r"].As<global::Neo4j.Driver.IRelationship>();
                var sourceNode = record["m"].As<global::Neo4j.Driver.INode>();

                // Check if this relationship has IsBidirectional property set to true
                if (relNode.Properties.ContainsKey("IsBidirectional") &&
                    relNode.Properties["IsBidirectional"] is bool isBidirectional &&
                    isBidirectional)
                {
                    // Create a new relationship instance for the reverse direction
                    var relationship = Activator.CreateInstance(elementType) as Model.IRelationship;
                    if (relationship != null)
                    {
                        PopulateEntity(relationship, relNode);

                        // For bidirectional, swap source and target
                        var sourceProp = elementType.GetProperty("Source");
                        var targetProp = elementType.GetProperty("Target");

                        if (sourceProp != null && targetProp != null)
                        {
                            // Source is the current node
                            sourceProp.SetValue(relationship, node);

                            // Target is the other node
                            var genericInterface = elementType.GetInterfaces()
                                .FirstOrDefault(i => i.IsGenericType &&
                                                   i.GetGenericTypeDefinition() == typeof(Model.IRelationship<,>));

                            if (genericInterface != null)
                            {
                                var sourceType = genericInterface.GetGenericArguments()[0];
                                var source = Activator.CreateInstance(sourceType) as Model.INode;
                                if (source != null)
                                {
                                    PopulateEntity(source, sourceNode);
                                    targetProp.SetValue(relationship, source);

                                    // Continue traversal for the source node
                                    if (currentDepth + 1 < options.TraversalDepth || options.TraversalDepth == -1)
                                    {
                                        await LoadNodeRelationships(source, options, tx, currentDepth + 1, processedNodes);
                                    }
                                }
                            }
                        }

                        // Add to the existing collection if not already present
                        var existingList = prop.GetValue(node) as IList;
                        if (existingList != null && !existingList.Cast<Model.IRelationship>().Any(r => r.Id == relationship.Id))
                        {
                            existingList.Add(relationship);
                        }
                    }
                }
            }
        }
    }

    internal async Task LoadRelationshipNodes(
        Model.IRelationship relationship,
        GraphOperationOptions options,
        IAsyncTransaction tx)  // Changed from IGraphTransaction to IAsyncTransaction
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
            var source = await GetNodeInternal(sourceType, relationship.SourceId, tx);
            sourceProp.SetValue(relationship, source);

            // Load relationships for source node if depth allows
            if (options.TraversalDepth > 1 || options.TraversalDepth == -1)
            {
                var processedNodes = new HashSet<string>();
                await LoadNodeRelationships(source, options with { TraversalDepth = options.TraversalDepth - 1 },
                    tx, currentDepth: 1, processedNodes);
            }
        }

        if (targetProp != null && !string.IsNullOrEmpty(relationship.TargetId))
        {
            var target = await GetNodeInternal(targetType, relationship.TargetId, tx);
            targetProp.SetValue(relationship, target);

            // Load relationships for target node if depth allows
            if (options.TraversalDepth > 1 || options.TraversalDepth == -1)
            {
                var processedNodes = new HashSet<string>();
                await LoadNodeRelationships(target, options with { TraversalDepth = options.TraversalDepth - 1 },
                    tx, currentDepth: 1, processedNodes);
            }
        }
    }

    private async Task<Model.INode> GetNodeInternal(Type nodeType, string id, IAsyncTransaction tx)
    {
        var label = GetLabel(nodeType);
        var cypher = $"MATCH (n:{label}) WHERE n.{nameof(Model.INode.Id)} = $id RETURN n";

        var result = await tx.RunAsync(cypher, new { id });
        var records = await result.ToListAsync();

        if (!records.Any())
        {
            var ex = new KeyNotFoundException($"Node with ID '{id}' not found");
            throw new GraphException(ex.Message, ex);
        }

        var node = Activator.CreateInstance(nodeType) as Model.INode;
        if (node == null)
        {
            throw new GraphException($"Failed to create instance of type {nodeType.Name}");
        }

        PopulateEntity(node, records[0]["n"].As<global::Neo4j.Driver.INode>());
        return node;
    }

    private void PopulateEntity(object entity, global::Neo4j.Driver.INode neo4jNode)
    {
        var entityType = entity.GetType();
        var properties = entityType.GetProperties(BindingFlags.Public | BindingFlags.Instance);

        // Special handling for INode.Id - always use the Id property from neo4j node
        if (entity is Model.INode node && neo4jNode.Properties.ContainsKey("Id"))
        {
            node.Id = neo4jNode.Properties["Id"].As<string>();
        }

        foreach (var prop in properties)
        {
            // Skip navigation properties (collections of relationships)
            if (prop.PropertyType.IsGenericType &&
                (prop.PropertyType.GetGenericTypeDefinition() == typeof(List<>) ||
                 prop.PropertyType.GetGenericTypeDefinition() == typeof(IList<>) ||
                 prop.PropertyType.GetGenericTypeDefinition() == typeof(ICollection<>)))
            {
                continue;
            }

            // Skip complex properties (other nodes)
            if (prop.PropertyType.IsAssignableTo(typeof(Model.INode)))
            {
                continue;
            }

            // Get the property name (considering PropertyAttribute)
            var propertyName = prop.GetCustomAttribute<PropertyAttribute>()?.Label ?? prop.Name;

            if (neo4jNode.Properties.ContainsKey(propertyName))
            {
                var value = neo4jNode.Properties[propertyName];

                try
                {
                    // Convert Neo4j value to .NET type
                    var convertedValue = ConvertFromNeo4jValue(value, prop.PropertyType);
                    prop.SetValue(entity, convertedValue);
                }
                catch (Exception ex)
                {
                    logger?.LogWarning(ex, $"Failed to set property {prop.Name} on {entityType.Name}");
                }
            }
        }
    }

    private void PopulateEntity(object entity, global::Neo4j.Driver.IRelationship neo4jRelationship)
    {
        var entityType = entity.GetType();
        var properties = entityType.GetProperties(BindingFlags.Public | BindingFlags.Instance);

        foreach (var prop in properties)
        {
            // Skip navigation properties (Source/Target)
            if (prop.Name == "Source" || prop.Name == "Target")
            {
                continue;
            }

            // Skip complex properties
            if (prop.PropertyType.IsAssignableTo(typeof(Model.INode)))
            {
                continue;
            }

            // Get the property name (considering PropertyAttribute)
            var propertyName = prop.GetCustomAttribute<PropertyAttribute>()?.Label ?? prop.Name;

            if (neo4jRelationship.Properties.ContainsKey(propertyName))
            {
                var value = neo4jRelationship.Properties[propertyName];

                try
                {
                    // Convert Neo4j value to .NET type
                    var convertedValue = ConvertFromNeo4jValue(value, prop.PropertyType);
                    prop.SetValue(entity, convertedValue);
                }
                catch (Exception ex)
                {
                    logger?.LogWarning(ex, $"Failed to set property {prop.Name} on {entityType.Name}");
                }
            }
        }
    }

    private object? ConvertFromNeo4jValue(object? neo4jValue, Type targetType)
    {
        if (neo4jValue == null)
            return null;

        // Handle nullable types
        var underlyingType = Nullable.GetUnderlyingType(targetType);
        if (underlyingType != null)
        {
            targetType = underlyingType;
        }

        // Direct type match
        if (neo4jValue.GetType() == targetType)
            return neo4jValue;

        // String
        if (targetType == typeof(string))
            return neo4jValue.ToString();

        // Numbers
        if (targetType == typeof(int))
            return Convert.ToInt32(neo4jValue);
        if (targetType == typeof(long))
            return Convert.ToInt64(neo4jValue);
        if (targetType == typeof(double))
            return Convert.ToDouble(neo4jValue);
        if (targetType == typeof(float))
            return Convert.ToSingle(neo4jValue);
        if (targetType == typeof(decimal))
            return Convert.ToDecimal(neo4jValue);

        // Boolean
        if (targetType == typeof(bool))
            return Convert.ToBoolean(neo4jValue);

        // DateTime
        if (targetType == typeof(DateTime))
        {
            if (neo4jValue is ZonedDateTime zdt)
                return zdt.ToDateTimeOffset().DateTime;
            if (neo4jValue is LocalDateTime ldt)
                return ldt.ToDateTime();
            if (neo4jValue is LocalDate ld)
                return ld.ToDateTime();
            return Convert.ToDateTime(neo4jValue);
        }

        // DateTimeOffset
        if (targetType == typeof(DateTimeOffset))
        {
            if (neo4jValue is ZonedDateTime zdt)
                return zdt.ToDateTimeOffset();
            if (neo4jValue is LocalDateTime ldt)
                return new DateTimeOffset(ldt.ToDateTime());
            return new DateTimeOffset(Convert.ToDateTime(neo4jValue));
        }

        // Guid
        if (targetType == typeof(Guid))
        {
            return Guid.Parse(neo4jValue.ToString()!);
        }

        // Enums
        if (targetType.IsEnum)
        {
            if (neo4jValue is string strValue)
                return Enum.Parse(targetType, strValue);
            return Enum.ToObject(targetType, neo4jValue);
        }

        // Arrays and Lists
        if (targetType.IsArray)
        {
            var elementType = targetType.GetElementType();
            if (neo4jValue is IList neo4jList)
            {
                var array = Array.CreateInstance(elementType!, neo4jList.Count);
                for (int i = 0; i < neo4jList.Count; i++)
                {
                    array.SetValue(ConvertFromNeo4jValue(neo4jList[i], elementType!), i);
                }
                return array;
            }
        }

        if (targetType.IsGenericType && targetType.GetGenericTypeDefinition() == typeof(List<>))
        {
            var elementType = targetType.GetGenericArguments()[0];
            if (neo4jValue is IList neo4jList)
            {
                var list = Activator.CreateInstance(targetType) as IList;
                foreach (var item in neo4jList)
                {
                    list?.Add(ConvertFromNeo4jValue(item, elementType));
                }
                return list;
            }
        }

        // Complex types (deserialize from JSON)
        if (neo4jValue is string jsonString && IsComplexType(targetType))
        {
            try
            {
                return System.Text.Json.JsonSerializer.Deserialize(jsonString, targetType);
            }
            catch
            {
                logger?.LogWarning($"Failed to deserialize JSON to {targetType.Name}: {jsonString}");
                return null;
            }
        }

        // Try type converter as last resort
        try
        {
            var converter = System.ComponentModel.TypeDescriptor.GetConverter(targetType);
            if (converter.CanConvertFrom(neo4jValue.GetType()))
            {
                return converter.ConvertFrom(neo4jValue);
            }
        }
        catch
        {
            // Ignore conversion errors
        }

        throw new NotSupportedException($"Cannot convert Neo4j value of type {neo4jValue.GetType()} to {targetType}");
    }

    private bool IsComplexType(Type type)
    {
        return !type.IsPrimitive &&
               !type.IsEnum &&
               type != typeof(string) &&
               type != typeof(DateTime) &&
               type != typeof(DateTimeOffset) &&
               type != typeof(TimeSpan) &&
               type != typeof(Guid) &&
               type != typeof(decimal) &&
               !type.IsArray &&
               !(type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>));
    }

    private async Task<T> GetNodeInternal<T>(string id, IAsyncTransaction tx)
        where T : Model.INode, new()
    {
        return (T)await GetNodeInternal(typeof(T), id, tx);
    }

    private async Task<R> GetRelationshipInternal<R>(string id, IAsyncTransaction tx)
        where R : Model.IRelationship, new()
    {
        var label = GetLabel(typeof(R));
        var cypher = $"MATCH ()-[r:{label}]->() WHERE r.{nameof(Model.IRelationship.Id)} = $id RETURN r";

        var result = await tx.RunAsync(cypher, new { id });
        var records = await result.ToListAsync();

        if (!records.Any())
        {
            var ex = new KeyNotFoundException($"Relationship with ID '{id}' not found");
            throw new GraphException(ex.Message, ex);
        }

        var relationship = new R();
        PopulateEntity(relationship, records[0]["r"].As<global::Neo4j.Driver.IRelationship>());
        return relationship;
    }
}
