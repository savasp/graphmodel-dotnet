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

namespace Cvoya.Graph.Model.Neo4j.Querying.Cypher.Execution;

using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using Cvoya.Graph.Model.Neo4j.Core;
using Cvoya.Graph.Model.Neo4j.Querying.Cypher.Builders;
using Cvoya.Graph.Model.Neo4j.Querying.Cypher.Visitors.Core;
using Cvoya.Graph.Model.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;


internal sealed class CypherEngine
{
    private enum ElementTerminal
    {
        None,
        First,
        FirstOrDefault,
        Last,
        Single,
        SingleOrDefault,
    }

    private static readonly IReadOnlyDictionary<MethodInfo, ElementTerminal> ElementTerminalMethods = CreateElementTerminalMethods();

    private readonly EntityFactory _entityFactory;
    private readonly ILogger<CypherEngine> _logger;
    private readonly CypherExecutor _executor;
    private readonly ResultMaterializer _materializer;
    private readonly ILoggerFactory? _loggerFactory;

    public CypherEngine(EntityFactory entityFactory, ILoggerFactory? loggerFactory)
    {
        _entityFactory = entityFactory ?? throw new ArgumentNullException(nameof(entityFactory));
        _logger = loggerFactory?.CreateLogger<CypherEngine>() ?? NullLogger<CypherEngine>.Instance;

        // Create our internal components
        _executor = new CypherExecutor(loggerFactory);
        _materializer = new ResultMaterializer(entityFactory, loggerFactory);
        _loggerFactory = loggerFactory;

    }

