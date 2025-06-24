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

namespace Cvoya.Graph.Model.Neo4j.Querying.Cypher.Visitors.Core;

using System.Linq.Expressions;
using Cvoya.Graph.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// Main visitor that orchestrates the translation of LINQ expressions to Cypher queries.
/// This refactored version eliminates method handlers and uses a unified expression visitor.
/// </summary>
internal class CypherQueryVisitor : ExpressionVisitor
{
    private readonly CypherQueryContext _context;
    private readonly ILogger<CypherQueryVisitor> _logger;
    private readonly ExpressionToCypherVisitor _expressionVisitor;

    public CypherQueryVisitor(Type type, ILoggerFactory? loggerFactory = null)
    {
        _context = new CypherQueryContext(type, loggerFactory);
        _logger = loggerFactory?.CreateLogger<CypherQueryVisitor>()
            ?? NullLogger<CypherQueryVisitor>.Instance;

        // Create unified expression visitor for translating expressions to Cypher
        _expressionVisitor = new ExpressionToCypherVisitor(
            _context.Builder,
            _context.Scope,
            _logger);
    }

    /// <summary>
    /// Gets the generated Cypher query.
    /// </summary>
    public CypherQuery Query => new(_context.GetQuery(), _context.GetParameters());

    protected override Expression VisitMethodCall(MethodCallExpression node)
    {
        _logger.LogDebug("Visiting method call: {Method}", node.Method.Name);
        _logger.LogDebug("Method declaring type: {DeclaringType}", node.Method.DeclaringType?.Name);
        _logger.LogDebug("Method arguments count: {ArgsCount}", node.Arguments.Count);

        try
        {
            // Handle LINQ methods with direct orchestration
            if (IsLinqMethod(node))
            {
                return HandleLinqMethod(node);
            }

            // For non-LINQ methods, delegate to expression visitor
            // This handles DateTime, String, Math, etc. methods within expressions
            var cypherResult = _expressionVisitor.VisitAndReturnCypher(node);
            _logger.LogDebug("Non-LINQ method {Method} translated to: {Cypher}", node.Method.Name, cypherResult);

            // Return a constant expression containing the Cypher string
            return Expression.Constant(cypherResult);
        }
        catch (GraphException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new GraphException(
                $"Error processing method '{node.Method.Name}': {ex.Message}",
                ex);
        }
    }

    private Expression HandleLinqMethod(MethodCallExpression node)
    {
        var methodName = node.Method.Name;
        _logger.LogDebug("Handling LINQ method: {Method}", methodName);

        // Get the source expression (the queryable being operated on)
        Expression? sourceExpression = node.Method.IsStatic
            ? (node.Arguments.Count > 0 ? node.Arguments[0] : null)
            : node.Object;

        // Visit the source first to establish context
        Expression? result = null;
        if (sourceExpression is not null)
            result = Visit(sourceExpression);

        // Handle specific LINQ methods
        switch (methodName)
        {
            case "Where":
                return HandleWhere(node, result);

            case "Select":
                return HandleSelect(node, result);

            case "OrderBy":
            case "OrderByDescending":
                return HandleOrderBy(node, result, methodName.EndsWith("Descending"));

            case "ThenBy":
            case "ThenByDescending":
                return HandleThenBy(node, result, methodName.EndsWith("Descending"));

            case "Take":
                return HandleTake(node, result);

            case "Skip":
                return HandleSkip(node, result);

            case "Distinct":
                return HandleDistinct(node, result);

            case "ToListAsyncMarker":
            case "ToArrayAsyncMarker":
                return HandleToList(node, result);

            case "FirstAsyncMarker":
            case "FirstOrDefaultAsyncMarker":
                return HandleFirst(node, result, methodName);

            case "SingleAsyncMarker":
            case "SingleOrDefaultAsyncMarker":
                return HandleSingle(node, result, methodName);

            case "AnyAsyncMarker":
                return HandleAny(node, result);

            case "AllAsyncMarker":
                return HandleAll(node, result);

            case "CountAsyncMarker":
            case "LongCountAsyncMarker":
                return HandleCount(node, result);

            case "SumAsyncMarker":
                return HandleSum(node, result);

            case "AverageAsyncMarker":
                return HandleAverage(node, result);

            case "MinAsyncMarker":
                return HandleMin(node, result);

            case "MaxAsyncMarker":
                return HandleMax(node, result);

            case "ContainsAsyncMarker":
                return HandleContains(node, result);

            case "ElementAtAsyncMarker":
            case "ElementAtOrDefaultAsyncMarker":
                return HandleElementAt(node, result, methodName.Contains("OrDefault"));

            case "SelectMany":
                return HandleSelectMany(node, result);

            case "GroupBy":
                return HandleGroupBy(node, result);

            case "Join":
                return HandleJoin(node, result);

            case "Union":
                return HandleUnion(node, result);

            case "WithTransaction":
                return HandleWithTransaction(node, result);

            case "PathSegments":
                return HandlePathSegments(node, result);

            case "Direction":
                return HandleDirection(node, result);

            default:
                throw new GraphException(
                    $"LINQ method '{methodName}' is not yet supported for Cypher translation. " +
                    "Consider using a supported method or restructuring your query.");
        }
    }

