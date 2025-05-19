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

using Cvoya.Graph.Provider.Model;
using Microsoft.Extensions.Logging;
using Neo4j.Driver;
using Neo4j.Driver.Mapping;

namespace Cvoya.Graph.Provider.Neo4j;

/// <summary>
/// Neo4j implementation of the IGraphProvider interface.
/// </summary>
public class Neo4jGraphProvider : IGraphProvider
{
    private readonly Lock disposeLock = new();
    private bool disposed = false;
    private readonly Microsoft.Extensions.Logging.ILogger? logger;

    private readonly IDriver driver;

    // Tracks labels/types for which constraints have been created
    private readonly HashSet<string> _constrainedLabels = [];
    private readonly Lock constraintLock = new();
    private bool _constraintsLoaded = false;


    public Neo4jGraphProvider(
        string? uri = null,
        string? username = null,
        string? password = null,
        Microsoft.Extensions.Logging.ILogger? logger = null)
    {
        this.logger = logger;
        uri ??= Environment.GetEnvironmentVariable("NEO4J_URI") ?? "bolt://localhost:7687";
        username ??= Environment.GetEnvironmentVariable("NEO4J_USER") ?? "neo4j";
        password ??= Environment.GetEnvironmentVariable("NEO4J_PASSWORD") ?? "password";
        this.driver = GraphDatabase.Driver(uri, AuthTokens.Basic(username, password));

        Task.Run(() => LoadExistingConstraintsAsync()).Wait();
    }

    public IQueryable<N> Nodes<N>(IGraphTransaction? transaction = null)
        where N : Model.INode, new()
    {
        // Stub: LINQ provider for querying nodes will be implemented later
        throw new NotImplementedException();
    }

    public IQueryable<R> Relationships<R, S, T>(IGraphTransaction? transaction = null)
        where R : IRelationship<S, T>, new()
        where S : Model.INode
        where T : Model.INode
    {
        // Stub: LINQ provider for querying relationships will be implemented later
        throw new NotImplementedException();
    }

    public async Task<T> GetNode<T>(string id, IGraphTransaction? transaction = null)
        where T : Model.INode, new()
    {
        if (string.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));

        var nodes = await this.GetNodes<T>([id], transaction);

