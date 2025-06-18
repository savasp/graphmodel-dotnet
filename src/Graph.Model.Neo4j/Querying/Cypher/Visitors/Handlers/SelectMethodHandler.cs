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
/// Handles the Select LINQ method by generating appropriate RETURN clauses.
/// </summary>
internal record SelectMethodHandler : MethodHandlerBase
{
    public override bool Handle(CypherQueryContext context, MethodCallExpression node, Expression result)
    {
        if (node.Method.Name != "Select" || node.Arguments.Count != 2)
        {
            return false;
        }

        // Get the selector (lambda expression)
        if (node.Arguments[1] is not UnaryExpression { Operand: LambdaExpression lambda })
        {
            throw new GraphException("Select method requires a lambda expression selector");
        }

        // Create expression visitor chain to process the selector
        var expressionVisitor = new CollectionMethodVisitor(
            context,
            new StringMethodVisitor(
                context,
                new BinaryExpressionVisitor(
                    context,
                    new BaseExpressionVisitor(context))));

        // Process different types of selections
        switch (lambda.Body)
        {
            case ParameterExpression parameter:
                // Simple identity selection: x => x
                var alias = context.Scope.CurrentAlias ?? "n";
                context.Builder.AddReturn(alias);
                break;

            case MemberExpression member:
                // Property selection: x => x.Property
                var memberExpression = expressionVisitor.Visit(member);
                context.Builder.AddReturn(memberExpression);
                break;

            case NewExpression newExpression:
                // Anonymous type projection: x => new { x.Prop1, x.Prop2 }
                HandleAnonymousTypeProjection(context, newExpression, expressionVisitor);
                break;

            default:
                // General expression
                var selectExpression = expressionVisitor.Visit(lambda.Body);
                context.Builder.AddReturn(selectExpression);
                break;
        }

        return true;
    }

    private static void HandleAnonymousTypeProjection(
        CypherQueryContext context,
        NewExpression newExpression,
        ICypherExpressionVisitor expressionVisitor)
    {
        if (newExpression.Arguments.Count == 0)
        {
            return;
        }

        for (var i = 0; i < newExpression.Arguments.Count; i++)
        {
            var argument = newExpression.Arguments[i];
            var memberName = newExpression.Members?[i]?.Name ?? $"Item{i}";

            var expression = expressionVisitor.Visit(argument);
            context.Builder.AddReturn($"{expression} AS {memberName}");
        }
    }
}