    private Expression HandleWhere(MethodCallExpression node, Expression? result)
    {
        if (node.Arguments.Count != 2)
            throw new GraphException("Where method must have exactly 2 arguments");

        var lambda = ExtractLambda(node.Arguments[1]);
        if (lambda == null)
            throw new GraphException("Where method requires a lambda expression");

        _logger.LogDebug("Processing WHERE clause with lambda");

        // Use the expression visitor to translate the lambda body to Cypher
        var whereCondition = _expressionVisitor.VisitAndReturnCypher(lambda.Body);
        _context.Builder.AddWhere(whereCondition);

        _logger.LogDebug("Added WHERE condition: {Condition}", whereCondition);
        return result ?? node.Arguments[0];
    }

    private Expression HandleSelect(MethodCallExpression node, Expression? result)
    {
        if (node.Arguments.Count != 2)
            throw new GraphException("Select method must have exactly 2 arguments");

        var lambda = ExtractLambda(node.Arguments[1]);
        if (lambda == null)
            throw new GraphException("Select method requires a lambda expression");

        _logger.LogDebug("Processing SELECT clause with lambda");

        // Check if this is a simple parameter access (e.g., x => x)
        if (lambda.Body is ParameterExpression)
        {
            // Simple projection - return the entire entity
            var alias = _context.Scope.CurrentAlias ?? "src";
            _context.Builder.AddReturn(alias);
            _context.Builder.EnableComplexPropertyLoading();
        }
        else
        {
            // Complex projection - translate the expression
            var selectExpression = _expressionVisitor.VisitAndReturnCypher(lambda.Body);
            _context.Builder.AddReturn(selectExpression);

            // For projections, disable complex property loading since we're not returning full entities
            _context.Builder.DisableComplexPropertyLoading();
        }

        _logger.LogDebug("Added SELECT projection");
        return result ?? node.Arguments[0];
    }

    private Expression HandleOrderBy(MethodCallExpression node, Expression? result, bool descending)
    {
        if (node.Arguments.Count != 2)
            throw new GraphException("OrderBy method must have exactly 2 arguments");

        var lambda = ExtractLambda(node.Arguments[1]);
        if (lambda == null)
            throw new GraphException("OrderBy method requires a lambda expression");

        _logger.LogDebug("Processing ORDER BY clause, descending: {Descending}", descending);

        var orderExpression = _expressionVisitor.VisitAndReturnCypher(lambda.Body);
        _context.Builder.AddOrderBy(orderExpression, descending);

        _logger.LogDebug("Added ORDER BY: {Expression} {Direction}", orderExpression, descending ? "DESC" : "ASC");
        return result ?? node.Arguments[0];
    }

    private Expression HandleThenBy(MethodCallExpression node, Expression? result, bool descending)
    {
        // ThenBy works the same as OrderBy for Cypher - just adds another sorting criterion
        return HandleOrderBy(node, result, descending);
    }

