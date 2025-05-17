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
using Neo4j.Driver;
using Neo4jNode = Neo4j.Driver.INode;
using Neo4jRelationship = Neo4j.Driver.IRelationship;

namespace Cvoya.Graph.Client.Neo4j;

// TODO: Change this to an IAsyncQueryProvider
internal class Neo4jCypherQueryProvider : IQueryProvider
{
    private readonly Neo4jGraphProvider _client;
    private readonly IGraphTransaction? _transaction;

    public Neo4jCypherQueryProvider(Neo4jGraphProvider client, IGraphTransaction? transaction)
    {
        _client = client;
        _transaction = transaction;
    }

    public IQueryable CreateQuery(Expression expression)
    {
        var elementType = expression.Type.GetGenericArguments().First();
        var queryableType = typeof(Neo4jQueryable<>).MakeGenericType(elementType);
        return (IQueryable)Activator.CreateInstance(queryableType, this, expression)!;
    }

    public IQueryable<TElement> CreateQuery<TElement>(Expression expression)
    {
        return new Neo4jQueryable<TElement>(this, expression);
    }

    public object? Execute(Expression expression)
    {
        // Use reflection to get the element type from the expression
        var elementType = expression.Type.GetGenericArguments().FirstOrDefault() ?? expression.Type;
        var method = typeof(Neo4jCypherQueryProvider).GetMethod(nameof(ExecuteAsync), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
        var genericMethod = method!.MakeGenericMethod(elementType);
        var visitedNodeIds = new HashSet<object>();
        var task = (Task)genericMethod.Invoke(this, [expression, 1, visitedNodeIds])!;
        task.Wait();
        var resultProperty = task.GetType().GetProperty("Result");
        return resultProperty!.GetValue(task);
    }

    public TResult Execute<TResult>(Expression expression)
    {
        return (TResult)Execute(expression)!;
    }

