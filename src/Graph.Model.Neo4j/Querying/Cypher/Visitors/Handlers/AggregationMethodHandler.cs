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

        // Map async marker methods to their base operations
        var baseOperation = GetBaseOperation(methodName);
        if (baseOperation == null)
            return false;

        // For operations that return individual nodes, enable complex property loading
        var nodeReturningOperations = new[] { "First", "FirstOrDefault", "Single", "SingleOrDefault", "ElementAt", "ElementAtOrDefault" };
        if (nodeReturningOperations.Contains(baseOperation) && context.Builder.NeedsComplexProperties(result.Type))
        {
            context.Builder.EnableComplexPropertyLoading();
        }

        return baseOperation switch
        {
            "Count" => HandleCount(context, node),
            "Any" => HandleAny(context, node),
            "All" => HandleAll(context, node),
            "First" => HandleFirst(context, node, false),
            "FirstOrDefault" => HandleFirst(context, node, true),
            "Single" => HandleSingle(context, node, false),
            "SingleOrDefault" => HandleSingle(context, node, true),
            "Sum" => HandleSum(context, node),
            "Average" => HandleAverage(context, node),
            "Min" => HandleMin(context, node),
            "Max" => HandleMax(context, node),
            "Contains" => HandleContains(context, node),
            "ElementAt" => HandleElementAt(context, node, false),
            "ElementAtOrDefault" => HandleElementAt(context, node, true),
            _ => false
        };
    }

    private static string? GetBaseOperation(string methodName)
    {
        // Remove "AsyncMarker" suffix to get the base operation
        if (methodName.EndsWith("AsyncMarker"))
        {
            var baseName = methodName[..^11]; // Remove "AsyncMarker"
            return baseName;
        }
        return null;
    }


    private static ICypherExpressionVisitor CreateExpressionVisitor(CypherQueryContext context)
    {
        return new ExpressionVisitorChainFactory(context).CreateStandardChain();
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

        var alias = context.Scope.CurrentAlias
            ?? throw new InvalidOperationException("No current alias set when handling First/FirstOrDefault");
        if (!context.Builder.HasReturnClause)
        {
            context.Builder.AddReturn(alias);
        }

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

        // Use LIMIT 2 so we can detect if there are multiple results
        context.Builder.AddLimit(2);

        var alias = context.Scope.CurrentAlias
            ?? throw new InvalidOperationException("No current alias set when handling Single/SingleOrDefault");
        if (!context.Builder.HasReturnClause)
        {
            context.Builder.AddReturn(alias);
        }

        return true;
    }

    private static bool HandleCount(CypherQueryContext context, MethodCallExpression node)
    {
        var alias = context.Scope.CurrentAlias
            ?? throw new InvalidOperationException("No current alias set when handling Count()");

        // Disable complex property loading for scalar results
        context.Builder.DisableComplexPropertyLoading();

        if (node.Arguments.Count == 1)
        {
            // Simple count
            context.Builder.AddReturn($"COUNT({alias})");
        }
        else if (node.Arguments.Count == 2 && node.Arguments[1] is UnaryExpression { Operand: LambdaExpression lambda })
        {
            // Count with predicate
            var expressionVisitor = CreateExpressionVisitor(context);
            var condition = expressionVisitor.Visit(lambda.Body);
            context.Builder.AddReturn($"COUNT(CASE WHEN {condition} THEN {alias} END)");
        }
        else
        {
            return false;
        }

        return true;
    }

    private static bool HandleAny(CypherQueryContext context, MethodCallExpression node)
    {
        var alias = context.Scope.CurrentAlias
            ?? throw new InvalidOperationException("No current alias set when handling Any()");

        // Disable complex property loading for scalar results
        context.Builder.DisableComplexPropertyLoading();

        if (node.Arguments.Count == 1)
        {
            // Simple existence check
            context.Builder.AddReturn($"COUNT({alias}) > 0 AS result");
        }
        else if (node.Arguments.Count == 2 && node.Arguments[1] is UnaryExpression { Operand: LambdaExpression lambda })
        {
            // Any with predicate
            var expressionVisitor = CreateExpressionVisitor(context);
            var condition = expressionVisitor.Visit(lambda.Body);
            context.Builder.AddReturn($"COUNT(CASE WHEN {condition} THEN {alias} END) > 0 AS result");
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

        // Disable complex property loading for scalar results
        context.Builder.DisableComplexPropertyLoading();

        if (node.Arguments[1] is UnaryExpression { Operand: LambdaExpression lambda })
        {
            var expressionVisitor = CreateExpressionVisitor(context);
            var condition = expressionVisitor.Visit(lambda.Body);

            var alias = context.Scope.CurrentAlias
                ?? throw new InvalidOperationException("No current alias set when handling All()");

            // All is true when count of non-matching items is 0
            context.Builder.AddReturn($"COUNT(CASE WHEN NOT ({condition}) THEN {alias} END) = 0 AS result");
        }

        return true;
    }

    private static bool HandleSum(CypherQueryContext context, MethodCallExpression node)
    {
        var alias = context.Scope.CurrentAlias
            ?? throw new InvalidOperationException("No current alias set when handling Sum()");

        // Disable complex property loading for scalar results
        context.Builder.DisableComplexPropertyLoading();

        if (node.Arguments.Count == 1)
        {
            // Direct sum on numeric queryable
            context.Builder.AddReturn($"SUM({alias})");
        }
        else if (node.Arguments.Count == 2 && node.Arguments[1] is UnaryExpression { Operand: LambdaExpression lambda })
        {
            // Sum with selector
            var expressionVisitor = CreateExpressionVisitor(context);
            var selector = expressionVisitor.Visit(lambda.Body);
            context.Builder.AddReturn($"SUM({selector})");
        }
        else
        {
            return false;
        }

        return true;
    }

    private static bool HandleAverage(CypherQueryContext context, MethodCallExpression node)
    {
        var alias = context.Scope.CurrentAlias
            ?? throw new InvalidOperationException("No current alias set when handling Average()");

        // Disable complex property loading for scalar results
        context.Builder.DisableComplexPropertyLoading();

        if (node.Arguments.Count == 1)
        {
            // Direct average on numeric queryable
            context.Builder.AddReturn($"AVG(toFloat({alias}))"); // Neo4j requires toFloat for average
        }
        else if (node.Arguments.Count == 2 && node.Arguments[1] is UnaryExpression { Operand: LambdaExpression lambda })
        {
            // Average with selector
            var expressionVisitor = CreateExpressionVisitor(context);
            var selector = expressionVisitor.Visit(lambda.Body);
            context.Builder.AddReturn($"AVG(toFloat({selector}))");
        }
        else
        {
            return false;
        }

        return true;
    }

    private static bool HandleMin(CypherQueryContext context, MethodCallExpression node)
    {
        var alias = context.Scope.CurrentAlias
            ?? throw new InvalidOperationException("No current alias set when handling Min()");

        // Disable complex property loading for scalar results
        context.Builder.DisableComplexPropertyLoading();

        if (node.Arguments.Count == 1)
        {
            // Direct min on comparable queryable
            context.Builder.AddReturn($"MIN({alias})");
        }
        else if (node.Arguments.Count == 2 && node.Arguments[1] is UnaryExpression { Operand: LambdaExpression lambda })
        {
            // Min with selector
            var expressionVisitor = CreateExpressionVisitor(context);
            var selector = expressionVisitor.Visit(lambda.Body);
            context.Builder.AddReturn($"MIN({selector})");
        }
        else
        {
            return false;
        }

        return true;
    }

    private static bool HandleMax(CypherQueryContext context, MethodCallExpression node)
    {
        var alias = context.Scope.CurrentAlias
            ?? throw new InvalidOperationException("No current alias set when handling Max()");

        // Disable complex property loading for scalar results
        context.Builder.DisableComplexPropertyLoading();

        if (node.Arguments.Count == 1)
        {
            // Direct max on comparable queryable
            context.Builder.AddReturn($"MAX({alias})");
        }
        else if (node.Arguments.Count == 2 && node.Arguments[1] is UnaryExpression { Operand: LambdaExpression lambda })
        {
            // Max with selector
            var expressionVisitor = CreateExpressionVisitor(context);
            var selector = expressionVisitor.Visit(lambda.Body);
            context.Builder.AddReturn($"MAX({selector})");
        }
        else
        {
            return false;
        }

        return true;
    }

    private static bool HandleContains(CypherQueryContext context, MethodCallExpression node)
    {
        if (node.Arguments.Count != 2)
        {
            return false;
        }

        // Disable complex property loading for scalar results
        context.Builder.DisableComplexPropertyLoading();

        var alias = context.Scope.CurrentAlias
            ?? throw new InvalidOperationException("No current alias set when handling Contains()");

        // Get the item to search for from the second argument
        var itemExpression = node.Arguments[1];
        var expressionVisitor = CreateExpressionVisitor(context);
        var itemValue = expressionVisitor.Visit(itemExpression);

        // Check if any item equals the search value
        context.Builder.AddReturn($"COUNT(CASE WHEN {alias} = {itemValue} THEN {alias} END) > 0 AS result");

        return true;
    }

    private static bool HandleElementAt(CypherQueryContext context, MethodCallExpression node, bool allowDefault)
    {
        if (node.Arguments.Count != 2)
        {
            return false;
        }

        // Get the index from the second argument
        var indexExpression = node.Arguments[1];
        if (indexExpression is not ConstantExpression { Value: int index })
        {
            return false;
        }

        var alias = context.Scope.CurrentAlias
            ?? throw new InvalidOperationException("No current alias set when handling ElementAt()");

        // Use SKIP and LIMIT to get the element at the specific index
        context.Builder.AddSkip(index);
        context.Builder.AddLimit(1);

        if (!context.Builder.HasReturnClause)
        {
            context.Builder.AddReturn(alias);
        }

        return true;
    }
}