    private Expression HandleTake(MethodCallExpression node, Expression? result)
    {
        if (node.Arguments.Count != 2)
            throw new GraphException("Take method must have exactly 2 arguments");

        // Extract the limit value
        var limitExpression = node.Arguments[1];
        var limitValue = EvaluateConstantExpression<int>(limitExpression);

        _context.Builder.SetLimit(limitValue);
        _logger.LogDebug("Set LIMIT: {Limit}", limitValue);

        return result ?? node.Arguments[0];
    }

    private Expression HandleSkip(MethodCallExpression node, Expression? result)
    {
        if (node.Arguments.Count != 2)
            throw new GraphException("Skip method must have exactly 2 arguments");

        // Extract the skip value
        var skipExpression = node.Arguments[1];
        var skipValue = EvaluateConstantExpression<int>(skipExpression);

        _context.Builder.SetSkip(skipValue);
        _logger.LogDebug("Set SKIP: {Skip}", skipValue);

        return result ?? node.Arguments[0];
    }

    private Expression HandleDistinct(MethodCallExpression node, Expression? result)
    {
        _context.Builder.SetDistinct(true);

        // For scalar projections, disable complex property loading
        if (_context.Scope.RootType != null && IsScalarOrPrimitive(_context.Scope.RootType))
        {
            _context.Builder.DisableComplexPropertyLoading();
        }

        _logger.LogDebug("Set DISTINCT flag");
        return result ?? node.Arguments[0];
    }

    private Expression HandleToList(MethodCallExpression node, Expression? result)
    {
        _logger.LogDebug("Processing ToList method");

        // Check if we need to enable complex property loading
        var resultType = node.Type.GetGenericArguments().FirstOrDefault();
        if (resultType != null && !IsScalarOrPrimitive(resultType))
        {
            _context.Builder.EnableComplexPropertyLoading();
            _logger.LogDebug("Enabled complex property loading for node query");
        }
        else
        {
            _logger.LogDebug("Root type is not a node or path segment, skipping complex property loading");
        }

        return result ?? node.Arguments[0];
    }

    private Expression HandleFirst(MethodCallExpression node, Expression? result, string methodName)
    {
        // Set limit to 1 for First/Single operations
        _context.Builder.SetLimit(1);

        // Handle optional where clause
        if (node.Arguments.Count == 2)
        {
            var lambda = ExtractLambda(node.Arguments[1]);
            if (lambda != null)
            {
                var whereCondition = _expressionVisitor.VisitAndReturnCypher(lambda.Body);
                _context.Builder.AddWhere(whereCondition);
            }
        }

        _logger.LogDebug("Added LIMIT 1 for {Method}", methodName);
        return result ?? node.Arguments[0];
    }

    private Expression HandleSingle(MethodCallExpression node, Expression? result, string methodName)
    {
        // Set limit to 1 for First/Single operations
        _context.Builder.SetLimit(1);

        // Handle optional where clause
        if (node.Arguments.Count == 2)
        {
            var lambda = ExtractLambda(node.Arguments[1]);
            if (lambda != null)
            {
                var whereCondition = _expressionVisitor.VisitAndReturnCypher(lambda.Body);
                _context.Builder.AddWhere(whereCondition);
            }
        }

        _logger.LogDebug("Added LIMIT 1 for {Method}", methodName);
        return result ?? node.Arguments[0];
    }

    private Expression HandleAny(MethodCallExpression node, Expression? result)
    {
        if (node.Arguments.Count == 2)
        {
            var lambda = ExtractLambda(node.Arguments[1]);
            if (lambda != null)
            {
                var condition = _expressionVisitor.VisitAndReturnCypher(lambda.Body);
                _context.Builder.AddWhere(condition);
            }
        }

        // For Any, we're checking existence
        _context.Builder.SetExistsQuery();

        _logger.LogDebug("Set EXISTS query");
        return result ?? node.Arguments[0];
    }

    private Expression HandleAll(MethodCallExpression node, Expression? result)
    {
        if (node.Arguments.Count == 2)
        {
            var lambda = ExtractLambda(node.Arguments[1]);
            if (lambda != null)
            {
                var condition = _expressionVisitor.VisitAndReturnCypher(lambda.Body);
                _context.Builder.AddWhere(condition);
            }
        }

        // For All, we're checking existence
        _context.Builder.SetExistsQuery();

        _logger.LogDebug("Set EXISTS query");
        return result ?? node.Arguments[0];
    }

