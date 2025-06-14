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

namespace Cvoya.Graph.Model.Neo4j.Querying.Cypher.Visitors;

using System.Linq.Expressions;
using Cvoya.Graph.Model.Neo4j.Core;
using Cvoya.Graph.Model.Neo4j.Linq;
using Cvoya.Graph.Model.Neo4j.Querying.Linq.Queryables;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

internal class CypherQueryVisitor : ExpressionVisitor
{
    private readonly CypherQueryBuilder _builder = new();
    private readonly Stack<QueryScope> _scopes = new([new QueryScope("n")]);
    private Type? _entityType;
    private readonly ILogger<CypherQueryVisitor> logger;
    private readonly GraphQueryContext queryContext;

    public CypherQueryVisitor(
        GraphQueryContext queryContext,
        bool shouldEnableComplexPropertyLoading = false,
        ILoggerFactory? loggerFactory = null)
    {
        this.queryContext = queryContext ?? throw new ArgumentNullException(nameof(queryContext));
        logger = loggerFactory?.CreateLogger<CypherQueryVisitor>() ?? NullLogger<CypherQueryVisitor>.Instance;

        if (shouldEnableComplexPropertyLoading)
        {
            _builder.EnableComplexPropertyLoading();
        }
    }

    public CypherQueryResult Build()
    {
        // If we're expecting a boolean result but haven't set up the query for that, fix it
        if (queryContext.IsScalarResult && queryContext.ResultType == typeof(bool) && !_builder.HasExplicitReturn)
        {
            _builder.SetExistsQuery();
        }

        // Check if we're dealing with entity types that might have complex properties
        if (_entityType != null && !queryContext.IsProjection && !queryContext.IsScalarResult)
        {
            if (ComplexPropertyHelper.HasComplexProperties(_entityType))
            {
                _builder.EnableComplexPropertyLoading();
            }
        }

        return _builder.Build();
    }

    public override Expression? Visit(Expression? node)
    {
        return node switch
        {
            // Handle our custom transaction expression
            GraphTransactionExpression txExpr => VisitTransaction(txExpr),
            _ => base.Visit(node)
        };
    }

    protected override Expression VisitMethodCall(MethodCallExpression node)
    {
        logger.LogDebug("Visiting method: {Method}", node.Method.Name);

        return node.Method.Name switch
        {
            "Where" => HandleWhere(node),
            "Select" => HandleSelect(node),
            "OrderBy" or "OrderByDescending" => HandleOrderBy(node),
            "ThenBy" or "ThenByDescending" => HandleThenBy(node),
            "Take" => HandleTake(node),
            "Skip" => HandleSkip(node),
            "First" or "FirstOrDefault" => HandleFirst(node),
            "Single" or "SingleOrDefault" => HandleSingle(node),
            "Count" or "LongCount" => HandleCount(node),
            "Any" => HandleAny(node),
            "All" => HandleAll(node),
            "Sum" => HandleSum(node),
            "Average" => HandleAverage(node),
            "Min" => HandleMin(node),
            "Max" => HandleMax(node),
            "Include" => HandleInclude(node),
            "PathSegments" => HandlePathSegments(node),
            _ => base.VisitMethodCall(node)
        };
    }

    protected override Expression VisitConstant(ConstantExpression node)
    {
        logger.LogDebug("Processing constant expression of type: {Type}", node.Type.Name);

        // Check for different types of queryables
        if (node.Value is IGraphNodeQueryable nodeQueryable)
        {
            logger.LogDebug("Found IGraphNodeQueryable: {QueryableType}", nodeQueryable.GetType().Name);

            var elementType = nodeQueryable.ElementType;
            _entityType = elementType;

            var label = Labels.GetLabelFromType(elementType);
            logger.LogDebug("Using label: {Label} for type: {Type}", label, elementType.Name);
            _builder.AddMatch("n", label);

            // Node queries use 'n' as the alias
            _scopes.Clear();
            _scopes.Push(new QueryScope("n"));
        }
        else if (node.Value is IGraphRelationshipQueryable relationshipQueryable)
        {
            logger.LogDebug("Found IGraphRelationshipQueryable: {QueryableType}", relationshipQueryable.GetType().Name);

            var elementType = relationshipQueryable.ElementType;
            _entityType = elementType;

            var relationshipType = Labels.GetLabelFromType(elementType);
            logger.LogDebug("Using relationship type: {Type} for type: {Type}", relationshipType, elementType.Name);

            // Fix: Use the new method
            _builder.AddRelationshipMatch(relationshipType);

            // Add default return for the relationship
            _builder.AddReturn("r");

            // Relationship queries use 'r' as the alias
            _scopes.Clear();
            _scopes.Push(new QueryScope("r"));
        }
        else if (node.Value is IGraphTraversalQueryable traversalQueryable)
        {
            // Handle traversal queries if needed
            logger.LogDebug("Found IGraphTraversalQueryable: {QueryableType}", traversalQueryable.GetType().Name);
            // TODO: Implement traversal handling
        }
        else if (node.Value is IGraphQueryable graphQueryable)
        {
            // Fallback for any other queryable types
            logger.LogDebug("Found generic IGraphQueryable: {QueryableType}", graphQueryable.GetType().Name);
            // This shouldn't really happen, but log it if it does
        }

        return node;
    }

