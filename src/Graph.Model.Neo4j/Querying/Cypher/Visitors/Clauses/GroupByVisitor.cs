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

internal sealed class GroupByVisitor(CypherQueryContext context) : ClauseVisitorBase<GroupByVisitor>(context)
{
    public void VisitGroupBy(LambdaExpression keySelector, LambdaExpression? elementSelector = null)
    {
        // Visit the key selector
        var keyExpression = Visit(keySelector.Body);
        var cypherKey = ExpressionToCypher(keyExpression);

        // Add the GROUP BY clause
        Builder.AddGroupBy(cypherKey);

        // If we have an element selector, we need to handle the projection
        if (elementSelector != null)
        {
            var elementExpression = Visit(elementSelector.Body);
            var cypherElement = ExpressionToCypher(elementExpression);
            Builder.AddReturn($"{cypherKey} AS key, COLLECT({cypherElement}) AS elements");
        }
        else
        {
            // Default grouping - return key and all matching nodes/relationships
            var currentAlias = Scope.CurrentAlias
                ?? throw new InvalidOperationException("No current alias set when building GROUP BY clause");
            Builder.AddReturn($"{cypherKey} AS key, COLLECT({currentAlias}) AS elements");
        }
    }

    private string ExpressionToCypher(Expression expression)
    {
        return expression switch
        {
            MemberExpression member => BuildMemberAccess(member),
            ConstantExpression constant => BuildConstant(constant),
            BinaryExpression binary => BuildBinary(binary),
            UnaryExpression unary => BuildUnary(unary),
            _ => throw new NotSupportedException($"Expression type {expression.NodeType} not supported in GROUP BY")
        };
    }

    private string BuildMemberAccess(MemberExpression member)
    {
        var obj = member.Expression switch
        {
            ParameterExpression param => Scope.GetAliasForType(param.Type)
                ?? param.Name
                ?? throw new InvalidOperationException($"No alias found for parameter of type {param.Type.Name}"),
            MemberExpression innerMember => ExpressionToCypher(innerMember),
            _ => ExpressionToCypher(member.Expression!)
        };

        return $"{obj}.{member.Member.Name}";
    }

    private string BuildConstant(ConstantExpression constant)
    {
        return constant.Value is null ? "null" : Builder.AddParameter(constant.Value);
    }

    private string BuildBinary(BinaryExpression binary)
    {
        var left = ExpressionToCypher(binary.Left);
        var right = ExpressionToCypher(binary.Right);

        var op = binary.NodeType switch
        {
            ExpressionType.Add => "+",
            ExpressionType.Subtract => "-",
            ExpressionType.Multiply => "*",
            ExpressionType.Divide => "/",
            ExpressionType.Modulo => "%",
            _ => throw new NotSupportedException($"Binary operator {binary.NodeType} not supported in GROUP BY")
        };

        return $"({left} {op} {right})";
    }

    private string BuildUnary(UnaryExpression unary)
    {
        var operand = ExpressionToCypher(unary.Operand);

        return unary.NodeType switch
        {
            ExpressionType.Negate => $"-{operand}",
            ExpressionType.Not => $"NOT {operand}",
            _ => operand
        };
    }
}