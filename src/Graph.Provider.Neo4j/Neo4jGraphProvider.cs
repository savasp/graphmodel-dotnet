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
    private readonly Lock disposeLock = new();
    private bool disposed = false;
    private readonly Microsoft.Extensions.Logging.ILogger? logger;

    private readonly IDriver driver;

    // Tracks labels/types for which constraints have been created
    private readonly HashSet<string> constrainedLabels = [];
    private readonly Lock constraintLock = new();
    private bool constraintsLoaded = false;

    private readonly string databaseName;

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

        this.driver = GraphDatabase.Driver(uri, AuthTokens.Basic(username, password));
    }

    /// <inheritdoc />
    public IQueryable<N> Nodes<N>(IGraphTransaction? transaction = null)
        where N : Model.INode, new()
    {
        // Provide a LINQ IQueryable for nodes
        return new Neo4jQueryable<N>(
            new Neo4jQueryProvider(this, typeof(N), transaction),
            Expression.Constant(null, typeof(IQueryable<N>))
        );
    }

    /// <inheritdoc />
    public IQueryable<R> Relationships<R>(IGraphTransaction? transaction = null)
        where R : Model.IRelationship, new()
    {
        // Provide a LINQ IQueryable for relationships
        return new Neo4jQueryable<R>(
            new Neo4jQueryProvider(this, typeof(R), transaction),
            Expression.Constant(null, typeof(IQueryable<R>))
        );
    }

    /// <inheritdoc />
    public async Task<T> GetNode<T>(string id, IGraphTransaction? transaction = null)
        where T : Model.INode, new()
    {
        var result = await GetNodes<T>([id], transaction);
        return result.FirstOrDefault() ?? throw new GraphException($"Node with ID '{id}' not found.");
    }

    /// <inheritdoc />
    public async Task<IEnumerable<T>> GetNodes<T>(IEnumerable<string> ids, IGraphTransaction? transaction = null)
        where T : Model.INode, new()
    {
        if (ids.Any(string.IsNullOrEmpty))
        {
            throw new ArgumentNullException(nameof(ids), "One or more IDs are null or empty.");
        }

        var whereInClause = string.Join(", ", ids.Select(id => $"'{id}'"));
        var cypher = $"MATCH (n) WHERE n.Id IN [{whereInClause}] RETURN n";
        var results = new List<T>();

        var (session, tx) = await GetOrCreateTransaction(transaction);
        try
        {
            var result = await tx.RunAsync(cypher);
            // TODO: Use the label of the retrieved node to deserialize to the correct type
            var record = await result.Select(r => r["n"].As<global::Neo4j.Driver.INode>()).ToListAsync();

            // TODO: Deserialize complex properties
            results.AddRange(record.Select(r => r.ConvertToGraphEntity<T>()));

            return results;
        }
        catch (Exception ex)
        {
            throw new GraphException($"Error retrieving node one or more of the nodes with IDs '{whereInClause}'", ex);
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
    public async Task<R> GetRelationship<R>(string id, IGraphTransaction? transaction = null)
        where R : Model.IRelationship, new()
    {
        var result = await this.GetRelationships<R>([id], transaction);

        return result.FirstOrDefault() ?? throw new GraphException($"Relationship with ID '{id}' not found.");
    }

    /// <inheritdoc />
    public async Task<IEnumerable<R>> GetRelationships<R>(IEnumerable<string> ids, IGraphTransaction? transaction = null)
        where R : Model.IRelationship, new()
    {
        if (ids is null || !ids.Any())
        {
            throw new ArgumentNullException(nameof(ids), "IDs cannot be null or empty.");
        }

        var relationships = new List<R>();
        foreach (var id in ids)
        {
            var cypher = $"""
                MATCH ()-[r]->()
                WHERE r.{nameof(Model.IRelationship.Id)} = '{id}'
                RETURN r, type(r) as type
                """;

            var (session, tx) = await GetOrCreateTransaction(transaction);
            try
            {
                var result = await tx.RunAsync(cypher);
                var records = await result.ToListAsync();
                foreach (var record in records)
                {
                    // TODO: Use the type of the retrieved node to deserialize to the correct type
                    var type = record["type"].As<string>();
                    var rel = record["r"].As<global::Neo4j.Driver.IRelationship>().ConvertToGraphEntity<R>();
                    relationships.Add(rel);
                }
            }
            catch (Exception ex)
            {
                throw new GraphException($"Error retrieving relationship with ID '{id}'", ex);
            }
            finally
            {
                if (transaction == null)
                {
                    await session.CloseAsync();
                }
            }
        }

        return relationships;
    }

    /// <inheritdoc />
    public async Task CreateNode<T>(T node, IGraphTransaction? transaction = null)
        where T : Model.INode, new()
    {
        if (node is null) throw new ArgumentNullException(nameof(node));

        node.EnsureNoReferenceCycle();

        var (session, tx) = await GetOrCreateTransaction(transaction);

        try
        {
            await this.CreateNode(null, node, tx);

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
    public async Task CreateRelationship<R>(R relationship, IGraphTransaction? transaction = null)
        where R : Model.IRelationship, new()
    {
        if (relationship is null) throw new ArgumentNullException(nameof(relationship));

        relationship.EnsureNoReferenceCycle();

        var type = relationship.GetType();
        var label = GetLabel(type);
        var (simpleProps, complexProps) = SerializationExtensions.GetSimpleAndComplexProperties(relationship);

        CheckRelationshipProperties(complexProps);

        var (session, tx) = await GetOrCreateTransaction(transaction);

        try
        {
            var cypher = $"""
                MATCH (a), (b) 
                WHERE a.{nameof(Model.INode.Id)} = '{relationship.SourceId}' 
                    AND b.{nameof(Model.INode.Id)} = '{relationship.TargetId}'
                CREATE (a)-[r:{label} $props]->(b)
                RETURN elementId(r) AS relId
                """;
            var result = await tx.RunAsync(cypher, new
            {
                props = simpleProps.ToDictionary(kv => kv.Key.Name, kv => kv.Value.ConvertToNeo4jValue()),
            });

            var record = await result.SingleAsync();

            if (result is null || string.IsNullOrEmpty(record["relId"].As<string>()))
            {
                throw new GraphException($"Failed to create relationship of type '{label}'");
            }

            if (transaction is null)
            {
                // We created the transaction, so we need to commit it
                await tx.CommitAsync();
            }
        }
        catch (Exception ex)
        {
            throw new GraphException($"Error creating relationship of type '{label}'", ex);
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
    public async Task UpdateNode<T>(T node, IGraphTransaction? transaction = null)
        where T : Model.INode, new()
    {
        if (node == null) throw new ArgumentNullException(nameof(node));

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
    public async Task UpdateRelationship<R>(R relationship, IGraphTransaction? transaction = null)
        where R : Model.IRelationship, new()
    {
        if (relationship is null) throw new ArgumentNullException(nameof(relationship));

        relationship.EnsureNoReferenceCycle();

        var (simpleProps, complexProps) = SerializationExtensions.GetSimpleAndComplexProperties(relationship);

        CheckRelationshipProperties(complexProps);

        var (session, tx) = await GetOrCreateTransaction(transaction);
        var cypher = $"MATCH ()-[r]->() WHERE r.{nameof(Model.IRelationship.Id)} = '{relationship.Id}' SET r += $props";

        try
        {
            await tx.RunAsync(cypher, new
            {
                props = simpleProps.ToDictionary(kv => kv.Key.Name, kv => kv.Value.ConvertToNeo4jValue()),
            });

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
        var (session, tx) = await GetOrCreateTransaction(transaction);

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
            throw new GraphException("Failed to execute Cypher query.", ex);
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

    private async Task<(IAsyncSession, IAsyncTransaction)> GetOrCreateTransaction(IGraphTransaction? transaction)
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
        var nodeAttr = type.GetCustomAttribute<NodeAttribute>();
        if (nodeAttr?.Label is { Length: > 0 }) return nodeAttr.Label;

        var relAttr = type.GetCustomAttribute<RelationshipAttribute>();
        if (relAttr?.Label is { Length: > 0 }) return relAttr.Label;

        var propertyAttr = type.GetCustomAttribute<PropertyAttribute>();
        if (propertyAttr?.Label is { Length: > 0 }) return propertyAttr.Label;

        var label = type.FullName?.Replace('.', '_')?.Replace("+", "_");
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
        List<string> relationshipPropertyNames = [nameof(IRelationship<,>.Source), nameof(IRelationship<,>.Target)];
        var check = complexProps
            .Select(p => p.Key)
            .Where(p => (!relationshipPropertyNames.Contains(p.Name)) && p.PropertyType.IsAssignableTo(typeof(Model.INode)));

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

    public void Dispose()
    {
        lock (disposeLock)
        {
            if (disposed) return;
            disposed = true;
        }

        driver.Dispose();
    }
}
