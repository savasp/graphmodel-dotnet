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
using Cvoya.Graph.Provider.Neo4j.Schema;

namespace Cvoya.Graph.Provider.Neo4j.Linq.Processors;

/// <summary>
/// Processes graph traversal operations like TraversalTo, TraversalRelationships, and TraversalPaths
/// </summary>
internal class TraversalProcessor
{
    public static void ProcessTraversalTo(MethodCallExpression methodCall, CypherBuildContext context)
    {
        // Extract traversal parameters
        var source = methodCall.Arguments[0];
        var direction = (TraversalDirection)((ConstantExpression)methodCall.Arguments[1]).Value!;
        var nodeFilter = ExtractLambdaFromConstant(methodCall.Arguments[2]);
        var relationshipFilter = ExtractLambdaFromConstant(methodCall.Arguments[3]);
        var targetFilter = ExtractLambdaFromConstant(methodCall.Arguments[4]);
        var minDepth = (int)((ConstantExpression)methodCall.Arguments[5]).Value!;
        var maxDepth = (int)((ConstantExpression)methodCall.Arguments[6]).Value!;

        var genericArgs = methodCall.Method.GetGenericArguments();
        var relationshipType = genericArgs[1];
        var targetType = genericArgs[2];

        var sourceAlias = context.CurrentAlias;
        var relAlias = context.GetNextAlias("r");
        var targetAlias = context.GetNextAlias("n");

        // Build traversal pattern
        var relLabel = Neo4jTypeManager.GetLabel(relationshipType);
        var targetLabel = Neo4jTypeManager.GetLabel(targetType);

        if (context.Match.Length > 0) context.Match.AppendLine();
        context.Match.Append($"({sourceAlias})");

        var relPattern = $"[{relAlias}:{relLabel}*{minDepth}..{maxDepth}]";
        switch (direction)
        {
            case TraversalDirection.Outgoing:
                context.Match.Append($"-{relPattern}->");
                break;
            case TraversalDirection.Incoming:
                context.Match.Append($"<-{relPattern}-");
                break;
            case TraversalDirection.Both:
                context.Match.Append($"-{relPattern}-");
                break;
        }
        context.Match.Append($"({targetAlias}:{targetLabel})");

        // Apply filters
        if (nodeFilter != null && !IsConstantTrue(nodeFilter.Body))
        {
            var oldAlias = context.CurrentAlias;
            context.CurrentAlias = sourceAlias;
            WhereProcessor.ProcessWhere(nodeFilter, context);
            context.CurrentAlias = oldAlias;
        }

        if (relationshipFilter != null && !IsConstantTrue(relationshipFilter.Body))
        {
            // For variable-length relationships, wrap in ALL clause
            if (context.Where.Length > 0) context.Where.Append(" AND ");

            // Create a temporary context to build the condition inside ALL clause
            var tempContext = new CypherBuildContext
            {
                CurrentAlias = "r",
                RootType = context.RootType
            };

            WhereProcessor.ProcessWhere(relationshipFilter, tempContext);

            if (tempContext.Where.Length > 0)
            {
                context.Where.Append($"ALL(r IN {relAlias} WHERE {tempContext.Where})");
            }
        }

        if (targetFilter != null && !IsConstantTrue(targetFilter.Body))
        {
            var oldAlias = context.CurrentAlias;
            context.CurrentAlias = targetAlias;
            WhereProcessor.ProcessWhere(targetFilter, context);
            context.CurrentAlias = oldAlias;
        }

        context.CurrentAlias = targetAlias;
        context.Return = targetAlias;
    }

