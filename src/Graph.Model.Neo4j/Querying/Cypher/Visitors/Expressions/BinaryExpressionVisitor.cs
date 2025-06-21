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

internal class BinaryExpressionVisitor(
    CypherQueryContext context, ICypherExpressionVisitor nextVisitor)
    : CypherExpressionVisitorBase<BinaryExpression>(context, nextVisitor)
{
    public override string VisitBinary(BinaryExpression node)
    {
        Logger.LogDebug("BinaryExpressionVisitor.VisitBinary called with NodeType: {NodeType}", node.NodeType);
        Logger.LogDebug("Visiting binary expression: {NodeType}", node.NodeType);
        Logger.LogDebug("Left expression type: {LeftType}, Node: {LeftNode}", node.Left?.GetType().FullName, node.Left);
        Logger.LogDebug("Right expression type: {RightType}, Node: {RightNode}", node.Right?.GetType().FullName, node.Right);

        // Get the root visitor to ensure we go through the entire chain
        var rootVisitor = GetRootVisitor();

        // Get the root visitor from context or use ourselves
        var visitor = Context.RootExpressionVisitor ?? this;

        // Use the root visitor to process sub-expressions
        string left = "NULL";
        if (node.Left != null)
        {
            Logger.LogDebug("Visiting left expression of type: {Type}", node.Left.GetType().Name);
            Logger.LogDebug("Expression: {Expression}", node.Left);
            left = visitor.Visit(node.Left);
        }

        string right = "NULL";
        if (node.Right != null)
        {
            Logger.LogDebug("Visiting right expression of type: {Type}", node.Right.GetType().Name);
            Logger.LogDebug("Expression: {Expression}", node.Right);
            right = visitor.Visit(node.Right);
        }

        Logger.LogDebug("Binary expression left: {Left}, right: {Right}", left, right);

        // For OR conditions, check if we're comparing the same property with different values
        if (node.NodeType == ExpressionType.OrElse)
        {
            var leftParts = left.Split('=').Select(p => p.Trim()).ToArray();
            var rightParts = right.Split('=').Select(p => p.Trim()).ToArray();

            if (leftParts.Length == 2 && rightParts.Length == 2 &&
                leftParts[0] == rightParts[0])
            {
                // Same property being compared, combine the values
                var expr = $"{leftParts[0]} = {leftParts[1]} OR {rightParts[0]} = {rightParts[1]}";
                Logger.LogDebug("Combined OR expression: {Expression}", expr);
                return expr;
            }
        }

        // For AND conditions, check if we're duplicating the same condition
        if (node.NodeType == ExpressionType.AndAlso && left == right)
        {
            return left;
        }

        // Handle all binary expression types
        Logger.LogDebug("Processing binary expression with NodeType: {NodeType}", node.NodeType);
        var expression = node.NodeType switch
        {
            ExpressionType.Equal => $"{left} = {right}",
            ExpressionType.NotEqual => $"{left} <> {right}",
            ExpressionType.GreaterThan => $"{left} > {right}",
            ExpressionType.GreaterThanOrEqual => $"{left} >= {right}",
            ExpressionType.LessThan => $"{left} < {right}",
            ExpressionType.LessThanOrEqual => $"{left} <= {right}",
            ExpressionType.AndAlso => $"({left} AND {right})",
            ExpressionType.OrElse => $"({left} OR {right})",
            ExpressionType.Add => $"{left} + {right}",
            ExpressionType.Subtract => $"{left} - {right}",
            ExpressionType.Multiply => $"{left} * {right}",
            ExpressionType.Divide => $"{left} / {right}",
            ExpressionType.Modulo => $"{left} % {right}",
            ExpressionType.Power => $"POWER({left}, {right})",
            ExpressionType.Coalesce => $"COALESCE({left}, {right})",
            _ => throw new NotSupportedException($"Binary operator {node.NodeType} is not supported")
        };
        Logger.LogDebug("Generated expression for {NodeType}: {Expression}", node.NodeType, expression);

        Logger.LogDebug("Binary expression result: {Expression}", expression);
        return expression;
    }

    public override string VisitUnary(UnaryExpression node) => NextVisitor!.VisitUnary(node);
    public override string VisitMember(MemberExpression node) => NextVisitor!.VisitMember(node);
    public override string VisitMethodCall(MethodCallExpression node) => NextVisitor!.VisitMethodCall(node);
    public override string VisitConstant(ConstantExpression node) => NextVisitor!.VisitConstant(node);
    public override string VisitParameter(ParameterExpression node) => NextVisitor!.VisitParameter(node);

    private ICypherExpressionVisitor GetRootVisitor()
    {
        // Walk back through the chain to find the root visitor
        var current = this as ICypherExpressionVisitor;
        var visited = new HashSet<ICypherExpressionVisitor>();

        while (current != null)
        {
            if (!visited.Add(current))
                break; // Prevent infinite loops

            var next = current switch
            {
                MemberExpressionVisitor m => m.NextVisitor,
                BinaryExpressionVisitor b => b.NextVisitor,
                BaseExpressionVisitor b => null, // Base is the end
                _ => null
            };

            if (next == null)
                break;

            // Check if next is a MemberExpressionVisitor - if so, that's our root
            if (next is MemberExpressionVisitor)
                return next;

            current = next;
        }

        // If we can't find a MemberExpressionVisitor, use ourselves
        return this;
    }
}