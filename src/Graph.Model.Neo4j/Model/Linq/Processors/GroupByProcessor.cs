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
/// Processes LINQ GroupBy clauses by building appropriate Cypher grouping and aggregation
/// </summary>
internal class GroupByProcessor
{
    public static void ProcessGroupBy(MethodCallExpression methodCall, CypherBuildContext context)
    {
        LambdaExpression? keySelector = ExtractLambdaFromQuote(methodCall.Arguments[1]);
        if (keySelector != null)
        {
            // Check if we're grouping TraversalPath results
            var sourceType = GetSourceElementType(methodCall);
            if (sourceType.IsGenericType &&
                sourceType.GetGenericTypeDefinition().FullName == "Cvoya.Graph.Model.TraversalPath`3")
            {
                // Special handling for grouping TraversalPath results
                ProcessTraversalPathGroupBy(keySelector, context);
            }
            else
            {
                // Regular GroupBy handling
                var keyExpression = CypherExpressionBuilder.BuildCypherExpression(keySelector.Body, context.CurrentAlias, context);

                context.IsGroupByQuery = true;
                context.GroupByKey = keyExpression;
                context.GroupByKeySelector = keySelector;
            }
        }
        else
        {
            throw new InvalidOperationException("Failed to extract key selector from GroupBy expression");
        }
    }

    private static void ProcessTraversalPathGroupBy(LambdaExpression keySelector, CypherBuildContext context)
    {
        // Mark this as a special TraversalPath grouping
        context.IsTraversalPathGroupBy = true;
        context.IsGroupByQuery = true;

        // For TraversalPath GroupBy, we typically group by a property of the Source or Target
        // e.g., paths.GroupBy(p => p.Target.Category)

        if (keySelector.Body is MemberExpression memberExpr)
        {
            // Handle nested member access like p.Target.Category
            var groupByExpression = BuildTraversalPathGroupByExpression(memberExpr, context);
            context.GroupByKey = groupByExpression;
            context.GroupByKeySelector = keySelector;
            System.Diagnostics.Debug.WriteLine($"DEBUG TraversalPath GroupBy: keySelector.Body = {keySelector.Body}, GroupByKey = {groupByExpression}");
        }
        else
        {
            // Fallback: try to build the expression normally
            var keyExpression = CypherExpressionBuilder.BuildCypherExpression(keySelector.Body, "p", context);
            context.GroupByKey = keyExpression;
            context.GroupByKeySelector = keySelector;
            System.Diagnostics.Debug.WriteLine($"DEBUG TraversalPath GroupBy (fallback): keySelector.Body = {keySelector.Body}, GroupByKey = {keyExpression}");
        }
    }

    private static string BuildTraversalPathGroupByExpression(MemberExpression memberExpr, CypherBuildContext context)
    {
        // Handle TraversalPath member access patterns like p.Target.Property or p.Source.Property
        if (memberExpr.Expression is MemberExpression parentMember &&
            parentMember.Expression is ParameterExpression)
        {
            var pathProperty = parentMember.Member.Name; // "Target", "Source", "Relationship"
            var entityProperty = memberExpr.Member.Name; // The actual property name

            return pathProperty switch
            {
                "Source" => $"n.{entityProperty}",      // Source maps to 'n'
                "Target" => $"t2.{entityProperty}",     // Target maps to 't2'
                "Relationship" => $"r1.{entityProperty}", // Relationship maps to 'r1'
                _ => $"n.{entityProperty}" // Default to source
            };
        }
        else if (memberExpr.Expression is ParameterExpression)
        {
            // Direct access to TraversalPath properties (e.g., ks.Source, ks.Target, ks.Relationship)
            var pathProperty = memberExpr.Member.Name;
            return pathProperty switch
            {
                "Source" => "n",        // Source maps to source alias 'n'
                "Target" => "t2",       // Target maps to target alias 't2'  
                "Relationship" => "r1", // Relationship maps to relationship alias 'r1'
                _ => $"p.{pathProperty}" // Fallback to path parameter
            };
        }

        // Fallback
        return CypherExpressionBuilder.BuildCypherExpression(memberExpr, "p", context);
    }

    // Helper methods that delegate to the main builder for now
    private static LambdaExpression? ExtractLambdaFromQuote(Expression expression)
    {
        if (expression is UnaryExpression { NodeType: ExpressionType.Quote } ue && ue.Operand is LambdaExpression lambda)
        {
            return lambda;
        }
        return expression as LambdaExpression;
    }

    private static Type GetSourceElementType(MethodCallExpression methodCall)
    {
        // Get the source type from the method call
        if (methodCall.Object != null)
        {
            var sourceType = methodCall.Object.Type;
            if (sourceType.IsGenericType)
            {
                return sourceType.GetGenericArguments()[0];
            }
        }
        else if (methodCall.Arguments.Count > 0)
        {
            var sourceType = methodCall.Arguments[0].Type;
            if (sourceType.IsGenericType)
            {
                return sourceType.GetGenericArguments()[0];
            }
        }

        return typeof(object);
    }
}
