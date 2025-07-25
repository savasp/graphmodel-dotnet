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
using System.Text.Json;
using Cvoya.Graph.Model.Neo4j.Core;
using Cvoya.Graph.Model.Neo4j.Querying.Cypher.Builders;
using Cvoya.Graph.Model.Neo4j.Querying.Cypher.Visitors.Core;
using Cvoya.Graph.Model.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;


internal sealed class CypherEngine
{
    private readonly EntityFactory _entityFactory;
    private readonly ILogger<CypherEngine> _logger;
    private readonly CypherExecutor _executor;
    private readonly ResultMaterializer _materializer;
    private readonly CypherResultProcessor _resultProcessor;
    private readonly ILoggerFactory? _loggerFactory;

    public CypherEngine(EntityFactory entityFactory, ILoggerFactory? loggerFactory)
    {
        _entityFactory = entityFactory ?? throw new ArgumentNullException(nameof(entityFactory));
        _logger = loggerFactory?.CreateLogger<CypherEngine>() ?? NullLogger<CypherEngine>.Instance;

        // Create our internal components
        _executor = new CypherExecutor(loggerFactory);
        _materializer = new ResultMaterializer(entityFactory, loggerFactory);
        _resultProcessor = new CypherResultProcessor(_entityFactory, loggerFactory);
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

            _logger.LogDebug($"Generated Cypher: {cypherQuery.Text}");
            _logger.LogDebug($"Parameters: {JsonSerializer.Serialize(cypherQuery.Parameters)}");

            // Execute the query
            var records = await _executor.ExecuteAsync(
                cypherQuery.Text,
                cypherQuery.Parameters,
                transaction,
                cancellationToken);

            // Let the materializer handle everything - no need to duplicate logic here
            var result = await _materializer.MaterializeAsync<T>(records, cancellationToken);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to execute query for type {Type}", typeof(T).Name);
            throw;
        }
    }

    private CypherQuery BuildCypherQuery(Type type, Expression expression, ILoggerFactory? loggerFactory = null)
    {
        // Extract the element type from the expression if the type is a collection
        var elementType = ExtractElementType(type, expression);

        var visitor = new CypherQueryVisitor(elementType, loggerFactory);
        visitor.Visit(expression);

        var (cypher, parameters) = visitor.Query;

        var paramBuilder = new CypherParameterBuilder(_entityFactory);
        var convertedParams = new Dictionary<string, object?>();

        foreach (var (key, value) in parameters)
        {
            convertedParams[key] = paramBuilder.BuildParameterValue(value);
        }

        return new CypherQuery(cypher, convertedParams);
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