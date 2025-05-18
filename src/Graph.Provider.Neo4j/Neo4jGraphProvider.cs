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

using System.Linq.Expressions;
using Cvoya.Graph.Provider.Model;
using Microsoft.Extensions.Logging;
using Neo4j.Driver;
using Neo4jNode = Neo4j.Driver.INode;
using Neo4jRelationship = Neo4j.Driver.IRelationship;

namespace Cvoya.Graph.Client.Neo4j;

/// <summary>
/// Neo4j implementation of the IGraphProvider interface.
/// </summary>
public class Neo4jGraphProvider : IGraphProvider
{
    private readonly IDriver _driver;
    private readonly Microsoft.Extensions.Logging.ILogger? _logger;

    // Tracks labels/types for which constraints have been created
    private readonly HashSet<string> _constrainedLabels = new();
    private readonly object _constraintLock = new();
    private bool _constraintsLoaded = false;

    public Neo4jGraphProvider(string uri, string user, string password, Microsoft.Extensions.Logging.ILogger? logger = null)
    {
        _driver = GraphDatabase.Driver(uri, AuthTokens.Basic(user, password));
        _logger = logger;
        // Load constraints at startup
        Task.Run(() => LoadExistingConstraintsAsync()).Wait();
    }

    private async Task LoadExistingConstraintsAsync()
    {
        if (_constraintsLoaded) return;
        lock (_constraintLock)
        {
            if (_constraintsLoaded) return;
            _constraintsLoaded = true;
        }
        try
        {
            await using var session = _driver.AsyncSession();
            var cypher = "SHOW CONSTRAINTS";
            var cursor = await session.RunAsync(cypher);
            while (await cursor.FetchAsync())
            {
                var record = cursor.Current;
                if (record.Values.TryGetValue("labelsOrTypes", out var labelsOrTypesObj) && labelsOrTypesObj is IEnumerable<object> labelsOrTypes)
                {
                    foreach (var label in labelsOrTypes)
                    {
                        if (label is string s)
                            _constrainedLabels.Add(s);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to load existing constraints from Neo4j.");
        }
    }

    private async Task EnsureConstraintsForLabelAsync(string label, IEnumerable<string> propertyNames, Type? nodeType = null)
    {
        await LoadExistingConstraintsAsync();
        lock (_constraintLock)
        {
            if (_constrainedLabels.Contains(label))
                return;
            _constrainedLabels.Add(label);
        }
        await using var session = _driver.AsyncSession();
        // Always add unique constraint for Id
        var cypher = $"CREATE CONSTRAINT IF NOT EXISTS FOR (n:`{label}`) REQUIRE n.Id IS UNIQUE";
        await session.RunAsync(cypher);
        // Add property existence constraints only for primitive properties
        if (nodeType != null)
        {
            foreach (var prop in nodeType.GetProperties())
            {
                if (prop.Name == nameof(Provider.Model.INode.Id)) continue;
                if (prop.PropertyType.IsValueType || prop.PropertyType == typeof(string))
                {
                    var propCypher = $"CREATE CONSTRAINT IF NOT EXISTS FOR (n:`{label}`) REQUIRE n.{prop.Name} IS NOT NULL";
                    await session.RunAsync(propCypher);
                }
            }
        }
        else
        {
            foreach (var prop in propertyNames)
            {
                if (prop == nameof(Provider.Model.INode.Id)) continue;
                var propCypher = $"CREATE CONSTRAINT IF NOT EXISTS FOR (n:`{label}`) REQUIRE n.{prop} IS NOT NULL";
                await session.RunAsync(propCypher);
            }
        }
    }

    public async Task<IGraphTransaction> BeginTransactionAsync()
    {
        var session = _driver.AsyncSession();
        var tx = await session.BeginTransactionAsync();
        return new Neo4jGraphTransaction(session, tx);
    }

    internal async Task<(IAsyncSession, IAsyncTransaction)> GetOrCreateTransactionAsync(IGraphTransaction? transaction)
    {
        if (transaction is Neo4jGraphTransaction neo4jTx && neo4jTx.IsActive)
        {
            var tx = neo4jTx.GetTransaction() ?? throw new InvalidOperationException("Transaction is not active.");
            return (neo4jTx.Session, tx);
        }
        else if (transaction == null)
        {
            var session = _driver.AsyncSession();
            var tx = await session.BeginTransactionAsync();
            return (session, tx);
        }
        else
        {
            throw new InvalidOperationException("Transaction is not active or not a Neo4j transaction.");
        }
    }

    public async Task<T> CreateNode<T>(T node, IGraphTransaction? transaction = null) where T : Provider.Model.INode, new()
    {
        var result = await CreateNodes(new[] { node }, transaction);
        return result.Single();
    }

    public async Task<IEnumerable<T>> CreateNodes<T>(IEnumerable<T> nodes, IGraphTransaction? transaction = null) where T : Provider.Model.INode, new()
    {
        var nodeList = nodes.ToList();
        if (nodeList.Count == 0) return nodeList;

        var label = this.TypeNameToLabel<T>();
        var propertyNames = typeof(T).GetProperties().Select(p => p.Name).ToList();
        await EnsureConstraintsForLabelAsync(label, propertyNames, typeof(T));

        // Cycle detection
        foreach (var node in nodeList)
        {
            var visited = new HashSet<object>();
            if (HasReferenceCycle(node, visited))
                throw new GraphProviderException($"Reference cycle detected in node of type {typeof(T).FullName}");
        }

        var (session, tx) = await GetOrCreateTransactionAsync(transaction);
        var createdNodes = new List<T>();
        try
        {
            foreach (var node in nodeList)
            {
                // 1. Serialize primitive and reference properties, and collect complex property nodes
                var (props, complexNodes) = Neo4jEntitySerializer.SerializeNodeWithComplexProperties(node);

                // 2. Recursively create complex property nodes first
                foreach (var (propertyNode, propertyName) in complexNodes)
                {
                    var propertyNodeType = propertyNode.GetType();
                    var propertyLabel = this.TypeNameToLabel(propertyNodeType);
                    var propertyPropertyNames = propertyNodeType.GetProperties().Select(p => p.Name).ToList();
                    await EnsureConstraintsForLabelAsync(propertyLabel, propertyPropertyNames, propertyNodeType);
                    // Recursively create the property node if it doesn't exist
                    var propertyId = propertyNodeType.GetProperty("Identifier")?.GetValue(propertyNode)?.ToString();
                    var existsCypher = $"MATCH (n:`{propertyLabel}`) WHERE n.Id = $id RETURN n";
                    var existsCursor = await tx.RunAsync(existsCypher, new { id = propertyId });
                    if (!await existsCursor.FetchAsync())
                    {
                        // Create the property node
                        var (propertyProps, _) = Neo4jEntitySerializer.SerializeNodeWithComplexProperties(propertyNode);
                        var createCypher = $"CREATE (n:{propertyLabel} $props) RETURN n";
                        await tx.RunAsync(createCypher, new { props = propertyProps });
                    }
                }

                // 3. Create the main node
                var cypher = $"CREATE (n:{label} $props) RETURN n";
                var cursor = await tx.RunAsync(cypher, new { props });
                var record = await cursor.SingleAsync();
                var n = record["n"].As<Neo4jNode>();
                var created = (T)Neo4jEntityDeserializer.DeserializeNode(typeof(T), n);
                createdNodes.Add(created);

                // 4. Create relationships from the main node to each complex property node
                var mainId = node.Id;
                foreach (var (propertyNode, propertyName) in complexNodes)
                {
                    var propertyNodeType = propertyNode.GetType();
                    var propertyLabel = this.TypeNameToLabel(propertyNodeType);
                    var propertyId = propertyNodeType.GetProperty("Identifier")?.GetValue(propertyNode)?.ToString();
                    var relType = propertyName;
                    var relCypher = $@"
                        MATCH (a:`{label}`), (b:`{propertyLabel}`)
                        WHERE a.Id = $mainId AND b.Id = $propertyId
                        MERGE (a)-[r:`{relType}`]->(b)
                        RETURN r
                    ";
                    await tx.RunAsync(relCypher, new { mainId, propertyId });
                }
            }

            if (transaction == null)
            {
                // Commit the transaction if we are not in an explicit transaction
                await tx.CommitAsync();
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to create nodes.");
            throw new GraphProviderException("Failed to create nodes.", ex);
        }
        finally
        {
            // Close the session only if we automatically created it
            if (transaction == null)
            {
                await session.CloseAsync();
            }
        }
        return createdNodes;
    }

    // Helper: Detect reference cycles in the object graph
    private bool HasReferenceCycle(object obj, HashSet<object> visited)
    {
        if (obj == null || obj is string || obj.GetType().IsValueType)
        {
            return false;
        }

        if (!visited.Add(obj))
        {
            return true;
        }

        foreach (var prop in obj.GetType().GetProperties())
        {
            var value = prop.GetValue(obj);
            if (value == null) continue;
            if (typeof(System.Collections.IEnumerable).IsAssignableFrom(prop.PropertyType) && prop.PropertyType != typeof(string))
            {
                var enumerable = value as System.Collections.IEnumerable;
                if (enumerable != null)
                {
                    foreach (var item in enumerable)
                    {
                        if (item != null && HasReferenceCycle(item, visited))
                            return true;
                    }
                }
            }
            else if (!prop.PropertyType.IsValueType && prop.PropertyType != typeof(string))
            {
                if (value != null && HasReferenceCycle(value, visited))
                    return true;
            }
        }
        visited.Remove(obj);

        return false;
    }

    public async Task<R> CreateRelationshipAsync<R, S, T>(R relationship, S sourceNode, T targetNode, IGraphTransaction? transaction = null)
        where R : Cvoya.Graph.Provider.Model.IRelationship<S, T>, new()
        where S : Cvoya.Graph.Provider.Model.INode
        where T : Cvoya.Graph.Provider.Model.INode
    {
        var result = await CreateRelationshipsAsync([(relationship, sourceNode, targetNode)], transaction);
        return result.Single();
    }

    private async Task<IEnumerable<R>> CreateRelationshipsAsync<R, S, T>(IEnumerable<(R relationship, S sourceNode, T targetNode)> relationships, IGraphTransaction? transaction = null)
        where R : Cvoya.Graph.Provider.Model.IRelationship<S, T>
        where S : Cvoya.Graph.Provider.Model.INode
        where T : Cvoya.Graph.Provider.Model.INode
    {
        // Check for complex properties
        var relType = typeof(R);
        foreach (var prop in relType.GetProperties())
        {
            if (!prop.PropertyType.IsPrimitive && prop.PropertyType != typeof(string) && !prop.PropertyType.IsEnum && !prop.PropertyType.IsValueType)
            {
                throw new InvalidOperationException($"IRelationship implementation '{relType.FullName}' contains non-primitive property '{prop.Name}' of type '{prop.PropertyType.Name}'. Neo4j relationships cannot have complex properties.");
            }
        }

        if (relationships.Count() == 0) return Enumerable.Empty<R>();

        var label = this.TypeNameToLabel<R>();
        var propertyNames = typeof(R).GetProperties().Select(p => p.Name).ToList();
        await EnsureConstraintsForLabelAsync(label, propertyNames);

        // Cycle detection
        foreach (var (relationship, sourceNode, targetNode) in relationships)
        {
            var visited = new HashSet<object>();
            if (HasReferenceCycle(relationship, visited))
                throw new GraphProviderException($"Reference cycle detected in relationship of type {typeof(R).FullName}");
        }

        var (session, tx) = await GetOrCreateTransactionAsync(transaction);
        var createdRels = new List<R>();
        try
        {
            foreach (var (relationship, sourceNode, targetNode) in relationships)
            {
                // Ensure SourceId and TargetId are set
                var sourceIdProp = relationship.GetType().GetProperty("SourceId");
                var targetIdProp = relationship.GetType().GetProperty("TargetId");
                if (sourceIdProp != null && sourceIdProp.CanWrite)
                    sourceIdProp.SetValue(relationship, sourceNode.Id);
                if (targetIdProp != null && targetIdProp.CanWrite)
                    targetIdProp.SetValue(relationship, targetNode.Id);
                var props = Neo4jEntitySerializer.SerializeProperties(relationship);
                // Use source and target node Ids
                var cypher = $@"
                    MATCH (a), (b)
                    WHERE a.Id = $sourceId AND b.Id = $targetId
                    MERGE (a)-[r:`{label}`]->(b)
                    SET r += $props
                    RETURN r
                ";
                var cursor = await tx.RunAsync(cypher, new { sourceId = relationship.Source.Id, targetId = relationship.Target.Id, props });
                if (!await cursor.FetchAsync())
                {
                    throw new GraphProviderException($"Failed to create relationship: source or target node not found (sourceId={relationship.Source.Id}, targetId={relationship.Target.Id})");
                }
                var r = cursor.Current["r"].As<Neo4jRelationship>();
                var created = DeserializeRelationship<R, S, T>(r);
                createdRels.Add(created);
            }
            if (transaction == null)
            {
                // Commit the transaction if we are not in an explicit transaction
                await tx.CommitAsync();
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to create relationships.");
            throw new GraphProviderException("Failed to create relationships.", ex);
        }
        finally
        {
            if (transaction == null)
                await session.CloseAsync();
        }
        return createdRels;
    }

    // Helper: Deserialize Neo4j IRelationship to R
    private R DeserializeRelationship<R, S, T>(Neo4jRelationship rel)
        where R : Cvoya.Graph.Provider.Model.IRelationship<S, T>
        where S : Cvoya.Graph.Provider.Model.INode
        where T : Cvoya.Graph.Provider.Model.INode
    {
        // Use non-generic overload to avoid compile-time constraints
        return (R)Neo4jEntityDeserializer.DeserializeRelationship(typeof(R), rel);
    }

    public async Task<T> GetNodeAsync<T>(string id, IGraphTransaction? transaction = null) where T : Provider.Model.INode, new()
    {
        if (string.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));
        var (session, tx) = await GetOrCreateTransactionAsync(transaction);
        try
        {
            var label = this.TypeNameToLabel<T>();
            var cypher = $"MATCH (n:`{label}`) WHERE n.Id = $id RETURN n";
            var cursor = await tx.RunAsync(cypher, new { id });
            var record = await cursor.SingleAsync();
            var n = record["n"].As<Neo4jNode>();
            // Use new deserializer to hydrate complex properties
            var result = (T)await Neo4jEntityDeserializer.DeserializeNodeWithComplexPropertiesAsync(typeof(T), n, _driver);
            return result;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to get node.");
            throw new GraphProviderException("Failed to get node.", ex);
        }
        finally
        {
            if (transaction == null)
            {
                await session.CloseAsync();
            }
        }
    }

    public async Task<IEnumerable<T>> GetNodesAsync<T>(IEnumerable<string> ids, IGraphTransaction? transaction = null) where T : Provider.Model.INode, new()
    {
        ArgumentNullException.ThrowIfNull(ids);
        var idList = ids.ToList();
        if (idList.Count == 0) return Enumerable.Empty<T>();
        var (session, tx) = await GetOrCreateTransactionAsync(transaction);
        try
        {
            var label = this.TypeNameToLabel<T>();
            var cypher = $"MATCH (n:`{label}`) WHERE n.Id IN $ids RETURN n";
            var cursor = await tx.RunAsync(cypher, new { ids = idList });
            var nodes = new List<T>();
            while (await cursor.FetchAsync())
            {
                var n = cursor.Current["n"].As<Neo4jNode>();
                var hydrated = (T)await Neo4jEntityDeserializer.DeserializeNodeWithComplexPropertiesAsync(typeof(T), n, _driver);
                nodes.Add(hydrated);
            }
            return nodes;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to get nodes.");
            throw new GraphProviderException("Failed to get nodes.", ex);
        }
        finally
        {
            if (transaction == null)
            {
                await session.CloseAsync();
            }
        }
    }

    public async Task DeleteNodeAsync(string id, IGraphTransaction? transaction = null)
    {
        if (string.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));
        var (session, tx) = await GetOrCreateTransactionAsync(transaction);
        try
        {
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
            _logger?.LogError(ex, "Failed to delete node.");
            throw new GraphProviderException("Failed to delete node.", ex);
        }
        finally
        {
            if (transaction == null)
            {
                await session.CloseAsync();
            }
        }
    }

    public async Task<T> GetRelationshipAsync<T, S, U>(string id, IGraphTransaction? transaction = null)
        where T : Cvoya.Graph.Provider.Model.IRelationship<S, U>, new()
        where S : Cvoya.Graph.Provider.Model.INode
        where U : Cvoya.Graph.Provider.Model.INode
    {
        if (string.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));
        var (session, tx) = await GetOrCreateTransactionAsync(transaction);
        try
        {
            var label = this.TypeNameToLabel<T>();
            var cypher = $"MATCH ()-[r:`{label}`]->() WHERE r.Id = $id RETURN r";
            var cursor = await tx.RunAsync(cypher, new { id });
            var record = await cursor.SingleAsync();
            var r = record["r"].As<Neo4jRelationship>();
            return DeserializeRelationship<T, S, U>(r);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to get relationship.");
            throw new GraphProviderException("Failed to get relationship.", ex);
        }
        finally
        {
            if (transaction == null)
            {
                await session.CloseAsync();
            }
        }
    }

    public async Task<IEnumerable<T>> GetRelationshipsAsync<T, S, U>(IEnumerable<string> ids, IGraphTransaction? transaction = null)
        where T : Cvoya.Graph.Provider.Model.IRelationship<S, U>, new()
        where S : Cvoya.Graph.Provider.Model.INode
        where U : Cvoya.Graph.Provider.Model.INode
    {
        ArgumentNullException.ThrowIfNull(ids);
        var idList = ids.ToList();
        if (idList.Count == 0) return Enumerable.Empty<T>();
        var (session, tx) = await GetOrCreateTransactionAsync(transaction);
        try
        {
            var label = this.TypeNameToLabel<T>();
            var cypher = $"MATCH ()-[r:`{label}`]->() WHERE r.Id IN $ids RETURN r";
            var cursor = await tx.RunAsync(cypher, new { ids = idList });
            var rels = new List<T>();
            while (await cursor.FetchAsync())
            {
                var r = cursor.Current["r"].As<Neo4jRelationship>();
                rels.Add(DeserializeRelationship<T, S, U>(r));
            }
            return rels;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to get relationships.");
            throw new GraphProviderException("Failed to get relationships.", ex);
        }
        finally
        {
            if (transaction == null)
            {
                await session.CloseAsync();
            }
        }
    }

    public async Task DeleteRelationshipAsync(string id, IGraphTransaction? transaction = null)
    {
        if (string.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));
        var (session, tx) = await GetOrCreateTransactionAsync(transaction);
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
            _logger?.LogError(ex, "Failed to delete relationship.");
            throw new GraphProviderException("Failed to delete relationship.", ex);
        }
        finally
        {
            if (transaction == null)
            {
                await session.CloseAsync();
            }
        }
    }

