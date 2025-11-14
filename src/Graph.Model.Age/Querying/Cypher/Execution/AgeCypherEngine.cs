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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Cvoya.Graph.Model.Age.Core;
using Cvoya.Graph.Model.Age.Core.Entities;
using Cvoya.Graph.Model.Age.Querying.Cypher.Visitors;
using Cvoya.Graph.Model.Age.Querying.Cypher.Visitors.Core;
using Cvoya.Graph.Model.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Npgsql.Age;
using Npgsql.Age.Types;

/// <summary>
/// Executes LINQ queries against AGE by converting them to Cypher.
/// </summary>
internal sealed class AgeCypherEngine
{
    private readonly AgeGraphContext _graphContext;
    private readonly ILogger<AgeCypherEngine> _logger;
    private readonly ILoggerFactory? _loggerFactory;
    private readonly EntityFactory _entityFactory;
    private readonly AgeEntityMapper _entityMapper;
    
    // New shared architecture components
    private readonly AgeResultProcessor _ageResultProcessor;
    private readonly ResultMaterializer<AgeValueConverter> _sharedMaterializer;
    
    public AgeCypherEngine(AgeGraphContext graphContext, ILoggerFactory loggerFactory)
    {
        _graphContext = graphContext ?? throw new ArgumentNullException(nameof(graphContext));
        _loggerFactory = loggerFactory;
        _logger = loggerFactory?.CreateLogger<AgeCypherEngine>() ?? NullLogger<AgeCypherEngine>.Instance;
        _entityFactory = new EntityFactory(loggerFactory);
        _entityMapper = new AgeEntityMapper(_entityFactory, loggerFactory);
        
        // Initialize shared architecture components
        _ageResultProcessor = new AgeResultProcessor(_entityFactory, _entityMapper, loggerFactory);
        var ageValueConverter = new AgeValueConverter();
        _sharedMaterializer = new ResultMaterializer<AgeValueConverter>(_entityFactory, ageValueConverter, loggerFactory);
    }

    public async Task<T?> ExecuteAsync<T>(
        Expression expression,
        AgeGraphTransaction? transaction,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Executing query for type {Type}", typeof(T).Name);

            // Detect if this is an aggregation operation
            var aggregationOp = DetectAggregationType(expression);
            var isAggregation = aggregationOp != null;
            
            // Extract the element type from the expression
            // For aggregation operations, use the result type T instead of the source element type
            var elementType = isAggregation ? typeof(T) : ExtractElementType(typeof(T), expression);

            // Detect if we have a projection
            var (hasProjection, projectionExpression, sourceElementType) = DetectProjection(expression);

            // Build and execute the Cypher query
            // For projections, use the source element type (e.g., relationship type) not the projected result type
            var cypherElementType = hasProjection ? sourceElementType : elementType;
            var (cypher, parameters) = BuildCypherQuery(cypherElementType, expression);

            _logger.LogDebug("Generated Cypher: {Cypher}", cypher);
            var materializedType = hasProjection ? sourceElementType : elementType;
            var result = await ExecuteQueryWithSharedArchitecture<T>(
                cypher,
                parameters,
                materializedType,
                transaction,
                cancellationToken,
                hasProjection ? projectionExpression : null,
                hasProjection ? elementType : null,
                aggregationOp);

            if (aggregationOp == "Single" && result is System.Collections.IEnumerable enumerable)
            {
                var count = 0;
                foreach (var _ in enumerable)
                {
                    count++;
                    if (count > 1)
                    {
                        throw new InvalidOperationException("Sequence contains more than one element");
                    }
                }
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to execute query for type {Type}", typeof(T).Name);
            throw;
        }
    }

    private (string cypher, Dictionary<string, object?> parameters) BuildCypherQuery(Type elementType, Expression expression)
    {
        // Use the visitor pattern for sophisticated LINQ query translation
        var context = new CypherQueryContext(elementType, _loggerFactory);
        var visitor = new AgeCypherQueryVisitor(context);
        
        try
        {
            // Let the visitor handle the expression tree traversal
            visitor.Visit(expression);
            
            // Finalize the query - adds default projections if needed
            // This ensures path segment queries work correctly even without explicit ToList/ToArray
            visitor.FinalizeQuery(elementType);
            
            // Get the built query and parameters
            var query = context.GetQuery();
            var parameters = context.GetParameters().ToDictionary(kv => kv.Key, kv => kv.Value);
            
            _logger?.LogInformation($"Generated Cypher query via visitor:\n{query}");
            return (query, parameters);
        }
        catch (NotSupportedException ex)
        {
            _logger?.LogError(ex, "Visitor-based translation failed for element type {ElementType}", elementType.Name);
            throw;
        }
    }

