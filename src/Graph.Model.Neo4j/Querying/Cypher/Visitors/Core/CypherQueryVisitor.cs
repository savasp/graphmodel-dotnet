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
using System.Reflection;
using Cvoya.Graph.Model;
using Cvoya.Graph.Model.Neo4j.Linq.Helpers;
using Cvoya.Graph.Model.Neo4j.Querying.Cypher.Builders;
using Cvoya.Graph.Model.Neo4j.Querying.Linq.Queryables;
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
            case "First":
            case "FirstOrDefault":
                return HandleFirst(node, result, methodName);

            case "SingleAsyncMarker":
            case "SingleOrDefaultAsyncMarker":
                return HandleSingle(node, result, methodName);

            case "LastAsyncMarker":
            case "LastOrDefaultAsyncMarker":
                return HandleLast(node, result, methodName);

            case "AnyAsyncMarker":
                return HandleAny(node, result);

            case "AllAsyncMarker":
                return HandleAll(node, result);

            case "CountAsyncMarker":
            case "LongCountAsyncMarker":
                return HandleCount(node, result);

            case "SumAsyncMarker":
            case "SumAsync":
                return HandleSum(node, result);

            case "AverageAsyncMarker":
            case "AverageAsync":
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

            case "PathSegments":
                return HandlePathSegments(node, result);

            case "Direction":
                return HandleDirection(node, result);

            case "WithDepth":
                return HandleWithDepth(node, result);

            case "Search":
                return HandleSearch(node, result);

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
        else if (lambda.Body is NewExpression newExpr)
        {
            // Anonymous type projection - handle each property individually
            _logger.LogDebug("Processing anonymous type projection with {PropertyCount} properties", newExpr.Arguments.Count);

            // Check if any projection contains special types that require complex property loading
            bool requiresComplexPropertyLoading = false;

            // Process each property in the anonymous type
            for (int i = 0; i < newExpr.Arguments.Count; i++)
            {
                var propertyExpr = newExpr.Arguments[i];
                var propertyName = newExpr.Members?[i].Name ?? $"Property{i}";

                // Check if this property is a special type that requires complex property loading
                if (IsSpecialTypeProjection(propertyExpr))
                {
                    requiresComplexPropertyLoading = true;
                    _logger.LogDebug("Projection {Property} contains special type requiring complex property loading", propertyName);
                }

                // Handle special case: direct path segment parameter projection (ps => ps)
                if (propertyExpr is ParameterExpression paramExpr &&
                    typeof(IGraphPathSegment).IsAssignableFrom(paramExpr.Type))
                {
                    // This is projecting the entire path segment - we need to return structured data
                    _logger.LogDebug("Processing direct path segment projection for property {Property}", propertyName);

                    // For path segments, return a structured object containing all components
                    // This will be handled by the CypherResultProcessor to create a proper path segment
                    var pathSegmentExpr = "{ StartNode: src, Relationship: r, EndNode: tgt }";
                    _context.Builder.AddUserProjection(pathSegmentExpr, propertyName);

                    _logger.LogDebug("Added path segment projection: {Property} = {Expression}", propertyName, pathSegmentExpr);
                }
                else
                {
                    // Only visit for complex property navigation, skip for method calls which are handled by ExpressionToCypherVisitor
                    if (propertyExpr is not MethodCallExpression)
                    {
                        // Visit the property expression to process complex property navigation
                        Visit(propertyExpr);
                    }

                    // Check if this is a special type projection that needs complex property structure
                    if (IsSpecialTypeProjection(propertyExpr))
                    {
                        var cypherExpr = GenerateSpecialTypeProjection(propertyExpr);
                        _context.Builder.AddUserProjection(cypherExpr, propertyName);
                        _logger.LogDebug("Added special type projection: {Property} = {Expression}", propertyName, cypherExpr);
                    }
                    else
                    {
                        // Regular projection - translate the expression to Cypher
                        var cypherExpr = _expressionVisitor.VisitAndReturnCypher(propertyExpr);
                        _context.Builder.AddUserProjection(cypherExpr, propertyName);
                        _logger.LogDebug("Added projection: {Property} = {Expression}", propertyName, cypherExpr);
                    }
                }
            }

            // Enable or disable complex property loading based on whether special types are projected
            if (requiresComplexPropertyLoading)
            {
                _context.Builder.EnableComplexPropertyLoading();
                _logger.LogDebug("Enabled complex property loading due to special types in projections");
            }
            else
            {
                // For projections without special types, disable complex property loading since we're not returning full entities
                _context.Builder.DisableComplexPropertyLoading();
            }
        }
        else
        {
            // Check if this is a path segment property access
            if (lambda.Body is MemberExpression memberExpr &&
                memberExpr.Expression is ParameterExpression param &&
                typeof(IGraphPathSegment).IsAssignableFrom(param.Type))
            {
                // This is accessing a path segment property - set the appropriate projection
                var projection = memberExpr.Member.Name switch
                {
                    nameof(IGraphPathSegment.StartNode) => CypherQueryBuilder.PathSegmentProjectionEnum.StartNode,
                    nameof(IGraphPathSegment.EndNode) => CypherQueryBuilder.PathSegmentProjectionEnum.EndNode,
                    nameof(IGraphPathSegment.Relationship) => CypherQueryBuilder.PathSegmentProjectionEnum.Relationship,
                    _ => CypherQueryBuilder.PathSegmentProjectionEnum.Full
                };

                _context.Builder.SetPathSegmentProjection(projection);
                _logger.LogDebug("Set path segment projection to {Projection} for property {Property}", projection, memberExpr.Member.Name);

                // Update the current alias based on the projection
                var newAlias = projection switch
                {
                    CypherQueryBuilder.PathSegmentProjectionEnum.StartNode => "src",
                    CypherQueryBuilder.PathSegmentProjectionEnum.EndNode => "tgt",
                    CypherQueryBuilder.PathSegmentProjectionEnum.Relationship => "r",
                    _ => _context.Scope.CurrentAlias
                };

                if (newAlias != null)
                {
                    _context.Scope.CurrentAlias = newAlias;
                    _logger.LogDebug("Updated current alias to {Alias} for path segment projection {Projection}", newAlias, projection);
                }

                // For path segment projections, ensure we have complex property loading enabled for the projected part
                _context.Builder.EnableComplexPropertyLoading();
            }
            else
            {
                // Single expression projection - translate the expression
                var selectExpression = _expressionVisitor.VisitAndReturnCypher(lambda.Body);
                _context.Builder.AddReturn(selectExpression);

                // For projections, disable complex property loading since we're not returning full entities
                _context.Builder.DisableComplexPropertyLoading();
            }
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

        // Check if complex property loading has been explicitly enabled (e.g., by projection processing)
        if (_context.Builder.IsComplexPropertyLoadingEnabled())
        {
            _logger.LogDebug("Complex property loading already enabled - preserving setting");
            return result ?? node.Arguments[0];
        }



        // Check if we need to enable complex property loading based on the result type
        var resultType = node.Type.GetGenericArguments().FirstOrDefault();
        if (resultType != null && !IsScalarOrPrimitive(resultType))
        {
            // Only enable if it hasn't been explicitly disabled (e.g., by JOIN operations)
            // Check the current state - if it's already disabled, respect that
            var currentType = _context.Scope.CurrentType;
            if (currentType != null && _context.Builder.NeedsComplexProperties(currentType))
            {
                _context.Builder.EnableComplexPropertyLoading();
                _logger.LogDebug("Enabled complex property loading for node query of type {Type}", currentType.Name);
            }
            else
            {
                _logger.LogDebug("Complex property loading disabled or not needed for current type");
            }
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

    private Expression HandleLast(MethodCallExpression node, Expression? result, string methodName)
    {
        // Set limit to 1 for Last operations
        _context.Builder.SetLimit(1);

        // For Last, we need to reverse the order to get the last element
        // This should be done after any existing ORDER BY clauses have been processed
        _context.Builder.ReverseOrderBy();

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

        _logger.LogDebug("Added LIMIT 1 and reversed ORDER BY for {Method}", methodName);
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
        // Disable complex property loading for aggregation queries
        _context.Builder.DisableComplexPropertyLoading();

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
        // Disable complex property loading for aggregation queries
        _context.Builder.DisableComplexPropertyLoading();
        _context.Builder.SetAggregationQuery();

        _logger.LogDebug("Processing SUM aggregation with {ArgCount} arguments", node.Arguments.Count);

        if (node.Arguments.Count == 1)
        {
            // Direct aggregation: SumAsync() on IGraphQueryable<int>, IGraphQueryable<double>, etc.
            var alias = _context.Scope.CurrentAlias ?? "src";
            _context.Builder.AddReturn($"sum({alias})");
            _logger.LogDebug("Added direct SUM aggregation");
        }
        else if (node.Arguments.Count == 2 || node.Arguments.Count == 3)
        {
            // Aggregation with selector: SumAsync(x => x.Property)
            var lambdaArgIndex = node.Arguments.Count == 2 ? 1 : 1; // Skip cancellationToken if present
            var lambda = ExtractLambda(node.Arguments[lambdaArgIndex]);
            if (lambda == null)
                throw new GraphException("Sum method requires a lambda expression when using a selector");

            var sumExpression = _expressionVisitor.VisitAndReturnCypher(lambda.Body);
            _context.Builder.AddReturn($"sum({sumExpression})");
            _logger.LogDebug("Added SUM aggregation with selector");
        }
        else
        {
            throw new GraphException($"Sum method has unexpected number of arguments: {node.Arguments.Count}");
        }

        return result ?? node.Arguments[0];
    }

    private Expression HandleAverage(MethodCallExpression node, Expression? result)
    {
        // Disable complex property loading for aggregation queries
        _context.Builder.DisableComplexPropertyLoading();
        _context.Builder.SetAggregationQuery();

        _logger.LogDebug("Processing AVERAGE aggregation with {ArgCount} arguments", node.Arguments.Count);

        if (node.Arguments.Count == 1)
        {
            // Direct aggregation: AverageAsync() on IGraphQueryable<int>, IGraphQueryable<double>, etc.
            var alias = _context.Scope.CurrentAlias ?? "src";
            _context.Builder.AddReturn($"avg({alias})");
            _logger.LogDebug("Added direct AVERAGE aggregation");
        }
        else if (node.Arguments.Count == 2 || node.Arguments.Count == 3)
        {
            // Aggregation with selector: AverageAsync(x => x.Property)
            var lambdaArgIndex = node.Arguments.Count == 2 ? 1 : 1; // Skip cancellationToken if present
            var lambda = ExtractLambda(node.Arguments[lambdaArgIndex]);
            if (lambda == null)
                throw new GraphException("Average method requires a lambda expression when using a selector");

            var avgExpression = _expressionVisitor.VisitAndReturnCypher(lambda.Body);
            _context.Builder.AddReturn($"avg({avgExpression})");
            _logger.LogDebug("Added AVERAGE aggregation with selector");
        }
        else
        {
            throw new GraphException($"Average method has unexpected number of arguments: {node.Arguments.Count}");
        }

        return result ?? node.Arguments[0];
    }

    private Expression HandleMin(MethodCallExpression node, Expression? result)
    {
        // Disable complex property loading for aggregation queries
        _context.Builder.DisableComplexPropertyLoading();
        _context.Builder.SetAggregationQuery();

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
        // Disable complex property loading for aggregation queries
        _context.Builder.DisableComplexPropertyLoading();
        _context.Builder.SetAggregationQuery();

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

        // Store the group by expression for later use in g.Key references
        // But don't add an explicit GROUP BY clause since Neo4j handles grouping implicitly
        // when there are aggregation functions in the RETURN clause
        _context.Scope.SetGroupByExpression(groupByExpression);

        _logger.LogDebug("Stored GROUP BY expression for implicit grouping: {Expression}", groupByExpression);
        return result ?? node.Arguments[0];
    }

    private Expression HandleJoin(MethodCallExpression node, Expression? result)
    {
        _logger.LogDebug("Processing JOIN clause - building query manually to avoid path segment mode");

        // Arguments: [0] = outer source, [1] = inner source, [2] = outer key selector, [3] = inner key selector, [4] = result selector
        if (node.Arguments.Count != 5)
        {
            throw new GraphException("Join method must have exactly 5 arguments");
        }

        // For JOINs, we need to manually construct the query instead of processing the full expression tree
        // This avoids triggering path segment mode from relationship queries

        // Clear any existing state and build fresh
        _context.Builder.ClearMatches();
        _context.Builder.ClearWhere();
        _context.Builder.DisableComplexPropertyLoading();

        // Extract the outer source (relationship queryable) manually
        var outerSource = node.Arguments[0];
        var outerQueryable = ExtractQueryableFromExpression(outerSource);

        // Extract the inner source (node queryable)
        var innerSource = node.Arguments[1];
        var innerQueryable = ExtractQueryableFromExpression(innerSource);

        string? outerAlias = null;
        string? innerAlias = null;

        // Add MATCH clause for the outer source (relationships)
        if (outerQueryable is IGraphRelationshipQueryable)
        {
            var relType = outerQueryable.ElementType;
            var relLabel = Labels.GetLabelFromType(relType);
            outerAlias = "r";

            // Add a simple relationship pattern without enabling path segments
            _context.Builder.AddMatchPattern($"(src)-[{outerAlias}:{relLabel}]->(tgt)");
            _context.Scope.CurrentAlias = outerAlias;
            _context.Scope.CurrentType = relType;
            _logger.LogDebug("Added outer relationship MATCH: (src)-[{Alias}:{Label}]->(tgt)", outerAlias, relLabel);
        }

        // Add MATCH clause for the inner source (nodes)
        if (innerQueryable is IGraphNodeQueryable)
        {
            var nodeType = innerQueryable.ElementType;
            var nodeLabel = Labels.GetLabelFromType(nodeType);
            innerAlias = _context.Scope.GetOrCreateAlias(nodeType, "joined");

            _context.Builder.AddMatch(innerAlias, nodeLabel);
            _logger.LogDebug("Added inner node MATCH: ({Alias}:{Label})", innerAlias, nodeLabel);
        }

        // Process any WHERE conditions from the outer source
        ExtractAndProcessWhereConditions(outerSource);

        // Process the JOIN condition
        var outerKeySelector = ExtractLambda(node.Arguments[2]);
        var innerKeySelector = ExtractLambda(node.Arguments[3]);

        if (outerKeySelector != null && innerKeySelector != null)
        {
            // Set context for outer key (relationship context)
            _context.Scope.CurrentAlias = outerAlias;
            var outerKey = _expressionVisitor.VisitAndReturnCypher(outerKeySelector.Body);

            // Set context for inner key (node context)
            _context.Scope.CurrentAlias = innerAlias;
            var innerKey = _expressionVisitor.VisitAndReturnCypher(innerKeySelector.Body);

            // Add the JOIN condition
            var joinCondition = $"{outerKey} = {innerKey}";
            _context.Builder.AddWhere(joinCondition);
            _logger.LogDebug("Added JOIN condition: {Condition}", joinCondition);
        }

        // Handle the result selector
        var resultSelector = ExtractLambda(node.Arguments[4]);
        if (resultSelector != null && resultSelector.Body is ParameterExpression param)
        {
            var paramIndex = resultSelector.Parameters.IndexOf(param);
            if (paramIndex == 1 && innerAlias != null) // Selecting the inner (second) parameter
            {
                // Set up the context for returning the joined entity
                _context.Scope.CurrentAlias = innerAlias;
                if (innerQueryable != null)
                {
                    _context.Scope.CurrentType = innerQueryable.ElementType;
                }

                // Set the main node alias in the builder so RETURN uses the correct alias
                _context.Builder.SetMainNodeAlias(innerAlias);
                _logger.LogDebug("JOIN result: selecting joined entity with alias {Alias}", innerAlias);
            }
            else if (paramIndex == 0 && outerAlias != null) // Selecting the outer (first) parameter
            {
                _context.Scope.CurrentAlias = outerAlias;
                if (outerQueryable != null)
                {
                    _context.Scope.CurrentType = outerQueryable.ElementType;
                }

                // Set the main node alias in the builder
                _context.Builder.SetMainNodeAlias(outerAlias);
                _logger.LogDebug("JOIN result: selecting outer entity with alias {Alias}", outerAlias);
            }
        }

        _logger.LogDebug("Completed JOIN processing with manual query construction");
        return result ?? node.Arguments[0];
    }

    private IQueryable? ExtractQueryableFromExpression(Expression expression)
    {
        // Traverse the expression tree to find the queryable
        if (expression is ConstantExpression { Value: IQueryable queryable })
        {
            return queryable;
        }

        if (expression is MethodCallExpression methodCall)
        {
            // For chained method calls, recursively look for the queryable
            for (int i = 0; i < methodCall.Arguments.Count; i++)
            {
                var arg = methodCall.Arguments[i];
                if (arg is ConstantExpression { Value: IQueryable q })
                {
                    return q;
                }

                // Recursively search method call arguments
                var nested = ExtractQueryableFromExpression(arg);
                if (nested != null)
                {
                    return nested;
                }
            }
        }

        return null;
    }

    private void ExtractAndProcessWhereConditions(Expression expression)
    {
        // Look for WHERE conditions in the outer source expression chain
        if (expression is MethodCallExpression methodCall && methodCall.Method.Name == "Where")
        {
            var whereClause = ExtractLambda(methodCall.Arguments[1]);
            if (whereClause != null)
            {
                var whereCondition = _expressionVisitor.VisitAndReturnCypher(whereClause.Body);
                _context.Builder.AddWhere(whereCondition);
                _logger.LogDebug("Added WHERE condition from outer source: {Condition}", whereCondition);
            }
        }

        // Recursively process nested method calls
        if (expression is MethodCallExpression nestedCall)
        {
            foreach (var arg in nestedCall.Arguments)
            {
                if (arg is MethodCallExpression)
                {
                    ExtractAndProcessWhereConditions(arg);
                }
            }
        }
    }

    private Expression HandleUnion(MethodCallExpression node, Expression? result)
    {
        // Union is complex - for now, provide a basic implementation or throw
        throw new GraphException("Union operations are not yet fully implemented in the refactored architecture");
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

                // Set up the pending path segment pattern with appropriate aliases
                var sourceAlias = _context.Scope.GetOrCreateAlias(sourceType, "src");
                var relAlias = _context.Scope.GetOrCreateAlias(relationshipType, "r");
                var targetAlias = _context.Scope.GetOrCreateAlias(targetType, "tgt");

                _context.Builder.SetPendingPathSegmentPattern(
                    sourceType, relationshipType, targetType,
                    sourceAlias, relAlias, targetAlias);

                _logger.LogDebug("Set up pending path segment pattern: ({Source}:{SourceType})-[{Rel}:{RelType}]->({Target}:{TargetType})",
                    sourceAlias, sourceType.Name, relAlias, relationshipType.Name, targetAlias, targetType.Name);
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

    private Expression HandleWithDepth(MethodCallExpression node, Expression? result)
    {
        _logger.LogDebug("Processing WithDepth method call");

        if (node.Arguments.Count == 3)
        {
            // Extract min and max depth arguments
            var minDepthArg = node.Arguments[1];
            var maxDepthArg = node.Arguments[2];

            var minDepth = EvaluateConstantExpression<int>(minDepthArg);
            var maxDepth = EvaluateConstantExpression<int>(maxDepthArg);

            _context.Builder.SetDepth(minDepth, maxDepth);
            _logger.LogDebug("Set traversal depth: min={MinDepth}, max={MaxDepth}", minDepth, maxDepth);
        }
        else if (node.Arguments.Count == 2)
        {
            // Single argument means max depth only
            var maxDepthArg = node.Arguments[1];
            var maxDepth = EvaluateConstantExpression<int>(maxDepthArg);

            _context.Builder.SetDepth(maxDepth);
            _logger.LogDebug("Set traversal max depth: {MaxDepth}", maxDepth);
        }
        else
        {
            throw new GraphException("WithDepth method must have 1 or 2 depth arguments");
        }

        return result ?? node.Arguments[0];
    }

    private Expression HandleSearch(MethodCallExpression node, Expression? result)
    {
        _logger.LogDebug("Processing Search method call");

        if (node.Arguments.Count != 2)
            throw new GraphException("Search method must have exactly 2 arguments");

        var searchQueryArg = node.Arguments[1];
        var searchQuery = EvaluateConstantExpression<string>(searchQueryArg);

        _logger.LogDebug("Processing Search with query: {Query}", searchQuery);

        // Determine the entity type from the source queryable
        var sourceType = result?.Type ?? node.Arguments[0].Type;
        var elementType = TypeHelpers.GetElementType(sourceType);

        // Create a SearchExpression and handle it
        var searchExpr = new SearchExpression(node.Arguments[0], searchQuery);
        HandleSearchExpression(searchExpr, elementType);

        return result ?? node.Arguments[0];
    }

    private void HandleSearchExpression(SearchExpression searchExpr, Type elementType)
    {
        // Check if we're in a path segments context
        if (_context.Builder.IsPathSegmentLoading())
        {
            // In path segments context, apply search as a WHERE condition on the target nodes
            var currentAlias = _context.Scope.CurrentAlias ?? "tgt";
            var searchParamName = _context.Builder.AddParameter(searchExpr.SearchQuery);

            // Create a WHERE condition that searches in the target node's searchable properties
            var searchableProps = GetSearchableProperties(elementType);
            // Build the Cypher WHERE clause to check each property
            var searchCondition = string.Join(" OR ", searchableProps.Split(',').Select(p => $"toLower(toString({currentAlias}.{p.Trim(' ', '\'', '"')})) CONTAINS toLower({searchParamName})"));
            _context.Builder.AddWhere($"({searchCondition})");

            _logger.LogDebug("Applied search as WHERE condition in path segments context: {Condition}", searchCondition);
            return;
        }

        var indexName = GetFullTextIndexName(elementType);
        var paramName = _context.Builder.AddParameter(searchExpr.SearchQuery);

        if (typeof(INode).IsAssignableFrom(elementType))
        {
            // Node full text search
            var alias = _context.Scope.GetOrCreateAlias(elementType, "n");
            _context.Builder.AddFullTextNodeSearch(indexName, paramName, alias);
            _context.Scope.CurrentAlias = alias;
            _context.Builder.SetMainNodeAlias(alias);
            _context.Builder.EnableComplexPropertyLoading();
        }
        else if (typeof(IRelationship).IsAssignableFrom(elementType))
        {
            // Relationship full text search  
            var alias = _context.Scope.GetOrCreateAlias(elementType, "r");
            _context.Builder.AddFullTextRelationshipSearch(indexName, paramName, alias);
            _context.Scope.CurrentAlias = alias;
            _context.Builder.SetMainNodeAlias(alias);

            // Set the relationship query flag directly without adding MATCH clauses
            // We'll use reflection to set the private field since there's no public method
            var builderType = _context.Builder.GetType();
            var field = builderType.GetField("_isRelationshipQuery", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            field?.SetValue(_context.Builder, true);

            // Disable complex property loading for relationships since they don't need it
            _context.Builder.DisableComplexPropertyLoading();
        }
        else
        {
            // Entity search (both nodes and relationships)
            var nodeAlias = _context.Scope.GetOrCreateAlias(typeof(INode), "n");
            var relAlias = _context.Scope.GetOrCreateAlias(typeof(IRelationship), "r");
            var nodeIndexName = "node_fulltext_index_all_labels";
            var relIndexName = "relationship_fulltext_index_all_types";
            _context.Builder.AddFullTextEntitySearch(nodeIndexName, relIndexName, paramName, nodeAlias, relAlias);
            _context.Scope.CurrentAlias = nodeAlias; // Default to node alias
            _context.Builder.SetMainNodeAlias(nodeAlias);
            // Disable complex property loading for entity search
            _context.Builder.DisableComplexPropertyLoading();
        }
    }

    private string GetSearchableProperties(Type elementType)
    {
        // Get the searchable properties for the given type
        var properties = new List<string>();

        foreach (var prop in elementType.GetProperties())
        {
            // Skip the base entity properties
            if (prop.Name == nameof(Model.IEntity.Id)) continue;
            if (typeof(Model.IRelationship).IsAssignableFrom(elementType))
            {
                if (prop.Name == nameof(Model.IRelationship.StartNodeId) ||
                    prop.Name == nameof(Model.IRelationship.EndNodeId) ||
                    prop.Name == nameof(Model.IRelationship.Direction))
                    continue;
            }

            // Only include string properties by default
            if (prop.PropertyType != typeof(string)) continue;

            // Check for explicit inclusion/exclusion via PropertyAttribute
            var propertyAttr = prop.GetCustomAttribute<PropertyAttribute>();
            if (propertyAttr != null)
            {
                if (propertyAttr.Ignore) continue;
                if (propertyAttr.IncludeInFullTextSearch == false) continue;
            }

            // Include by default for string properties
            var propertyName = propertyAttr?.Label ?? prop.Name;
            properties.Add(propertyName);
        }

        return string.Join(", ", properties.Select(p => $"'{p}'"));
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
                // Special handling for DynamicRelationship - it should match any relationship type
                if (queryable.ElementType == typeof(Model.DynamicRelationship))
                {
                    _logger.LogDebug("Adding dynamic relationship match (any type)");
                    _context.Builder.AddRelationshipMatch(""); // Empty string means match any type
                    _context.Scope.CurrentAlias = "r";

                    // Relationships are always treated as path segments, so they need complex property loading
                    _logger.LogDebug("Dynamic relationship requires complex property loading");
                    _context.Builder.EnableComplexPropertyLoading();
                }
                else
                {
                    // Get all compatible labels to support inheritance hierarchies
                    var compatibleLabels = Labels.GetCompatibleLabels(queryable.ElementType);
                    var relLabel = compatibleLabels.Count == 1
                        ? compatibleLabels[0]
                        : string.Join("|", compatibleLabels);

                    _logger.LogDebug("Adding relationship match with label(s): {RelLabel}", relLabel);
                    _context.Builder.AddRelationshipMatch(relLabel);
                    _context.Scope.CurrentAlias = "r";

                    // Relationships are always treated as path segments, so they need complex property loading
                    _logger.LogDebug("Relationship type {Type} requires complex property loading", queryable.ElementType.Name);
                    _context.Builder.EnableComplexPropertyLoading();
                }
            }
            else if (node.Value is IGraphNodeQueryable)
            {
                // For nodes, generate the MATCH clause using the queryable's element type
                var alias = _context.Scope.GetOrCreateAlias(queryable.ElementType, "src");

                // Special handling for DynamicNode - it should match any node label
                if (queryable.ElementType == typeof(Model.DynamicNode))
                {
                    _logger.LogDebug("Adding dynamic node match (any label)");
                    _context.Builder.AddMatch(alias, ""); // Empty string means match any label
                }
                else
                {
                    // Get all compatible labels to support inheritance hierarchies
                    var compatibleLabels = Labels.GetCompatibleLabels(queryable.ElementType);
                    if (compatibleLabels.Count == 1)
                    {
                        // Single label - use traditional syntax
                        var label = compatibleLabels[0];
                        _logger.LogDebug("Adding MATCH clause: ({Alias}:{Label})", alias, label);
                        _context.Builder.AddMatch(alias, label);
                    }
                    else
                    {
                        // Multiple labels - use label union syntax for inheritance support
                        var labelUnion = string.Join("|", compatibleLabels);
                        _logger.LogDebug("Adding MATCH clause with inheritance support: ({Alias}:{LabelUnion})", alias, labelUnion);
                        _context.Builder.AddMatch(alias, labelUnion);
                    }
                }

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

    private bool IsSpecialTypeProjection(Expression expression)
    {
        // Check if this expression represents a projection of a special type that requires complex property loading

        // Case 1: Direct parameter access to path segment properties (e.g., ps.StartNode, ps.Relationship)
        if (expression is MemberExpression memberExpr &&
            memberExpr.Expression is ParameterExpression param &&
            typeof(IGraphPathSegment).IsAssignableFrom(param.Type))
        {
            var memberName = memberExpr.Member.Name;
            if (memberName == nameof(IGraphPathSegment.StartNode) ||
                memberName == nameof(IGraphPathSegment.EndNode) ||
                memberName == nameof(IGraphPathSegment.Relationship))
            {
                return true;
            }
        }

        // Case 2: Parameter access that represents a node or relationship type
        if (expression is ParameterExpression paramExpr)
        {
            return typeof(Model.INode).IsAssignableFrom(paramExpr.Type) ||
                   typeof(Model.IRelationship).IsAssignableFrom(paramExpr.Type) ||
                   typeof(IGraphPathSegment).IsAssignableFrom(paramExpr.Type);
        }

        // Case 3: Member access that results in a node or relationship type
        if (expression is MemberExpression memberAccess)
        {
            var memberType = memberAccess.Type;
            return typeof(Model.INode).IsAssignableFrom(memberType) ||
                   typeof(Model.IRelationship).IsAssignableFrom(memberType) ||
                   typeof(IGraphPathSegment).IsAssignableFrom(memberType);
        }

        return false;
    }

    private string GenerateSpecialTypeProjection(Expression expression)
    {
        // Generate complex property structure for special types (INode, IRelationship, IGraphPathSegment)

        // Case 1: Direct parameter access to path segment properties (e.g., ps.StartNode, ps.Relationship)
        if (expression is MemberExpression memberExpr &&
            memberExpr.Expression is ParameterExpression param &&
            typeof(IGraphPathSegment).IsAssignableFrom(param.Type))
        {
            var memberName = memberExpr.Member.Name;
            return memberName switch
            {
                nameof(IGraphPathSegment.StartNode) => "{ Node: src, ComplexProperties: src_flat_properties }",
                nameof(IGraphPathSegment.EndNode) => "{ Node: tgt, ComplexProperties: tgt_flat_properties }",
                nameof(IGraphPathSegment.Relationship) => "r", // Relationships don't need complex property structure in this context
                _ => _expressionVisitor.VisitAndReturnCypher(expression)
            };
        }

        // Case 2: Parameter access that represents a node or relationship type
        if (expression is ParameterExpression paramExpr)
        {
            if (typeof(Model.INode).IsAssignableFrom(paramExpr.Type))
            {
                // This is a direct node parameter - use the current alias with complex properties
                var alias = _context.Scope.CurrentAlias ?? "src";
                return $"{{ Node: {alias}, ComplexProperties: {alias}_flat_properties }}";
            }

            if (typeof(Model.IRelationship).IsAssignableFrom(paramExpr.Type))
            {
                // This is a direct relationship parameter
                return "r";
            }

            if (typeof(IGraphPathSegment).IsAssignableFrom(paramExpr.Type))
            {
                // This is a direct path segment parameter - return structured object
                return "{ StartNode: { Node: src, ComplexProperties: src_flat_properties }, Relationship: r, EndNode: { Node: tgt, ComplexProperties: tgt_flat_properties } }";
            }
        }

        // Case 3: Member access that results in a node or relationship type
        if (expression is MemberExpression memberAccess)
        {
            var memberType = memberAccess.Type;
            if (typeof(Model.INode).IsAssignableFrom(memberType))
            {
                // Get the base expression and determine the appropriate alias
                var baseExpr = _expressionVisitor.VisitAndReturnCypher(memberAccess.Expression!);
                var alias = baseExpr; // This should be something like "src" or "tgt"
                return $"{{ Node: {alias}, ComplexProperties: {alias}_flat_properties }}";
            }

            if (typeof(Model.IRelationship).IsAssignableFrom(memberType))
            {
                // Relationships typically map to "r"
                return "r";
            }
        }

        // Fallback to regular expression translation
        return _expressionVisitor.VisitAndReturnCypher(expression);
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

    protected override Expression VisitExtension(Expression node)
    {
        _logger.LogDebug("VisitExtension called with node type: {NodeType}", node.GetType().Name);

        if (node is FullTextSearchExpression searchExpr)
        {
            _logger.LogDebug("Handling full text search expression for query: {Query}", searchExpr.SearchQuery);

            // Handle full text search by adding appropriate Cypher
            HandleFullTextSearch(searchExpr);
            return node;
        }

        return base.VisitExtension(node);
    }

    private void HandleFullTextSearch(FullTextSearchExpression searchExpr)
    {
        var indexName = GetFullTextIndexName(searchExpr.EntityType);
        var paramName = _context.Builder.AddParameter(searchExpr.SearchQuery);

        if (typeof(INode).IsAssignableFrom(searchExpr.EntityType))
        {
            // Node full text search
            var alias = _context.Scope.GetOrCreateAlias(searchExpr.EntityType, "n");


            {
                _context.Builder.AddFullTextNodeSearch(indexName, paramName, alias);
            }

            _context.Scope.CurrentAlias = alias;
            _context.Builder.SetMainNodeAlias(alias);
            _context.Builder.EnableComplexPropertyLoading();
        }
        else if (typeof(IRelationship).IsAssignableFrom(searchExpr.EntityType))
        {
            // Relationship full text search  
            var alias = _context.Scope.GetOrCreateAlias(searchExpr.EntityType, "r");


            {
                _context.Builder.AddFullTextRelationshipSearch(indexName, paramName, alias);
            }

            _context.Scope.CurrentAlias = alias;
            _context.Builder.SetMainNodeAlias(alias);

            // Set the relationship query flag directly without adding MATCH clauses
            // We'll use reflection to set the private field since there's no public method
            var builderType = _context.Builder.GetType();
            var field = builderType.GetField("_isRelationshipQuery", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            field?.SetValue(_context.Builder, true);

            // Disable complex property loading for relationships since they don't need it
            _context.Builder.DisableComplexPropertyLoading();
        }
        else
        {
            // Entity search (both nodes and relationships)
            var nodeAlias = _context.Scope.GetOrCreateAlias(typeof(INode), "n");
            var relAlias = _context.Scope.GetOrCreateAlias(typeof(IRelationship), "r");
            var nodeIndexName = "node_fulltext_index_all_labels";
            var relIndexName = "relationship_fulltext_index_all_types";
            _context.Builder.AddFullTextEntitySearch(nodeIndexName, relIndexName, paramName, nodeAlias, relAlias);
            _context.Scope.CurrentAlias = nodeAlias; // Default to node alias
            _context.Builder.SetMainNodeAlias(nodeAlias);
            // Disable complex property loading for entity search
            _context.Builder.DisableComplexPropertyLoading();
        }
    }

    private static string GetFullTextIndexName(Type entityType)
    {
        if (typeof(INode).IsAssignableFrom(entityType))
        {
            // For interface types, use the global nodes index
            if (entityType == typeof(INode))
            {
                return "node_fulltext_index_all_labels";
            }

            // For strongly typed entities, use the specific label index
            return $"node_fulltext_index_{Model.Labels.GetLabelFromType(entityType).ToLowerInvariant()}";
        }
        else if (typeof(IRelationship).IsAssignableFrom(entityType))
        {
            // For interface types, use the global relationships index
            if (entityType == typeof(IRelationship))
            {
                return "relationship_fulltext_index_all_types";
            }

            // For strongly typed entities, use the specific type index
            return $"relationship_fulltext_index_{Model.Labels.GetLabelFromType(entityType).ToLowerInvariant()}";
        }
        else
        {
            // For entity types, use the global nodes index as a fallback
            return "node_fulltext_index_all_labels";
        }
    }
}