    private Expression HandlePathSegments(MethodCallExpression node)
    {
        logger.LogDebug("Processing PathSegments for method: {Method}", node.Method.Name);

        // PathSegments is a generic method, so we need to get the types
        if (node.Method.IsGenericMethodDefinition || node.Method.IsGenericMethod)
        {
            var genericArgs = node.Method.GetGenericArguments();
            logger.LogDebug("Generic args count: {Count}", genericArgs.Length);

            if (genericArgs.Length == 2)
            {
                var relationshipType = genericArgs[0];
                var targetNodeType = genericArgs[1];
                logger.LogDebug("Relationship type: {RelType}, Target type: {TargetType}",
                    relationshipType.Name, targetNodeType.Name);

                // Get the source type from the expression
                // For extension methods, the instance is in node.Object
                Expression? sourceExpression = node.Object;

                // If node.Object is null, it might be a static method call with the source as first argument
                if (sourceExpression == null && node.Arguments.Count > 0)
                {
                    sourceExpression = node.Arguments[0];
                }

                logger.LogDebug("Source expression type: {Type}, Expression node type: {NodeType}, Is null: {IsNull}",
                    sourceExpression?.Type.Name, sourceExpression?.NodeType, sourceExpression == null);

                if (sourceExpression != null)
                {
                    // First, let's visit the source expression to set up the initial match
                    Visit(sourceExpression);

                    // Now _entityType should be set from visiting the source
                    if (_entityType != null)
                    {
                        // Get labels
                        var sourceLabel = Labels.GetLabelFromType(_entityType);
                        var relLabel = Labels.GetLabelFromType(relationshipType);
                        var targetLabel = Labels.GetLabelFromType(targetNodeType);

                        logger.LogDebug("Creating path pattern: {Source} -[{RelType}]-> {Target}",
                            sourceLabel, relLabel, targetLabel);

                        // Clear any existing match and add the path pattern
                        // Since we've already visited the source, we need to replace the simple node match
                        _builder.ClearMatches(); // Add this method to CypherQueryBuilder

                        // Add the match with the source node and the path extension
                        _builder.AddMatch("n", sourceLabel, $"-[r:{relLabel}]->(t:{targetLabel})");
                        logger.LogDebug("Added match clause");

                        // Return all three elements
                        _builder.AddReturn("n");
                        _builder.AddReturn("r");
                        _builder.AddReturn("t");
                        logger.LogDebug("Added return clauses");

                        // Mark this as a projection query
                        queryContext.IsProjection = true;
                        queryContext.ProjectionType = node.Type;
                        logger.LogDebug("Set projection context");
                    }
                    else
                    {
                        logger.LogWarning("Entity type not set after visiting source expression");
                    }
                }
                else
                {
                    logger.LogWarning("Source expression is null");
                }
            }
            else
            {
                logger.LogWarning("Expected 2 generic arguments but got {Count}", genericArgs.Length);
            }
        }
        else
        {
            logger.LogWarning("Method is not generic");
        }

        return node;
    }

    private Expression HandleAll(MethodCallExpression node)
    {
        logger.LogDebug("Processing all clause for method: {Method}", node.Method.Name);

        Visit(node.Arguments[0]);

        if (node.Arguments[1] is UnaryExpression { Operand: LambdaExpression lambda })
        {
            // All(predicate) means: NOT EXISTS (WHERE NOT predicate)
            // So we need to find any record where the predicate is false
            var negatedBody = Expression.Not(lambda.Body);
            var negatedLambda = Expression.Lambda(negatedBody, lambda.Parameters);

            var whereVisitor = new WhereClauseVisitor(_scopes.Peek(), _builder);
            whereVisitor.ProcessWhereClause(negatedLambda);

            _builder.SetNotExistsQuery();
        }

        queryContext.IsScalarResult = true;
        return node;
    }

