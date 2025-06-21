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

namespace Cvoya.Graph.Model.Neo4j.Querying.Cypher.Visitors;

using System.Linq.Expressions;
using Cvoya.Graph.Model.Neo4j.Querying.Cypher.Visitors.Core;
using Microsoft.Extensions.Logging;

internal sealed class PathSegmentVisitor(CypherQueryContext context) : CypherVisitorBase<PathSegmentVisitor>(context)
{
    public override Expression? Visit(Expression? node)
    {
        if (node is MethodCallExpression methodCall &&
            methodCall.Method.Name == "PathSegments")
        {
            return VisitPathSegmentsMethodCall(methodCall);
        }

        return base.Visit(node);
    }

    protected override Expression VisitMethodCall(MethodCallExpression node)
    {
        Logger.LogDebug("Visiting method call: {MethodName}", node.Method.Name);

        // Add this to see what methods are actually being processed
        if (node.Method.Name == "ToList")
        {
            Logger.LogDebug("Found ToList call! Arguments: {Count}", node.Arguments.Count);
        }


        if (node.Method.Name == "PathSegments")
        {
            Logger.LogDebug("Found PathSegments call! Arguments: {Count}", node.Arguments.Count);
            return VisitPathSegmentsMethodCall(node);
        }

        return base.VisitMethodCall(node);
    }

    private Expression VisitPathSegmentsMethodCall(MethodCallExpression methodCall)
    {
        Logger.LogDebug("Processing PathSegments method call");

        if (Scope.TraversalInfo is not null)
        {
            BuildPathSegmentQuery();
        }

        // Continue visiting the object expression (the queryable)
        return Visit(methodCall.Object) ?? methodCall;
    }

    private void BuildPathSegmentQuery()
    {
        if (!Scope.IsPathSegmentContext || Scope.TraversalInfo is null)
        {
            Logger.LogDebug("Not in path segment context, skipping");
            return;
        }

        // Don't clear matches or build the pattern immediately
        // Instead, just store the traversal info and defer pattern building
        var sourceType = Scope.TraversalInfo.SourceNodeType;
        var relType = Scope.TraversalInfo.RelationshipType;
        var targetType = Scope.TraversalInfo.TargetNodeType;

        Logger.LogDebug("Building path segment query for {SourceType} -> {RelType} -> {TargetType}",
                    sourceType.Name, relType.Name, targetType.Name);

        var sourceAlias = Scope.GetOrCreateAlias(sourceType, "src");
        var relAlias = Scope.GetOrCreateAlias(relType, "r");
        var targetAlias = Scope.GetOrCreateAlias(targetType, "tgt");

        // Mark that we need to build the path segment pattern later
        Builder.SetPendingPathSegmentPattern(sourceType, relType, targetType, sourceAlias, relAlias, targetAlias);

        Builder.EnablePathSegmentLoading();
        Scope.CurrentAlias = sourceAlias;
    }
}