    public void Dispose()
    {
        _driver?.Dispose();
    }

    public IQueryable<T> Query<T>(IGraphTransaction? transaction = null) where T : Provider.Model.INode, new()
    {
        return new Neo4jQueryable<T>(new Neo4jCypherQueryProvider(this, transaction), Expression.Constant(new Neo4jQueryable<T>(null!, null!)));
    }

    public async Task<T> UpdateNodeAsync<T>(T node, IGraphTransaction? transaction = null) where T : Provider.Model.INode, new()
    {
        if (node == null) throw new ArgumentNullException(nameof(node));
        var (session, tx) = await GetOrCreateTransactionAsync(transaction);
        try
        {
            // TODO: Update complex properties as well

            var label = this.TypeNameToLabel<T>();
            // Only update primitive properties
            var props = node.GetType().GetProperties()
                .Where(p => p.PropertyType.IsValueType || p.PropertyType == typeof(string))
                .ToDictionary(p => p.Name, p => p.GetValue(node));
            var cypher = $"MATCH (n:`{label}`) WHERE n.Id = $id SET n += $props RETURN n";
            var cursor = await tx.RunAsync(cypher, new { id = node.Id, props });
            var record = await cursor.SingleAsync();
            var n = record["n"].As<Neo4jNode>();

            var updatedNode = Neo4jEntityDeserializer.DeserializeNode<T>(n);
            if (transaction == null)
            {
                // We created the transaction, so we need to commit it
                await tx.CommitAsync();
            }

            return updatedNode;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to update node.");
            throw new GraphProviderException("Failed to update node.", ex);
        }
        finally
        {
            if (transaction == null)
            {
                await session.CloseAsync();
            }
        }
    }

