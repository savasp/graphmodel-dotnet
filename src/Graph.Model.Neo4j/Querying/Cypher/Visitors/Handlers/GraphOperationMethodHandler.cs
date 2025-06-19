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
using System.Reflection;
using Cvoya.Graph.Model;
using Cvoya.Graph.Model.Neo4j.Querying.Cypher.Visitors.Core;
using Cvoya.Graph.Model.Neo4j.Querying.Cypher.Visitors.Expressions;
using Microsoft.Extensions.Logging;

/// <summary>
/// Handles graph-specific operations like Include, Traverse, etc.
/// </summary>
internal record GraphOperationMethodHandler : MethodHandlerBase
{
    public override bool Handle(CypherQueryContext context, MethodCallExpression node, Expression result)
    {
        var methodName = node.Method.Name;
        
        // Add debug logging
        var logger = context.LoggerFactory?.CreateLogger(nameof(GraphOperationMethodHandler));
        logger?.LogDebug($"Handling graph operation method: {methodName}");

        return methodName switch
        {
            "Include" => HandleInclude(context, node),
            "Traverse" => HandleTraverse(context, node),
            "WithDepth" => HandleWithDepth(context, node),
            "Relationships" => HandleRelationships(context, node),
            "PathSegments" => HandlePathSegments(context, node),
            "WithTransaction" => HandleWithTransaction(context, node),
            _ => false
        };
    }

    private static bool HandleInclude(CypherQueryContext context, MethodCallExpression node)
    {
        if (node.Arguments.Count != 2)
        {
            return false;
        }

        if (node.Arguments[1] is UnaryExpression { Operand: LambdaExpression lambda })
        {
            var expressionVisitor = CreateExpressionVisitor(context);

            // Process the include expression to add appropriate MATCH clauses
            if (lambda.Body is MemberExpression member)
            {
                var currentAlias = context.Scope.CurrentAlias ?? "n";
                var relationshipType = GetRelationshipType(member);
                var targetAlias = context.Scope.GetOrCreateAlias(member.Type,
                    GetPreferredAlias(member.Type));

                // Add OPTIONAL MATCH for the relationship
                context.Builder.AddOptionalMatch(
                    $"({currentAlias})-[:{relationshipType}]->({targetAlias})");
            }
        }

        return true;
    }

    private static bool HandleTraverse(CypherQueryContext context, MethodCallExpression node)
    {
        var logger = context.LoggerFactory?.CreateLogger(nameof(GraphOperationMethodHandler));
        logger?.LogDebug("HandleTraverse called");
        
        if (node.Arguments.Count < 2)
        {
            logger?.LogDebug("HandleTraverse: insufficient arguments");
            return false;
        }

        // Extract relationship and target node types from the generic method arguments
        if (node.Method.IsGenericMethod)
        {
            var genericArgs = node.Method.GetGenericArguments();
            if (genericArgs.Length >= 2)
            {
                var relationshipType = genericArgs[0];
                var targetNodeType = genericArgs[1];
                
                logger?.LogDebug($"HandleTraverse: setting traversal info for {relationshipType.Name} -> {targetNodeType.Name}");
                
                // Store traversal information in scope for later pattern generation
                context.Scope.SetTraversalInfo(relationshipType, targetNodeType);
                return true;
            }
        }

        logger?.LogDebug("HandleTraverse: failed to extract generic arguments");
        return false;
    }

    private static string BuildDepthPattern(CypherQueryScope scope)
    {
        // If both min and max depth are specified
        if (scope.TraversalMinDepth.HasValue && scope.TraversalMaxDepth.HasValue)
        {
            if (scope.TraversalMinDepth == scope.TraversalMaxDepth)
            {
                return scope.TraversalMinDepth.Value.ToString();
            }
            return $"{scope.TraversalMinDepth}..{scope.TraversalMaxDepth}";
        }
        
        // If only max depth is specified (common case)
        if (scope.TraversalMaxDepth.HasValue)
        {
            return $"1..{scope.TraversalMaxDepth}";
        }
        
        // Default to unlimited depth
        return "";
    }

    private static bool HandleRelationships(CypherQueryContext context, MethodCallExpression node)
    {
        // This would return relationships in a path or from a node
        var currentAlias = context.Scope.CurrentAlias ?? "n";
        context.Builder.AddReturn($"relationships({currentAlias})");
        return true;
    }

    private static bool HandlePathSegments(CypherQueryContext context, MethodCallExpression node)
    {
        // This would return path segments
        var currentAlias = context.Scope.CurrentAlias ?? "n";
        context.Builder.AddReturn($"nodes({currentAlias})");
        return true;
    }

    private static bool HandleWithTransaction(CypherQueryContext context, MethodCallExpression node)
    {
        // Transaction handling would be done at a higher level
        // For now, just pass through
        return true;
    }