    private Expression HandleCount(MethodCallExpression node, Expression? result)
    {
        // Handle optional where clause
        if (node.Arguments.Count == 2)
        {
            var lambda = ExtractLambda(node.Arguments[1]);
            if (lambda != null)
            {
                var whereCondition = _expressionVisitor.VisitAndReturnCypher(lambda.Body);
                _context.Builder.AddWhere(whereCondition);
            }
        }

        var alias = _context.Scope.CurrentAlias ?? "src";
        _context.Builder.AddReturn($"count({alias})");

        _logger.LogDebug("Added COUNT aggregation");
        return result ?? node.Arguments[0];
    }

    private Expression HandleSum(MethodCallExpression node, Expression? result)
    {
        if (node.Arguments.Count != 2)
            throw new GraphException("Sum method must have exactly 2 arguments");

        var lambda = ExtractLambda(node.Arguments[1]);
        if (lambda == null)
            throw new GraphException("Sum method requires a lambda expression");

        _logger.LogDebug("Processing SUM aggregation");

        var sumExpression = _expressionVisitor.VisitAndReturnCypher(lambda.Body);
        _context.Builder.AddReturn($"sum({sumExpression})");

        _logger.LogDebug("Added SUM aggregation");
        return result ?? node.Arguments[0];
    }

    private Expression HandleAverage(MethodCallExpression node, Expression? result)
    {
        if (node.Arguments.Count != 2)
            throw new GraphException("Average method must have exactly 2 arguments");

        var lambda = ExtractLambda(node.Arguments[1]);
        if (lambda == null)
            throw new GraphException("Average method requires a lambda expression");

        _logger.LogDebug("Processing AVERAGE aggregation");

        var avgExpression = _expressionVisitor.VisitAndReturnCypher(lambda.Body);
        _context.Builder.AddReturn($"avg({avgExpression})");

        _logger.LogDebug("Added AVERAGE aggregation");
        return result ?? node.Arguments[0];
    }

    private Expression HandleMin(MethodCallExpression node, Expression? result)
    {
        if (node.Arguments.Count != 2)
            throw new GraphException("Min method must have exactly 2 arguments");

        var lambda = ExtractLambda(node.Arguments[1]);
        if (lambda == null)
            throw new GraphException("Min method requires a lambda expression");

        _logger.LogDebug("Processing MIN aggregation");

        var minExpression = _expressionVisitor.VisitAndReturnCypher(lambda.Body);
        _context.Builder.AddReturn($"min({minExpression})");

        _logger.LogDebug("Added MIN aggregation");
        return result ?? node.Arguments[0];
    }

    private Expression HandleMax(MethodCallExpression node, Expression? result)
    {
        if (node.Arguments.Count != 2)
            throw new GraphException("Max method must have exactly 2 arguments");

        var lambda = ExtractLambda(node.Arguments[1]);
        if (lambda == null)
            throw new GraphException("Max method requires a lambda expression");

        _logger.LogDebug("Processing MAX aggregation");

        var maxExpression = _expressionVisitor.VisitAndReturnCypher(lambda.Body);
        _context.Builder.AddReturn($"max({maxExpression})");

        _logger.LogDebug("Added MAX aggregation");
        return result ?? node.Arguments[0];
    }

    private Expression HandleContains(MethodCallExpression node, Expression? result)
    {
        if (node.Arguments.Count != 2)
            throw new GraphException("Contains method must have exactly 2 arguments");

        var lambda = ExtractLambda(node.Arguments[1]);
        if (lambda == null)
            throw new GraphException("Contains method requires a lambda expression");

        _logger.LogDebug("Processing CONTAINS method");

        var containsExpression = _expressionVisitor.VisitAndReturnCypher(lambda.Body);
        _context.Builder.AddWhere($"({containsExpression})");

        _logger.LogDebug("Added CONTAINS condition");
        return result ?? node.Arguments[0];
    }

