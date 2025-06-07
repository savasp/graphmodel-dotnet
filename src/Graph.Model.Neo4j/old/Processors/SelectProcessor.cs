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

using System.Linq.Expressions;
using Cvoya.Graph.Model;

namespace Cvoya.Graph.Provider.Neo4j.Linq.Processors;

/// <summary>
/// Processes LINQ Select clauses by building appropriate Cypher projections
/// </summary>
internal class SelectProcessor
{
    public static void ProcessSelect(LambdaExpression selector, CypherBuildContext context)
    {
        // Clear IsPathResult since we're now projecting to a different shape
        context.IsPathResult = false;

        // Handle GroupBy + Select combinations
        if (context.IsGroupByQuery)
        {
            ProcessGroupBySelect(selector, context);
            return;
        }

        // For simple projections, build the Cypher expression
        var projection = CypherExpressionBuilder.BuildCypherExpression(selector.Body, context.CurrentAlias, context);
        context.Return = projection;
    }

    private static void ProcessGroupBySelect(LambdaExpression selector, CypherBuildContext context)
    {
        if (context.IsTraversalPathGroupBy)
        {
            // Special handling for TraversalPath grouping
            ProcessTraversalPathGroupBySelect(selector, context);
            return;
        }

        // For GroupBy + Select, we need to build WITH and aggregation clauses
        // e.g., .GroupBy(p => p.FirstName).Select(g => new { Name = g.Key, Count = g.Count() })
        // should generate: WITH p.FirstName as name, COUNT(p) as count RETURN { Name: name, Count: count }

        if (selector.Body is NewExpression newExpr)
        {
            // Handle anonymous type projections like new { Name = g.Key, Count = g.Count() }
            var withClauseItems = new List<string>();
            var returnProjections = new List<string>();

            for (int i = 0; i < newExpr.Arguments.Count; i++)
            {
                var argument = newExpr.Arguments[i];
                var memberName = newExpr.Members?[i].Name ?? $"Field{i}";

                if (IsGroupingKeyAccess(argument))
                {
                    // g.Key -> use the GroupByKey
                    var keyAlias = $"key_{i}";
                    withClauseItems.Add($"{context.GroupByKey} as {keyAlias}");
                    returnProjections.Add($"{memberName}: {keyAlias}");
                }
                else if (IsGroupingAggregation(argument, out var aggregationType))
                {
                    // g.Count(), g.Sum(x => x.Property), etc.
                    var aggAlias = $"agg_{i}";
                    var aggregation = BuildGroupingAggregation(argument, aggregationType, context);
                    withClauseItems.Add($"{aggregation} as {aggAlias}");
                    returnProjections.Add($"{memberName}: {aggAlias}");
                }
                else
                {
                    // Fallback: try to build a general expression
                    var expr = CypherExpressionBuilder.BuildCypherExpression(argument, context.CurrentAlias, context);
                    returnProjections.Add($"{memberName}: {expr}");
                }
            }

            // Build the WITH clause
            if (withClauseItems.Count > 0)
            {
                context.With = "WITH " + string.Join(", ", withClauseItems);
            }

            // Build the RETURN clause
            context.Return = "{ " + string.Join(", ", returnProjections) + " }";
        }
        else
        {
            // Single expression select
            var projection = CypherExpressionBuilder.BuildCypherExpression(selector.Body, context.CurrentAlias, context);
            context.Return = projection;
        }
    }

