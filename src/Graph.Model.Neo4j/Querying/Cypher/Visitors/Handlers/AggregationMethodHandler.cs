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
/// Handles aggregation LINQ methods like Count, Any, All, etc.
/// </summary>
internal record AggregationMethodHandler : MethodHandlerBase
{
    public override bool Handle(CypherQueryContext context, MethodCallExpression node, Expression result)
    {
        var methodName = node.Method.Name;

        return methodName switch
        {
            "Count" => HandleCount(context, node),
            "Any" => HandleAny(context, node),
            "All" => HandleAll(context, node),
            "First" => HandleFirst(context, node, false),
            "FirstOrDefault" => HandleFirst(context, node, true),
            "Single" => HandleSingle(context, node, false),
            "SingleOrDefault" => HandleSingle(context, node, true),
            _ => false
        };
    }

    private static bool HandleCount(CypherQueryContext context, MethodCallExpression node)
    {
        // Ensure we have a current alias set up - this should have been done by visiting the source
        var alias = context.Scope.CurrentAlias;
        if (alias == null)
        {
            // If no alias is set, we need to set up the query structure ourselves
            // Extract the element type from the source queryable
            var sourceArg = node.Arguments[0];
            Type? elementType = null;

            // Try to get the element type from the source expression type
            if (sourceArg.Type.IsGenericType)
            {
                var genericArgs = sourceArg.Type.GetGenericArguments();
                if (genericArgs.Length > 0)
                {
                    elementType = genericArgs[0];
                }
            }

            if (elementType != null)
            {
                // Set up the match clause for the element type
                var label = Labels.GetLabelFromType(elementType);
                context.Builder.AddMatch("n", label);
                context.Scope.CurrentAlias = "n";
                alias = "n";
            }
            else
            {
                throw new InvalidOperationException("Could not determine element type for Count method.");
            }
        }

        if (node.Arguments.Count == 1)
        {
            // Simple count
            context.Builder.AddReturn($"COUNT({alias})");
        }
        else if (node.Arguments.Count == 2)
        {
            // Count with predicate
            if (node.Arguments[1] is UnaryExpression { Operand: LambdaExpression lambda })
            {
                var expressionVisitor = CreateExpressionVisitor(context);
                var condition = expressionVisitor.Visit(lambda.Body);

                // Use CASE for conditional counting
                context.Builder.AddReturn($"COUNT(CASE WHEN {condition} THEN {alias} END)");
            }
        }
        else
        {
            return false;
        }

        return true;
    }

    private static bool HandleAny(CypherQueryContext context, MethodCallExpression node)
    {
        // Ensure we have a current alias set up - this should have been done by visiting the source
        var alias = context.Scope.CurrentAlias;
        if (alias == null)
        {
            // If no alias is set, we need to set up the query structure ourselves
            // Extract the element type from the source queryable
            var sourceArg = node.Arguments[0];
            Type? elementType = null;

            // Try to get the element type from the source expression type
            if (sourceArg.Type.IsGenericType)
            {
                var genericArgs = sourceArg.Type.GetGenericArguments();
                if (genericArgs.Length > 0)
                {
                    elementType = genericArgs[0];
                }
            }

            if (elementType != null)
            {
                // Set up the match clause for the element type
                var label = Labels.GetLabelFromType(elementType);
                context.Builder.AddMatch("n", label);
                context.Scope.CurrentAlias = "n";
                alias = "n";
            }
            else
            {
                throw new InvalidOperationException("Could not determine element type for Any method.");
            }
        }

        if (node.Arguments.Count == 1)
        {
            // Simple existence check
            context.Builder.AddReturn($"COUNT({alias}) > 0 AS result");
        }
        else if (node.Arguments.Count == 2)
        {
            // Any with predicate
            if (node.Arguments[1] is UnaryExpression { Operand: LambdaExpression lambda })
            {
                var expressionVisitor = CreateExpressionVisitor(context);
                var condition = expressionVisitor.Visit(lambda.Body);

                context.Builder.AddReturn($"COUNT(CASE WHEN {condition} THEN {alias} END) > 0 AS result");
            }
        }
        else
        {
            return false;
        }

        return true;
    }

    private static bool HandleAll(CypherQueryContext context, MethodCallExpression node)
    {
        if (node.Arguments.Count != 2)
        {
            return false;
        }

        if (node.Arguments[1] is UnaryExpression { Operand: LambdaExpression lambda })
        {
            var expressionVisitor = CreateExpressionVisitor(context);
            var condition = expressionVisitor.Visit(lambda.Body);

            var alias = context.Scope.CurrentAlias ?? "n";
            // All is true when count of non-matching items is 0
            context.Builder.AddReturn($"COUNT(CASE WHEN NOT ({condition}) THEN {alias} END) = 0");
        }

        return true;
    }

    private static bool HandleFirst(CypherQueryContext context, MethodCallExpression node, bool allowDefault)
    {
        if (node.Arguments.Count == 2 && node.Arguments[1] is UnaryExpression { Operand: LambdaExpression lambda })
        {
            // First with predicate
            var expressionVisitor = CreateExpressionVisitor(context);
            var condition = expressionVisitor.Visit(lambda.Body);
            context.Builder.AddWhere(condition);
        }

        // Add LIMIT 1 for First/FirstOrDefault
        context.Builder.AddLimit(1);

        // For FirstOrDefault, we should return the item or null
        var alias = context.Scope.CurrentAlias ?? "n";
        context.Builder.AddReturn(alias);

        return true;
    }

    private static bool HandleSingle(CypherQueryContext context, MethodCallExpression node, bool allowDefault)
    {
        if (node.Arguments.Count == 2 && node.Arguments[1] is UnaryExpression { Operand: LambdaExpression lambda })
        {
            // Single with predicate
            var expressionVisitor = CreateExpressionVisitor(context);
            var condition = expressionVisitor.Visit(lambda.Body);
            context.Builder.AddWhere(condition);
        }

        // For Single/SingleOrDefault, we expect exactly one result
        // In Neo4j, we can use LIMIT 2 and check the count in application code
        context.Builder.AddLimit(2);

        var alias = context.Scope.CurrentAlias ?? "n";
        context.Builder.AddReturn(alias);

        return true;
    }

    private static ICypherExpressionVisitor CreateExpressionVisitor(CypherQueryContext context)
    {
        return new ExpressionVisitorChainFactory(context).CreateStandardChain();
    }
}