    public async Task<R> UpdateRelationshipAsync<R, S, T>(R relationship, IGraphTransaction? transaction = null)
        where R : Cvoya.Graph.Provider.Model.IRelationship<S, T>, new()
        where S : Cvoya.Graph.Provider.Model.INode
        where T : Cvoya.Graph.Provider.Model.INode
    {
        if (relationship == null) throw new ArgumentNullException(nameof(relationship));
        var (session, tx) = await GetOrCreateTransactionAsync(transaction);
        try
        {
            var label = this.TypeNameToLabel<R>();
            var props = Neo4jEntitySerializer.SerializeProperties(relationship);
            var cypher = $"MATCH ()-[r:`{label}`]->() WHERE r.Id = $id SET r += $props RETURN r";
            var cursor = await tx.RunAsync(cypher, new { id = relationship.Id, props });
            var record = await cursor.SingleAsync();
            var r = record["r"].As<Neo4jRelationship>();
            var updatedRelationship = Neo4jEntityDeserializer.DeserializeRelationship<R>(r);
            if (transaction == null)
            {
                // We created the transaction, so we need to commit it
                await tx.CommitAsync();
            }

            return updatedRelationship;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to update relationship.");
            throw new GraphProviderException("Failed to update relationship.", ex);
        }
        finally
        {
            if (transaction == null)
            {
                await session.CloseAsync();
            }
        }
    }

