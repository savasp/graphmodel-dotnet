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
using Cvoya.Graph.Model.Neo4j.Querying.Cypher.Visitors.Core;
using Cvoya.Graph.Model.Neo4j.Querying.Cypher.Visitors.Expressions;
using Microsoft.Extensions.Logging;

internal sealed class WhereVisitor(string alias, CypherQueryContext context) : ClauseVisitorBase<WhereVisitor>(context)
{
    private readonly ICypherExpressionVisitor _expressionVisitor =
        new ExpressionVisitorChainFactory(context).CreateWhereClauseChain(alias);

    public void ProcessWhereClause(LambdaExpression lambda)
    {
        Logger.LogDebug("Processing WHERE clause: {Expression}", lambda);

        // Visit the lambda body to get the expression
        var expression = _expressionVisitor.Visit(lambda.Body);
        Logger.LogDebug("Generated WHERE expression: {Expression}", expression);

        // Add the expression to the query builder
        Builder.AddWhere(expression);
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