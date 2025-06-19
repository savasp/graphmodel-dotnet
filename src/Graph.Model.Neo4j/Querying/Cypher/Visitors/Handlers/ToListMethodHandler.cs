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
using Cvoya.Graph.Model.Neo4j.Querying.Cypher;
using Cvoya.Graph.Model.Neo4j.Querying.Cypher.Visitors.Core;
using Microsoft.Extensions.Logging;

/// <summary>
/// Handles the ToList LINQ method which is a terminal operation that executes the query.
/// </summary>
internal record ToListMethodHandler : MethodHandlerBase
{
    public override bool Handle(CypherQueryContext context, MethodCallExpression node, Expression result)
    {
        var logger = context.LoggerFactory?.CreateLogger(nameof(ToListMethodHandler));
        logger?.LogDebug("ToListMethodHandler called");

        if (node.Method.Name != "ToList" || node.Arguments.Count != 1)
        {
            logger?.LogDebug("ToListMethodHandler: not a ToList method or wrong arguments");
            return false;
        }

        // ToList is a terminal operation - finalize any pending traversal patterns
        FinalizeTraversalPatterns(context);

        // If no explicit return has been set, add a default return
        if (!context.Builder.HasReturnClause)
        {
            var alias = context.Scope.CurrentAlias ?? "n";
            logger?.LogDebug($"ToListMethodHandler: adding default return for alias {alias}");
            context.Builder.AddReturn(alias);
        }

        logger?.LogDebug("ToListMethodHandler: completed successfully");
        return true;
    }

    private static void FinalizeTraversalPatterns(CypherQueryContext context)
    {
        var logger = context.LoggerFactory?.CreateLogger(nameof(ToListMethodHandler));
        logger?.LogDebug("FinalizeTraversalPatterns called");

        // If we have pending traversal information, generate the pattern now
        if (context.Scope.TraversalInfo != null)
        {
            logger?.LogDebug($"Found traversal info: {context.Scope.TraversalInfo.RelationshipType.Name} -> {context.Scope.TraversalInfo.TargetNodeType.Name}");

            var currentAlias = context.Scope.CurrentAlias ?? "n";
            var targetAlias = context.Scope.GetOrCreateAlias(
                context.Scope.TraversalInfo.TargetNodeType,
                GetPreferredAlias(context.Scope.TraversalInfo.TargetNodeType));

            // Build the relationship pattern with depth constraints if available
            var depthPattern = BuildDepthPattern(context.Scope);
            var relationshipLabel = GetRelationshipLabel(context.Scope.TraversalInfo.RelationshipType);

            // Clear existing matches and set up proper traversal pattern
            context.Builder.ClearMatches();

            // Get labels for both source and target types
            var sourceLabel = GetNodeLabel(context.Scope.RootType);
            var targetLabel = GetNodeLabel(context.Scope.TraversalInfo.TargetNodeType);

            var pattern = $"({currentAlias}:{sourceLabel})-[:{relationshipLabel}*{depthPattern}]->({targetAlias}:{targetLabel})";
            logger?.LogDebug($"Generated traversal pattern: {pattern}");

            // Add the traversal pattern
            context.Builder.AddMatchPattern(pattern);
            context.Scope.CurrentAlias = targetAlias;

            // For traversal targets, enable complex property loading
            context.Builder.EnableComplexPropertyLoading();
            logger?.LogDebug("Enabled complex property loading for traversal targets");
        }
        else
        {
            // Check if this is a relationship query
            if (context.Builder.IsRelationshipQuery)
            {
                logger?.LogDebug("No traversal info found - this is a relationship query, skipping complex property loading");
                // For relationship queries, don't enable complex property loading
                // The relationship data will be returned as-is
            }
            else
            {
                logger?.LogDebug("No traversal info found - enabling complex property loading for regular node query");
                // This is a regular node query, enable complex property loading
                context.Builder.EnableComplexPropertyLoading();
            }
        }
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

    private static string GetPreferredAlias(Type type)
    {
        var name = type.Name;

        // Remove interface prefix
        if (name.StartsWith("I") && name.Length > 1 && char.IsUpper(name[1]))
        {
            name = name[1..];
        }

        // Take first letter and make it lowercase
        return char.ToLower(name[0]).ToString();
    }

    private static string GetRelationshipLabel(Type relationshipType)
    {
        // Extract relationship label from type name
        // This could be enhanced to use attributes or conventions
        return relationshipType.Name.ToUpper();
    }

    private static string GetNodeLabel(Type nodeType)
    {
        // Extract node label from type name
        // This could be enhanced to use attributes or conventions
        return nodeType.Name.ToUpper();
    }
}