    public static void ProcessTraversalRelationships(MethodCallExpression methodCall, CypherBuildContext context)
    {
        // Extract traversal parameters - similar to ProcessTraversalTo
        var source = methodCall.Arguments[0];
        var direction = (TraversalDirection)((ConstantExpression)methodCall.Arguments[1]).Value!;
        var nodeFilter = ExtractLambdaFromConstant(methodCall.Arguments[2]);
        var relationshipFilter = ExtractLambdaFromConstant(methodCall.Arguments[3]);
        var minDepth = (int)((ConstantExpression)methodCall.Arguments[4]).Value!;
        var maxDepth = (int)((ConstantExpression)methodCall.Arguments[5]).Value!;

        var genericArgs = methodCall.Method.GetGenericArguments();
        var relationshipType = genericArgs[1];

        var sourceAlias = context.CurrentAlias;
        var relAlias = context.GetNextAlias("r");
        var targetAlias = context.GetNextAlias("n");

        // Build traversal pattern for relationships
        var relLabel = Neo4jTypeManager.GetLabel(relationshipType);

        if (context.Match.Length > 0) context.Match.AppendLine();
        context.Match.Append($"({sourceAlias})");

        // Pattern for single-hop vs variable-length relationships
        var relPattern = minDepth == 1 && maxDepth == 1
            ? $"[{relAlias}:{relLabel}]"
            : $"[{relAlias}:{relLabel}*{minDepth}..{maxDepth}]";

        switch (direction)
        {
            case TraversalDirection.Outgoing:
                context.Match.Append($"-{relPattern}->");
                break;
            case TraversalDirection.Incoming:
                context.Match.Append($"<-{relPattern}-");
                break;
            case TraversalDirection.Both:
                context.Match.Append($"-{relPattern}-");
                break;
        }
        context.Match.Append($"({targetAlias})");

        // Apply filters
        if (nodeFilter != null && !IsConstantTrue(nodeFilter.Body))
        {
            var oldAlias = context.CurrentAlias;
            context.CurrentAlias = sourceAlias;
            WhereProcessor.ProcessWhere(nodeFilter, context);
            context.CurrentAlias = oldAlias;
        }

        if (relationshipFilter != null && !IsConstantTrue(relationshipFilter.Body))
        {
            var oldAlias = context.CurrentAlias;
            context.CurrentAlias = relAlias;

            // For single relationships, apply filter directly; for variable length, use ALL
            if (minDepth == 1 && maxDepth == 1)
            {
                WhereProcessor.ProcessWhere(relationshipFilter, context);
            }
            else
            {
                // For variable-length relationships, build ALL clause with proper condition
                var tempContext = new CypherBuildContext
                {
                    CurrentAlias = "r",
                    RootType = context.RootType
                };

                WhereProcessor.ProcessWhere(relationshipFilter, tempContext);

                if (tempContext.Where.Length > 0)
                {
                    if (context.Where.Length > 0)
                    {
                        context.Where.Append(" AND ");
                    }
                    context.Where.Append($"ALL(r IN {relAlias} WHERE {tempContext.Where})");
                }
            }

            context.CurrentAlias = oldAlias;
        }

        // Return the relationship alias instead of target alias
        context.CurrentAlias = relAlias;
        context.Return = relAlias;

        // Set the query root type to indicate this is a relationship query
        context.QueryRootType = GraphQueryContext.QueryRootType.Relationship;
    }

    public static void ProcessTraversalPaths(MethodCallExpression methodCall, CypherBuildContext context)
    {
        // Extract traversal parameters - similar to ProcessTraversalTo
        var source = methodCall.Arguments[0];
        var direction = (TraversalDirection)((ConstantExpression)methodCall.Arguments[1]).Value!;
        var nodeFilter = ExtractLambdaFromConstant(methodCall.Arguments[2]);
        var relationshipFilter = ExtractLambdaFromConstant(methodCall.Arguments[3]);
        var minDepth = (int)((ConstantExpression)methodCall.Arguments[4]).Value!;
        var maxDepth = (int)((ConstantExpression)methodCall.Arguments[5]).Value!;

        var genericArgs = methodCall.Method.GetGenericArguments();
        var relationshipType = genericArgs[1];

        var sourceAlias = context.CurrentAlias;
        var relAlias = context.GetNextAlias("r");
        var targetAlias = context.GetNextAlias("n");

        // Build traversal pattern - similar to ProcessTraversalTo but structured for path results
        var relLabel = Neo4jTypeManager.GetLabel(relationshipType);

        if (context.Match.Length > 0) context.Match.AppendLine();
        context.Match.Append($"p = ({sourceAlias})");

        var relPattern = $"[{relAlias}:{relLabel}*{minDepth}..{maxDepth}]";
        switch (direction)
        {
            case TraversalDirection.Outgoing:
                context.Match.Append($"-{relPattern}->");
                break;
            case TraversalDirection.Incoming:
                context.Match.Append($"<-{relPattern}-");
                break;
            case TraversalDirection.Both:
                context.Match.Append($"-{relPattern}-");
                break;
        }
        context.Match.Append($"({targetAlias})");

        // Apply filters
        if (nodeFilter != null && !IsConstantTrue(nodeFilter.Body))
        {
            var oldAlias = context.CurrentAlias;
            context.CurrentAlias = sourceAlias;
            if (context.Where.Length > 0) context.Where.Append(" AND ");
            WhereProcessor.ProcessWhere(nodeFilter, context);
            context.CurrentAlias = oldAlias;
        }

        if (relationshipFilter != null && !IsConstantTrue(relationshipFilter.Body))
        {
            if (context.Where.Length > 0) context.Where.Append(" AND ");
            context.Where.Append($"ALL(r IN relationships(p) WHERE ");

            var oldAlias = context.CurrentAlias;
            context.CurrentAlias = "r";
            WhereProcessor.ProcessWhere(relationshipFilter, context);
            context.CurrentAlias = oldAlias;

            context.Where.Append(")");
        }

        // For path queries, return the source, relationships, and target as separate columns
        context.Return = $"{sourceAlias}, {relAlias}, {targetAlias}";
        context.IsPathResult = true;
    }

    // Helper methods
    private static LambdaExpression? ExtractLambdaFromConstant(Expression expression)
    {
        if (expression is ConstantExpression ce && ce.Value is LambdaExpression lambda)
        {
            return lambda;
        }
        return null;
    }

    private static bool IsConstantTrue(Expression expression)
    {
        if (expression is ConstantExpression ce && ce.Value is bool boolValue)
        {
            return boolValue;
        }
        return false;
    }
}
