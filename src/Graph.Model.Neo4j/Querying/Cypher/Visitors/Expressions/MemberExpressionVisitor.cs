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

        // Check if this is accessing a complex property chain
        if (IsComplexPropertyNavigation(node))
        {
            Logger.LogDebug("Complex property navigation detected: {Expression}", node);
            return HandleComplexPropertyNavigation(node);
        }

        // If this is a simple property access on the root parameter, use the alias
        if (node.Expression is ParameterExpression && !string.IsNullOrEmpty(_alias))
        {
            Logger.LogDebug("Simple property access on root parameter: {MemberName}", node.Member.Name);
            Logger.LogDebug("Using alias: {Alias}", _alias);
            Logger.LogDebug("Final result: {Result}", $"{_alias}.{node.Member.Name}");
            return $"{_alias}.{node.Member.Name}";
        }

        // Delegate to the next visitor for other cases
        Logger.LogDebug("Delegating to next visitor because no conditions matched");
        Logger.LogDebug("_alias is: {Alias}", _alias);
        Logger.LogDebug("node.Expression is ParameterExpression: {IsParam}", node.Expression is ParameterExpression);
        return NextVisitor?.VisitMember(node) ?? throw new InvalidOperationException("No next visitor available for member expression");
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