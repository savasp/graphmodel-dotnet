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

        // Handle path segment property access first
        if (node.Expression is ParameterExpression param && Context.Scope.IsPathSegmentContext)
        {
            Logger.LogDebug("Path segment property access: {MemberName}", node.Member.Name);
            return HandlePathSegmentPropertyAccess(node);
        }

        // Check if this is a complex property navigation (nested member access)
        if (HasComplexPropertyNavigation(node))
        {
            Logger.LogDebug("Complex property navigation detected, delegating to next visitor");
            // Delegate to the next visitor in the chain (should be BaseExpressionVisitor)
            return NextVisitor?.VisitMember(node) ?? throw new InvalidOperationException("No next visitor available for complex property navigation");
        }

        // Handle regular property access
        return HandleRegularPropertyAccess(node);
    }

    private bool HasComplexPropertyNavigation(MemberExpression node)
    {
        // Check if the expression involves nested member access beyond a parameter
        // This would be something like p.Address.City where node.Expression is p.Address (another MemberExpression)
        return node.Expression is MemberExpression memberExpr &&
               memberExpr.Expression is not null; // Make sure we have a deeper expression
    }

    private string HandlePathSegmentPropertyAccess(MemberExpression node)
    {
        // Map path segment properties (StartNode, EndNode, Relationship) to aliases
        return node.Member.Name switch
        {
            nameof(IGraphPathSegment.StartNode) => Context.Builder.PathSegmentSourceAlias ?? "src",
            nameof(IGraphPathSegment.EndNode) => Context.Builder.PathSegmentTargetAlias ?? "tgt",
            nameof(IGraphPathSegment.Relationship) => Context.Builder.PathSegmentRelationshipAlias ?? "r",
            _ => throw new NotSupportedException($"Path segment property '{node.Member.Name}' is not supported")
        };
    }

    private string HandleRegularPropertyAccess(MemberExpression node)
    {
        // Use the current alias for regular property access
        var currentAlias = Context.Scope.CurrentAlias;
        if (string.IsNullOrEmpty(currentAlias))
        {
            Logger.LogWarning("No current alias available for property access: {Property}", node.Member.Name);
            currentAlias = DetermineAliasFromProjectionContext();
        }

        Logger.LogDebug("Using current alias {Alias} for property {Property}", currentAlias, node.Member.Name);
        return $"{currentAlias}.{node.Member.Name}";
    }

    private string DetermineAliasFromProjectionContext()
    {
        // Determine alias based on the projection context
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

}