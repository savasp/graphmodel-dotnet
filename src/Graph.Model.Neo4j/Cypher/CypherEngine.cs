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
using Cvoya.Graph.Model.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Neo4j.Driver;

namespace Cvoya.Graph.Model.Neo4j.Cypher;

internal class CypherEngine(GraphContext graphContext)
{
    private readonly ILogger<CypherEngine> _logger =
        graphContext.LoggerFactory?.CreateLogger<CypherEngine>() ?? NullLogger<CypherEngine>.Instance;

    public async Task<T> ExecuteAsync<T>(
        string cypher,
        GraphTransaction transaction,
        GraphQueryContext queryContext,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(cypher);
        ArgumentNullException.ThrowIfNull(queryContext);

        _logger.LogDebug("Executing Cypher query: {Query}", cypher);
        _logger.LogDebug("With parameters: {Parameters}",
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
            // Populate schema information for better result handling
            PopulateEntitySchema(queryContext);

            var shouldLoadComplexProps = ShouldLoadComplexProperties(queryContext.ResultType);
            var visitor = new CypherQueryVisitor(queryContext, shouldLoadComplexProps, graphContext.LoggerFactory);

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

    private void PopulateEntitySchema(GraphQueryContext queryContext)
    {
        if (queryContext.ResultType == null) return;

        // Get the actual entity type (handle collections)
        var entityType = queryContext.ResultType;
        if (entityType.IsGenericType && typeof(IEnumerable).IsAssignableFrom(entityType))
        {
            entityType = entityType.GetGenericArguments()[0];
        }

        // Only populate schema for entity types
        if (!typeof(INode).IsAssignableFrom(entityType) && !typeof(IRelationship).IsAssignableFrom(entityType))
            return;

        try
        {
            queryContext.TargetEntitySchema = 

            _logger?.LogDebug("Populated schema for type {Type}: HasComplexProperties = {HasComplexProperties}",
                entityType.Name, queryContext.TargetEntitySchema?.HasComplexProperties ?? false);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Could not populate schema for type {Type}", entityType.Name);
        }
    }

    private bool ShouldLoadComplexProperties(Type? resultType)
    {
        if (resultType == null) return false;

        // Handle collections - get the element type
        if (resultType.IsGenericType)
        {
            var genericTypeDef = resultType.GetGenericTypeDefinition();
            if (genericTypeDef == typeof(IEnumerable<>) ||
                genericTypeDef == typeof(List<>) ||
                genericTypeDef == typeof(IList<>))
            {
                resultType = resultType.GetGenericArguments()[0];
            }
        }

        // Only nodes can have complex properties
        if (!typeof(INode).IsAssignableFrom(resultType))
            return false;

        // Check the generated schema to see if this type has complex properties
        try
        {
            var serializer = EntitySerializerRegistry.GetSerializer(resultType);
            if (serializer?.GetSchema() is { } schema)
            {
                _logger?.LogDebug("Type {Type} has complex properties: {HasComplexProperties}",
                    resultType.Name, schema.HasComplexProperties);
                return schema.HasComplexProperties;
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Could not check schema for type {Type}", resultType.Name);
        }

        return false;
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
        var convertedValue = EntitySerializerBase.ConvertFromNeo4jValue(value, typeof(T));

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
                /*
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
                */
                return (T)list;
            }
        }
        /*
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
                        var convertedValue = EntitySerializerBase.ConvertFromNeo4jValue(value, typeof(T));
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
                                entity = EntitySerializerBase.ConvertFromNeo4jValue(value, elementType);
                            }

                            if (entity != null)
                            {
                                results.Add(entity);
                            }
                        }
                    }

                    return ConvertToCollectionType<T>(results, elementType);
                }
        */
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

            // Group records by main entity to handle complex properties
            var entityGroups = GroupRecordsByMainEntity(records, queryContext);

            foreach (var entityGroup in entityGroups)
            {
                var entity = ProcessEntityWithComplexProperties(
                    entityGroup.MainNode,
                    entityGroup.RelatedNodes,
                    elementType,
                    queryContext);
                results.Add(entity);
            }

            return ConvertToCollectionType<T>(results, elementType);
        }
        else
        {
            // Handle single entity result
            if (records.Count == 0)
            {
                return default!;
            }

            // For single entity queries with complex properties, we need to consolidate all records
            if (queryContext.TargetEntitySchema?.HasComplexProperties == true)
            {
                var consolidatedEntity = ConsolidateRecordsForSingleEntity(records, typeof(T), queryContext);
                return (T)consolidatedEntity;
            }

            // For simple single entity queries, just use the first record
            var record = records.First();
            return (T)DeserializeEntityFromRecord(record, typeof(T), queryContext);
        }
    }