    private Expression HandleSum(MethodCallExpression node)
    {
        logger.LogDebug("Processing sum clause for method: {Method}", node.Method.Name);

        Visit(node.Arguments[0]);

        if (node.Arguments.Count > 1 && node.Arguments[1] is UnaryExpression { Operand: LambdaExpression lambda })
        {
            // Sum with selector
            var selectVisitor = new SelectClauseVisitor(_scopes.Peek(), _builder);
            var propertyName = selectVisitor.GetPropertyName(lambda.Body);
            _builder.SetAggregation("sum", $"{_scopes.Peek().Alias}.{propertyName}");
        }
        else
        {
            // Sum without selector - for collections of numbers
            _builder.SetAggregation("sum", _scopes.Peek().Alias);
        }

        queryContext.IsScalarResult = true;
        return node;
    }

    private Expression HandleAverage(MethodCallExpression node)
    {
        logger.LogDebug("Processing average clause for method: {Method}", node.Method.Name);

        Visit(node.Arguments[0]);

        if (node.Arguments.Count > 1 && node.Arguments[1] is UnaryExpression { Operand: LambdaExpression lambda })
        {
            // Average with selector
            var selectVisitor = new SelectClauseVisitor(_scopes.Peek(), _builder);
            var propertyName = selectVisitor.GetPropertyName(lambda.Body);
            _builder.SetAggregation("avg", $"{_scopes.Peek().Alias}.{propertyName}");
        }
        else
        {
            // Average without selector
            _builder.SetAggregation("avg", _scopes.Peek().Alias);
        }

        queryContext.IsScalarResult = true;
        return node;
    }

    private Expression HandleMin(MethodCallExpression node)
    {
        logger.LogDebug("Processing min clause for method: {Method}", node.Method.Name);

        Visit(node.Arguments[0]);

        if (node.Arguments.Count > 1 && node.Arguments[1] is UnaryExpression { Operand: LambdaExpression lambda })
        {
            // Min with selector
            var selectVisitor = new SelectClauseVisitor(_scopes.Peek(), _builder);
            var propertyName = selectVisitor.GetPropertyName(lambda.Body);
            _builder.SetAggregation("min", $"{_scopes.Peek().Alias}.{propertyName}");
        }
        else
        {
            // Min without selector - returns the minimum entity based on some default comparison
            _builder.SetAggregation("min", _scopes.Peek().Alias);
        }

        queryContext.IsScalarResult = true;
        return node;
    }

    private Expression HandleMax(MethodCallExpression node)
    {
        logger.LogDebug("Processing max clause for method: {Method}", node.Method.Name);

        Visit(node.Arguments[0]);

        if (node.Arguments.Count > 1 && node.Arguments[1] is UnaryExpression { Operand: LambdaExpression lambda })
        {
            // Max with selector
            var selectVisitor = new SelectClauseVisitor(_scopes.Peek(), _builder);
            var propertyName = selectVisitor.GetPropertyName(lambda.Body);
            _builder.SetAggregation("max", $"{_scopes.Peek().Alias}.{propertyName}");
        }
        else
        {
            // Max without selector
            _builder.SetAggregation("max", _scopes.Peek().Alias);
        }

        queryContext.IsScalarResult = true;
        return node;
    }

    private Expression HandleWhere(MethodCallExpression node)
    {
        logger.LogDebug("Processing where clause for method: {Method}", node.Method.Name);

        // First, visit the source expression (the queryable we're filtering)
        Visit(node.Arguments[0]);

        // Then process the lambda expression for the WHERE clause
        if (node.Arguments.Count > 1 && node.Arguments[1] is UnaryExpression { Operand: LambdaExpression lambda })
        {
            logger.LogDebug("Processing WHERE lambda with parameter: {Parameter}", lambda.Parameters[0].Name);

            var whereVisitor = new WhereClauseVisitor(_scopes.Peek(), _builder);
            whereVisitor.ProcessWhereClause(lambda);

            logger.LogDebug("WHERE clause processed successfully");
        }
        else
        {
            logger.LogWarning("WHERE method call missing lambda expression");
        }

        return node;
    }

    private Expression HandleSelect(MethodCallExpression node)
    {
        logger.LogDebug("Processing select clause for method: {Method}", node.Method.Name);

        Visit(node.Arguments[0]);

        if (node.Arguments[1] is UnaryExpression { Operand: LambdaExpression lambda })
        {
            queryContext.IsProjection = true;
            queryContext.ProjectionType = lambda.ReturnType;

            var selectVisitor = new SelectClauseVisitor(_scopes.Peek(), _builder);
            selectVisitor.Visit(lambda.Body);
        }

        return node;
    }

