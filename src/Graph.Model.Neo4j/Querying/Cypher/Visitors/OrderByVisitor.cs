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

internal sealed class OrderByVisitor : ExpressionVisitor
{
    private readonly QueryScope _scope;
    private readonly CypherQueryBuilder _builder;
    private readonly List<(string Expression, bool IsDescending)> _orderClauses = [];

    public OrderByVisitor(QueryScope scope, CypherQueryBuilder builder)
    {
        _scope = scope ?? throw new ArgumentNullException(nameof(scope));
        _builder = builder ?? throw new ArgumentNullException(nameof(builder));
    }

    public void VisitOrderBy(LambdaExpression selector, bool isDescending = false, bool isThenBy = false)
    {
        var expression = Visit(selector.Body);
        var cypherExpression = ExpressionToCypher(expression);

        if (isThenBy)
        {
            _orderClauses.Add((cypherExpression, isDescending));
        }
        else
        {
            // Clear any existing order clauses for OrderBy (not ThenBy)
            _orderClauses.Clear();
            _orderClauses.Add((cypherExpression, isDescending));
        }

        // Build the complete ORDER BY clause
        BuildOrderByClause();
    }

    private void BuildOrderByClause()
    {
        foreach (var clause in _orderClauses)
        {
            _builder.AddOrderBy(clause.Expression, clause.IsDescending);
        }
    }

    private string ExpressionToCypher(Expression expression)
    {
        return expression switch
        {
            MemberExpression member => BuildMemberAccess(member),
            MethodCallExpression method => BuildMethodCall(method),
            UnaryExpression unary => ExpressionToCypher(unary.Operand),
            ConstantExpression constant => constant.Value is null ? "null" : _builder.AddParameter(constant.Value),
            _ => throw new NotSupportedException($"Expression type {expression.NodeType} not supported in ORDER BY")
        };
    }

    private string BuildMemberAccess(MemberExpression member)
    {
        var obj = member.Expression switch
        {
            ParameterExpression param => _scope.GetAliasForType(param.Type)
                ?? param.Name
                ?? throw new InvalidOperationException($"No alias found for parameter of type {param.Type.Name}"),
            MemberExpression innerMember => ExpressionToCypher(innerMember),
            _ => ExpressionToCypher(member.Expression!)
        };

        return $"{obj}.{member.Member.Name}";
    }

    private string BuildMethodCall(MethodCallExpression method)
    {
        // Handle common string methods used in ordering
        return method.Method.Name switch
        {
            "ToUpper" => $"toUpper({ExpressionToCypher(method.Object!)})",
            "ToLower" => $"toLower({ExpressionToCypher(method.Object!)})",
            "Trim" => $"trim({ExpressionToCypher(method.Object!)})",
            _ => throw new NotSupportedException($"Method {method.Method.Name} not supported in ORDER BY")
        };
    }
}