    private object ConsolidateRecordsForSingleEntity(
        List<IRecord> records,
        Type requestedType,
        GraphQueryContext queryContext)
    {
        if (records.Count == 0)
        {
            throw new InvalidOperationException("No records to consolidate");
        }

        // The pattern from your Cypher query: each record has (mainNode, relatedNodes)
        global::Neo4j.Driver.INode? mainNode = null;
        var allRelatedNodes = new List<object>();

        foreach (var record in records)
        {
            if (record.Values.Count >= 2)
            {
                // Extract main node from first record or verify it's the same across records
                if (record[0] is global::Neo4j.Driver.INode currentMainNode)
                {
                    if (mainNode == null)
                    {
                        mainNode = currentMainNode;
                    }
                    // Optionally verify it's the same entity across records
                    else if (mainNode.ElementId != currentMainNode.ElementId)
                    {
                        _logger?.LogWarning("Different main nodes found across records: {Id1} vs {Id2}",
                            mainNode.ElementId, currentMainNode.ElementId);
                    }
                }

                // Collect related nodes from this record
                if (record[1] is IList<object> relatedNodes)
                {
                    allRelatedNodes.AddRange(relatedNodes);
                }
            }
        }

        if (mainNode == null)
        {
            throw new InvalidOperationException("No main node found in records");
        }

        // Create the entity with all its complex properties
        return DeserializeNodeWithComplexProperties(
            mainNode,
            allRelatedNodes,
            requestedType,
            queryContext);
    }

    private List<EntityGroup> GroupRecordsByMainEntity(
        List<IRecord> records,
        GraphQueryContext queryContext)
    {
        var groups = new Dictionary<string, EntityGroup>();

        foreach (var record in records)
        {
            if (record.Values.Count >= 2 &&
                record[0] is global::Neo4j.Driver.INode mainNode &&
                record[1] is IList<object> relatedNodes)
            {
                var entityId = mainNode.ElementId;

                if (!groups.TryGetValue(entityId, out var group))
                {
                    group = new EntityGroup { MainNode = mainNode, RelatedNodes = [] };
                    groups[entityId] = group;
                }

                // Add related nodes from this record
                group.RelatedNodes.AddRange(relatedNodes);
            }
        }

        return groups.Values.ToList();
    }

    private object ProcessEntityWithComplexProperties(
        global::Neo4j.Driver.INode mainNode,
        List<object> allRelatedNodes,
        Type requestedType,
        GraphQueryContext queryContext)
    {
        return DeserializeNodeWithComplexProperties(
            mainNode,
            allRelatedNodes,
            requestedType,
            queryContext);
    }

    // Helper class to group records by main entity
    private class EntityGroup
    {
        public global::Neo4j.Driver.INode MainNode { get; set; } = null!;
        public List<object> RelatedNodes { get; set; } = [];
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

