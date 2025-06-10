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
using System.Linq.Expressions;
using System.Reflection;
using Cvoya.Graph.Model.Neo4j.Linq;
using Cvoya.Graph.Model.Neo4j.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Neo4j.Driver;

namespace Cvoya.Graph.Model.Neo4j.Cypher;

internal class CypherEngine(GraphContext graphContext)
{
    private readonly ILogger<CypherEngine>? _logger =
        graphContext.LoggerFactory?.CreateLogger<CypherEngine>() ?? NullLogger<CypherEngine>.Instance;
    private readonly GraphEntitySerializer _serializer = new GraphEntitySerializer(graphContext);

    public async Task<T> ExecuteAsync<T>(
        string cypher,
        GraphTransaction transaction,
        GraphQueryContext queryContext,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(cypher);
        ArgumentNullException.ThrowIfNull(queryContext);

        _logger?.LogDebug("Executing Cypher query: {Query}", cypher);
        _logger?.LogDebug("With parameters: {Parameters}",
            string.Join(", ", queryContext.Parameters?.Select(p => $"{p.Key}={p.Value}") ?? []));

        // Use the parameters from the query context
        var parameters = queryContext.Parameters ?? new Dictionary<string, object>();
        var result = await transaction.Transaction.RunAsync(cypher, parameters);

        // Handle different result types based on query context
        return queryContext switch
        {
            { IsScalarResult: true } => await HandleScalarResultAsync<T>(result, cancellationToken),
            { IsProjection: true } => await HandleProjectionResultAsync<T>(result, queryContext, cancellationToken),
            _ => await HandleEntityResultAsync<T>(result, queryContext, cancellationToken)
        };
    }

    public string ExpressionToCypherVisitor(
        Expression expression,
        GraphQueryContext queryContext)
    {
        ArgumentNullException.ThrowIfNull(expression);
        ArgumentNullException.ThrowIfNull(queryContext);

        _logger?.LogDebug("Converting expression to Cypher: {ExpressionType}", expression.NodeType);
        _logger?.LogDebug("Initial query context - IsScalar: {IsScalar}, ResultType: {ResultType}",
            queryContext.IsScalarResult, queryContext.ResultType?.Name);

        try
        {
            var visitor = new CypherQueryVisitor(queryContext, graphContext.LoggerFactory);
            visitor.Visit(expression);

            var result = visitor.Build();

            // IMPORTANT: Copy the parameters from the builder to the query context
            queryContext.Parameters = result.Parameters;

            _logger?.LogDebug("After visitor - IsScalar: {IsScalar}, ResultType: {ResultType}",
                queryContext.IsScalarResult, queryContext.ResultType?.Name);
            _logger?.LogDebug("Generated Cypher: {Cypher}", result.Cypher);
            _logger?.LogDebug("Parameters: {Parameters}", string.Join(", ", result.Parameters.Select(p => $"{p.Key}={p.Value}")));

            return result.Cypher;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to convert expression to Cypher");
            throw new InvalidOperationException($"Failed to convert expression to Cypher: {ex.Message}", ex);
        }
    }

    private async Task<T> HandleScalarResultAsync<T>(IResultCursor result, CancellationToken cancellationToken)
    {
        var record = await result.SingleAsync();
        var value = record.Values.First().Value;

        // Handle null values
        if (value == null)
        {
            return default!;
        }

        // Use the serializer to convert Neo4j values to .NET types
        var convertedValue = _serializer.ConvertScalarFromNeo4jValue(value, typeof(T));

        if (convertedValue is T typedValue)
        {
            return typedValue;
        }

        // If the serializer couldn't handle it, we are in trouble...
        throw new InvalidOperationException(
            $"Cannot convert Neo4j value '{value}' to type {typeof(T)}. " +
            "Ensure the serializer can handle this type or use a different query context.");
    }