    public async Task<T?> ExecuteAsync<T>(
            Expression expression,
            GraphTransaction transaction,
            CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Executing query for type {Type}", typeof(T).Name);

            // Build the Cypher query from the expression
            var cypherQuery = BuildCypherQuery(typeof(T), expression, _loggerFactory);

            LogCypherQuery(cypherQuery);

            // Execute the query
            var records = await _executor.ExecuteAsync(
                cypherQuery.Text,
                cypherQuery.Parameters,
                transaction,
                cancellationToken).ConfigureAwait(false);

            ValidateElementTerminalRecordCount(GetElementTerminal(expression), records.Count);

            // Let the materializer handle everything - no need to duplicate logic here
            var result = await _materializer.MaterializeAsync<T>(records, cypherQuery.GraphPathTypes, cancellationToken).ConfigureAwait(false);
            return result;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to execute query for type {Type}", typeof(T).Name);
            throw;
        }
    }

    public async IAsyncEnumerable<T> StreamAsync<T>(
        Expression expression,
        GraphTransaction transaction,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        _logger.LogDebug("Streaming query for type {Type}", typeof(T).Name);

        var cypherQuery = BuildCypherQuery(typeof(T), expression, _loggerFactory);
        LogCypherQuery(cypherQuery);

        var records = _executor.StreamAsync(
            cypherQuery.Text,
            cypherQuery.Parameters,
            transaction,
            cancellationToken);

        if (cypherQuery.GraphPathTypes is { } graphPathTypes && typeof(T) == typeof(IGraphPath))
        {
            await foreach (var path in StreamGraphPathsAsync(records, graphPathTypes, cancellationToken)
                .WithCancellation(cancellationToken)
                .ConfigureAwait(false))
            {
                yield return (T)path;
            }

            yield break;
        }

        await foreach (var record in records.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var item = await _materializer.MaterializeRecordAsync<T>(
                record,
                cancellationToken).ConfigureAwait(false);

            if (item is not null)
            {
                yield return item;
            }
        }
    }

    private async IAsyncEnumerable<IGraphPath> StreamGraphPathsAsync(
        IAsyncEnumerable<global::Neo4j.Driver.IRecord> records,
        (Type Source, Type Relationship, Type Target) graphPathTypes,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        List<CypherResultProcessor.GraphPathHop>? currentHops = null;
        int? currentPathIndex = null;

        await foreach (var record in records.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            cancellationToken.ThrowIfCancellationRequested();

            // TraversePaths orders rows by pathIndex, then hopIndex, so a path can be emitted once its index changes.
            var hop = _materializer.ProcessGraphPathHop(record, graphPathTypes, cancellationToken);
            if (currentPathIndex is int pathIndex && hop.PathIndex != pathIndex)
            {
                yield return _materializer.MaterializeGraphPath(currentHops!, graphPathTypes);
                currentHops = [];
            }

            currentPathIndex = hop.PathIndex;
            currentHops ??= [];
            currentHops.Add(hop);
        }

        if (currentHops is { Count: > 0 })
        {
            yield return _materializer.MaterializeGraphPath(currentHops, graphPathTypes);
        }
    }

    private static IReadOnlyDictionary<MethodInfo, ElementTerminal> CreateElementTerminalMethods()
    {
        var methods = new Dictionary<MethodInfo, ElementTerminal>();

        AddAll(nameof(QueryTerminals.FirstAsyncMarker), ElementTerminal.First);
        AddAll(nameof(QueryTerminals.FirstOrDefaultAsyncMarker), ElementTerminal.FirstOrDefault);
        AddAll(nameof(QueryTerminals.LastAsyncMarker), ElementTerminal.Last);
        AddAll(nameof(QueryTerminals.SingleAsyncMarker), ElementTerminal.Single);
        AddAll(nameof(QueryTerminals.SingleOrDefaultAsyncMarker), ElementTerminal.SingleOrDefault);

        return methods;

        void AddAll(string name, ElementTerminal terminal)
        {
            foreach (var method in typeof(QueryTerminals).GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
                .Where(m => m.Name == name))
            {
                methods[method] = terminal;
            }
        }
    }

    private static ElementTerminal GetElementTerminal(Expression expression)
    {
        if (expression is not MethodCallExpression methodCall)
            return ElementTerminal.None;

        var key = methodCall.Method.IsGenericMethod
            ? methodCall.Method.GetGenericMethodDefinition()
            : methodCall.Method;

        return ElementTerminalMethods.TryGetValue(key, out var terminal)
            ? terminal
            : ElementTerminal.None;
    }

    private static void ValidateElementTerminalRecordCount(ElementTerminal terminal, int recordCount)
    {
        switch (terminal)
        {
            case ElementTerminal.First when recordCount == 0:
            case ElementTerminal.Last when recordCount == 0:
            case ElementTerminal.Single when recordCount == 0:
                throw new InvalidOperationException("Sequence contains no elements");

            case ElementTerminal.Single when recordCount > 1:
            case ElementTerminal.SingleOrDefault when recordCount > 1:
                throw new InvalidOperationException("Sequence contains more than one element");

            case ElementTerminal.None:
            case ElementTerminal.First:
            case ElementTerminal.FirstOrDefault:
            case ElementTerminal.Last:
            case ElementTerminal.Single:
            case ElementTerminal.SingleOrDefault:
                return;
        }
    }

    private void LogCypherQuery(CypherQuery cypherQuery)
    {
        if (_logger.IsEnabled(LogLevel.Debug))
        {
            var parameterNames = cypherQuery.Parameters.Keys.ToArray();
            _logger.LogDebug("Generated Cypher: {Cypher}", cypherQuery.Text);
            _logger.LogDebug(
                "Generated Cypher parameter names: {ParameterNames}; count: {ParameterCount}",
                parameterNames,
                parameterNames.Length);
        }

        if (_logger.IsEnabled(LogLevel.Trace))
        {
            _logger.LogTrace("Generated Cypher parameters: {Parameters}", cypherQuery.Parameters);
        }
    }

    private CypherQuery BuildCypherQuery(Type type, Expression expression, ILoggerFactory? loggerFactory = null)
    {
        // Extract the element type from the expression if the type is a collection
        var elementType = ExtractElementType(type, expression);

        var visitor = new CypherQueryVisitor(elementType, loggerFactory);
        visitor.Visit(expression);

        var query = visitor.Query;

        var paramBuilder = new CypherParameterBuilder(_entityFactory);
        var convertedParams = new Dictionary<string, object?>();

        foreach (var (key, value) in query.Parameters)
        {
            convertedParams[key] = paramBuilder.BuildParameterValue(value);
        }

        return new CypherQuery(query.Text, convertedParams, query.GraphPathTypes);
    }

    // Helper visitor to detect if we have a projection
    private sealed class ProjectionDetectorVisitor : ExpressionVisitor
    {
        public bool HasProjection { get; private set; }

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            if (node.Method.Name == "Select" && node.Arguments.Count >= 2)
            {
                // Check if the selector is projecting to a different type
                if (node.Arguments[1] is UnaryExpression { Operand: LambdaExpression lambda })
                {
                    var sourceType = lambda.Parameters[0].Type;
                    var resultType = lambda.ReturnType;

                    if (sourceType != resultType)
                    {
                        HasProjection = true;
                    }
                }
            }

            return base.VisitMethodCall(node);
        }
    }

    private static Type ExtractElementType(Type resultType, Expression expression)
    {
        // If the result type is a collection (List<T>, IEnumerable<T>, etc.), 
        // extract the element type
        if (resultType.IsGenericType)
        {
            var genericArgs = resultType.GetGenericArguments();
            if (genericArgs.Length == 1)
            {
                return genericArgs[0];
            }
        }

        // Try to extract from the expression tree by finding the first queryable constant
        return ExtractElementTypeFromExpression(expression) ?? resultType;
    }

    private static Type? ExtractElementTypeFromExpression(Expression expression)
    {
        if (expression is ConstantExpression constant &&
            constant.Value is IQueryable queryable)
        {
            return queryable.ElementType;
        }

        if (expression is MethodCallExpression methodCall)
        {
            // Check the object of the method call
            if (methodCall.Object != null)
            {
                var objType = ExtractElementTypeFromExpression(methodCall.Object);
                if (objType != null) return objType;
            }

            // Check the arguments
            foreach (var arg in methodCall.Arguments)
            {
                var argType = ExtractElementTypeFromExpression(arg);
                if (argType != null) return argType;
            }
        }

        if (expression is UnaryExpression unary)
        {
            return ExtractElementTypeFromExpression(unary.Operand);
        }

        return null;
    }
}