    private object DeserializeNodeWithComplexProperties(
        global::Neo4j.Driver.INode node,
        IList<object> relatedNodes,
        Type requestedType,
        GraphQueryContext queryContext)
    {
        // First create the main entity
        var entity = DeserializeNodeFromNeo4jNode(
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
        IList<object> relatedNodes,
        GraphQueryContext queryContext)
    {
        if (queryContext.TargetEntitySchema?.HasComplexProperties != true)
        {
            _logger?.LogDebug("No complex properties to populate for {Type}", entity.GetType().Name);
            return;
        }

        var type = entity.GetType();
        var complexPropsByRelType = new Dictionary<string, List<object>>();

        foreach (var relatedNodeObj in relatedNodes)
        {
            if (relatedNodeObj is not IDictionary<string, object> dict)
            {
                _logger?.LogWarning("Expected dictionary for related node, got {Type}", relatedNodeObj?.GetType().Name);
                continue;
            }

            var neo4jNode = dict["Node"] as global::Neo4j.Driver.INode;
            var relType = dict["RelType"] as string;

            if (neo4jNode == null || relType == null)
            {
                _logger?.LogWarning("Missing Node or RelType in related node data");
                continue;
            }

            // Convert relationship type back to property name
            var propName = GraphDataModel.RelationshipTypeNameToPropertyName(relType);

            if (!complexPropsByRelType.ContainsKey(propName))
                complexPropsByRelType[propName] = [];

            // Deserialize the complex property node
            try
            {
                var complexEntity = DeserializeNodeFromNeo4jNode(
                    neo4jNode,
                    typeof(object), // Let the serializer figure out the actual type
                    queryContext.UseMostDerivedType);

                complexPropsByRelType[propName].Add(complexEntity);

                _logger?.LogDebug("Deserialized complex property {PropName} of type {Type}",
                    propName, complexEntity.GetType().Name);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to deserialize complex property {PropName}", propName);
            }
        }

        // Set the properties on the entity using schema information
        SetComplexPropertiesOnEntity(entity, complexPropsByRelType, queryContext);
    }

    private void SetComplexPropertiesOnEntity(
    object entity,
    Dictionary<string, List<object>> complexPropsByRelType,
    GraphQueryContext queryContext)
    {
        var type = entity.GetType();
        var schema = queryContext.TargetEntitySchema!;

        foreach (var (propName, values) in complexPropsByRelType)
        {
            var prop = type.GetProperty(propName, BindingFlags.Public | BindingFlags.Instance);
            if (prop?.CanWrite != true)
            {
                _logger?.LogWarning("Property {PropName} not found or not writable on {Type}", propName, type.Name);
                continue;
            }

            // Check the schema to understand the property type
            var neo4jPropName = GraphDataModel.RelationshipTypeNameToPropertyName(propName);
            if (!schema.Properties.TryGetValue(neo4jPropName, out var propSchema))
            {
                _logger?.LogWarning("No schema found for property {PropName}", propName);
                continue;
            }

            try
            {
                if (propSchema.PropertyType == PropertyType.Complex)
                {
                    // Single complex property
                    prop.SetValue(entity, values.FirstOrDefault());
                }
                else if (propSchema.PropertyType == PropertyType.ComplexCollection)
                {
                    // Collection of complex properties
                    var collection = CreateCollection(prop.PropertyType, values);
                    prop.SetValue(entity, collection);
                }

                _logger?.LogDebug("Set property {PropName} with {Count} values", propName, values.Count);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to set property {PropName} on {Type}", propName, type.Name);
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
        // Check if we have the complex properties pattern: main node + related nodes
        if (record.Values.Count == 2 &&
            queryContext.TargetEntitySchema?.HasComplexProperties == true)
        {
            if (TryDeserializeNodeWithComplexProperties(record, requestedType, queryContext, out var complexResult))
            {
                return complexResult!;
            }
        }

        // If we have exactly one value, handle it based on its type
        if (record.Values.Count == 1)
        {
            var value = record.Values.First().Value;
            return DeserializeSingleValue(value, requestedType, queryContext);
        }

        // For multiple values, we need to understand the pattern
        if (record.Values.Count == 3)
        {
            // Check for path pattern (source-relationship-target)
            if (TryDeserializePathPattern(record, requestedType, queryContext, out var result))
            {
                return result!;
            }
        }

        // If we can't handle it, provide a helpful error message
        var valueTypes = string.Join(", ", record.Values.Select(v => v.Value?.GetType().Name ?? "null"));
        throw new NotSupportedException(
            $"Cannot deserialize record with {record.Values.Count} values to type {requestedType}. " +
            $"Record contains: {valueTypes}. Schema HasComplexProperties: {queryContext.TargetEntitySchema?.HasComplexProperties}");
    }

    private object DeserializeSingleValue(object? value, Type requestedType, GraphQueryContext queryContext)
    {
        if (value == null)
        {
            return null!;
        }

        // Handle Neo4j entities
        if (value is global::Neo4j.Driver.INode node)
        {
            return DeserializeNodeFromNeo4jNode(
                node,
                requestedType,
                queryContext.UseMostDerivedType);
        }

        if (value is global::Neo4j.Driver.IRelationship relationship)
        {
            return DeserializeRelationshipFromNeo4jRelationship(
                relationship,
                requestedType);
        }

        // Handle scalar values - use the serializer's method which now uses EntitySerializerBase
        var converted = EntitySerializerBase.ConvertFromNeo4jValue(value, requestedType);
        if (converted == null && !IsNullableType(requestedType))
        {
            throw new NotSupportedException($"Cannot convert value of type {value.GetType()} to non-nullable type {requestedType}");
        }
        return converted ?? throw new NotSupportedException($"Cannot convert value of type {value.GetType()} to {requestedType}");
    }

    private static bool IsNullableType(Type type)
    {
        return !type.IsValueType || Nullable.GetUnderlyingType(type) != null;
    }

    private bool TryDeserializeNodeWithComplexProperties(
        IRecord record,
        Type requestedType,
        GraphQueryContext queryContext,
        out object? result)
    {
        result = null;

        // Look for the pattern: INode + List<object> (from your Cypher query)
        if (record.Values.Count != 2) return false;

        var firstValue = record[0];
        var secondValue = record[1];

        // The pattern should be: main node, then related nodes list
        if (firstValue is global::Neo4j.Driver.INode mainNode &&
            secondValue is IList<object> relatedNodes)
        {
            _logger?.LogDebug("Deserializing node with {Count} complex properties", relatedNodes.Count);

            result = DeserializeNodeWithComplexProperties(
                mainNode,
                relatedNodes,
                requestedType,
                queryContext);
            return true;
        }

        return false;
    }

    private bool TryDeserializePathPattern(
        IRecord record,
        Type requestedType,
        GraphQueryContext queryContext,
        out object? result)
    {
        result = null;

        // Check if it's a path pattern: node-relationship-node
        if (record[0] is global::Neo4j.Driver.INode sourceNode &&
            record[1] is global::Neo4j.Driver.IRelationship rel &&
            record[2] is global::Neo4j.Driver.INode targetNode)
        {
            // Determine what to return based on the requested type
            if (typeof(IRelationship).IsAssignableFrom(requestedType))
            {
                result = DeserializeRelationshipFromNeo4jRelationship(rel, requestedType);
                return true;
            }

            if (typeof(INode).IsAssignableFrom(requestedType))
            {
                // This is still a bit arbitrary - might need more context
                // Consider adding metadata to the query context to indicate which node to return
                result = DeserializeNodeFromNeo4jNode(
                    sourceNode,
                    requestedType,
                    queryContext.UseMostDerivedType);
                return true;
            }

            // If requestedType is IGraphPathSegment<,,>, handle that here
            if (requestedType.IsGenericType &&
                requestedType.GetGenericTypeDefinition() == typeof(IGraphPathSegment<,,>))
            {
                // Create the path segment
                var genericArgs = requestedType.GetGenericArguments();
                var concreteType = typeof(GraphPathSegment<,,>).MakeGenericType(genericArgs);

                var source = DeserializeNodeFromNeo4jNode(sourceNode, genericArgs[0], queryContext.UseMostDerivedType);
                var relationship = DeserializeRelationshipFromNeo4jRelationship(rel, genericArgs[1]);
                var target = DeserializeNodeFromNeo4jNode(targetNode, genericArgs[2], queryContext.UseMostDerivedType);

                result = Activator.CreateInstance(concreteType, source, relationship, target);
                return true;
            }
        }

        return false;
    }

    public object DeserializeNodeFromNeo4jNode(
        global::Neo4j.Driver.INode neo4jNode,
        Type targetType,
        bool useMostDerivedType = true)
    {
        ArgumentNullException.ThrowIfNull(neo4jNode);
        ArgumentNullException.ThrowIfNull(targetType);

        // Resolve the most derived type if requested
        if (useMostDerivedType)
        {
            // Use the label from the Neo4j node to find the most derived type
            var label = neo4jNode.Labels[0];
            var resolvedType = Labels.GetMostDerivedType(targetType, label)
                ?? throw new GraphException($"No type found for label '{label}' that is assignable to {targetType.Name}. " +
                    "Ensure the label matches a registered type in the GraphDataModel.");

            if (resolvedType != targetType)
            {
                targetType = resolvedType;
                _logger.LogDebug($"Resolved type {resolvedType.Name} from labels {label} for requested type {targetType.Name}");
            }
        }

        return graphContext.EntityFactory.CreateInstance(targetType, neo4jNode);
    }

    public object DeserializeRelationshipFromNeo4jRelationship(
        global::Neo4j.Driver.IRelationship neo4jRelationship,
        Type targetType,
        bool useMostDerivedType = true)
    {
        ArgumentNullException.ThrowIfNull(neo4jRelationship);
        ArgumentNullException.ThrowIfNull(targetType);

        // Resolve the most derived type if requested
        if (useMostDerivedType)
        {
            // Use the label from the Neo4j relationship to find the most derived type
            var label = neo4jRelationship.Type;
            var resolvedType = Labels.GetMostDerivedType(targetType, label)
                ?? throw new GraphException($"No type found for label '{label}' that is assignable to {targetType.Name}. " +
                    "Ensure the label matches a registered type in the GraphDataModel.");

            if (resolvedType != targetType)
            {
                targetType = resolvedType;
                _logger.LogDebug("Resolved type {ResolvedType} from labels {Labels} for requested type {RequestedType}",
                    resolvedType.Name, label, targetType.Name);
            }
        }

        // Create the relationship instance
        return graphContext.EntityFactory.CreateInstance(targetType, neo4jRelationship);
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
