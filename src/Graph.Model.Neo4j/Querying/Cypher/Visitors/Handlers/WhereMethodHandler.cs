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
/// Handles the Where LINQ method by generating appropriate WHERE clauses.
/// </summary>
internal record WhereMethodHandler : MethodHandlerBase
{
    public override bool Handle(CypherQueryContext context, MethodCallExpression node, Expression result)
    {
        if (node.Method.Name != "Where" || node.Arguments.Count != 2)
        {
            return false;
        }

        // Get the predicate (lambda expression)
        if (node.Arguments[1] is not UnaryExpression { Operand: LambdaExpression lambda })
        {
            throw new GraphException("Where method requires a lambda expression predicate");
        }

        // Create expression visitor chain to process the predicate
        var expressionVisitor = new CollectionMethodVisitor(
            context,
            new StringMethodVisitor(
                context,
                new BinaryExpressionVisitor(
                    context,
                    new BaseExpressionVisitor(context))));

        // Process the lambda body to generate the WHERE expression
        var whereExpression = expressionVisitor.Visit(lambda.Body);

        // Add the WHERE clause to the query builder
        context.Builder.AddWhere(whereExpression);

        return true;
    }
}
