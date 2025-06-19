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
/// Handles Join LINQ method by generating appropriate MATCH clauses with conditions.
/// </summary>
internal record JoinMethodHandler : MethodHandlerBase
{
    public override bool Handle(CypherQueryContext context, MethodCallExpression node, Expression result)
    {
        if (node.Method.Name != "Join" || node.Arguments.Count != 5)
        {
            return false;
        }

        // Arguments: outer, inner, outerKeySelector, innerKeySelector, resultSelector
        var outerKeySelector = node.Arguments[2];
        var innerKeySelector = node.Arguments[3];
        var resultSelector = node.Arguments[4];

        if (outerKeySelector is not UnaryExpression { Operand: LambdaExpression outerLambda } ||
            innerKeySelector is not UnaryExpression { Operand: LambdaExpression innerLambda } ||
            resultSelector is not UnaryExpression { Operand: LambdaExpression resultLambda })
        {
            throw new GraphException("Join method requires lambda expressions for key selectors and result selector");
        }

        var expressionVisitor = CreateExpressionVisitor(context);

        // Process the key selectors
        var outerKey = expressionVisitor.Visit(outerLambda.Body);
        var innerKey = expressionVisitor.Visit(innerLambda.Body);

        // Create aliases for the joined entities
        var outerAlias = context.Scope.CurrentAlias ?? "n";
        var innerAlias = context.Scope.GetOrCreateAlias(typeof(object), "m");

        // Add a MATCH clause with the join condition
        context.Builder.AddMatch($"({outerAlias}), ({innerAlias})");
        context.Builder.AddWhere($"{outerKey} = {innerKey}");

        // Handle the result selector
        var resultExpression = expressionVisitor.Visit(resultLambda.Body);
        context.Builder.AddReturn(resultExpression);

        return true;
    }

    private static ICypherExpressionVisitor CreateExpressionVisitor(CypherQueryContext context)
    {
        return new ExpressionVisitorChainFactory(context).CreateStandardChain();
    }
}
