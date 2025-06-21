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

    private static string DetermineWhereAlias(CypherQueryContext context)
    {
        var logger = context.LoggerFactory?.CreateLogger<WhereMethodHandler>();

        logger?.LogDebug("DetermineWhereAlias called");
        logger?.LogDebug("IsPathSegmentContext: {IsPathSegment}", context.Scope.IsPathSegmentContext);
        logger?.LogDebug("HasUserProjections: {HasProjections}", context.Builder.HasUserProjections);

        // If we're in a path segment context and have user projections,
        // use the alias based on what we're projecting
        if (context.Scope.IsPathSegmentContext && context.Builder.HasUserProjections)
        {
            var projection = context.Builder.GetPathSegmentProjection();
            logger?.LogDebug("PathSegmentProjection: {Projection}", projection);
            var alias = projection switch
            {
                CypherQueryBuilder.PathSegmentProjection.EndNode => context.Builder.PathSegmentTargetAlias ?? "tgt",
                CypherQueryBuilder.PathSegmentProjection.StartNode => context.Builder.PathSegmentSourceAlias ?? "src",
                CypherQueryBuilder.PathSegmentProjection.Relationship => context.Builder.PathSegmentRelationshipAlias ?? "r",
                _ => context.Builder.PathSegmentSourceAlias ?? "src"
            };

            logger?.LogDebug("Selected alias for path segment projection: {Alias}", alias);
            return alias;
        }

        // If we're in path segment context but no projections yet, use source
        if (context.Scope.IsPathSegmentContext)
        {
            var alias = context.Builder.PathSegmentSourceAlias ?? "src";
            logger?.LogDebug("Path segment context, no projections, using source alias: {Alias}", alias);
            return alias;
        }

        // Regular node query
        var regularAlias = context.Builder.RootNodeAlias ?? "src";
        logger?.LogDebug("Regular node query, using root alias: {Alias}", regularAlias);
        return regularAlias;
    }
}
