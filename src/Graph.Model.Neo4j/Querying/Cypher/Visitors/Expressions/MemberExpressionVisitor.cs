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

namespace Cvoya.Graph.Model.Neo4j.Querying.Cypher.Visitors.Expressions;

using System.Linq.Expressions;
using Cvoya.Graph.Model.Neo4j.Querying.Cypher.Visitors.Core;
using Microsoft.Extensions.Logging;
using static Cvoya.Graph.Model.Neo4j.Querying.Cypher.Builders.CypherQueryBuilder;

/// <summary>
/// Handles member access expressions and enables complex property loading when needed.
/// </summary>
internal class MemberExpressionVisitor(string? alias, CypherQueryContext context, ICypherExpressionVisitor? nextVisitor = null)
    : CypherExpressionVisitorBase<MemberExpressionVisitor>(context, nextVisitor)
{
    private readonly string? _alias = alias;

    public override string Visit(Expression expression)
    {
        if (expression is null)
        {
            throw new ArgumentNullException(nameof(expression), "Expression cannot be null");
        }

        Logger.LogDebug("MemberExpressionVisitor.Visit called with alias: {Alias}, Expression type: {Type}",
            _alias, expression.GetType().Name);
        return base.Visit(expression);
    }

    public override string VisitMember(MemberExpression node)
    {
        Logger.LogDebug("MemberExpressionVisitor.VisitMember called with alias: {Alias}, Expression: {Expression}", _alias, node);
        Logger.LogDebug("Node.Expression type: {Type}, IsParameterExpression: {IsParam}", node.Expression?.GetType(), node.Expression is ParameterExpression);
        Logger.LogDebug("IsPathSegmentContext: {IsPathSegment}", Context.Scope.IsPathSegmentContext);

        // Check if this is accessing a path segment property
        if (node.Expression is ParameterExpression param && Context.Scope.IsPathSegmentContext)
        {
            Logger.LogDebug("Path segment property access: {MemberName}", node.Member.Name);

            // Check if this is actually a path segment property (StartNode, EndNode, Relationship)
            if (node.Member.Name is nameof(IGraphPathSegment.StartNode) or
                                    nameof(IGraphPathSegment.EndNode) or
                                    nameof(IGraphPathSegment.Relationship))
            {
                // Map path segment properties to the correct aliases
                var propertyMapping = node.Member.Name switch
                {
                    nameof(IGraphPathSegment.StartNode) => Context.Builder.PathSegmentSourceAlias ?? "src",
                    nameof(IGraphPathSegment.EndNode) => Context.Builder.PathSegmentTargetAlias ?? "tgt",
                    nameof(IGraphPathSegment.Relationship) => Context.Builder.PathSegmentRelationshipAlias ?? "r",
                    _ => throw new NotSupportedException($"Path segment property '{node.Member.Name}' is not supported")
                };

                Logger.LogDebug("Mapped path segment property {Property} to alias {Alias}", node.Member.Name, propertyMapping);
                return propertyMapping;
            }
            else
            {
                // This is a regular property access on a projected node/relationship
                // Use the current context alias (which should be set by the projection)
                var currentAlias = Context.Scope.CurrentAlias;
                if (string.IsNullOrEmpty(currentAlias))
                {
                    Logger.LogWarning("No current alias available for property access: {Property}", node.Member.Name);
                    // Fall back to determining alias based on projection context
                    currentAlias = DetermineAliasFromProjectionContext();
                }

                Logger.LogDebug("Using current alias {Alias} for property {Property}", currentAlias, node.Member.Name);
                return $"{currentAlias}.{node.Member.Name}";
            }
        }

        // Handle other member expressions using the next visitor in the chain
        return NextVisitor?.VisitMember(node) ?? HandleComplexPropertyNavigation(node);
    }

    private string DetermineAliasFromProjectionContext()
    {
        // Check what was projected in the path segment context
        var projection = Context.Builder.PathSegmentProjection;
        return projection switch
        {
            PathSegmentProjectionEnum.EndNode => Context.Builder.PathSegmentTargetAlias ?? "tgt",
            PathSegmentProjectionEnum.StartNode => Context.Builder.PathSegmentSourceAlias ?? "src",
            PathSegmentProjectionEnum.Relationship => Context.Builder.PathSegmentRelationshipAlias ?? "r",
            _ => "src" // Default fallback
        };
    }

    public override string VisitBinary(BinaryExpression node) =>
        NextVisitor?.VisitBinary(node) ?? throw new InvalidOperationException("No next visitor available for binary expression");

    public override string VisitUnary(UnaryExpression node) =>
        NextVisitor?.VisitUnary(node) ?? throw new InvalidOperationException("No next visitor available for unary expression");

    public override string VisitMethodCall(MethodCallExpression node) =>
        NextVisitor?.VisitMethodCall(node) ?? throw new InvalidOperationException("No next visitor available for method call expression");

    public override string VisitConstant(ConstantExpression node) =>
        NextVisitor?.VisitConstant(node) ?? throw new InvalidOperationException("No next visitor available for constant expression");

    public override string VisitParameter(ParameterExpression node) =>
        NextVisitor?.VisitParameter(node) ?? throw new InvalidOperationException("No next visitor available for parameter expression");

    private bool IsComplexPropertyNavigation(MemberExpression node)
    {
        // Check if we're navigating through a complex property
        // This would be something like p.Address.Street where Address is a complex property

        if (node.Expression is not MemberExpression parentMember)
            return false;

        // Check if the parent member is a complex property
        var parentPropertyType = parentMember.Type;
        return !GraphDataModel.IsSimple(parentPropertyType);
    }

    private string HandleComplexPropertyNavigation(MemberExpression node)
    {
        // TODO: implement complex property navigation handling

        // For now, throw an exception to clearly indicate this needs implementation
        // In the future, this will generate the appropriate MATCH patterns and joins
        throw new NotSupportedException(
            $"Complex property navigation '{node}' is not yet supported. " +
            "This will be implemented to generate appropriate MATCH patterns for complex property relationships.");
    }
}