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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Cvoya.Graph.Model;
using Cvoya.Graph.Model.Age.Core;
using Cvoya.Graph.Model.Age.Core.Entities;
using Cvoya.Graph.Model.Age.Querying.Cypher.Visitors.Core;
using Cvoya.Graph.Model.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Npgsql.Age;

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
            var aggregationOp = AggregationDetector.DetectAggregationType(expression);
            var isAggregation = aggregationOp != null;

            // Handle ToDictionary specially — execute the underlying query then build the dictionary client-side
            if (aggregationOp == "ToDictionary")
            {
                return await ExecuteToDictionaryAsync<T>(expression, transaction, cancellationToken);
            }

            // Extract the element type from the expression
            // For aggregation operations, use the result type T instead of the source element type
            var elementType = isAggregation ? typeof(T) : QueryExpressionAnalyzer.ExtractElementType(typeof(T), expression);

            // Detect if we have a projection
            var (hasProjection, projectionExpression, sourceElementType) = QueryExpressionAnalyzer.DetectProjection(expression);

            // Build and execute the Cypher query
            // For projections, use the source element type (e.g., relationship type) not the projected result type
            var cypherElementType = hasProjection ? sourceElementType : elementType;
            var (cypher, parameters) = BuildCypherQuery(cypherElementType, expression);

            _logger.LogTrace("Generated Cypher: {Cypher}", cypher);
            // For projections to scalar types, use the projected result type for materialization
            // (the query returns scalar values, not full entities). For entity projections,
            // use the source element type.
            // For aggregations, always use typeof(T) since the Cypher returns scalar values
            // regardless of intermediate projections (e.g., count(*), sum(age)).
            var projectedElementType = hasProjection && !isAggregation
                ? QueryExpressionAnalyzer.ExtractProjectedResultType(typeof(T), projectionExpression)
                : elementType;
            var materializedType = isAggregation ? typeof(T) : (hasProjection ? projectedElementType : elementType);
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
        catch (Exception ex) when (ex is NpgsqlException or PostgresException)
        {
            var correlationId = Guid.NewGuid().ToString("N");
            _logger.LogError(ex, "Database query execution failed. CorrelationId: {CorrelationId}", correlationId);
            throw new GraphException(
                $"Query execution failed: {ex.Message}. CorrelationId: {correlationId}.", ex);
        }
    }

    private (string cypher, Dictionary<string, object?> parameters) BuildCypherQuery(Type elementType, Expression expression)
    {
        var context = new CypherQueryContext(elementType, _loggerFactory, _graphContext.SchemaRegistry);
        var visitor = new AgeCypherQueryVisitor(context);

        try
        {
            visitor.Visit(expression);
            visitor.FinalizeQuery(elementType);

            var query = context.GetQuery();
            var parameters = context.GetParameters().ToDictionary(kv => kv.Key, kv => kv.Value);

            _logger?.LogInformation("Generated Cypher query via visitor:{NewLine}{Cypher}", Environment.NewLine, query);
            return (query, parameters);
        }
        catch (NotSupportedException ex)
        {
            _logger?.LogError(ex, "Visitor-based translation failed for element type {ElementType}", elementType.Name);
            throw;
        }
    }

    private static readonly ConcurrentDictionary<Type, Func<AgeCypherEngine, Expression, AgeGraphTransaction?, CancellationToken, Task<object?>>>
        ExecuteAsListDelegates = new();

    private static Func<AgeCypherEngine, Expression, AgeGraphTransaction?, CancellationToken, Task<object?>>
        BuildExecuteAsListDelegate(Type elementType)
    {
        var wrapperMethod = typeof(AgeCypherEngine)
            .GetMethod(nameof(ExecuteAsListAsyncBoxed), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
            ?.MakeGenericMethod(elementType)
            ?? throw new InvalidOperationException($"Could not construct ExecuteAsListAsyncBoxed<{elementType.Name}>");

        var engineParam = Expression.Parameter(typeof(AgeCypherEngine), "engine");
        var exprParam = Expression.Parameter(typeof(Expression), "expression");
        var txParam = Expression.Parameter(typeof(AgeGraphTransaction), "transaction");
        var ctParam = Expression.Parameter(typeof(CancellationToken), "cancellationToken");

        var call = Expression.Call(null, wrapperMethod, engineParam, exprParam, txParam, ctParam);

        return Expression.Lambda<Func<AgeCypherEngine, Expression, AgeGraphTransaction?, CancellationToken, Task<object?>>>(
            call, engineParam, exprParam, txParam, ctParam).Compile();
    }

    private static async Task<object?> ExecuteAsListAsyncBoxed<TElement>(
        AgeCypherEngine engine, Expression sourceExpression, AgeGraphTransaction? transaction, CancellationToken cancellationToken)
    {
        return await engine.ExecuteAsListAsync<TElement>(sourceExpression, transaction, cancellationToken).ConfigureAwait(false);
    }

    private static Func<object?, object?> CreateKeySelectorFunc(LambdaExpression keySelectorLambda, Type sourceElementType)
    {
        var parameter = Expression.Parameter(typeof(object), "item");
        var body = Expression.Convert(
            Expression.Invoke(keySelectorLambda, Expression.Convert(parameter, sourceElementType)),
            typeof(object));
        return Expression.Lambda<Func<object?, object?>>(body, parameter).Compile();
    }

    private async Task<T?> ExecuteToDictionaryAsync<T>(Expression expression, AgeGraphTransaction? transaction, CancellationToken cancellationToken)
    {
        // The expression is: ToDictionaryAsyncMarker(source.Expression, keySelector)
        // We need to:
        // 1. Extract the source expression and keySelector
        // 2. Execute the source as List<TSource>
        // 3. Build Dictionary<TKey, TSource> using the keySelector
        if (expression is not MethodCallExpression toDictCall || toDictCall.Arguments.Count < 2)
            throw new InvalidOperationException("Expected ToDictionaryAsyncMarker call with source and keySelector");

        var sourceExpression = toDictCall.Arguments[0];
        var keySelectorArg = toDictCall.Arguments[1];

        // Extract the keySelector lambda
        LambdaExpression keySelectorLambda;
        if (keySelectorArg is UnaryExpression { NodeType: ExpressionType.Quote } quote)
            keySelectorLambda = (LambdaExpression)quote.Operand;
        else
            keySelectorLambda = (LambdaExpression)keySelectorArg;

        // Extract source element type from the queryable
        var sourceElementType = QueryExpressionAnalyzer.ExtractElementTypeFromExpression(sourceExpression) ?? typeof(object);

        // Execute the underlying query as List<TSourceElement> via cached delegate (no reflection)
        var executeAsListDelegate = ExecuteAsListDelegates.GetOrAdd(sourceElementType, BuildExecuteAsListDelegate);
        var sourceList = (System.Collections.IEnumerable)(await executeAsListDelegate(this, sourceExpression, transaction, cancellationToken).ConfigureAwait(false))!;

        // Build dictionary from the list using the compiled key selector (no DynamicInvoke)
        var keySelectorFunc = CreateKeySelectorFunc(keySelectorLambda, sourceElementType);
        var dictType = typeof(Dictionary<,>).MakeGenericType(keySelectorLambda.ReturnType, sourceElementType);
        var dict = (System.Collections.IDictionary)Activator.CreateInstance(dictType)!;

        foreach (var item in sourceList)
        {
            var key = keySelectorFunc(item);
            dict.Add(key!, item);
        }

        return (T?)dict;
    }

    private async Task<List<TElement>> ExecuteAsListAsync<TElement>(
        Expression sourceExpression, AgeGraphTransaction? transaction, CancellationToken cancellationToken)
    {
        // Build a fresh query from the source expression
        var (cypher, parameters) = BuildCypherQuery(typeof(TElement), sourceExpression);

        _logger.LogDebug("ToDictionary source Cypher: {Cypher}", cypher);

        // Execute and materialize as list
        var entityInfos = await ExecuteRawQueryAsync(
            cypher, parameters, typeof(TElement), transaction, cancellationToken);

        var materialized = await _sharedMaterializer.MaterializeAsync<List<TElement>>(entityInfos, cancellationToken);
        return materialized ?? [];
    }

    private async Task<List<EntityInfo>> ExecuteRawQueryAsync(
        string cypher, Dictionary<string, object?> parameters, Type elementType,
        AgeGraphTransaction? transaction, CancellationToken cancellationToken)
    {
        var command = _graphContext.Connection.CreateCypherCommand(_graphContext.GraphName, cypher, parameters);
        command.Transaction = transaction?.Transaction;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await _ageResultProcessor.ProcessAsync(reader, elementType, cancellationToken).ConfigureAwait(false);
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
        // Build the command - use custom column definitions for projections or path segments
        NpgsqlCommand command;
        var isPathSegmentType = elementType.IsGenericType &&
            (elementType.GetGenericTypeDefinition().Name.Contains("GraphPathSegment", StringComparison.Ordinal) ||
             elementType.GetGenericTypeDefinition().Name.Contains("IGraphPathSegment", StringComparison.Ordinal));

        if (projectionExpression != null || isPathSegmentType)
        {
            string columnDefs;
            if (projectionExpression != null)
            {
                columnDefs = ColumnDefinitionBuilder.BuildColumnDefinitions(projectionExpression);
            }
            else
            {
                columnDefs = ColumnDefinitionBuilder.BuildPathSegmentColumnDefinitions();
            }
            command = ColumnDefinitionBuilder.CreateCypherCommandWithColumns(_graphContext.Connection, _graphContext.GraphName, cypher, parameters, columnDefs);
        }
        else
        {
            command = _graphContext.Connection.CreateCypherCommand(_graphContext.GraphName, cypher, parameters);
        }

        command.Transaction = transaction?.Transaction;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

        // For scalar results (not INode/IRelationship), read native types directly.
        // Projections (anonymous types, member projections) need the EntityInfo pipeline
        // so the ResultMaterializer can construct proper objects from multi-column returns.
        // Path segments also need the EntityInfo pipeline since they're composed of vertices/edges.
        var isProjection = projectionExpression != null || elementType.IsGenericType
            && elementType.GetCustomAttributes(typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute), false).Length > 0
            && elementType.Name.StartsWith("<>") && elementType.Name.Contains("AnonymousType");
        if (!typeof(INode).IsAssignableFrom(elementType) && !typeof(IRelationship).IsAssignableFrom(elementType)
            && !isPathSegmentType && !isProjection)
        {
            return await ScalarResultMaterializer.MaterializeAsync<T>(reader, elementType, cancellationToken, aggregationType).ConfigureAwait(false);
        }

        // Use AgeResultProcessor to convert raw results to EntityInfo structures
        var entityInfos = await _ageResultProcessor.ProcessAsync(
            reader, elementType, cancellationToken, projectionExpression, projectionResultType, aggregationType);

        // Use shared ResultMaterializer to convert EntityInfo to final objects
        return await _sharedMaterializer.MaterializeAsync<T>(entityInfos, cancellationToken).ConfigureAwait(false);
    }

    // MaterializeScalarResultAsync moved to ScalarResultMaterializer.MaterializeAsync

    // BuildColumnDefinitions, CreateCypherCommandWithColumns, BuildPathSegmentColumnDefinitions,
    // and GetNewExpressionParameterName moved to ColumnDefinitionBuilder.
}
