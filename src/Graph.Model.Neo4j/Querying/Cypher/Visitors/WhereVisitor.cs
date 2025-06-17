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
using Cvoya.Graph.Model.Neo4j.Querying.Cypher.Builders;
using Cvoya.Graph.Model.Neo4j.Querying.Cypher.Visitors.Expressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

internal class WhereVisitor : ExpressionVisitor
{
    private readonly QueryScope _scope;
    private readonly CypherQueryBuilder _builder;
    private readonly ILogger<WhereVisitor> _logger;
    private readonly ICypherExpressionVisitor _expressionVisitor;

    public WhereVisitor(QueryScope scope, CypherQueryBuilder builder, ILoggerFactory? loggerFactory = null)
    {
        _scope = scope ?? throw new ArgumentNullException(nameof(scope));
        _builder = builder ?? throw new ArgumentNullException(nameof(builder));
        _logger = loggerFactory?.CreateLogger<WhereVisitor>() ?? NullLogger<WhereVisitor>.Instance;

        // Create a chain of visitors
        var baseVisitor = new BaseExpressionVisitor(scope, builder, loggerFactory);
        var binaryVisitor = new BinaryExpressionVisitor(baseVisitor, loggerFactory);
        var stringVisitor = new StringMethodVisitor(binaryVisitor, loggerFactory);
        var collectionVisitor = new CollectionMethodVisitor(stringVisitor, loggerFactory);
        _expressionVisitor = collectionVisitor;
    }

    public void ProcessWhereClause(LambdaExpression lambda)
    {
        _logger.LogDebug("Processing WHERE clause: {Expression}", lambda);

        // Visit the lambda body to get the expression
        var expression = _expressionVisitor.Visit(lambda.Body);
        _logger.LogDebug("Generated WHERE expression: {Expression}", expression);

        // Add the expression to the query builder
        _builder.AddWhere(expression);
    }

    protected override Expression VisitBinary(BinaryExpression node)
    {
        _expressionVisitor.VisitBinary(node);
        return node;
    }

    protected override Expression VisitUnary(UnaryExpression node)
    {
        _expressionVisitor.VisitUnary(node);
        return node;
    }

    protected override Expression VisitMember(MemberExpression node)
    {
        _expressionVisitor.VisitMember(node);
        return node;
    }

    protected override Expression VisitMethodCall(MethodCallExpression node)
    {
        _expressionVisitor.VisitMethodCall(node);
        return node;
    }

    protected override Expression VisitConstant(ConstantExpression node)
    {
        _expressionVisitor.VisitConstant(node);
        return node;
    }

    protected override Expression VisitParameter(ParameterExpression node)
    {
        _expressionVisitor.VisitParameter(node);
        return node;
    }
}