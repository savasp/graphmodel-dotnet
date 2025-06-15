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
using System.Text;
using Cvoya.Graph.Model.Neo4j.Querying.Cypher.Builders;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

internal class WhereVisitor(QueryScope scope, CypherQueryBuilder builder, ILoggerFactory? loggerFactory = null) : ExpressionVisitor
{
    private readonly Stack<string> _expressions = new();
    private readonly ILogger logger = loggerFactory?.CreateLogger<WhereVisitor>() ?? NullLogger<WhereVisitor>.Instance;

    public void ProcessWhereClause(LambdaExpression lambda)
    {
        logger.LogDebug("Processing WHERE clause lambda: {Lambda}", lambda);

        Visit(lambda.Body);

        if (_expressions.Count == 1)
        {
            var whereClause = _expressions.Pop();
            logger.LogDebug("Adding WHERE clause to builder: {WhereClause}", whereClause);
            builder.AddWhere(whereClause);
        }
        else if (_expressions.Count > 1)
        {
            throw new InvalidOperationException($"Where clause processing left {_expressions.Count} expressions on stack");
        }
        else
        {
            logger.LogWarning("WHERE clause processing produced no expressions");
        }
    }

    protected override Expression VisitBinary(BinaryExpression node)
    {
        logger.LogDebug("Visiting binary expression: {NodeType}", node.NodeType);

        Visit(node.Left);
        if (_expressions.Count == 0)
            throw new InvalidOperationException($"Left side of {node.NodeType} produced no value");
        var left = _expressions.Pop();

        Visit(node.Right);
        if (_expressions.Count == 0)
            throw new InvalidOperationException($"Right side of {node.NodeType} produced no value");
        var right = _expressions.Pop();

        var expression = node.NodeType switch
        {
            ExpressionType.Equal => $"{left} = {right}",
            ExpressionType.NotEqual => $"{left} <> {right}",
            ExpressionType.GreaterThan => $"{left} > {right}",
            ExpressionType.GreaterThanOrEqual => $"{left} >= {right}",
            ExpressionType.LessThan => $"{left} < {right}",
            ExpressionType.LessThanOrEqual => $"{left} <= {right}",
            ExpressionType.AndAlso => $"({left} AND {right})",
            ExpressionType.OrElse => $"({left} OR {right})",
            _ => throw new NotSupportedException($"Binary operator {node.NodeType} is not supported")
        };

        logger.LogDebug("Binary expression result: {Expression}", expression);
        _expressions.Push(expression);
        return node;
    }

    protected override Expression VisitMember(MemberExpression node)
    {
        // Check if this is a parameter access (like p.Name)
        if (node.Expression is ParameterExpression param)
        {
            var propertyPath = $"{scope.CurrentAlias}.{node.Member.Name}";
            logger.LogDebug("Pushing property path: {PropertyPath}", propertyPath);
            _expressions.Push(propertyPath);
        }
        else if (node.Expression is UnaryExpression unary && unary.Operand is ParameterExpression)
        {
            // Handle cases like Convert(n, IEntity).Id where n is a parameter
            var propertyPath = $"{scope.CurrentAlias}.{node.Member.Name}";
            logger.LogDebug("Pushing property path from converted parameter: {PropertyPath}", propertyPath);
            _expressions.Push(propertyPath);
        }
        else if (node.Expression is MemberExpression memberExpr)
        {
            // Handle nested member access like r.StartNode.Name
            var path = BuildPropertyPath(node);
            if (path != null)
            {
                logger.LogDebug("Pushing nested property path: {PropertyPath}", path);
                _expressions.Push(path);
            }
            else
            {
                // Fall back to evaluation
                var value = EvaluateMemberExpression(node);
                HandleConstantValue(value);
            }
        }
        else
        {
            // For other member access, try to evaluate it as a constant
            var value = EvaluateMemberExpression(node);
            HandleConstantValue(value);
        }

        return node;
    }

    protected override Expression VisitConstant(ConstantExpression node)
    {
        if (node.Value == null)
        {
            logger.LogDebug("Pushing null constant");
            _expressions.Push("null");
        }
        else
        {
            var param = builder.AddParameter(node.Value);
            logger.LogDebug("Pushing constant parameter {ParamName} = {Value}", param, node.Value);
            _expressions.Push(param);
        }
        return node;
    }

