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

using System.Linq.Expressions;

namespace Cvoya.Graph.Model.Neo4j.Cypher;

internal class SelectClauseVisitor(QueryScope scope, CypherQueryBuilder builder) : ExpressionVisitor
{
    private readonly Stack<(string Expression, string? Alias)> _projections = new();
    private string? _currentMemberName;

    protected override Expression VisitNew(NewExpression node)
    {
        // Handle anonymous type projections like: .Select(x => new { x.Name, x.Age })
        for (int i = 0; i < node.Arguments.Count; i++)
        {
            if (node.Members?[i] is { } member)
            {
                _currentMemberName = member.Name;
                Visit(node.Arguments[i]);
                _currentMemberName = null;
            }
        }

        // Add all projections to the builder
        while (_projections.TryPop(out var projection))
        {
            builder.AddReturn(projection.Expression, projection.Alias);
        }

        return node;
    }

    protected override Expression VisitMemberInit(MemberInitExpression node)
    {
        // Handle member initialization like: .Select(x => new Person { Name = x.Name })
        Visit(node.NewExpression);

        foreach (var binding in node.Bindings.OfType<MemberAssignment>())
        {
            _currentMemberName = binding.Member.Name;
            Visit(binding.Expression);
            _currentMemberName = null;
        }

        // Add all projections to the builder
        while (_projections.TryPop(out var projection))
        {
            builder.AddReturn(projection.Expression, projection.Alias);
        }

        return node;
    }

    protected override Expression VisitMember(MemberExpression node)
    {
        var path = BuildPropertyPath(node);

        // If we're selecting a single property without aliasing
        if (_currentMemberName == null && node.Expression?.Type != null)
        {
            _projections.Push((path, null));
        }
        else
        {
            _projections.Push((path, _currentMemberName));
        }

        return node;
    }

    protected override Expression VisitMethodCall(MethodCallExpression node)
    {
        // Handle method calls in projections
        var expression = node.Method.Name switch
        {
            "Count" when node.Object == null => HandleAggregateFunction(node, "count"),
            "Sum" => HandleAggregateFunction(node, "sum"),
            "Average" => HandleAggregateFunction(node, "avg"),
            "Min" => HandleAggregateFunction(node, "min"),
            "Max" => HandleAggregateFunction(node, "max"),
            "ToLower" => HandleStringFunction(node, "toLower"),
            "ToUpper" => HandleStringFunction(node, "toUpper"),
            "ToString" => HandleStringFunction(node, "toString"),
            _ => throw new NotSupportedException($"Method {node.Method.Name} is not supported in SELECT clause")
        };

        _projections.Push((expression, _currentMemberName));
        return node;
    }

    protected override Expression VisitBinary(BinaryExpression node)
    {
        // Handle binary operations in projections like concatenation
        if (node.NodeType == ExpressionType.Add && node.Type == typeof(string))
        {
            Visit(node.Left);
            var left = _projections.Pop().Expression;

            Visit(node.Right);
            var right = _projections.Pop().Expression;

            _projections.Push(($"{left} + {right}", _currentMemberName));
            return node;
        }

        return base.VisitBinary(node);
    }

    protected override Expression VisitConstant(ConstantExpression node)
    {
        if (node.Value == null)
        {
            _projections.Push(("null", _currentMemberName));
        }
        else if (node.Type == typeof(string))
        {
            _projections.Push(($"'{node.Value}'", _currentMemberName));
        }
        else
        {
            _projections.Push((node.Value.ToString()!, _currentMemberName));
        }

        return node;
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

    private string HandleAggregateFunction(MethodCallExpression node, string function)
    {
        if (node.Arguments.Count > 0)
        {
            Visit(node.Arguments[0]);
            var expression = _projections.Pop().Expression;
            return $"{function}({expression})";
        }

        return $"{function}({scope.Alias})";
    }

    private string HandleStringFunction(MethodCallExpression node, string function)
    {
        Visit(node.Object!);
        var target = _projections.Pop().Expression;
        return $"{function}({target})";
    }
}