    private static void ProcessTraversalPathGroupBySelect(LambdaExpression selector, CypherBuildContext context)
    {
        // Clear IsPathResult since we're projecting to a map result
        context.IsPathResult = false;

        if (selector.Body is NewExpression newExpr)
        {
            var returnProjections = new List<string>();

            for (int i = 0; i < newExpr.Arguments.Count; i++)
            {
                var argument = newExpr.Arguments[i];
                var memberName = newExpr.Members?[i].Name ?? $"Field{i}";

                bool handled = false;

                if (IsGroupingKeyAccess(argument))
                {
                    // g.Key -> the grouped node
                    returnProjections.Add($"{memberName}: {context.GroupByKey ?? "n"}");
                    handled = true;
                }
                else if (argument is MemberExpression me && me.Expression is MemberExpression keyAccess &&
                         keyAccess.Member.Name == "Key")
                {
                    // g.Key.SomeProperty -> access property of the grouped key
                    var propertyName = me.Member.Name;
                    returnProjections.Add($"{memberName}: {context.GroupByKey ?? "n"}.{propertyName}");
                    handled = true;
                }
                else if (IsGroupingAggregation(argument, out var aggregationType))
                {
                    switch (aggregationType)
                    {
                        case "Count":
                            returnProjections.Add($"{memberName}: size(paths)");
                            handled = true;
                            break;

                        case "Average":
                            // Handle g.Average(k => k.Target.Age)
                            if (argument is MethodCallExpression avgCall && avgCall.Arguments.Count > 0)
                            {
                                var avgLambda = ExtractLambdaFromQuote(avgCall.Arguments.Last());
                                if (avgLambda != null)
                                {
                                    var listExpr = BuildTraversalPathListComprehensionValue(avgLambda, context);
                                    returnProjections.Add($"{memberName}: reduce(sum = 0.0, item IN {listExpr} | sum + item) / size({listExpr})");
                                    handled = true;
                                }
                            }
                            break;

                        case "Sum":
                            // Handle g.Sum(k => k.Target.Age)
                            if (argument is MethodCallExpression sumCall && sumCall.Arguments.Count > 0)
                            {
                                var sumLambda = ExtractLambdaFromQuote(sumCall.Arguments.Last());
                                if (sumLambda != null)
                                {
                                    var listExpr = BuildTraversalPathListComprehensionValue(sumLambda, context);
                                    returnProjections.Add($"{memberName}: reduce(sum = 0, item IN {listExpr} | sum + item)");
                                    handled = true;
                                }
                            }
                            break;

                        case "Max":
                        case "Min":
                            // Handle g.Max(k => k.Target.Age) or g.Min(k => k.Target.Age)
                            if (argument is MethodCallExpression minMaxCall && minMaxCall.Arguments.Count > 0)
                            {
                                var minMaxLambda = ExtractLambdaFromQuote(minMaxCall.Arguments.Last());
                                if (minMaxLambda != null)
                                {
                                    var listExpr = BuildTraversalPathListComprehensionValue(minMaxLambda, context);
                                    var func = aggregationType.ToLowerInvariant();
                                    returnProjections.Add($"{memberName}: {func}({listExpr})");
                                    handled = true;
                                }
                            }
                            break;

                        case "Select":
                            // Handle g.Select(k => new { ... })
                            if (argument is MethodCallExpression methodCall &&
                                methodCall.Arguments[0] is MethodCallExpression selectCall &&
                                selectCall.Method.Name == "Select")
                            {
                                // For extension method Select, it's a static method with 2 args
                                LambdaExpression? selectLambda = null;
                                bool isGroupingSource = false;

                                if (selectCall.Method.IsStatic && selectCall.Arguments.Count == 2)
                                {
                                    // Static extension method: Enumerable.Select(source, lambda)
                                    var sourceType = selectCall.Arguments[0].Type;
                                    isGroupingSource = sourceType.IsGenericType &&
                                                     sourceType.GetGenericTypeDefinition() == typeof(IGrouping<,>);
                                    selectLambda = ExtractLambdaFromQuote(selectCall.Arguments[1]);
                                }
                                else if (!selectCall.Method.IsStatic && selectCall.Arguments.Count == 1)
                                {
                                    // Instance method: source.Select(lambda)
                                    var sourceType = selectCall.Object?.Type;
                                    isGroupingSource = sourceType?.IsGenericType == true &&
                                                     sourceType.GetGenericTypeDefinition() == typeof(IGrouping<,>);
                                    selectLambda = ExtractLambdaFromQuote(selectCall.Arguments[0]);
                                }

                                if (isGroupingSource && selectLambda != null)
                                {
                                    var listExpr = BuildTraversalPathListComprehension(selectLambda, context);
                                    returnProjections.Add($"{memberName}: {listExpr}");
                                    handled = true;
                                }
                            }
                            break;
                    }
                }

                // Handle ToList() calls on filtered/projected group data
                if (!handled && argument is MethodCallExpression toListCall && toListCall.Method.Name == "ToList")
                {
                    // This handles patterns like: group.Where(...).Select(...).ToList()
                    if (toListCall.Object != null || (toListCall.Arguments.Count > 0 && IsGroupingChain(toListCall.Arguments[0])))
                    {
                        var sourceExpression = toListCall.Object ?? toListCall.Arguments[0];
                        var listComprehension = BuildGroupingChainListComprehension(sourceExpression, context);
                        returnProjections.Add($"{memberName}: {listComprehension}");
                        handled = true;
                    }
                }

                // Fallback: try to build a general Cypher expression
                if (!handled)
                {
                    try
                    {
                        System.Diagnostics.Debug.WriteLine($"DEBUG: Trying to build expression for {memberName}: {argument} (Type: {argument.GetType().Name})");
                        if (argument is MethodCallExpression debugMc)
                        {
                            System.Diagnostics.Debug.WriteLine($"DEBUG: MethodCall: {debugMc.Method.Name}, DeclaringType: {debugMc.Method.DeclaringType?.Name}");
                        }
                        var cypherExpr = CypherExpressionBuilder.BuildCypherExpression(argument, context.GroupByKey ?? context.CurrentAlias, context);
                        returnProjections.Add($"{memberName}: {cypherExpr}");
                    }
                    catch (Exception ex)
                    {
                        // If we can't build the expression, use a placeholder
                        System.Diagnostics.Debug.WriteLine($"DEBUG: Failed to build expression for {memberName}: {ex.Message}");
                        returnProjections.Add($"{memberName}: null");
                    }
                }
            }

            context.Return = "{ " + string.Join(", ", returnProjections) + " }";
        }
        else
        {
            // Simple expression
            var projection = CypherExpressionBuilder.BuildCypherExpression(selector.Body, context.CurrentAlias, context);
            context.Return = projection;
        }
    }