    /// <summary>
    /// Execute a raw Cypher query with parameters and return results as dynamic objects.
    /// </summary>
    public async Task<IEnumerable<dynamic>> ExecuteCypher(string cypher, object? parameters = null, IGraphTransaction? transaction = null)
    {
        var (session, tx) = await GetOrCreateTransactionAsync(transaction);
        var results = new List<dynamic>();
        try
        {
            var cursor = await tx.RunAsync(cypher, parameters);
            while (await cursor.FetchAsync())
            {
                var record = cursor.Current;
                results.Add(record.Values);
            }

            if (transaction == null)
            {
                // We created the transaction, so we need to commit it
                await tx.CommitAsync();
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to execute Cypher query.");
            throw new GraphProviderException("Failed to execute Cypher query.", ex);
        }
        finally
        {
            if (transaction == null)
            {
                await session.CloseAsync();
            }
        }
        return results;
    }

    async Task<IEnumerable<dynamic>> IGraphProvider.ExecuteCypher(string cypher, object? parameters, IGraphTransaction? transaction)
    {
        return await ExecuteCypher(cypher, parameters, transaction);
    }

    private string TypeNameToLabel<T>() => TypeNameToLabel(typeof(T));
    private string TypeNameToLabel(Type type)
    {
        var typeName = type.FullName ?? type.Name;
        var label = typeName.Replace('.', '_').Replace('+', '_');
        return label;
    }

    public IQueryable<N> Nodes<N>(IGraphTransaction? transaction = null) where N : Provider.Model.INode, new()
    {
        throw new NotImplementedException();
    }

    public IQueryable<R> Relationships<R, S, T>(IGraphTransaction? transaction = null)
        where R : IRelationship<S, T>, new()
        where S : Provider.Model.INode
        where T : Provider.Model.INode
    {
        throw new NotImplementedException();
    }

    public Task<T> GetNode<T>(string id, IGraphTransaction? transaction = null) where T : Provider.Model.INode, new()
    {
        throw new NotImplementedException();
    }

    public Task<IEnumerable<T>> GetNodes<T>(IEnumerable<string> ids, IGraphTransaction? transaction = null) where T : Provider.Model.INode, new()
    {
        throw new NotImplementedException();
    }

    public Task<T> GetRelationship<T, S, U>(string id, IGraphTransaction? transaction = null)
        where T : IRelationship<S, U>, new()
        where S : Provider.Model.INode
        where U : Provider.Model.INode
    {
        throw new NotImplementedException();
    }

    public Task<IEnumerable<T>> GetRelationships<T, S, U>(IEnumerable<string> ids, IGraphTransaction? transaction = null)
        where T : IRelationship<S, U>, new()
        where S : Provider.Model.INode
        where U : Provider.Model.INode
    {
        throw new NotImplementedException();
    }

    public Task<R> CreateRelationship<R, S, T>(R relationship, IGraphTransaction? transaction = null)
        where R : IRelationship<S, T>, new()
        where S : Provider.Model.INode
        where T : Provider.Model.INode
    {
        throw new NotImplementedException();
    }

    public Task<T> UpdateNode<T>(T node, IGraphTransaction? transaction = null) where T : Provider.Model.INode, new()
    {
        throw new NotImplementedException();
    }

    public Task<R> UpdateRelationship<R, S, T>(R relationship, IGraphTransaction? transaction = null)
        where R : IRelationship<S, T>, new()
        where S : Provider.Model.INode
        where T : Provider.Model.INode
    {
        throw new NotImplementedException();
    }

    public Task<IGraphTransaction> BeginTransaction()
    {
        throw new NotImplementedException();
    }

    public Task DeleteNode(string id, IGraphTransaction? transaction = null)
    {
        throw new NotImplementedException();
    }

    public Task DeleteRelationship(string id, IGraphTransaction? transaction = null)
    {
        throw new NotImplementedException();
    }
}