    private Expression HandleElementAt(MethodCallExpression node, Expression? result, bool orDefault)
    {
        if (node.Arguments.Count != 2)
            throw new GraphException("ElementAt method must have exactly 2 arguments");

        var indexExpression = node.Arguments[1];
        var indexValue = EvaluateConstantExpression<int>(indexExpression);

        _logger.LogDebug("Processing ELEMENTAT method, index: {Index}", indexValue);

        var alias = _context.Scope.CurrentAlias ?? "src";
        var elementAtExpression = $"{alias}[{indexValue}]";
        if (orDefault)
        {
            elementAtExpression = $"coalesce({elementAtExpression}, null)";
        }

        _context.Builder.AddReturn(elementAtExpression);

        _logger.LogDebug("Added ELEMENTAT projection");
        return result ?? node.Arguments[0];
    }

    private Expression HandleSelectMany(MethodCallExpression node, Expression? result)
    {
        if (node.Arguments.Count != 2)
            throw new GraphException("SelectMany method must have exactly 2 arguments");

        var lambda = ExtractLambda(node.Arguments[1]);
        if (lambda == null)
            throw new GraphException("SelectMany method requires a lambda expression");

        _logger.LogDebug("Processing SELECT MANY method");

        var selectManyExpression = _expressionVisitor.VisitAndReturnCypher(lambda.Body);
        _context.Builder.AddReturn(selectManyExpression);

        _logger.LogDebug("Added SELECT MANY projection");
        return result ?? node.Arguments[0];
    }

    private Expression HandleGroupBy(MethodCallExpression node, Expression? result)
    {
        if (node.Arguments.Count != 2)
            throw new GraphException("GroupBy method must have exactly 2 arguments");

        var lambda = ExtractLambda(node.Arguments[1]);
        if (lambda == null)
            throw new GraphException("GroupBy method requires a lambda expression");

        _logger.LogDebug("Processing GROUP BY method");

        var groupByExpression = _expressionVisitor.VisitAndReturnCypher(lambda.Body);
        _context.Builder.AddGroupBy(groupByExpression);

        _logger.LogDebug("Added GROUP BY");
        return result ?? node.Arguments[0];
    }

    private Expression HandleJoin(MethodCallExpression node, Expression? result)
    {
        // Join is complex - for now, provide a basic implementation or throw
        throw new GraphException("Join operations are not yet fully implemented in the refactored architecture");
    }

    private Expression HandleUnion(MethodCallExpression node, Expression? result)
    {
        // Union is complex - for now, provide a basic implementation or throw
        throw new GraphException("Union operations are not yet fully implemented in the refactored architecture");
    }

    private Expression HandleWithTransaction(MethodCallExpression node, Expression? result)
    {
        // WithTransaction just sets up the transaction context and doesn't affect the Cypher query
        // We simply pass through to the source expression
        _logger.LogDebug("Ignoring WithTransaction method (transaction context handled elsewhere)");
        return result ?? node.Arguments[0];
    }

    private Expression HandlePathSegments(MethodCallExpression node, Expression? result)
    {
        _logger.LogDebug("Processing PathSegments method call");

        // PathSegments method typically has generic arguments that specify the path structure
        var method = node.Method;
        if (method.IsGenericMethod)
        {
            var genericArgs = method.GetGenericArguments();
            if (genericArgs.Length == 3)
            {
                var sourceType = genericArgs[0];
                var relationshipType = genericArgs[1];
                var targetType = genericArgs[2];

                _logger.LogDebug("PathSegments: Source={Source}, Relationship={Relationship}, Target={Target}",
                    sourceType.Name, relationshipType.Name, targetType.Name);

                // Set up path segment context in the scope
                _context.Scope.SetTraversalInfo(sourceType, relationshipType, targetType);
                _context.Builder.EnablePathSegmentLoading();
            }
        }

        _logger.LogDebug("Configured path segment traversal");
        return result ?? node.Arguments[0];
    }

    private Expression HandleDirection(MethodCallExpression node, Expression? result)
    {
        _logger.LogDebug("Processing Direction method call");

        if (node.Arguments.Count >= 2)
        {
            // The second argument should be the direction
            var directionArg = node.Arguments[1];
            if (directionArg is ConstantExpression { Value: GraphTraversalDirection direction })
            {
                _context.Builder.SetTraversalDirection(direction);
                _logger.LogDebug("Set traversal direction: {Direction}", direction);
            }
        }

        return result ?? node.Arguments[0];
    }

