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

internal class OrderByClauseVisitor(QueryScope scope, CypherQueryBuilder builder) : ExpressionVisitor
{
    private readonly Stack<string> _orderExpressions = new();
    private bool _isDescending;

    public void Visit(Expression expression, bool isDescending)
    {
        _isDescending = isDescending;
        Visit(expression);

        // After visiting, we should have the expression on the stack
        if (_orderExpressions.TryPop(out var orderExpression))
        {
            builder.AddOrderBy(orderExpression, _isDescending);
        }
    }

    protected override Expression VisitMember(MemberExpression node)
    {
        var path = BuildPropertyPath(node);
        _orderExpressions.Push(path);
        return node;
    }

    protected override Expression VisitMethodCall(MethodCallExpression node)
    {
        // Handle method calls that might be used in ordering
        var expression = node.Method.Name switch
        {
            "ToLower" => HandleToLower(node),
            "ToUpper" => HandleToUpper(node),
            "ToString" => HandleToString(node),
            _ => throw new NotSupportedException($"Method {node.Method.Name} is not supported in ORDER BY clause")
        };

        _orderExpressions.Push(expression);
        return node;
    }

    protected override Expression VisitUnary(UnaryExpression node)
    {
        // Handle conversions and other unary operations
        if (node.NodeType == ExpressionType.Convert || node.NodeType == ExpressionType.ConvertChecked)
        {
            // Just visit the operand
            return Visit(node.Operand);
        }

        return base.VisitUnary(node);
    }

    private string BuildPropertyPath(MemberExpression node)
    {
        var parts = new Stack<string>();

        for (var current = node; current is not null; current = current.Expression as MemberExpression)
        {
            parts.Push(current.Member.Name);
        }

        return $"{scope.CurrentAlias}.{string.Join(".", parts)}";
    }

    private string HandleToLower(MethodCallExpression node)
    {
        Visit(node.Object!);
        var target = _orderExpressions.Pop();
        return $"toLower({target})";
    }

    private string HandleToUpper(MethodCallExpression node)
    {
        Visit(node.Object!);
        var target = _orderExpressions.Pop();
        return $"toUpper({target})";
    }

    private string HandleToString(MethodCallExpression node)
    {
        Visit(node.Object!);
        var target = _orderExpressions.Pop();
        return $"toString({target})";
    }
}