    private async Task<T> HandleProjectionResultAsync<T>(
        IResultCursor result,
        GraphQueryContext queryContext,
        CancellationToken cancellationToken)
    {
        var records = await result.ToListAsync(cancellationToken);

        // Check if we're dealing with IEnumerable<IGraphPathSegment<,,>>
        if (typeof(T).IsGenericType && typeof(IEnumerable).IsAssignableFrom(typeof(T)))
        {
            var elementType = typeof(T).GetGenericArguments()[0];
            if (elementType.IsGenericType && elementType.GetGenericTypeDefinition() == typeof(IGraphPathSegment<,,>))
            {
                var genericArgs = elementType.GetGenericArguments();
                var sourceType = genericArgs[0];
                var relType = genericArgs[1];
                var targetType = genericArgs[2];

                // Create the concrete implementation type
                var concreteType = typeof(GraphPathSegment<,,>).MakeGenericType(sourceType, relType, targetType);
                var listType = typeof(List<>).MakeGenericType(elementType);
                var list = Activator.CreateInstance(listType) as IList ?? throw new InvalidOperationException();

                foreach (var record in records)
                {
                    // We expect 3 values: source node, relationship, target node
                    if (record.Values.Count >= 3)
                    {
                        var sourceNode = _serializer.DeserializeNodeFromNeo4jNode(
                            record[0].As<global::Neo4j.Driver.INode>(),
                            sourceType,
                            queryContext.UseMostDerivedType);

                        var relationship = _serializer.DeserializeRelationshipFromNeo4jRelationship(
                            record[1].As<global::Neo4j.Driver.IRelationship>(),
                            relType);

                        var targetNode = _serializer.DeserializeNodeFromNeo4jNode(
                            record[2].As<global::Neo4j.Driver.INode>(),
                            targetType,
                            queryContext.UseMostDerivedType);

                        var pathSegment = Activator.CreateInstance(
                            concreteType,
                            sourceNode,
                            relationship,
                            targetNode);

                        list.Add(pathSegment);
                    }
                }

                return (T)list;
            }
        }

        // Handle single entity projections (like Select(r => r.Relationship).FirstOrDefault())
        if (!typeof(IEnumerable).IsAssignableFrom(typeof(T)) || typeof(T) == typeof(string))
        {
            var record = records.FirstOrDefault();
            if (record == null)
            {
                return default!;
            }

            // If we have multiple values (n, r, t) but want just one part
            // This happens with queries like .Select(r => r.Relationship)
            if (record.Values.Count == 3 &&
                record[0] is global::Neo4j.Driver.INode &&
                record[1] is global::Neo4j.Driver.IRelationship rel &&
                record[2] is global::Neo4j.Driver.INode)
            {
                // We're selecting just the relationship
                if (typeof(IRelationship).IsAssignableFrom(typeof(T)))
                {
                    return (T)_serializer.DeserializeRelationshipFromNeo4jRelationship(rel, typeof(T));
                }
            }

            // Handle other single value projections
            if (record.Values.Count == 1)
            {
                var value = record.Values.First().Value;

                if (value is global::Neo4j.Driver.INode node && typeof(INode).IsAssignableFrom(typeof(T)))
                {
                    return (T)_serializer.DeserializeNodeFromNeo4jNode(
                        node,
                        typeof(T),
                        queryContext.UseMostDerivedType);
                }

                if (value is global::Neo4j.Driver.IRelationship relationship && typeof(IRelationship).IsAssignableFrom(typeof(T)))
                {
                    return (T)_serializer.DeserializeRelationshipFromNeo4jRelationship(
                        relationship,
                        typeof(T));
                }

                // Handle scalar projections
                var convertedValue = _serializer.ConvertScalarFromNeo4jValue(value, typeof(T));
                return convertedValue is T typedValue ? typedValue : default!;
            }
        }

        // Handle collection projections
        if (typeof(IEnumerable).IsAssignableFrom(typeof(T)) && typeof(T) != typeof(string))
        {
            var elementType = typeof(T).IsArray
                ? typeof(T).GetElementType()!
                : typeof(T).GetGenericArguments().FirstOrDefault() ?? typeof(object);

            var results = new List<object>();

            foreach (var record in records)
            {
                // Similar logic as above but for collections
                if (record.Values.Count == 3 &&
                    record[1] is global::Neo4j.Driver.IRelationship rel &&
                    typeof(IRelationship).IsAssignableFrom(elementType))
                {
                    var entity = _serializer.DeserializeRelationshipFromNeo4jRelationship(rel, elementType);
                    results.Add(entity);
                }
                else if (record.Values.Count == 1)
                {
                    var value = record.Values.First().Value;
                    object? entity = null;

                    if (value is global::Neo4j.Driver.INode node)
                    {
                        entity = _serializer.DeserializeNodeFromNeo4jNode(
                            node,
                            elementType,
                            queryContext.UseMostDerivedType);
                    }
                    else if (value is global::Neo4j.Driver.IRelationship relationship)
                    {
                        entity = _serializer.DeserializeRelationshipFromNeo4jRelationship(
                            relationship,
                            elementType);
                    }
                    else
                    {
                        entity = _serializer.ConvertScalarFromNeo4jValue(value, elementType);
                    }

                    if (entity != null)
                    {
                        results.Add(entity);
                    }
                }
            }

            return ConvertToCollectionType<T>(results, elementType);
        }

        // If we get here, we don't know how to handle this projection
        throw new NotImplementedException($"Projection handling for type {typeof(T)} with {records.FirstOrDefault()?.Values.Count ?? 0} values not implemented yet!");
    }

