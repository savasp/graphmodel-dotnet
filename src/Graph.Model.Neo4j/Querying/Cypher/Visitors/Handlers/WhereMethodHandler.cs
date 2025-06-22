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
using Cvoya.Graph.Model.Neo4j.Querying.Cypher.Builders;
using Cvoya.Graph.Model.Neo4j.Querying.Cypher.Visitors.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using static Cvoya.Graph.Model.Neo4j.Querying.Cypher.Builders.CypherQueryBuilder;

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

        context.Builder.HasAppliedRootWhere = true;

        // Mark that we've applied the root predicate
        context.Builder.HasAppliedRootWhere = true;

        // Get the predicate (lambda expression)
        if (node.Arguments[1] is not UnaryExpression { Operand: LambdaExpression lambda })
        {
            throw new GraphException("Where method requires a lambda expression predicate");
        }

        // Determine the correct alias based on current context
        var targetAlias = DetermineWhereAlias(context);

        context.Builder.SetPendingWhere(lambda, targetAlias);

        return true;
    }

    // ...existing code...

    private string DetermineWhereAlias(CypherQueryContext context)
    {
        var logger = context.LoggerFactory?.CreateLogger(nameof(WhereMethodHandler));
        logger?.LogDebug("DetermineWhereAlias called");
        logger?.LogDebug("IsPathSegmentContext: {IsPathSegmentContext}", context.Scope.IsPathSegmentContext);
        logger?.LogDebug("HasUserProjections: {HasUserProjections}", context.Builder.HasUserProjections);

        // For path segment contexts, we need to check what was projected
        if (context.Scope.IsPathSegmentContext)
        {
            // Check if we have a projection that determines the alias
            if (context.Builder.HasUserProjections)
            {
                var rootType = context.Scope.RootType;

                // If the root type is a relationship, use the relationship alias
                if (typeof(IRelationship).IsAssignableFrom(rootType))
                {
                    logger?.LogDebug("Path segment context with relationship projection, using relationship alias: r");
                    return "r";
                }

                // If the root type is a node, we need to determine which node based on the projection
                if (typeof(INode).IsAssignableFrom(rootType))
                {
                    // Check the path segment projection to determine the correct alias
                    var projection = context.Builder.PathSegmentProjection;
                    var alias = projection switch
                    {
                        PathSegmentProjectionEnum.StartNode => context.Builder.PathSegmentSourceAlias ?? "src",
                        PathSegmentProjectionEnum.EndNode => context.Builder.PathSegmentTargetAlias ?? "tgt",
                        PathSegmentProjectionEnum.Relationship => "r",
                        _ => context.Scope.CurrentAlias ?? "src"
                    };

                    logger?.LogDebug("Path segment context with node projection {Projection}, using alias: {Alias}", projection, alias);
                    return alias;
                }
            }

            // Default to source alias for path segment filtering (when no specific projection)
            logger?.LogDebug("Path segment context, using source alias: src");
            return context.Scope.CurrentAlias ?? "src";
        }

        // Rest of existing logic for non-path segment contexts...
        if (context.Builder.HasUserProjections)
        {
            var rootType = context.Scope.RootType;
            if (typeof(IRelationship).IsAssignableFrom(rootType))
            {
                logger?.LogDebug("Root type is relationship, using relationship alias: r");
                return "r";
            }
        }

        var defaultAlias = context.Scope.CurrentAlias ?? "src";
        logger?.LogDebug("Using alias '{Alias}' for Where method", defaultAlias);
        return defaultAlias;
    }
}