    private string? DetectAggregationType(Expression expression)
    {
        Expression current = expression;
        while (current is MethodCallExpression methodCall)
        {
            var methodName = methodCall.Method.Name;
            
            // Check for Count/LongCount markers (both sync and async)
            if (methodName == "Count" || methodName == "CountAsync" || methodName == "CountAsyncMarker" || 
                methodName == "LongCount" || methodName == "LongCountAsync" || methodName == "LongCountAsyncMarker")
            {
                return "Count";
            }
            
            // Check for Any markers (both sync and async)
            if (methodName == "Any" || methodName == "AnyAsync" || methodName == "AnyAsyncMarker")
            {
                return "Any";
            }
            
            // Check for All markers
            if (methodName == "All" || methodName == "AllAsync" || methodName == "AllAsyncMarker")
            {
                return "All";
            }
            
            // Check for Sum markers
            if (methodName == "Sum" || methodName == "SumAsync" || methodName == "SumAsyncMarker")
            {
                return "Sum";
            }
            
            // Check for Average markers
            if (methodName == "Average" || methodName == "AverageAsync" || methodName == "AverageAsyncMarker")
            {
                return "Average";
            }
            
            // Check for Min markers
            if (methodName == "Min" || methodName == "MinAsync" || methodName == "MinAsyncMarker")
            {
                return "Min";
            }
            
            // Check for Max markers
            if (methodName == "Max" || methodName == "MaxAsync" || methodName == "MaxAsyncMarker")
            {
                return "Max";
            }
            
            // Check for First/Last/Single markers
            if (methodName == "First" || methodName == "FirstAsync" || methodName == "FirstAsyncMarker" ||
                methodName == "FirstOrDefault" || methodName == "FirstOrDefaultAsync" || methodName == "FirstOrDefaultAsyncMarker")
            {
                return "First";
            }
            
            if (methodName == "Last" || methodName == "LastAsync" || methodName == "LastAsyncMarker" ||
                methodName == "LastOrDefault" || methodName == "LastOrDefaultAsync" || methodName == "LastOrDefaultAsyncMarker")
            {
                return "Last";
            }
            
            if (methodName == "Single" || methodName == "SingleAsync" || methodName == "SingleAsyncMarker" ||
                methodName == "SingleOrDefault" || methodName == "SingleOrDefaultAsync" || methodName == "SingleOrDefaultAsyncMarker")
            {
                return "Single";
            }
            
            if (methodCall.Arguments.Count > 0)
            {
                current = methodCall.Arguments[0];
            }
            else
            {
                break;
            }
        }
        
        return null;
    }

    private (bool hasProjection, LambdaExpression? projectionExpression, Type sourceElementType) DetectProjection(Expression expression)
    {
        Expression current = expression;
        while (current is MethodCallExpression methodCall)
        {
            if (methodCall.Method.Name == "Select" && methodCall.Arguments.Count >= 2)
            {
                // Found a Select operation
                var lambdaArg = methodCall.Arguments[1];
                
                if (lambdaArg is UnaryExpression { NodeType: ExpressionType.Quote } quote)
                {
                    lambdaArg = quote.Operand;
                }
                
                if (lambdaArg is LambdaExpression lambda)
                {
                    // Get the source element type from the first argument
                    var sourceType = methodCall.Arguments[0].Type;
                    if (sourceType.IsGenericType)
                    {
                        var genericArgs = sourceType.GetGenericArguments();
                        if (genericArgs.Length > 0)
                        {
                            return (true, lambda, genericArgs[0]);
                        }
                    }
                }
            }
            
            if (methodCall.Arguments.Count > 0)
            {
                current = methodCall.Arguments[0];
            }
            else
            {
                break;
            }
        }
        
        return (false, null, typeof(object));
    }
    