    private static bool IsGroupingKeyAccess(Expression expression)
    {
        return expression is MemberExpression me && me.Member.Name == "Key";
    }

    private static bool IsGroupingAggregation(Expression expression, out string aggregationType)
    {
        aggregationType = string.Empty;

        if (expression is MethodCallExpression mce)
        {
            aggregationType = mce.Method.Name;
            return aggregationType is "Count" or "Sum" or "Average" or "Max" or "Min" or "Select";
        }

        return false;
    }

    private static string BuildGroupingAggregation(Expression expression, string aggregationType, CypherBuildContext context)
    {
        if (expression is MethodCallExpression methodCall)
        {
            switch (aggregationType)
            {
                case "Count":
                    return $"COUNT({context.CurrentAlias})";
                case "Sum":
                    if (methodCall.Arguments.Count > 0)
                    {
                        var sumSelector = ExtractLambdaFromQuote(methodCall.Arguments[0]);
                        if (sumSelector != null)
                        {
                            var property = CypherExpressionBuilder.BuildCypherExpression(sumSelector.Body, context.CurrentAlias, context);
                            return $"SUM({property})";
                        }
                    }
                    break;
                case "Average":
                    if (methodCall.Arguments.Count > 0)
                    {
                        var avgSelector = ExtractLambdaFromQuote(methodCall.Arguments[0]);
                        if (avgSelector != null)
                        {
                            var property = CypherExpressionBuilder.BuildCypherExpression(avgSelector.Body, context.CurrentAlias, context);
                            return $"AVG({property})";
                        }
                    }
                    break;
                case "Max":
                    if (methodCall.Arguments.Count > 0)
                    {
                        var maxSelector = ExtractLambdaFromQuote(methodCall.Arguments[0]);
                        if (maxSelector != null)
                        {
                            var property = CypherExpressionBuilder.BuildCypherExpression(maxSelector.Body, context.CurrentAlias, context);
                            return $"MAX({property})";
                        }
                    }
                    break;
                case "Min":
                    if (methodCall.Arguments.Count > 0)
                    {
                        var minSelector = ExtractLambdaFromQuote(methodCall.Arguments[0]);
                        if (minSelector != null)
                        {
                            var property = CypherExpressionBuilder.BuildCypherExpression(minSelector.Body, context.CurrentAlias, context);
                            return $"MIN({property})";
                        }
                    }
                    break;
            }
        }

        return $"COUNT({context.CurrentAlias})"; // Fallback
    }

