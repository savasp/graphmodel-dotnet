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
/// Handles the ToList LINQ method which is a terminal operation that executes the query.
/// </summary>
internal record ToListMethodHandler : MethodHandlerBase
{
    public override bool Handle(CypherQueryContext context, MethodCallExpression node, Expression result)
    {
        var logger = context.LoggerFactory?.CreateLogger(nameof(ToListMethodHandler));
        logger?.LogDebug("ToListMethodHandler called");

        var methodName = node.Method.Name;

        // Handle both the async marker and any direct calls that somehow got through
        if (!IsToListOperation(methodName))
        {
            logger?.LogDebug("ToListMethodHandler: not a ToList-related method");
            return false;
        }

        if (node.Arguments.Count < 1)
        {
            logger?.LogDebug("ToListMethodHandler: wrong number of arguments");
            return false;
        }

        // ToList is a terminal operation - finalize any pending traversal patterns
        FinalizeTraversalPatterns(context);

        // If no explicit return has been set, add a default return
        if (!context.Builder.HasReturnClause)
        {
            var alias = context.Scope.CurrentAlias
                ?? throw new InvalidOperationException("No current alias set when building ToList return clause");
            logger?.LogDebug($"ToListMethodHandler: adding default return for alias {alias}");
            context.Builder.AddReturn(alias);
        }

        logger?.LogDebug("ToListMethodHandler: completed successfully");
        return true;
    }

    private static bool IsToListOperation(string methodName) =>
            methodName is "ToListAsyncMarker" or "ToArrayAsyncMarker" or "ToDictionaryAsyncMarker" or "ToLookupAsyncMarker";

    private static void FinalizeTraversalPatterns(CypherQueryContext context)
    {
        var logger = context.LoggerFactory?.CreateLogger(nameof(ToListMethodHandler));
        logger?.LogDebug("FinalizeTraversalPatterns called");

        // Check if this is a relationship query
        if (context.Builder.IsRelationshipQuery)
        {
            logger?.LogDebug("This is a relationship query, skipping complex property loading");
            return;
        }

        var rootType = context.Scope.RootType;

        // If the root type is a scalar/primitive, skip complex property loading entirely
        if (IsScalarOrPrimitive(rootType))
        {
            logger?.LogDebug("Root type is scalar/primitive, skipping complex property loading");
            var builderType = context.Builder.GetType();
            builderType.GetField("_includeComplexProperties", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(context.Builder, false);
            builderType.GetField("_loadPathSegment", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(context.Builder, false);
            return;
        }

        // For path segments, only enable complex property loading if the projection is a node, path segment, or relationship
        if (typeof(IGraphPathSegment).IsAssignableFrom(rootType))
        {
            logger?.LogDebug("Root type is IGraphPathSegment, checking for user projections...");

            // Only enable complex property loading if there are no user projections (i.e., returning the full path segment)
            if (!context.Builder.HasUserProjections)
            {
                logger?.LogDebug("Enabling complex property loading for path segment query (no user projections)");
                context.Builder.EnablePathSegmentLoading();
            }
            else
            {
                logger?.LogDebug("Skipping complex property loading for path segment query (user projections present)");
            }
            return;
        }

        // For regular node queries, only enable if no explicit user projections
        if (typeof(INode).IsAssignableFrom(rootType))
        {
            if (context.Builder.NeedsComplexProperties(rootType))
            {
                logger?.LogDebug("Enabling complex property loading for node query");
                context.Builder.EnableComplexPropertyLoading();
            }
            else
            {
                logger?.LogDebug("Skipping complex property loading - query has explicit user projections");
            }
            return;
        }

        logger?.LogDebug("Root type is not a node or path segment, skipping complex property loading");
    }

    private static bool IsScalarOrPrimitive(Type type)
    {
        // Covers primitives, enums, strings, Guids, DateTime, etc.
        if (type.IsPrimitive || type.IsEnum)
            return true;

        if (type == typeof(string) || type == typeof(Guid) || type == typeof(DateTime) || type == typeof(DateTimeOffset) || type == typeof(decimal))
            return true;

        // Nullable<T> where T is scalar
        if (Nullable.GetUnderlyingType(type) is { } underlying)
            return IsScalarOrPrimitive(underlying);

        return false;
    }
}