    private async Task<T> HandleEntityResultAsync<T>(
        IResultCursor result,
        GraphQueryContext queryContext,
        CancellationToken cancellationToken)
    {
        var records = await result.ToListAsync(cancellationToken);

        if (typeof(IEnumerable).IsAssignableFrom(typeof(T)) && typeof(T) != typeof(string))
        {
            // Handle collection results
            var elementType = typeof(T).IsArray
                ? typeof(T).GetElementType()!
                : typeof(T).GetGenericArguments().FirstOrDefault() ?? typeof(object);

            var results = new List<object>();

            foreach (var record in records)
            {
                var entity = DeserializeEntityFromRecord(
                    record,
                    elementType,
                    queryContext);
                results.Add(entity);
            }

            // Convert to appropriate collection type
            return ConvertToCollectionType<T>(results, elementType);
        }
        else
        {
            // Handle single entity result
            var record = records.FirstOrDefault();
            if (record == null)
            {
                return default!;
            }

            return (T)DeserializeEntityFromRecord(
                record,
                typeof(T),
                queryContext);
        }
    }

    private static T ConvertToCollectionType<T>(List<object> items, Type elementType)
    {
        // Handle arrays
        if (typeof(T).IsArray)
        {
            var array = Array.CreateInstance(elementType, items.Count);
            for (int i = 0; i < items.Count; i++)
            {
                array.SetValue(items[i], i);
            }
            return (T)(object)array;
        }

        // Handle List<T>
        if (typeof(T).IsGenericType && typeof(T).GetGenericTypeDefinition() == typeof(List<>))
        {
            var listType = typeof(List<>).MakeGenericType(elementType);
            var list = (IList)Activator.CreateInstance(listType)!;
            foreach (var item in items)
            {
                list.Add(item);
            }
            return (T)list;
        }

        // Handle IEnumerable<T>, ICollection<T>, etc.
        if (typeof(T).IsGenericType)
        {
            // Default to List<T> for interfaces
            var listType = typeof(List<>).MakeGenericType(elementType);
            var list = (IList)Activator.CreateInstance(listType)!;
            foreach (var item in items)
            {
                list.Add(item);
            }

            // If T is directly assignable from List<T>, return it
            if (typeof(T).IsAssignableFrom(listType))
            {
                return (T)list;
            }

            // Try to create an instance of T using the list as constructor parameter
            try
            {
                return (T)Activator.CreateInstance(typeof(T), list)!;
            }
            catch
            {
                // If that fails, just return the list and hope for the best
                return (T)list;
            }
        }

        throw new NotSupportedException($"Cannot convert results to collection type {typeof(T)}");
    }

    private object DeserializeNodeWithComplexPropertiesAsync(
        global::Neo4j.Driver.INode node,
        List<object> relatedNodes,
        Type requestedType,
        GraphQueryContext queryContext)
    {
        // This is similar to what the serializer does, but we already have the data
        // so we can deserialize directly without another query

        // First create the main entity
        var entity = _serializer.DeserializeNodeFromNeo4jNode(
            node,
            requestedType,
            queryContext.UseMostDerivedType);

        // Then populate its complex properties using the related nodes
        PopulateComplexPropertiesFromResults(
            entity,
            relatedNodes,
            queryContext);

        return entity;
    }

