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
internal class MemberExpressionVisitor(CypherQueryContext context, ICypherExpressionVisitor? nextVisitor = null)
    : CypherExpressionVisitorBase<MemberExpressionVisitor>(context, nextVisitor)
{
    public override string VisitMember(MemberExpression node)
    {
        // Check if this is accessing a complex property chain
        if (IsComplexPropertyNavigation(node))
        {
            Logger.LogDebug("Complex property navigation detected: {Expression}", node);
            return HandleComplexPropertyNavigation(node);
        }

        // Delegate to the next visitor for regular member access
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