    protected override Expression VisitMethodCall(MethodCallExpression node)
    {
        var expression = node.Method.Name switch
        {
            "Contains" when node.Object != null => HandleStringMethod(node, "CONTAINS"),
            "StartsWith" => HandleStringMethod(node, "STARTS WITH"),
            "EndsWith" => HandleStringMethod(node, "ENDS WITH"),
            "ToLower" => HandleToLower(node),
            "ToUpper" => HandleToUpper(node),
            _ => throw new NotSupportedException($"Method {node.Method.Name} is not supported in WHERE clause")
        };

        _expressions.Push(expression);
        return node;
    }

    protected override Expression VisitUnary(UnaryExpression node)
    {
        if (node.NodeType == ExpressionType.Not)
        {
            Visit(node.Operand);
            var operand = _expressions.Pop();
            _expressions.Push($"NOT ({operand})");
            return node;
        }

        return base.VisitUnary(node);
    }

    private static object? EvaluateMemberExpression(MemberExpression node)
    {
        // Don't try to evaluate if the expression contains parameters
        if (ContainsParameter(node))
        {
            throw new InvalidOperationException($"Cannot evaluate member expression containing parameter: {node}");
        }

        // This evaluates member expressions that aren't parameter-based
        // For example, if someone uses a captured variable in the lambda
        var objectMember = Expression.Convert(node, typeof(object));
        var getterLambda = Expression.Lambda<Func<object?>>(objectMember);
        var getter = getterLambda.Compile();
        return getter();
    }

    private static bool ContainsParameter(Expression expression)
    {
        var visitor = new ParameterExpressionVisitor();
        visitor.Visit(expression);
        return visitor.ContainsParameter;
    }

    private class ParameterExpressionVisitor : ExpressionVisitor
    {
        public bool ContainsParameter { get; private set; }

        protected override Expression VisitParameter(ParameterExpression node)
        {
            ContainsParameter = true;
            return base.VisitParameter(node);
        }
    }

    private string HandleStringMethod(MethodCallExpression node, string cypherOperator)
    {
        Visit(node.Object!);
        var target = _expressions.Pop();

        Visit(node.Arguments[0]);
        var argument = _expressions.Pop();

        return $"{target} {cypherOperator} {argument}";
    }

    private string HandleToLower(MethodCallExpression node)
    {
        Visit(node.Object!);
        var target = _expressions.Pop();
        return $"toLower({target})";
    }

    private string HandleToUpper(MethodCallExpression node)
    {
        Visit(node.Object!);
        var target = _expressions.Pop();
        return $"toUpper({target})";
    }

    private string? BuildPropertyPath(MemberExpression node)
    {
        var parts = new Stack<string>();
        Expression? current = node;  // Make current nullable

        // Walk up the member chain
        while (current is MemberExpression member)
        {
            parts.Push(member.Member.Name);
            current = member.Expression;  // This could be null, which is fine
        }

        // Check if we end at a parameter
        if (current is ParameterExpression param)
        {
            // For path segments, we need to map the property correctly
            // r.StartNode -> n (the source node)
            // r.EndNode -> t (the target node)
            // r.Relationship -> r (the relationship)

            var firstPart = parts.Count > 0 ? parts.Pop() : "";
            var alias = firstPart switch
            {
                "StartNode" => "n",
                "EndNode" => "t",
                "Relationship" => "r",
                _ => scope.CurrentAlias
            };

            // Build the rest of the path
            var pathBuilder = new StringBuilder(alias);
            foreach (var part in parts.Reverse())
            {
                pathBuilder.Append('.').Append(part);
            }

            return pathBuilder.ToString();
        }

        return null;
    }

    private void HandleConstantValue(object? value)
    {
        if (value is null)
        {
            logger.LogDebug("Pushing null");
            _expressions.Push("null");
        }
        else
        {
            var paramName = builder.AddParameter(value);
            logger.LogDebug("Pushing parameter {ParamName} = {Value}", paramName, value);
            _expressions.Push(paramName);
        }
    }
}