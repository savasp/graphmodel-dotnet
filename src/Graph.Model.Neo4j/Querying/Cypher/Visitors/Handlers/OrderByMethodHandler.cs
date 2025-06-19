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
/// Handles OrderBy and OrderByDescending LINQ methods by generating appropriate ORDER BY clauses.
/// </summary>
internal record OrderByMethodHandler : MethodHandlerBase
{
    public override bool Handle(CypherQueryContext context, MethodCallExpression node, Expression result)
    {
        var methodName = node.Method.Name;

        if (methodName is not ("OrderBy" or "OrderByDescending") || node.Arguments.Count != 2)
        {
            return false;
        }

        // Get the key selector (lambda expression)
        if (node.Arguments[1] is not UnaryExpression { Operand: LambdaExpression lambda })
        {
            throw new GraphException($"{methodName} method requires a lambda expression key selector");
        }

        // Create expression visitor chain to process the key selector
        var expressionVisitor = new ExpressionVisitorChainFactory(context).CreateOrderByChain();

        // Process the lambda body to generate the ORDER BY expression
        var orderExpression = expressionVisitor.Visit(lambda.Body);

        // Determine sort direction
        var direction = methodName == "OrderByDescending" ? "DESC" : "ASC";

        // Add the ORDER BY clause to the query builder
        context.Builder.AddOrderBy($"{orderExpression} {direction}");

        return true;
    }
}