    // Helper methods that delegate to the main builder for now
    private static LambdaExpression? ExtractLambdaFromQuote(Expression expression)
    {
        // This will be moved to a shared utility class
        if (expression is UnaryExpression { NodeType: ExpressionType.Quote } ue && ue.Operand is LambdaExpression lambda)
        {
            return lambda;
        }
        return expression as LambdaExpression;
    }

    private static string BuildTraversalPathListComprehensionValue(LambdaExpression lambda, CypherBuildContext context)
    {
        var lambdaParam = lambda.Parameters.FirstOrDefault()?.Name;
        if (string.IsNullOrEmpty(lambdaParam))
        {
            throw new InvalidOperationException("Lambda expression must have at least one parameter");
        }

        var listComprehensionVar = "p";
        var originalAlias = context.CurrentAlias;

        var projectionBody = BuildCypherExpressionWithVariableMapping(lambda.Body, lambdaParam, listComprehensionVar, context);
        context.CurrentAlias = originalAlias;

        return $"[{listComprehensionVar} IN paths | {projectionBody}]";
    }

    private static string BuildTraversalPathListComprehension(LambdaExpression lambda, CypherBuildContext context)
    {
        var lambdaParam = lambda.Parameters.FirstOrDefault()?.Name;
        if (string.IsNullOrEmpty(lambdaParam))
        {
            throw new InvalidOperationException("Lambda expression must have at least one parameter");
        }

        var listComprehensionVar = "p";
        var originalAlias = context.CurrentAlias;

        var projectionBody = BuildCypherExpressionWithVariableMapping(lambda.Body, lambdaParam, listComprehensionVar, context);
        context.CurrentAlias = originalAlias;

        return $"[{listComprehensionVar} IN paths | {projectionBody}]";
    }

    private static string BuildCypherExpressionWithVariableMapping(Expression expression, string fromParam, string toVar, CypherBuildContext context)
    {
        // This is a simplified version - the full implementation would be in the main builder
        switch (expression)
        {
            case NewExpression newExpr:
                var members = new List<string>();
                for (int i = 0; i < newExpr.Arguments.Count; i++)
                {
                    var memberName = newExpr.Members?[i]?.Name ?? $"Item{i + 1}";
                    var memberExpr = BuildCypherExpressionWithVariableMapping(newExpr.Arguments[i], fromParam, toVar, context);
                    members.Add($"{memberName}: {memberExpr}");
                }
                return "{ " + string.Join(", ", members) + " }";

            case MemberExpression memberExpr:
                return BuildMemberExpressionWithMapping(memberExpr, fromParam, toVar, context);

            case BinaryExpression binaryExpr:
                var left = BuildCypherExpressionWithVariableMapping(binaryExpr.Left, fromParam, toVar, context);
                var right = BuildCypherExpressionWithVariableMapping(binaryExpr.Right, fromParam, toVar, context);
                var op = GetCypherOperator(binaryExpr.NodeType);
                return $"({left} {op} {right})";

            default:
                return CypherExpressionBuilder.BuildCypherExpression(expression, toVar, context);
        }
    }