    private Expression HandleOrderBy(MethodCallExpression node)
    {
        logger.LogDebug("Processing order by clause for method: {Method}", node.Method.Name);

        Visit(node.Arguments[0]);

        if (node.Arguments[1] is UnaryExpression { Operand: LambdaExpression lambda })
        {
            var orderByVisitor = new OrderByClauseVisitor(_scopes.Peek(), _builder);
            orderByVisitor.Visit(lambda.Body, node.Method.Name.Contains("Descending"));
        }

        return node;
    }

    private Expression HandleThenBy(MethodCallExpression node)
    {
        logger.LogDebug("Processing then by clause for method: {Method}", node.Method.Name);

        Visit(node.Arguments[0]);

        if (node.Arguments[1] is UnaryExpression { Operand: LambdaExpression lambda })
        {
            var orderByVisitor = new OrderByClauseVisitor(_scopes.Peek(), _builder);
            orderByVisitor.Visit(lambda.Body, node.Method.Name.Contains("Descending"));
        }

        return node;
    }

    private Expression HandleTake(MethodCallExpression node)
    {
        logger.LogDebug("Processing take clause for method: {Method}", node.Method.Name);

        Visit(node.Arguments[0]);

        if (node.Arguments[1] is ConstantExpression { Value: int limit })
        {
            _builder.SetLimit(limit);
        }

        return node;
    }

    private Expression HandleSkip(MethodCallExpression node)
    {
        logger.LogDebug("Processing skip clause for method: {Method}", node.Method.Name);

        Visit(node.Arguments[0]);

        if (node.Arguments[1] is ConstantExpression { Value: int skip })
        {
            _builder.SetSkip(skip);
        }

        return node;
    }

    private Expression HandleFirst(MethodCallExpression node)
    {
        logger.LogDebug("Processing first clause for method: {Method}", node.Method.Name);

        Visit(node.Arguments[0]);
        _builder.SetLimit(1);
        return node;
    }

    private Expression HandleSingle(MethodCallExpression node)
    {
        logger.LogDebug("Processing single clause for method: {Method}", node.Method.Name);

        Visit(node.Arguments[0]);
        _builder.SetLimit(2); // Get 2 to check for multiple results
        return node;
    }

    private Expression HandleCount(MethodCallExpression node)
    {
        logger.LogDebug("Processing count clause for method: {Method}", node.Method.Name);

        Visit(node.Arguments[0]);
        _builder.SetAggregation("count", _scopes.Peek().Alias);
        queryContext.IsScalarResult = true;
        return node;
    }

    private Expression HandleAny(MethodCallExpression node)
    {
        logger.LogDebug("Processing any clause for method: {Method}", node.Method.Name);

        Visit(node.Arguments[0]);

        if (node.Arguments.Count > 1)
        {
            // Any with predicate
            if (node.Arguments[1] is UnaryExpression { Operand: LambdaExpression lambda })
            {
                var whereVisitor = new WhereClauseVisitor(_scopes.Peek(), _builder);
                whereVisitor.Visit(lambda.Body);
            }
        }

        _builder.SetLimit(1);
        _builder.SetExistsQuery();
        queryContext.IsScalarResult = true;
        return node;
    }

    private Expression HandleInclude(MethodCallExpression node)
    {
        logger.LogDebug("Processing include clause for method: {Method}", node.Method.Name);

        Visit(node.Arguments[0]);

        if (node.Arguments[1] is UnaryExpression { Operand: LambdaExpression lambda })
        {
            var includeVisitor = new IncludeClauseVisitor(_scopes.Peek(), _builder);
            includeVisitor.Visit(lambda.Body);
        }

        return node;
    }

    private static GraphQueryContext.QueryRootType DetermineRootType(IGraphQueryable queryable) =>
        queryable switch
        {
            IGraphNodeQueryable => GraphQueryContext.QueryRootType.Node,
            IGraphRelationshipQueryable => GraphQueryContext.QueryRootType.Relationship,
            IGraphTraversalQueryable => GraphQueryContext.QueryRootType.Path,
            _ => GraphQueryContext.QueryRootType.Custom
        };

    private Expression? VisitTransaction(GraphTransactionExpression transactionExpression)
    {
        logger.LogDebug("Processing transaction attachment");

        // Store the transaction in the query context
        if (transactionExpression.Transaction is GraphTransaction neo4jTx)
        {
            queryContext.Transaction = neo4jTx;
        }
        else
        {
            throw new NotSupportedException(
                $"Transaction type {transactionExpression.Transaction.GetType()} is not supported by Neo4j provider");
        }

        // Continue visiting the source expression
        return transactionExpression.InnerExpression is not null
            ? Visit(transactionExpression.InnerExpression)
            : transactionExpression;
    }
}