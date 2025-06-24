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
using Cvoya.Graph.Model.Neo4j.Querying.Cypher.Visitors.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using static Cvoya.Graph.Model.Neo4j.Querying.Cypher.Builders.CypherQueryBuilder;

namespace Cvoya.Graph.Model.Neo4j.Querying.Cypher.Visitors.Handlers;

/// <summary>
/// Base class for method handlers that provides common functionality.
/// This refactored version centralizes logic that was previously duplicated across handlers.
/// </summary>
internal abstract record class MethodHandlerBase : IMethodHandler
{
    public abstract bool Handle(CypherQueryContext context, MethodCallExpression node, Expression result);

    /// <summary>
    /// Extracts lambda expression from method arguments.
    /// Handles both direct lambdas and wrapped lambdas.
    /// </summary>
    protected static LambdaExpression? ExtractLambda(Expression expression)
    {
        return expression switch
        {
            LambdaExpression directLambda => directLambda,
            UnaryExpression { Operand: LambdaExpression unaryLambda } => unaryLambda,
            _ => null
        };
    }

    /// <summary>
    /// Validates that a method call matches the expected pattern.
    /// </summary>
    protected static bool ValidateMethodCall(MethodCallExpression node, string expectedMethodName, int expectedArgCount)
    {
        return node.Method.Name == expectedMethodName && node.Arguments.Count == expectedArgCount;
    }

    /// <summary>
    /// Determines the correct alias for the current context.
    /// This method centralizes the complex alias resolution logic that was duplicated
    /// across multiple handlers (WhereMethodHandler, SelectMethodHandler, etc.).
    /// </summary>
    protected static string DetermineContextAlias(CypherQueryContext context, string? methodName = null)
    {
        var logger = context.LoggerFactory?.CreateLogger($"MethodHandler.{methodName}");
        logger?.LogDebug("DetermineContextAlias called for method: {Method}", methodName);
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
        logger?.LogDebug("Using alias '{Alias}' for {Method} method", defaultAlias, methodName);
        return defaultAlias;
    }

    /// <summary>
    /// Creates a logger for the specific handler type.
    /// </summary>
    protected static ILogger CreateLogger(CypherQueryContext context, string handlerName)
    {
        return context.LoggerFactory?.CreateLogger($"Handler.{handlerName}")
            ?? NullLogger.Instance;
    }
}

