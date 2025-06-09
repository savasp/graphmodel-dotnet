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

namespace Cvoya.Graph.Model.Neo4j.Cypher;

using System.Linq.Expressions;
using Cvoya.Graph.Model.Neo4j.Linq;
using Microsoft.Extensions.Logging;

internal class CypherQueryVisitor(GraphQueryContext queryContext, ILogger? logger = null) : ExpressionVisitor
{
    private readonly CypherQueryBuilder _builder = new();
    private readonly Stack<QueryScope> _scopes = new([new QueryScope("n")]);
    private Type? _entityType;

    public CypherQueryResult Build()
    {
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
        logger?.LogDebug("Visiting method: {Method}", node.Method.Name);

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
            "Count" => HandleCount(node),
            "Any" => HandleAny(node),
            "Include" => HandleInclude(node),
            _ => base.VisitMethodCall(node)
        };
    }

    protected override Expression VisitConstant(ConstantExpression node)
    {
        if (node.Value is IGraphQueryable queryable)
        {
            var currentScope = _scopes.Peek();
            _builder.AddMatch(currentScope.Alias, GetLabelFromQueryable(queryable));
            queryContext.RootType = DetermineRootType(queryable);

            // Capture the entity type for complex property detection
            _entityType = queryable.ElementType;
        }

        return base.VisitConstant(node);
    }

    private Expression HandleWhere(MethodCallExpression node)
    {
        Visit(node.Arguments[0]);

        if (node.Arguments[1] is UnaryExpression { Operand: LambdaExpression lambda })
        {
            var whereVisitor = new WhereClauseVisitor(_scopes.Peek(), _builder);
            whereVisitor.Visit(lambda.Body);
        }

        return node;
    }

    private Expression HandleSelect(MethodCallExpression node)
    {
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
        Visit(node.Arguments[0]);

        if (node.Arguments[1] is ConstantExpression { Value: int limit })
        {
            _builder.SetLimit(limit);
        }

        return node;
    }

    private Expression HandleSkip(MethodCallExpression node)
    {
        Visit(node.Arguments[0]);

        if (node.Arguments[1] is ConstantExpression { Value: int skip })
        {
            _builder.SetSkip(skip);
        }

        return node;
    }

    private Expression HandleFirst(MethodCallExpression node)
    {
        Visit(node.Arguments[0]);
        _builder.SetLimit(1);
        queryContext.IsScalarResult = true;
        return node;
    }

    private Expression HandleSingle(MethodCallExpression node)
    {
        Visit(node.Arguments[0]);
        _builder.SetLimit(2); // Get 2 to check for multiple results
        queryContext.IsScalarResult = true;
        return node;
    }

    private Expression HandleCount(MethodCallExpression node)
    {
        Visit(node.Arguments[0]);
        _builder.SetAggregation("count", _scopes.Peek().Alias);
        queryContext.IsScalarResult = true;
        return node;
    }

    private Expression HandleAny(MethodCallExpression node)
    {
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
        Visit(node.Arguments[0]);

        if (node.Arguments[1] is UnaryExpression { Operand: LambdaExpression lambda })
        {
            var includeVisitor = new IncludeClauseVisitor(_scopes.Peek(), _builder);
            includeVisitor.Visit(lambda.Body);
        }

        return node;
    }

    private static string? GetLabelFromQueryable(IGraphQueryable queryable)
    {
        // TODO: Extract label from queryable metadata
        return null;
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
        logger?.LogDebug("Processing transaction attachment");

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
        return transactionExpression.Source is not null
            ? Visit(transactionExpression.Source)
            : transactionExpression;
    }
}