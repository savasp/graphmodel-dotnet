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

namespace Cvoya.Graph.Model.Age.Querying.Cypher.Execution;

using System.Linq.Expressions;
using System.Reflection;
using Cvoya.Graph.Model.Age.Core;
using Cvoya.Graph.Model.Age.Core.Entities;
using Cvoya.Graph.Model.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql.Age;
using Npgsql.Age.Types;

/// <summary>
/// Executes LINQ queries against AGE by converting them to Cypher.
/// </summary>
internal sealed class AgeCypherEngine
{
    private readonly AgeGraphContext _graphContext;
    private readonly ILogger<AgeCypherEngine> _logger;
    private readonly EntityFactory _entityFactory;
    private readonly AgeEntityMapper _entityMapper;

    public AgeCypherEngine(AgeGraphContext graphContext, ILoggerFactory? loggerFactory)
    {
        _graphContext = graphContext ?? throw new ArgumentNullException(nameof(graphContext));
        _logger = loggerFactory?.CreateLogger<AgeCypherEngine>() ?? NullLogger<AgeCypherEngine>.Instance;
        _entityFactory = new EntityFactory(loggerFactory);
        _entityMapper = new AgeEntityMapper(_entityFactory, loggerFactory);
    }

    public async Task<T?> ExecuteAsync<T>(
        Expression expression,
        AgeGraphTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Executing query for type {Type}", typeof(T).Name);

            // Extract the element type from the expression
            var elementType = ExtractElementType(typeof(T), expression);

            // Build and execute the Cypher query
            var (cypher, parameters) = BuildCypherQuery(elementType, expression);

            _logger.LogDebug("Generated Cypher: {Cypher}", cypher);

            // Execute the query
            var results = await ExecuteQueryAsync(cypher, parameters, elementType, transaction, cancellationToken);

            // Materialize the results
            return MaterializeResults<T>(results, typeof(T));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to execute query for type {Type}", typeof(T).Name);
            throw;
        }
    }

    private (string cypher, Dictionary<string, object?> parameters) BuildCypherQuery(Type elementType, Expression expression)
    {
        // For now, implement basic query building
        // This is a simplified version - full implementation would parse the entire expression tree

        var parameters = new Dictionary<string, object?>();
        string cypher;

        // Check if this is a node or relationship query
        if (typeof(INode).IsAssignableFrom(elementType))
        {
            // Build node query with complex properties
            var label = GetLabel(elementType);
            var baseMatch = $"MATCH (n:{label})";
            
            // Add OPTIONAL MATCH for complex properties
            var complexPropertyMatches = new List<string>();
            var complexProps = GetComplexProperties(elementType);
            _logger?.LogInformation($"Building query for {elementType.Name} with {complexProps.Count} complex properties: {string.Join(", ", complexProps.Select(p => p.Name))}");
            
            foreach (var prop in complexProps)
            {
                var relType = GraphDataModel.PropertyNameToRelationshipTypeName(prop.Name);
                _logger?.LogInformation($"Adding OPTIONAL MATCH for complex property '{prop.Name}' with relationship type '{relType}'");
                complexPropertyMatches.Add($"OPTIONAL MATCH (n)-[r_{prop.Name}:{relType}]->(cp_{prop.Name})");
            }

            // Check for Where clauses
            var whereClause = ExtractWhereClause(expression, parameters);
            
            if (complexPropertyMatches.Count > 0)
            {
                // For AGE, we'll use a simpler approach without collect()
                // Build RETURN clause that returns complex property nodes directly
                var returnItems = new List<string> { "n" };
                foreach (var prop in complexProps)
                {
                    returnItems.Add($"cp_{prop.Name}");
                }
                
                var allMatches = string.Join("\n", new[] { baseMatch }.Concat(complexPropertyMatches));
                if (!string.IsNullOrEmpty(whereClause))
                {
                    cypher = $"{allMatches}\nWHERE {whereClause}\nRETURN {string.Join(", ", returnItems)}";
                }
                else
                {
                    cypher = $"{allMatches}\nRETURN {string.Join(", ", returnItems)}";
                }
                
                _logger?.LogInformation($"Generated Cypher query:\n{cypher}");
            }
            else
            {
                // No complex properties, simple query
                if (!string.IsNullOrEmpty(whereClause))
                {
                    cypher = $"{baseMatch} WHERE {whereClause} RETURN n";
                }
                else
                {
                    cypher = $"{baseMatch} RETURN n";
                }
            }
        }
        else if (typeof(IRelationship).IsAssignableFrom(elementType))
        {
            // Build relationship query
            var label = GetLabel(elementType);
            cypher = $"MATCH ()-[r:{label}]->() RETURN r";

            // Check for Where clauses
            var whereClause = ExtractWhereClause(expression, parameters);
            if (!string.IsNullOrEmpty(whereClause))
            {
                cypher = $"MATCH ()-[r:{label}]->() WHERE {whereClause} RETURN r";
            }
        }
        else
        {
            throw new NotSupportedException($"Query type {elementType.Name} is not supported");
        }

        return (cypher, parameters);
    }

    private string GetLabel(Type type)
    {
        // Get label from attribute or use type name
        var nodeAttribute = type.GetCustomAttributes(typeof(NodeAttribute), true).FirstOrDefault() as NodeAttribute;
        if (nodeAttribute != null && !string.IsNullOrEmpty(nodeAttribute.Label))
        {
            return nodeAttribute.Label;
        }

        var relAttribute = type.GetCustomAttributes(typeof(RelationshipAttribute), true).FirstOrDefault() as RelationshipAttribute;
        if (relAttribute != null && !string.IsNullOrEmpty(relAttribute.Label))
        {
            return relAttribute.Label;
        }

        // Fall back to type name if no label specified
        return type.Name;
    }

    private List<PropertyInfo> GetComplexProperties(Type type)
    {
        var complexProps = new List<PropertyInfo>();
        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
        
        foreach (var prop in properties)
        {
            // Skip simple properties and collections of simple types
            if (GraphDataModel.IsSimple(prop.PropertyType) || 
                GraphDataModel.IsCollectionOfSimple(prop.PropertyType))
            {
                continue;
            }
            
            // Skip graph-specific properties
            if (prop.Name == nameof(IEntity.Id) || 
                prop.Name == nameof(INode.Labels) ||
                prop.Name == nameof(IRelationship.StartNodeId) ||
                prop.Name == nameof(IRelationship.EndNodeId))
            {
                continue;
            }
            
            complexProps.Add(prop);
        }
        
        return complexProps;
    }

    private string ExtractWhereClause(Expression expression, Dictionary<string, object?> parameters)
    {
        // Simple Where clause extraction for basic equality checks
        // The expression might be wrapped in FirstOrDefaultAsyncMarker or other markers
        // Unwrap to get to the Where call
        
        Expression current = expression;
        while (current is MethodCallExpression methodCall)
        {
            // If this is a Where call, process it
            if (methodCall.Method.Name == "Where" && methodCall.Arguments.Count >= 2)
            {
                // Get the lambda expression (second argument to Where)
                var lambdaArg = methodCall.Arguments[1];
                
                // Unwrap Quote if present
                if (lambdaArg is UnaryExpression { NodeType: ExpressionType.Quote } quote)
                {
                    lambdaArg = quote.Operand;
                }
                
                if (lambdaArg is LambdaExpression lambda)
                {
                    var body = lambda.Body;
                    
                    // Handle Convert wrapper (Convert(n, IEntity).Id)
                    if (body is UnaryExpression { NodeType: ExpressionType.Convert } convert)
                    {
                        body = convert.Operand;
                    }
                    
                    // Now check for equality
                    if (body is BinaryExpression { NodeType: ExpressionType.Equal } binary)
                    {
                        // Get left side - handle Convert if present
                        Expression left = binary.Left;
                        if (left is UnaryExpression { NodeType: ExpressionType.Convert } leftConvert)
                        {
                            left = leftConvert.Operand;
                        }
                        
                        if (left is MemberExpression leftMember && leftMember.Member.Name == "Id")
                        {
                            // Get right side value
                            object? value = null;
                            if (binary.Right is ConstantExpression constant)
                            {
                                value = constant.Value;
                            }
                            else if (binary.Right is MemberExpression rightMember)
                            {
                                // Closure variable
                                try
                                {
                                    value = Expression.Lambda(rightMember).Compile().DynamicInvoke();
                                }
                                catch
                                {
                                    // Ignore if we can't evaluate
                                }
                            }

                            if (value != null)
                            {
                                var paramName = $"param_{parameters.Count}";
                                parameters[paramName] = value;
                                return $"n.Id = ${paramName}";
                            }
                        }
                    }
                }
                
                // If we found a Where but couldn't parse it, stop here
                return string.Empty;
            }
            
            // Move to the first argument (the source of the method call)
            if (methodCall.Arguments.Count > 0)
            {
                current = methodCall.Arguments[0];
            }
            else
            {
                break;
            }
        }

        return string.Empty;
    }

    private async Task<List<object>> ExecuteQueryAsync(
        string cypher,
        Dictionary<string, object?> parameters,
        Type elementType,
        AgeGraphTransaction transaction,
        CancellationToken cancellationToken)
    {
        var results = new List<object>();
        var complexProps = GetComplexProperties(elementType);
        var hasComplexProps = complexProps.Count > 0;
        _logger.LogDebug("Executing Cypher query against AGE: {Cypher}", cypher);
        await using var command = transaction.Connection.CreateCypherCommand(_graphContext.GraphName, cypher, parameters);
        command.Transaction = transaction.Transaction;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            if (typeof(INode).IsAssignableFrom(elementType))
            {
                // Read the main node (first column)
                var nodeAgtype = reader.GetFieldValue<Agtype>(0);
                
                if (!nodeAgtype.IsVertex)
                {
                    continue;
                }

                var vertex = nodeAgtype.GetVertex();
                var entityInfo = _entityMapper.MapVertex(vertex, elementType);

                // If we have complex properties, read them from additional columns
                if (hasComplexProps)
                {
                    var complexPropertyDict = new Dictionary<string, Property>(StringComparer.Ordinal);

                    for (int i = 0; i < complexProps.Count; i++)
                    {
                        var prop = complexProps[i];
                        var columnIndex = i + 1; // +1 because first column is the node
                        
                        try
                        {
                            var propAgtype = reader.GetFieldValue<Agtype>(columnIndex);
                            
                            // Check if it's a vertex (complex property node)
                            if (propAgtype.IsVertex)
                            {
                                var complexVertex = propAgtype.GetVertex();
                                var complexEntityInfo = _entityMapper.MapVertex(complexVertex, prop.PropertyType);
                                
                                // Single complex property
                                complexPropertyDict[prop.Name] = new Property(prop, prop.Name, false, complexEntityInfo);
                            }
                        }
                        catch
                        {
                            // If reading complex property fails (e.g., column is null), just skip it
                            // This is expected for OPTIONAL MATCH that doesn't find a match
                            continue;
                        }
                    }

                    // Merge complex properties into entity info
                    if (complexPropertyDict.Count > 0)
                    {
                        var allComplexProps = new Dictionary<string, Property>(entityInfo.ComplexProperties, StringComparer.Ordinal);
                        foreach (var kvp in complexPropertyDict)
                        {
                            allComplexProps[kvp.Key] = kvp.Value;
                        }

                        entityInfo = new EntityInfo(
                            entityInfo.ActualType,
                            entityInfo.Label,
                            entityInfo.ActualLabels,
                            entityInfo.SimpleProperties,
                            allComplexProps);
                    }
                }

                var node = _entityFactory.Deserialize(entityInfo);
                results.Add(node);
            }
            else if (typeof(IRelationship).IsAssignableFrom(elementType))
            {
                var agtype = reader.GetFieldValue<Agtype>(0);
                
                if (agtype.IsEdge)
                {
                    var edge = agtype.GetEdge();
                    var entityInfo = _entityMapper.MapEdge(edge, elementType);
                    var relationship = _entityFactory.Deserialize(entityInfo);
                    results.Add(relationship);
                }
            }
        }

        return results;
    }

    private T? MaterializeResults<T>(List<object> results, Type resultType)
    {
        // Handle different result types
        if (resultType.IsGenericType)
        {
            var genericTypeDef = resultType.GetGenericTypeDefinition();

            // IEnumerable<T>, List<T>, etc.
            if (genericTypeDef == typeof(IEnumerable<>) ||
                genericTypeDef == typeof(List<>) ||
                genericTypeDef == typeof(IList<>) ||
                genericTypeDef == typeof(ICollection<>))
            {
                var elementType = resultType.GetGenericArguments()[0];
                var listType = typeof(List<>).MakeGenericType(elementType);
                var list = (System.Collections.IList)Activator.CreateInstance(listType)!;

                foreach (var item in results)
                {
                    list.Add(item);
                }

                return (T?)list;
            }
        }

        // Single element or null
        return results.Count > 0 ? (T?)results[0] : default;
    }

    private static Type ExtractElementType(Type resultType, Expression expression)
    {
        // If the result type is a collection, extract the element type
        if (resultType.IsGenericType)
        {
            var genericArgs = resultType.GetGenericArguments();
            if (genericArgs.Length == 1)
            {
                return genericArgs[0];
            }
        }

        // Try to extract from the expression tree
        return ExtractElementTypeFromExpression(expression) ?? resultType;
    }

    private static Type? ExtractElementTypeFromExpression(Expression expression)
    {
        if (expression is ConstantExpression constant)
        {
            var queryableType = constant.Type;
            if (queryableType.IsGenericType)
            {
                var interfaces = queryableType.GetInterfaces();
                foreach (var iface in interfaces)
                {
                    if (iface.IsGenericType && iface.GetGenericTypeDefinition() == typeof(IQueryable<>))
                    {
                        return iface.GetGenericArguments()[0];
                    }
                }
            }
        }

        if (expression is MethodCallExpression methodCall && methodCall.Arguments.Count > 0)
        {
            return ExtractElementTypeFromExpression(methodCall.Arguments[0]);
        }

        return null;
    }
}
