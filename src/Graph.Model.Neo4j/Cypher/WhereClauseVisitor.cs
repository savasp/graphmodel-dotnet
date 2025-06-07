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

    protected override Expression VisitBinary(BinaryExpression node)
    {
        Visit(node.Left);
        var left = _expressions.Pop();

        Visit(node.Right);
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
        var path = BuildPropertyPath(node);
        _expressions.Push(path);
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

    public override Expression? Visit(Expression? node)
    {
        var result = base.Visit(node);

        if (_expressions.Count > 0 && node == result)
        {
            builder.AddWhere(_expressions.Pop());
        }

        return result;
    }

    private string BuildPropertyPath(MemberExpression node)
    {
        var parts = new Stack<string>();

        for (var current = node; current is not null; current = current.Expression as MemberExpression)
        {
            parts.Push(current.Member.Name);
        }

        return $"{scope.Alias}.{string.Join(".", parts)}";
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
}