    public async Task<IEnumerable<T>> ExecuteAsync<T>(Expression expression, int traversalDepth = 1, HashSet<object>? visitedNodeIds = null)
    {
        var (cypher, parameters) = CypherExpressionTranslator.TranslateWithParameters(expression, typeof(T));
        var (session, tx) = await _client.GetOrCreateTransactionAsync(_transaction);
        var results = new List<T>();
        visitedNodeIds ??= new HashSet<object>();
        var cursor = await tx.RunAsync(cypher, parameters);
        while (await cursor.FetchAsync())
        {
            var current = cursor.Current["n"];
            if (typeof(T) == typeof(string) || typeof(T).IsPrimitive)
            {
                results.Add((T)current);
            }
            else if (current is Neo4jNode n)
            {
                if (typeof(Cvoya.Graph.Provider.Model.INode).IsAssignableFrom(typeof(T)))
                {
                    var nodeId = n.Properties["Id"];
                    if (visitedNodeIds.Contains(nodeId))
                        continue;
                    visitedNodeIds.Add(nodeId);
                    var method = typeof(Neo4jGraphProvider).GetMethod("DeserializeNode", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    var generic = method!.MakeGenericMethod(typeof(T));
                    object? nodeObjRaw = generic.Invoke(_client, [n]);
                    if (nodeObjRaw is not null)
                    {
                        var nodeObj = (T)nodeObjRaw;

                        // Populate IRelationship properties and hydrate related nodes recursively
                        foreach (var prop in typeof(T).GetProperties())
                        {
                            var relType = prop.PropertyType;
                            if (relType.IsGenericType && relType.GetGenericTypeDefinition() == typeof(Cvoya.Graph.Provider.Model.IRelationship<,>))
                            {
                                // Single relationship
                                var relLabel = relType.FullName ?? relType.Name;
                                var cypherRel = $"MATCH ()-[r:`{relLabel}`]->() WHERE r.SourceId = $id OR r.TargetId = $id RETURN r LIMIT 1";
                                var relCursor = await tx.RunAsync(cypherRel, new { id = n.Properties["Id"] });
                                if (await relCursor.FetchAsync())
                                {
                                    var relObj = relCursor.Current["r"];
                                    if (relObj is Neo4jRelationship rel)
                                    {
                                        var relMethod = typeof(Neo4jGraphProvider).GetMethod("DeserializeRelationship", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                                        var relGeneric = relMethod!.MakeGenericMethod(relType.GetGenericArguments());
                                        var relValueRaw = relGeneric.Invoke(_client, [rel]);
                                        if (relValueRaw != null)
                                        {
                                            prop.SetValue(nodeObj, relValueRaw);

                                            // Hydrate related node recursively if depth > 1
                                            if (traversalDepth > 1)
                                            {
                                                var relProps = rel.Properties;
                                                var sourceId = relProps.ContainsKey("SourceId") ? relProps["SourceId"] : null;
                                                var targetId = relProps.ContainsKey("TargetId") ? relProps["TargetId"] : null;
                                                var nodeType = relType.GetGenericArguments()[0]; // Assume source node type
                                                var relatedNodeId = Equals(sourceId, nodeId) ? targetId : sourceId;
                                                if (relatedNodeId != null && !visitedNodeIds.Contains(relatedNodeId))
                                                {
                                                    var cypherNode = "MATCH (n) WHERE n.Id = $id RETURN n LIMIT 1";
                                                    var relatedCursor = await tx.RunAsync(cypherNode, new { id = relatedNodeId });
                                                    if (await relatedCursor.FetchAsync())
                                                    {
                                                        var relatedNeo4jNode = relatedCursor.Current["n"] as Neo4jNode;
                                                        if (relatedNeo4jNode != null)
                                                        {
                                                            var hydrateMethod = typeof(Neo4jCypherQueryProvider).GetMethod(nameof(ExecuteAsync), System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                                                            var hydrateGeneric = hydrateMethod!.MakeGenericMethod(nodeType);
                                                            var hydrated = await (Task<IEnumerable<object>>)hydrateGeneric.Invoke(this, [Expression.Constant(relatedNeo4jNode), traversalDepth - 1, visitedNodeIds])!;
                                                            // Optionally set hydrated node on relationship if your model supports it
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                            else if (relType.IsGenericType && typeof(System.Collections.IEnumerable).IsAssignableFrom(relType))
                            {
                                // Collection of relationships
                                var elemType = relType.GetGenericArguments().FirstOrDefault();
                                if (elemType != null && elemType.IsGenericType && elemType.GetGenericTypeDefinition() == typeof(Cvoya.Graph.Provider.Model.IRelationship<,>))
                                {
                                    var relLabel = elemType.FullName ?? elemType.Name;
                                    var cypherRel = $"MATCH ()-[r:`{relLabel}`]->() WHERE r.SourceId = $id OR r.TargetId = $id RETURN r";
                                    var relCursor = await tx.RunAsync(cypherRel, new { id = n.Properties["Id"] });
                                    var relList = (System.Collections.IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(elemType))!;
                                    while (await relCursor.FetchAsync())
                                    {
                                        var relObj = relCursor.Current["r"];
                                        if (relObj is Neo4jRelationship rel)
                                        {
                                            var relMethod = typeof(Neo4jGraphProvider).GetMethod("DeserializeRelationship", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                                            var relGeneric = relMethod!.MakeGenericMethod(elemType.GetGenericArguments());
                                            var relValueRaw = relGeneric.Invoke(_client, [rel]);
                                            if (relValueRaw != null)
                                            {
                                                relList.Add(relValueRaw);

                                                // Hydrate related node recursively if depth > 1
                                                if (traversalDepth > 1)
                                                {
                                                    var relProps = rel.Properties;
                                                    var sourceId = relProps.ContainsKey("SourceId") ? relProps["SourceId"] : null;
                                                    var targetId = relProps.ContainsKey("TargetId") ? relProps["TargetId"] : null;
                                                    var nodeType = elemType.GetGenericArguments()[0]; // Assume source node type
                                                    var relatedNodeId = Equals(sourceId, n.Properties["Id"]) ? targetId : sourceId;
                                                    if (relatedNodeId != null && !visitedNodeIds.Contains(relatedNodeId))
                                                    {
                                                        var cypherNode = "MATCH (n) WHERE n.Id = $id RETURN n LIMIT 1";
                                                        var relatedCursor = await tx.RunAsync(cypherNode, new { id = relatedNodeId });
                                                        if (await relatedCursor.FetchAsync())
                                                        {
                                                            var relatedNeo4jNode = relatedCursor.Current["n"] as Neo4jNode;
                                                            if (relatedNeo4jNode != null)
                                                            {
                                                                var hydrateMethod = typeof(Neo4jCypherQueryProvider).GetMethod(nameof(ExecuteAsync), System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                                                                var hydrateGeneric = hydrateMethod!.MakeGenericMethod(nodeType);
                                                                var hydrated = await (Task<IEnumerable<object>>)hydrateGeneric.Invoke(this, [Expression.Constant(relatedNeo4jNode), traversalDepth - 1, visitedNodeIds])!;
                                                                // Optionally set hydrated node on relationship if your model supports it
                                                            }
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                    }
                                    prop.SetValue(nodeObj, relList);
                                }
                            }
                        }
                        results.Add(nodeObj);
                    }
                }
                else
                {
                    // For POCOs, map properties by name (including nested objects and collections)
                    results.Add((T)MapToPoco(typeof(T), n.Properties));
                }
            }
            else if (current is IReadOnlyDictionary<string, object> dict)
            {
                // Anonymous or POCO projection (including nested objects and collections)
                results.Add((T)MapToPoco(typeof(T), dict));
            }
            else
            {
                // For projections (e.g., Select), try to convert
                results.Add((T)Convert.ChangeType(current, typeof(T)));
            }
        }
        if (_transaction == null)
            await session.CloseAsync();
        return results;
    }

    // Recursively hydrate IRelationship properties and related nodes up to a given depth
    private async Task HydrateRelationshipsAndRelatedNodes(object nodeObj, string nodeId, IAsyncTransaction tx, int depth)
    {
        if (depth <= 0 || nodeObj == null) return;
        var nodeType = nodeObj.GetType();
        foreach (var prop in nodeType.GetProperties())
        {
            var relType = prop.PropertyType;
            if (relType.IsGenericType && relType.GetGenericTypeDefinition() == typeof(Cvoya.Graph.Provider.Model.IRelationship<,>))
            {
                // Single relationship
                var relLabel = relType.FullName ?? relType.Name;
                var cypherRel = $"MATCH ()-[r:`{relLabel}`]->() WHERE r.SourceId = $id OR r.TargetId = $id RETURN r LIMIT 1";
                var relCursor = await tx.RunAsync(cypherRel, new { id = nodeId });
                if (await relCursor.FetchAsync())
                {
                    var relObj = relCursor.Current["r"];
                    if (relObj is Neo4jRelationship rel)
                    {
                        var relMethod = typeof(Neo4jGraphProvider).GetMethod("DeserializeRelationship", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        var relGeneric = relMethod!.MakeGenericMethod(relType.GetGenericArguments());
                        var relValue = relGeneric.Invoke(_client, [rel]);
                        if (relValue != null)
                        {
                            prop.SetValue(nodeObj, relValue);
                            // Hydrate related nodes
                            await HydrateRelatedNodesForRelationship(relValue, rel, tx, depth - 1);
                        }
                    }
                }
            }
            else if (relType.IsGenericType && typeof(System.Collections.IEnumerable).IsAssignableFrom(relType))
            {
                // Collection of relationships
                var elemType = relType.GetGenericArguments().FirstOrDefault();
                if (elemType != null && elemType.IsGenericType && elemType.GetGenericTypeDefinition() == typeof(Cvoya.Graph.Provider.Model.IRelationship<,>))
                {
                    var relLabel = elemType.FullName ?? elemType.Name;
                    var cypherRel = $"MATCH ()-[r:`{relLabel}`]->() WHERE r.SourceId = $id OR r.TargetId = $id RETURN r";
                    var relCursor = await tx.RunAsync(cypherRel, new { id = nodeId });
                    var relList = (System.Collections.IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(elemType))!;
                    while (await relCursor.FetchAsync())
                    {
                        var relObj = relCursor.Current["r"];
                        if (relObj is Neo4jRelationship rel)
                        {
                            var relMethod = typeof(Neo4jGraphProvider).GetMethod("DeserializeRelationship", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                            var relGeneric = relMethod!.MakeGenericMethod(elemType.GetGenericArguments());
                            var relValue = relGeneric.Invoke(_client, [rel]);
                            if (relValue != null)
                            {
                                relList.Add(relValue);
                                // Hydrate related nodes
                                await HydrateRelatedNodesForRelationship(relValue, rel, tx, depth - 1);
                            }
                        }
                    }
                    prop.SetValue(nodeObj, relList);
                }
            }
        }
    }

    // Hydrate source and target nodes for a relationship object
    private async Task HydrateRelatedNodesForRelationship(object relObj, Neo4jRelationship rel, IAsyncTransaction tx, int depth)
    {
        if (depth <= 0 || relObj == null) return;
        var relType = relObj.GetType();
        var sourceIdProp = relType.GetProperty("SourceId");
        var targetIdProp = relType.GetProperty("TargetId");
        var sourceId = sourceIdProp?.GetValue(relObj)?.ToString();
        var targetId = targetIdProp?.GetValue(relObj)?.ToString();
        var sourceNodeProp = relType.GetProperties().FirstOrDefault(p => p.PropertyType.GetInterface(nameof(Cvoya.Graph.Provider.Model.INode)) != null);
        var targetNodeProp = relType.GetProperties().LastOrDefault(p => p.PropertyType.GetInterface(nameof(Cvoya.Graph.Provider.Model.INode)) != null);
        if (sourceNodeProp != null && sourceId != null)
        {
            var nodeType = sourceNodeProp.PropertyType;
            var label = nodeType.FullName ?? nodeType.Name;
            var cypher = $"MATCH (n:`{label}`) WHERE n.Id = $id RETURN n";
            var cursor = await tx.RunAsync(cypher, new { id = sourceId });
            if (await cursor.FetchAsync())
            {
                var n = cursor.Current["n"] as Neo4jNode;
                var method = typeof(Neo4jGraphProvider).GetMethod("DeserializeNode", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var generic = method!.MakeGenericMethod(nodeType);
                object? nodeObjRaw = generic.Invoke(_client, [n!]);
                if (nodeObjRaw is not null)
                {
                    var nodeObj = nodeObjRaw;
                    sourceNodeProp.SetValue(relObj, nodeObj);
                    await HydrateRelationshipsAndRelatedNodes(nodeObj, sourceId, tx, depth - 1);
                }
            }
        }
        if (targetNodeProp != null && targetId != null)
        {
            var nodeType = targetNodeProp.PropertyType;
            var label = nodeType.FullName ?? nodeType.Name;
            var cypher = $"MATCH (n:`{label}`) WHERE n.Id = $id RETURN n";
            var cursor = await tx.RunAsync(cypher, new { id = targetId });
            if (await cursor.FetchAsync())
            {
                var n = cursor.Current["n"] as Neo4jNode;
                var method = typeof(Neo4jGraphProvider).GetMethod("DeserializeNode", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var generic = method!.MakeGenericMethod(nodeType);
                object? nodeObjRaw = generic.Invoke(_client, [n!]);
                if (nodeObjRaw is not null)
                {
                    var nodeObj = nodeObjRaw;
                    targetNodeProp.SetValue(relObj, nodeObj);
                    await HydrateRelationshipsAndRelatedNodes(nodeObj, targetId, tx, depth - 1);
                }
            }
        }
    }

    // Helper: Recursively map Neo4j dictionary to POCO/anonymous type, including nested objects and collections
    private static object MapToPoco(Type targetType, IReadOnlyDictionary<string, object> dict)
    {
        var obj = Activator.CreateInstance(targetType)!;
        foreach (var prop in targetType.GetProperties())
        {
            if (!dict.TryGetValue(prop.Name, out var value) || value == null)
                continue;
            if (prop.PropertyType == typeof(string) || prop.PropertyType.IsPrimitive)
            {
                prop.SetValue(obj, value);
            }
            else if (typeof(System.Collections.IEnumerable).IsAssignableFrom(prop.PropertyType) && prop.PropertyType != typeof(string))
            {
                // Handle collections
                var elementType = prop.PropertyType.IsArray
                    ? prop.PropertyType.GetElementType()!
                    : prop.PropertyType.GetGenericArguments().FirstOrDefault() ?? typeof(object);
                var list = (System.Collections.IEnumerable)value;
                var constructedList = (System.Collections.IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(elementType))!;
                foreach (var item in list)
                {
                    if (item == null)
                        constructedList.Add(null);
                    else if (item is IReadOnlyDictionary<string, object> subDict)
                        constructedList.Add(MapToPoco(elementType, subDict));
                    else
                        constructedList.Add(Convert.ChangeType(item, elementType));
                }
                if (prop.PropertyType.IsArray)
                {
                    var array = Array.CreateInstance(elementType, constructedList.Count);
                    constructedList.CopyTo(array, 0);
                    prop.SetValue(obj, array);
                }
                else
                {
                    prop.SetValue(obj, constructedList);
                }
            }
            else if (value is IReadOnlyDictionary<string, object> subDict)
            {
                // Nested object
                prop.SetValue(obj, MapToPoco(prop.PropertyType, subDict));
            }
            else
            {
                prop.SetValue(obj, Convert.ChangeType(value, prop.PropertyType));
            }
        }
        return obj;
    }
}