    private async Task<T?> ExecuteQueryWithSharedArchitecture<T>(
        string cypher,
        Dictionary<string, object?> parameters,
        Type elementType,
        AgeGraphTransaction? transaction,
        CancellationToken cancellationToken,
        LambdaExpression? projectionExpression = null,
        Type? projectionResultType = null,
        string? aggregationType = null)
    {
        // Build the command - use custom column definitions for projections
        NpgsqlCommand command;
        if (projectionExpression != null)
        {
            // Extract column names from the projection expression
            var columnDefs = BuildColumnDefinitions(projectionExpression);
            command = CreateCypherCommandWithColumns(_graphContext.Connection, _graphContext.GraphName, cypher, parameters, columnDefs);
        }
        else
        {
            command = _graphContext.Connection.CreateCypherCommand(_graphContext.GraphName, cypher, parameters);
        }

        command.Transaction = transaction?.Transaction;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

        // Use AgeResultProcessor to convert raw results to EntityInfo structures
        var entityInfos = await _ageResultProcessor.ProcessAsync(
            reader, elementType, cancellationToken, projectionExpression, projectionResultType, aggregationType);

        // Use shared ResultMaterializer to convert EntityInfo to final objects
        return await _sharedMaterializer.MaterializeAsync<T>(entityInfos, cancellationToken);
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

    private string BuildColumnDefinitions(LambdaExpression projectionExpression)
    {
        var body = projectionExpression.Body;
        
        // Handle simple property projection: Select(p => p.FirstName)
        if (body is MemberExpression memberExpr)
        {
            var columnName = memberExpr.Member.Name;
            // Use double quotes for PostgreSQL identifier escaping
            return $"(\"{columnName}\" agtype)";
        }
        
        // Handle anonymous type projection: Select(p => new { p.FirstName, p.LastName })
        if (body is NewExpression newExpr)
        {
            var columns = new List<string>();
            
            for (int i = 0; i < newExpr.Arguments.Count; i++)
            {
                var member = newExpr.Members?[i];
                var columnName = member?.Name ?? $"field{i}";
                var memberType = member switch
                {
                    PropertyInfo pi => pi.PropertyType,
                    FieldInfo fi => fi.FieldType,
                    _ => newExpr.Arguments[i].Type
                };
                
                // Apache AGE doesn't support proper identifier escaping in Cypher
                // Use c_ prefix in Cypher RETURN, then map to actual property name in SQL column definition
                var cypherAlias = $"c_{columnName}";
                
                if (IsPathSegmentType(memberType))
                {
                    // Path segments are expanded into three columns: source node, relationship, target node
                    columns.Add($"\"{cypherAlias}_src\" agtype");
                    columns.Add($"\"{cypherAlias}_r\" agtype");
                    columns.Add($"\"{cypherAlias}_tgt\" agtype");
                }
                else
                {
                    // Use double quotes for PostgreSQL identifier escaping in SQL portion
                    columns.Add($"\"{cypherAlias}\" agtype");
                }
            }
            
            return $"({string.Join(", ", columns)})";
        }
        
        // Default fallback
        return "(result agtype)";
    }

    private static bool IsPathSegmentType(Type type)
    {
        if (!type.IsGenericType)
        {
            return false;
        }

        if (type.GetGenericTypeDefinition().Name.Contains("GraphPathSegment", StringComparison.Ordinal))
        {
            return true;
        }

        return type.GetInterfaces()
            .Any(i => i.IsGenericType && i.GetGenericTypeDefinition().Name.Contains("IGraphPathSegment", StringComparison.Ordinal));
    }

    private NpgsqlCommand CreateCypherCommandWithColumns(
        NpgsqlConnection connection,
        string graphName,
        string cypher,
        Dictionary<string, object?> parameters,
        string columnDefinitions)
    {
        // Build the SQL query with explicit column definitions
        // Escape the Cypher query for use in dollar-quoted string
        var escapedCypher = cypher.Replace("\\", "\\\\");
        
        // Serialize parameters to JSON
        var parametersJson = System.Text.Json.JsonSerializer.Serialize(parameters);
        var agtypeParams = new Agtype(parametersJson);
        
        // Build the full SQL query
        var sql = $"SELECT * FROM ag_catalog.cypher('{graphName}', $$ {escapedCypher} $$, $1) as {columnDefinitions};";
        
        var command = new NpgsqlCommand(sql, connection);
        command.Parameters.Add(new NpgsqlParameter { Value = agtypeParams, DataTypeName = "ag_catalog.agtype" });
        
        return command;
    }

}
