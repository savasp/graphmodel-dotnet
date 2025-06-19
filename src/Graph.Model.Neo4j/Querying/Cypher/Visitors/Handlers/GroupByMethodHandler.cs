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
/// Handles GroupBy LINQ method by generating appropriate WITH and GROUP BY clauses.
/// </summary>
internal record GroupByMethodHandler : MethodHandlerBase
{
    public override bool Handle(CypherQueryContext context, MethodCallExpression node, Expression result)
    {
        if (node.Method.Name != "GroupBy" || node.Arguments.Count < 2)
        {
            return false;
        }

        // Get the key selector (lambda expression)
        if (node.Arguments[1] is not UnaryExpression { Operand: LambdaExpression keySelector })
        {
            throw new GraphException("GroupBy method requires a lambda expression key selector");
        }

        // Create expression visitor chain to process the key selector
        var expressionVisitor = CreateExpressionVisitor(context);

        // Process the lambda body to generate the grouping key expression
        var keyExpression = expressionVisitor.Visit(keySelector.Body);

        // In Neo4j, grouping is typically done with aggregate functions in RETURN
        // Add the grouping key to WITH clause first, then to RETURN
        var currentAlias = context.Scope.CurrentAlias ?? "n";

        // Handle element selector if present (3-argument form)
        if (node.Arguments.Count >= 3 && node.Arguments[2] is UnaryExpression { Operand: LambdaExpression elementSelector })
        {
            var elementExpression = expressionVisitor.Visit(elementSelector.Body);

            // For now, collect elements into a list
            context.Builder.AddReturn($"{keyExpression} AS key, collect({elementExpression}) AS elements");
        }
        else
        {
            // Simple grouping - collect the entire nodes
            context.Builder.AddReturn($"{keyExpression} AS key, collect({currentAlias}) AS elements");
        }

        return true;
    }

    private static ICypherExpressionVisitor CreateExpressionVisitor(CypherQueryContext context)
    {
        return new ExpressionVisitorChainFactory(context).CreateGroupByChain();
    }
}