        return nodes.FirstOrDefault() ?? throw new KeyNotFoundException($"Node with ID {id} not found.");
    }

    public async Task<IEnumerable<T>> GetNodes<T>(IEnumerable<string> ids, IGraphTransaction? transaction = null)
        where T : Model.INode, new()
    {
        var label = Neo4jGraphProvider.TypeNameToLabel<T>();

        if (ids.Any(string.IsNullOrEmpty))
        {
            throw new ArgumentNullException(nameof(ids), "One or more IDs are null or empty.");
        }

        var whereInClause = string.Join(", ", ids.Select(id => $"'{id}'"));
        var cypher = $"MATCH (n:{label}) WHERE n.Id IN [{whereInClause}] RETURN n";
        var results = new List<T>();

        var (session, tx) = await GetOrCreateTransactionAsync(transaction);
        try
        {
            var cursor = await tx.RunAsync(cypher);
            while (await cursor.FetchAsync())
            {
                var created = cursor.Current["n"].As<global::Neo4j.Driver.INode>().Properties.FromDictionary<T>();
                results.Add(created);
            }

            return results;
        }
        catch (Exception ex)
        {
            throw new GraphProviderException($"Error retrieving node one or more of the nodes with IDs '{whereInClause}'", ex);
        }
        finally
        {
            if (transaction == null)
            {
                await session.CloseAsync();
            }
        }
    }

    public Task<T> GetRelationship<T, S, U>(string id, IGraphTransaction? transaction = null)
        where T : IRelationship<S, U>, new()
        where S : Model.INode
        where U : Model.INode
    {
        throw new NotImplementedException();
    }

    public Task<IEnumerable<T>> GetRelationships<T, S, U>(IEnumerable<string> ids, IGraphTransaction? transaction = null)
        where T : IRelationship<S, U>, new()
        where S : Model.INode
        where U : Model.INode
    {
        throw new NotImplementedException();
    }

    public async Task<T> CreateNode<T>(T node, IGraphTransaction? transaction = null)
        where T : Model.INode, new()
    {
        if (node == null) throw new ArgumentNullException(nameof(node));

        // Cycle detection
        var visited = new HashSet<object>();
        if (HasReferenceCycle(node, visited))
        {
            throw new GraphProviderException($"Reference cycle detected in relationship of type {typeof(T).FullName}");
        }

        var label = Neo4jGraphProvider.TypeNameToLabel<T>();
        var propertyNames = typeof(T).GetProperties().Select(p => p.Name).ToList();
        await EnsureConstraintsForLabelAsync(label, propertyNames, typeof(T));

        var cypher = $"CREATE (n:{label} $props) RETURN n";
        var (session, tx) = await GetOrCreateTransactionAsync(transaction);

        try
        {
            var result = await tx.RunAsync(cypher, new { props = node.ToDictionary() });
            var record = await result.SingleAsync();
            var createdNode = record["n"].As<global::Neo4j.Driver.INode>().Properties.FromDictionary<T>();
            if (transaction == null)
            {
                // Commit the transaction if we are not in an explicit transaction
                await tx.CommitAsync();
            }
            return createdNode;
        }
        catch (Exception ex)
        {
            throw new GraphProviderException($"Error creating node of type '{typeof(T).Name}'", ex);
        }
        finally
        {
            if (transaction == null)
            {
                await session.CloseAsync();
            }
        }
    }

    public async Task<R> CreateRelationship<R, S, T>(R relationship, IGraphTransaction? transaction = null)
        where R : IRelationship<S, T>, new()
        where S : Model.INode
        where T : Model.INode
    {
        if (relationship == null) throw new ArgumentNullException(nameof(relationship));

        // Check for complex properties
        var relType = typeof(R);
        foreach (var prop in relType.GetProperties())
        {
            if (!prop.PropertyType.IsPrimitive && prop.PropertyType != typeof(string) && !prop.PropertyType.IsEnum && !prop.PropertyType.IsValueType)
            {
                throw new GraphProviderException($"IRelationship implementation '{relType.FullName}' contains non-primitive property '{prop.Name}' of type '{prop.PropertyType.Name}'. Relationships cannot have complex properties.");
            }
        }

        // Cycle detection
        var visited = new HashSet<object>();
        if (HasReferenceCycle(relationship, visited))
        {
            throw new GraphProviderException($"Reference cycle detected in relationship of type {typeof(R).FullName}");
        }

        var label = Neo4jGraphProvider.TypeNameToLabel<T>();
        var propertyNames = typeof(T).GetProperties().Select(p => p.Name).ToList();
        await EnsureConstraintsForLabelAsync(label, propertyNames, typeof(T));

        var props = relationship.ToDictionary();

        var cypher = $@"
                MATCH (a), (b)
                WHERE a.Id = $sourceId AND b.Id = $targetId
                MERGE (a)-[r:`{label}`]->(b)
                SET r += $props
                RETURN r
            ";

        var (session, tx) = await GetOrCreateTransactionAsync(transaction);
        try
        {
            var cursor = await tx.RunAsync(cypher, new { sourceId = relationship.Source.Id, targetId = relationship.Target.Id, props });
            if (!await cursor.FetchAsync())
            {
                throw new GraphProviderException($"Failed to create relationship: source or target node not found (sourceId={relationship.Source.Id}, targetId={relationship.Target.Id})");
            }

            var createdRelationship = cursor.Current["r"].As<global::Neo4j.Driver.IRelationship>().Properties.FromDictionary<R>();
            if (transaction == null)
            {
                // Commit the transaction if we are not in an explicit transaction
                await tx.CommitAsync();
            }
            return createdRelationship;
        }
        catch (Exception ex)
        {
            throw new GraphProviderException($"Error creating relationship of type '{typeof(R).Name}'", ex);
        }
        finally
        {
            if (transaction == null)
            {
                await session.CloseAsync();
            }
        }
    }

    public Task<T> UpdateNode<T>(T node, IGraphTransaction? transaction = null)
        where T : Model.INode, new()
    {
        throw new NotImplementedException();
    }

    public Task<R> UpdateRelationship<R, S, T>(R relationship, IGraphTransaction? transaction = null)
        where R : IRelationship<S, T>, new()
        where S : Model.INode
        where T : Model.INode
    {
        throw new NotImplementedException();
    }

    public async Task<IGraphTransaction> BeginTransaction()
    {
        var session = driver.AsyncSession();
        var tx = await session.BeginTransactionAsync();
        return new Neo4jGraphTransaction(session, tx);
    }

    public Task DeleteNode(string id, IGraphTransaction? transaction = null)
    {
        throw new NotImplementedException();
    }

    public Task DeleteRelationship(string id, IGraphTransaction? transaction = null)
    {
        throw new NotImplementedException();
    }

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
            logger?.LogError(ex, "Failed to execute Cypher query.");
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

    public void Dispose()
    {
        lock (disposeLock)
        {
            if (disposed) return;
            driver?.Dispose();
            disposed = true;
        }
    }

    private async Task<(IAsyncSession, IAsyncTransaction)> GetOrCreateTransactionAsync(IGraphTransaction? transaction)
    {
        if (transaction is Neo4jGraphTransaction neo4jTx && neo4jTx.IsActive)
        {
            var tx = neo4jTx.GetTransaction() ?? throw new InvalidOperationException("Transaction is not active.");
            return (neo4jTx.Session, tx);
        }
        else if (transaction == null)
        {
            var session = driver.AsyncSession();
            var tx = await session.BeginTransactionAsync();
            return (session, tx);
        }
        else
        {
            throw new InvalidOperationException("Transaction is not active or not a Neo4j transaction.");
        }
    }

    private async Task LoadExistingConstraintsAsync()
    {
        if (_constraintsLoaded) return;
        lock (constraintLock)
        {
            if (_constraintsLoaded) return;
            _constraintsLoaded = true;
        }

        var cypher = "SHOW CONSTRAINTS";
        var (session, tx) = await GetOrCreateTransactionAsync(null);

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
                            _constrainedLabels.Add(s);
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Log(l => l.LogError(ex, "Failed to load existing constraints from Neo4j."));
        }
        finally
        {
            await session.CloseAsync();
        }
    }

    private async Task EnsureConstraintsForLabelAsync(string label, IEnumerable<string> propertyNames, Type? nodeType = null)
    {
        await LoadExistingConstraintsAsync();
        lock (constraintLock)
        {
            if (_constrainedLabels.Contains(label))
                return;
            _constrainedLabels.Add(label);
        }
        await using var session = driver.AsyncSession();
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

    private static string TypeNameToLabel<T>() => TypeNameToLabel(typeof(T));

    private static string TypeNameToLabel(Type type)
    {
        var typeName = type.FullName ?? type.Name;
        var label = typeName.Replace('.', '_').Replace('+', '_');
        return label;
    }

    private void Log(Action<Microsoft.Extensions.Logging.ILogger> logAction)
    {
        if (logger != null)
        {
            logAction(logger);
        }
    }

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
}