    private static string BuildMemberExpressionWithMapping(MemberExpression memberExpr, string fromParam, string toVar, CypherBuildContext context)
    {
        if (memberExpr.Expression is ParameterExpression paramExpr && paramExpr.Name == fromParam)
        {
            return $"{toVar}.{memberExpr.Member.Name}";
        }

        if (memberExpr.Expression is MemberExpression parentMember)
        {
            var parentExpr = BuildMemberExpressionWithMapping(parentMember, fromParam, toVar, context);
            return $"{parentExpr}.{memberExpr.Member.Name}";
        }

        return CypherExpressionBuilder.BuildCypherExpression(memberExpr, toVar, context);
    }

    /// <summary>
    /// Maps expression types to Cypher operators
    /// </summary>
    private static string GetCypherOperator(ExpressionType nodeType)
    {
        return nodeType switch
        {
            ExpressionType.Add => "+",
            ExpressionType.Subtract => "-",
            ExpressionType.Multiply => "*",
            ExpressionType.Divide => "/",
            ExpressionType.Equal => "=",
            ExpressionType.NotEqual => "<>",
            ExpressionType.LessThan => "<",
            ExpressionType.GreaterThan => ">",
            ExpressionType.LessThanOrEqual => "<=",
            ExpressionType.GreaterThanOrEqual => ">=",
            ExpressionType.AndAlso => "AND",
            ExpressionType.OrElse => "OR",
            _ => throw new NotSupportedException($"Operator {nodeType} not supported")
        };
    }

    /// <summary>
    /// Checks if an expression represents a grouping operation chain (like group.Where(...).Select(...))
    /// </summary>
    private static bool IsGroupingChain(Expression expression)
    {
        if (expression is MethodCallExpression methodCall)
        {
            // Check if this is a method on a grouping
            if (methodCall.Object?.Type.IsGenericType == true &&
                methodCall.Object.Type.GetGenericTypeDefinition() == typeof(IGrouping<,>))
            {
                return true;
            }

            // Check if this is a chain of LINQ methods starting from a grouping
            if (methodCall.Method.Name is "Where" or "Select" or "OrderBy" or "OrderByDescending")
            {
                var source = methodCall.Object ?? (methodCall.Arguments.Count > 0 ? methodCall.Arguments[0] : null);
                return source != null && IsGroupingChain(source);
            }
        }

        return expression?.Type.IsGenericType == true &&
               expression.Type.GetGenericTypeDefinition() == typeof(IGrouping<,>);
    }

    /// <summary>
    /// Builds a Cypher list comprehension for a grouping chain like group.Where(...).Select(...)
    /// </summary>
    private static string BuildGroupingChainListComprehension(Expression chainExpression, CypherBuildContext context)
    {
        // For TraversalPath grouping, we need to build a pattern comprehension
        // The pattern should be: [(sourceAlias)-[relAlias:LABEL]->(targetAlias) WHERE condition | projection]

        string whereClause = "";
        string projection = "t"; // Default to target alias

        // Walk the expression chain to extract Where clauses and final projection
        var current = chainExpression;
        while (current is MethodCallExpression methodCall)
        {
            switch (methodCall.Method.Name)
            {
                case "Where":
                    var whereLambda = ExtractLambdaFromQuote(methodCall.Arguments.Last());
                    if (whereLambda != null)
                    {
                        var condition = BuildTraversalPathCondition(whereLambda, context);
                        whereClause = string.IsNullOrEmpty(whereClause) ? condition : $"{whereClause} AND {condition}";
                    }
                    current = methodCall.Object ?? methodCall.Arguments[0];
                    break;

                case "Select":
                    var selectLambda = ExtractLambdaFromQuote(methodCall.Arguments.Last());
                    if (selectLambda != null)
                    {
                        projection = BuildTraversalPathProjection(selectLambda, context);
                    }
                    current = methodCall.Object ?? methodCall.Arguments[0];
                    break;

                default:
                    // Unknown method, stop processing
                    current = null;
                    break;
            }

            if (current == null) break;
        }

        // Build the pattern comprehension using the traversal pattern from context
        // We should reuse the relationship and target aliases from the MATCH clause
        var sourceAlias = "n";  // Source is typically 'n'
        var relAlias = "r";     // Relationship alias
        var targetAlias = "t";  // Target alias

        // Get the relationship label from context or use a default
        // This should match what was used in ProcessTraversePath
        var relationshipLabel = "KNOWS"; // TODO: Extract this from context

        // Build the final pattern comprehension
        if (string.IsNullOrEmpty(whereClause))
        {
            return $"[({sourceAlias})-[{relAlias}:{relationshipLabel}]->({targetAlias}) | {projection}]";
        }
        else
        {
            return $"[({sourceAlias})-[{relAlias}:{relationshipLabel}]->({targetAlias}) WHERE {whereClause} | {projection}]";
        }
    }

