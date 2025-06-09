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

internal class WhereClauseVisitor(QueryScope scope, CypherQueryBuilder builder) : ExpressionVisitor
{
    private readonly Stack<string> _expressions = new();

    public void ProcessWhereClause(LambdaExpression lambda)
    {
        Visit(lambda.Body);

        if (_expressions.Count == 1)
        {
            builder.AddWhere(_expressions.Pop());
        }
        else if (_expressions.Count > 1)
        {
            throw new InvalidOperationException($"Where clause processing left {_expressions.Count} expressions on stack");
        }
    }

    protected override Expression VisitBinary(BinaryExpression node)
    {
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

        _expressions.Push(expression);
        return node;
    }

    protected override Expression VisitMember(MemberExpression node)
    {
        // Check if this is a parameter access (like p.Name)
        if (node.Expression is ParameterExpression)
        {
            _expressions.Push($"{scope.Alias}.{node.Member.Name}");
        }
        else
        {
            // For other member access, try to evaluate it as a constant
            var value = EvaluateMemberExpression(node);

            if (value is null)
            {
                _expressions.Push("null");
            }
            else
            {
                var paramName = builder.AddParameter(value);
                _expressions.Push(paramName);
            }
        }

        return node;
    }

    protected override Expression VisitConstant(ConstantExpression node)
    {
        if (node.Value == null)
        {
            _expressions.Push("null");
        }
        else
        {
            var param = builder.AddParameter(node.Value);
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

    private static object? EvaluateMemberExpression(MemberExpression node)
    {
        // This evaluates member expressions that aren't parameter-based
        // For example, if someone uses a captured variable in the lambda
        var objectMember = Expression.Convert(node, typeof(object));
        var getterLambda = Expression.Lambda<Func<object?>>(objectMember);
        var getter = getterLambda.Compile();
        return getter();
    }
}