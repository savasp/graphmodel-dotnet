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

namespace Cvoya.Graph.Model.Neo4j.Querying.Cypher.Visitors.Handlers;

using System.Linq.Expressions;
using Cvoya.Graph.Model.Neo4j.Querying.Cypher.Visitors.Core;
using Cvoya.Graph.Model.Neo4j.Querying.Cypher.Visitors.Expressions;

/// <summary>
/// Handles Take and Skip LINQ methods by generating appropriate LIMIT and SKIP clauses.
/// </summary>
internal record LimitMethodHandler : MethodHandlerBase
{
    public override bool Handle(CypherQueryContext context, MethodCallExpression node, Expression result)
    {
        var methodName = node.Method.Name;

        if (methodName is not ("Take" or "Skip") || node.Arguments.Count != 2)
        {
            return false;
        }

        // Get the count argument
        var countArgument = node.Arguments[1];

        // Extract the count value
        var count = ExtractConstantValue(countArgument);

        switch (methodName)
        {
            case "Take":
                context.Builder.AddLimit(count);
                break;
            case "Skip":
                context.Builder.AddSkip(count);
                break;
        }

        return true;
    }

    private static int ExtractConstantValue(Expression expression)
    {
        return expression switch
        {
            ConstantExpression constant when constant.Value is int intValue => intValue,
            ConstantExpression constant when constant.Value is long longValue => (int)longValue,
            UnaryExpression { NodeType: ExpressionType.Convert, Operand: ConstantExpression innerConstant }
                when innerConstant.Value is int innerIntValue => innerIntValue,
            UnaryExpression { NodeType: ExpressionType.Convert, Operand: ConstantExpression innerConstant }
                when innerConstant.Value is long innerLongValue => (int)innerLongValue,
            _ => throw new GraphException($"Take/Skip requires a constant integer value, got {expression.NodeType}")
        };
    }
}