    protected override Expression VisitConstant(ConstantExpression node)
    {
        _logger.LogDebug("VisitConstant called with value type: {ValueType}", node.Value?.GetType().Name ?? "null");

        // Handle queryable sources - these are the actual data sources we need to query
        if (node.Value is IQueryable queryable)
        {
            _logger.LogDebug("Found queryable of element type {Type}", queryable.ElementType.Name);

            // Check if this is a relationship queryable
            if (node.Value is IGraphRelationshipQueryable)
            {
                var relLabel = Labels.GetLabelFromType(queryable.ElementType);
                _context.Builder.AddRelationshipMatch(relLabel);
                _context.Scope.CurrentAlias = "r";

                // Relationships are always treated as path segments, so they need complex property loading
                _logger.LogDebug("Relationship type {Type} requires complex property loading", queryable.ElementType.Name);
                _context.Builder.EnableComplexPropertyLoading();
            }
            else if (node.Value is IGraphNodeQueryable)
            {
                // For nodes, generate the MATCH clause using the queryable's element type
                var alias = _context.Scope.GetOrCreateAlias(queryable.ElementType, "src");
                var label = Labels.GetLabelFromType(queryable.ElementType);
                _logger.LogDebug("Adding MATCH clause: ({Alias}:{Label})", alias, label);
                _context.Builder.AddMatch(alias, label);

                // Check if this node type needs complex property loading
                if (_context.Builder.NeedsComplexProperties(queryable.ElementType))
                {
                    _logger.LogDebug("Node type {Type} requires complex property loading", queryable.ElementType.Name);
                    _context.Builder.EnableComplexPropertyLoading();
                }

                // Set the current alias so that parameter expressions can be resolved
                _context.Scope.CurrentAlias = alias;
                _logger.LogDebug("Set up base query with alias: {Alias}", alias);
            }

            return node;
        }

        return base.VisitConstant(node);
    }

    // Helper methods
    private static LambdaExpression? ExtractLambda(Expression expression)
    {
        return expression switch
        {
            LambdaExpression directLambda => directLambda,
            UnaryExpression { Operand: LambdaExpression unaryLambda } => unaryLambda,
            _ => null
        };
    }

    private static T EvaluateConstantExpression<T>(Expression expression)
    {
        if (expression is ConstantExpression constant && constant.Value is T value)
        {
            return value;
        }

        // Try to evaluate the expression
        var lambda = Expression.Lambda(expression);
        var compiled = lambda.Compile();
        var result = compiled.DynamicInvoke();

        if (result is T typedResult)
        {
            return typedResult;
        }

        throw new GraphException($"Unable to evaluate expression as {typeof(T).Name}: {expression}");
    }

    private static bool IsLinqMethod(MethodCallExpression node)
    {
        return node.Method.DeclaringType == typeof(Queryable) ||
               node.Method.DeclaringType == typeof(Enumerable) ||
               (node.Method.DeclaringType?.Namespace?.StartsWith("System.Linq") ?? false) ||
               (node.Method.DeclaringType?.Name?.Contains("GraphQueryable") ?? false) ||
               (node.Method.DeclaringType?.Name?.Contains("QueryableAsyncExtensions") ?? false) ||
               (node.Method.DeclaringType?.Name?.Contains("GraphNodeQueryableExtensions") ?? false) ||
               (node.Method.DeclaringType?.Name?.Contains("GraphRelationshipQueryableExtensions") ?? false) ||
               (node.Method.DeclaringType?.Name?.Contains("GraphTraversalExtensions") ?? false);
    }

    private static bool IsScalarOrPrimitive(Type type)
    {
        if (type.IsPrimitive || type.IsEnum)
            return true;

        if (type == typeof(string) || type == typeof(Guid) || type == typeof(DateTime) ||
            type == typeof(DateTimeOffset) || type == typeof(decimal))
            return true;

        if (Nullable.GetUnderlyingType(type) is { } underlying)
            return IsScalarOrPrimitive(underlying);

        return false;
    }
}