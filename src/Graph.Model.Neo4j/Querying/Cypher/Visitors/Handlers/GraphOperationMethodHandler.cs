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
using Microsoft.Extensions.Logging;

/// <summary>
/// Handles graph-specific operations
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
            "PathSegments" => HandlePathSegments(context, node),
            "WithTransaction" => HandleWithTransaction(context, node),
            "WithDepth" => HandleWithDepth(context, node),
            "Direction" => HandleDirection(context, node),
            _ => false
        };
    }

    private static bool HandlePathSegments(CypherQueryContext context, MethodCallExpression node)
    {
        var logger = context.LoggerFactory?.CreateLogger(nameof(GraphOperationMethodHandler));
        logger?.LogDebug("HandlePathSegments called");

        var genericArgs = node.Method.GetGenericArguments();
        if (genericArgs.Length != 3)
        {
            logger?.LogError("PathSegments method doesn't have the expected generic arguments");
            return false;
        }

        var sourceType = genericArgs[0];
        var relationshipType = genericArgs[1];
        var targetNodeType = genericArgs[2];

        // Store the path segment info in the scope
        context.Scope.SetTraversalInfo(sourceType, relationshipType, targetNodeType);
        // DON'T set IsPathSegmentContext here - it will affect the wrong parts of the query!
        // context.Scope.IsPathSegmentContext = true;

        logger?.LogDebug($"Source Type: {sourceType.Name}, Relationship Type: {relationshipType.Name}, Target Node Type: {targetNodeType.Name}");
        var pathSegmentVisitor = new PathSegmentVisitor(context);
        pathSegmentVisitor.Visit(node);

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

        // Extract depth parameters from the method call
        if (node.Arguments.Count == 2) // WithDepth(maxDepth)
        {
            var maxDepthArg = node.Arguments[1];
            if (maxDepthArg is ConstantExpression { Value: int maxDepth })
            {
                logger?.LogDebug($"Setting max depth: {maxDepth}");
                context.Builder.SetDepth(maxDepth);
                return true;
            }
        }
        else if (node.Arguments.Count == 3) // WithDepth(minDepth, maxDepth)
        {
            var minDepthArg = node.Arguments[1];
            var maxDepthArg = node.Arguments[2];

            if (minDepthArg is ConstantExpression { Value: int minDepth } &&
                maxDepthArg is ConstantExpression { Value: int maxDepth })
            {
                logger?.LogDebug($"Setting depth range: {minDepth}-{maxDepth}");
                context.Builder.SetDepth(minDepth, maxDepth);
                return true;
            }
        }

        logger?.LogWarning("Could not extract depth parameters from WithDepth method call");
        return false;
    }

    private static bool HandleDirection(CypherQueryContext context, MethodCallExpression node)
    {
        var logger = context.LoggerFactory?.CreateLogger(nameof(GraphOperationMethodHandler));
        logger?.LogDebug("HandleDirection called");

        // Only allow one Direction() in the expression tree
        if (context.Builder.TraversalDirection is not null)
        {
            throw new InvalidOperationException("Only one Direction() call is allowed in a traversal query.");
        }

        if (node.Arguments.Count == 2 && node.Arguments[1] is ConstantExpression ce && ce.Value is GraphTraversalDirection direction)
        {
            context.Builder.SetTraversalDirection(direction);
            logger?.LogDebug($"Set traversal direction: {direction}");
            return true;
        }

        logger?.LogWarning("Could not extract direction from Direction() method call");
        return false;
    }
}