    /// <summary>
    /// Builds a condition expression for traversal paths (e.g., k.Relationship.Since > date)
    /// </summary>
    private static string BuildTraversalPathCondition(LambdaExpression lambda, CypherBuildContext context)
    {
        var lambdaParam = lambda.Parameters.FirstOrDefault()?.Name;
        if (string.IsNullOrEmpty(lambdaParam))
        {
            return "true";
        }

        // For pattern comprehensions, we need to map TraversalPath properties to pattern variables
        // k.Relationship.Since -> r.Since
        // k.Source.Property -> (source alias).Property  
        // k.Target.Property -> t.Property
        return BuildPatternComprehensionExpression(lambda.Body, lambdaParam, context);
    }

    /// <summary>
    /// Builds a projection expression for traversal paths (e.g., k.Target.FirstName)
    /// </summary>
    private static string BuildTraversalPathProjection(LambdaExpression lambda, CypherBuildContext context)
    {
        var lambdaParam = lambda.Parameters.FirstOrDefault()?.Name;
        if (string.IsNullOrEmpty(lambdaParam))
        {
            return "t"; // Default to target
        }

        // For pattern comprehensions, we need to map TraversalPath properties to pattern variables
        // k.Target.FirstName -> t.FirstName
        // k.Source.Property -> (source alias).Property
        // k.Relationship.Property -> r.Property
        return BuildPatternComprehensionExpression(lambda.Body, lambdaParam, context);
    }

    /// <summary>
    /// Builds a Cypher expression for pattern comprehensions by mapping TraversalPath properties
    /// </summary>
    private static string BuildPatternComprehensionExpression(Expression expression, string lambdaParam, CypherBuildContext context)
    {
        // Handle member access like k.Target.FirstName, k.Relationship.Since, etc.
        if (expression is MemberExpression memberExpr)
        {
            if (memberExpr.Expression is MemberExpression parentMember &&
                parentMember.Expression is ParameterExpression paramExpr &&
                paramExpr.Name == lambdaParam)
            {
                // This is a nested member access like k.Target.FirstName
                var pathProperty = parentMember.Member.Name; // "Target", "Source", "Relationship"
                var entityProperty = memberExpr.Member.Name; // "FirstName", "Since", etc.

                return pathProperty switch
                {
                    "Source" => $"n.{entityProperty}",      // Source maps to 'n'
                    "Target" => $"t.{entityProperty}",      // Target maps to 't'
                    "Relationship" => $"r.{entityProperty}", // Relationship maps to 'r'
                    _ => $"t.{entityProperty}" // Default to target
                };
            }
        }
        else if (expression is BinaryExpression binaryExpr)
        {
            // Handle binary expressions like k.Relationship.Since > date
            var left = BuildPatternComprehensionExpression(binaryExpr.Left, lambdaParam, context);
            var right = BuildPatternComprehensionExpression(binaryExpr.Right, lambdaParam, context);
            var op = GetCypherOperator(binaryExpr.NodeType);
            return $"({left} {op} {right})";
        }
        else if (expression is ConstantExpression || expression is MethodCallExpression)
        {
            // For constants and method calls, delegate to the main expression builder
            return CypherExpressionBuilder.BuildCypherExpression(expression, "n", context);
        }

        // Fallback to main expression builder
        return CypherExpressionBuilder.BuildCypherExpression(expression, "n", context);
    }
}