    private void PopulateComplexPropertiesFromResults(
            object entity,
            List<object> relatedNodes,
            GraphQueryContext queryContext)
    {
        // Similar logic to the serializer's PopulateComplexPropertiesAsync
        // but we work with the data we already have from the query

        var type = entity.GetType();
        var complexPropsByRelType = new Dictionary<string, List<object>>();

        foreach (var relatedNode in relatedNodes)
        {
            if (relatedNode is not IDictionary<string, object> dict) continue;

            var neo4jNode = dict["Node"] as global::Neo4j.Driver.INode;
            var relType = dict["RelType"] as string;

            if (neo4jNode == null || relType == null) continue;

            var propName = GraphDataModel.RelationshipTypeNameToPropertyName(relType);

            if (!complexPropsByRelType.ContainsKey(propName))
                complexPropsByRelType[propName] = [];

            // Deserialize the complex property node - use the correct method!
            var complexEntity = _serializer.DeserializeNodeFromNeo4jNode(
                neo4jNode,
                typeof(INode), // We'll let the serializer figure out the actual type
                queryContext.UseMostDerivedType);

            complexPropsByRelType[propName].Add(complexEntity);
        }

        // Set the properties on the entity
        foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!prop.CanWrite || !complexPropsByRelType.ContainsKey(prop.Name)) continue;

            var values = complexPropsByRelType[prop.Name];

            if (GraphDataModel.IsComplex(prop.PropertyType))
            {
                prop.SetValue(entity, values.FirstOrDefault());
            }
            else if (GraphDataModel.IsCollectionOfComplex(prop.PropertyType))
            {
                var collection = CreateCollection(prop.PropertyType, values);
                prop.SetValue(entity, collection);
            }
        }
    }

    private static object CreateCollection(Type collectionType, IEnumerable<object> items)
    {
        if (collectionType.IsArray)
        {
            var elementType = collectionType.GetElementType()!;
            var array = Array.CreateInstance(elementType, items.Count());
            var index = 0;
            foreach (var item in items)
            {
                array.SetValue(item, index++);
            }
            return array;
        }

        var listType = typeof(List<>).MakeGenericType(collectionType.GetGenericArguments()[0]);
        var list = (IList)Activator.CreateInstance(listType)!;
        foreach (var item in items)
        {
            list.Add(item);
        }
        return list;
    }

    private object DeserializeEntityFromRecord(
           IRecord record,
           Type requestedType,
           GraphQueryContext queryContext)
    {
        // Check what's in the record and route to the appropriate deserialization method

        // Simple case: single node
        if (record.Values.Count == 1 && record.Values.First().Value is global::Neo4j.Driver.INode node)
        {
            // Use DeserializeNodeFromNeo4jNodeAsync instead of DeserializeNodeAsync
            return _serializer.DeserializeNodeFromNeo4jNode(
                node,
                requestedType,
                queryContext.UseMostDerivedType);
        }

        // Complex case: node with related nodes (for complex properties)
        if (record.Values.Count == 2)
        {
            if (record.Values.FirstOrDefault(v => v.Value is global::Neo4j.Driver.INode).Value is global::Neo4j.Driver.INode mainNode && record.Values.FirstOrDefault(v => v.Value is List<object>).Value is List<object> relatedNodes)
            {
                return DeserializeNodeWithComplexPropertiesAsync(
                    mainNode,
                    relatedNodes,
                    requestedType,
                    queryContext);
            }
        }

        // Handle other cases like relationships, projections, etc.
        throw new NotSupportedException($"Cannot deserialize record with {record.Values.Count} values to type {requestedType}");
    }
}

// Extension method to help with collection casting
internal static class CollectionExtensions
{
    public static IEnumerable<T> Cast<T>(this IEnumerable<object> source, Type elementType)
    {
        var castMethod = typeof(Enumerable).GetMethod(nameof(Enumerable.Cast))!.MakeGenericMethod(elementType);
        return (IEnumerable<T>)castMethod.Invoke(null, [source])!;
    }
}