    private static bool HandleWithDepth(CypherQueryContext context, MethodCallExpression node)
    {
        var logger = context.LoggerFactory?.CreateLogger(nameof(GraphOperationMethodHandler));
        logger?.LogDebug("HandleWithDepth called");
        
        // WithDepth should extract depth constraints from the queryable instance
        if (node.Arguments.Count < 2)
        {
            logger?.LogDebug("HandleWithDepth: insufficient arguments");
            return false;
        }

        // Log all arguments to understand the structure
        logger?.LogDebug($"HandleWithDepth: method object = {node.Object}");
        for (int i = 0; i < node.Arguments.Count; i++)
        {
            var arg = node.Arguments[i];
            logger?.LogDebug($"HandleWithDepth: arg[{i}] = {arg.GetType().Name}: {arg}");
            if (arg is ConstantExpression constExpr)
            {
                logger?.LogDebug($"HandleWithDepth: arg[{i}] constant value type: {constExpr.Value?.GetType().Name ?? "null"}");
            }
        }

        // For instance methods, the queryable should be in node.Object
        if (node.Object is ConstantExpression { Value: var queryableInstance } && queryableInstance != null)
        {
            logger?.LogDebug($"HandleWithDepth: found queryable in Object: {queryableInstance.GetType().Name}");
            
            // Use reflection to get the depth information from the queryable
            var queryableType = queryableInstance.GetType();
            var minDepthField = queryableType.GetField("_minDepth", BindingFlags.NonPublic | BindingFlags.Instance);
            var maxDepthField = queryableType.GetField("_maxDepth", BindingFlags.NonPublic | BindingFlags.Instance);

            if (minDepthField?.GetValue(queryableInstance) is int minDepth &&
                maxDepthField?.GetValue(queryableInstance) is int maxDepth)
            {
                logger?.LogDebug($"HandleWithDepth: extracted depth from queryable: min={minDepth}, max={maxDepth}");
                
                // Store depth information in scope for traversal patterns to use
                context.Scope.SetTraversalDepth(minDepth, maxDepth);
                logger?.LogDebug("HandleWithDepth: stored depth info in scope");
                
                // Also extract and store traversal information
                var sourceTypeArg = queryableType.GetGenericArguments().ElementAtOrDefault(0);
                var relTypeArg = queryableType.GetGenericArguments().ElementAtOrDefault(1);
                var targetTypeArg = queryableType.GetGenericArguments().ElementAtOrDefault(2);
                
                if (relTypeArg != null && targetTypeArg != null)
                {
                    context.Scope.SetTraversalInfo(relTypeArg, targetTypeArg);
                    logger?.LogDebug($"HandleWithDepth: stored traversal info: rel={relTypeArg.Name}, target={targetTypeArg.Name}");
                    
                    // Generate the traversal pattern immediately
                    GenerateTraversalPattern(context, logger);
                }
                
                return true;
            }
        }

        logger?.LogDebug("HandleWithDepth: could not extract depth information");
        return false;
    }

    private static void GenerateTraversalPattern(CypherQueryContext context, ILogger? logger)
    {
        logger?.LogDebug("GenerateTraversalPattern: starting");
        
        if (context.Scope.TraversalInfo == null)
        {
            logger?.LogDebug("GenerateTraversalPattern: no traversal info found");
            return;
        }
        
        var currentAlias = context.Scope.CurrentAlias ?? "src";
        var targetAlias = "n";  // Always use "n" for the target to match CypherResultProcessor expectations

        // Build the relationship pattern with depth constraints if available
        var depthPattern = BuildDepthPattern(context.Scope);
        var relationshipLabel = GetRelationshipLabel(context.Scope.TraversalInfo.RelationshipType);
        
        // Clear existing matches and set up proper traversal pattern
        context.Builder.ClearMatches();
        
        // Get labels for both source and target types
        var sourceLabel = GetNodeLabel(context.Scope.RootType);
        var targetLabel = GetNodeLabel(context.Scope.TraversalInfo.TargetNodeType);
        
        var pattern = $"({currentAlias}:{sourceLabel})-[:{relationshipLabel}*{depthPattern}]->({targetAlias}:{targetLabel})";
        logger?.LogDebug($"GenerateTraversalPattern: generated pattern: {pattern}");
        
        // Add the traversal pattern
        context.Builder.AddMatchPattern(pattern);
        
        // Update the main node alias for complex property loading
        context.Builder.SetMainNodeAlias(targetAlias);
        context.Scope.CurrentAlias = targetAlias;
        
        // Clear any existing return clauses and add return for target (using "n")
        context.Builder.ClearReturn();
        context.Builder.AddReturn(targetAlias);
        logger?.LogDebug($"GenerateTraversalPattern: added return for {targetAlias}");
        
        // Enable complex property loading for the target nodes
        context.Builder.EnableComplexPropertyLoading();
        logger?.LogDebug("GenerateTraversalPattern: enabled complex property loading");
        
        logger?.LogDebug($"GenerateTraversalPattern: set current alias to {targetAlias}");
    }

    private static string GetRelationshipType(MemberExpression member)
    {
        // Extract relationship type from property name or attributes
        // This is a simplified implementation
        return member.Member.Name.ToUpper();
    }

    private static string GetPreferredAlias(Type type)
    {
        var name = type.Name;

        // Remove generic type markers
        var genericIndex = name.IndexOf('`');
        if (genericIndex > 0)
        {
            name = name[..genericIndex];
        }

        // Remove interface prefix
        if (name.StartsWith("I") && name.Length > 1 && char.IsUpper(name[1]))
        {
            name = name[1..];
        }

        return char.ToLower(name[0]).ToString();
    }

    private static ICypherExpressionVisitor CreateExpressionVisitor(CypherQueryContext context)
    {
        return new ExpressionVisitorChainFactory(context).CreateStandardChain();
    }

    private static string GetRelationshipLabel(Type relationshipType)
    {
        // Use the Labels class to get the proper relationship type
        return Labels.GetLabelFromType(relationshipType);
    }

    private static string GetNodeLabel(Type nodeType)
    {
        // Use the Labels class to get the proper node label
        return Labels.GetLabelFromType(nodeType);
    }
}
