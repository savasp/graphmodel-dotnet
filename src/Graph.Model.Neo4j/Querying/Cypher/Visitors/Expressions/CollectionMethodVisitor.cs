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

internal class CollectionMethodVisitor(
    CypherQueryContext context, ICypherExpressionVisitor innerVisitor)
    : CypherExpressionVisitorBase<CollectionMethodVisitor>(context, innerVisitor)
{
    public override string VisitMethodCall(MethodCallExpression node)
    {
        // Handle conversion operators (op_Implicit, op_Explicit)
        if (node.Method.Name.StartsWith("op_") && node.Arguments.Count == 1)
        {
            Logger.LogDebug("Processing conversion operator: {Method}", node.Method.Name);
            // Just visit the argument, ignoring the conversion
            return NextVisitor!.Visit(node.Arguments[0]);
        }

        // Check if it's a Contains method on a collection
        if (node.Method.Name == "Contains")
        {
            Logger.LogDebug("Processing Contains method call");

            // Handle instance Contains method (e.g., list.Contains(item))
            if (node.Object != null)
            {
                var collection = NextVisitor!.Visit(node.Object);
                var value = NextVisitor!.Visit(node.Arguments[0]);

                Logger.LogDebug("Generating IN expression for Contains: {Value} IN {Collection}", value, collection);
                return $"{value} IN {collection}";
            }
            // Handle static Contains method (e.g., Enumerable.Contains(list, item))
            else if (node.Arguments.Count == 2)
            {
                var collection = NextVisitor!.Visit(node.Arguments[0]);
                var value = NextVisitor!.Visit(node.Arguments[1]);

                Logger.LogDebug("Generating IN expression for static Contains: {Value} IN {Collection}", value, collection);
                return $"{value} IN {collection}";
            }
        }

        // Check for other Enumerable methods
        if (node.Method.DeclaringType != typeof(Enumerable))
        {
            return NextVisitor!.VisitMethodCall(node);
        }

        Logger.LogDebug("Visiting collection method: {MethodName}", node.Method.Name);

        var collectionArg = NextVisitor!.Visit(node.Arguments[0]);

        if (node.Arguments.Count > 1)
        {
            var predicate = NextVisitor!.Visit(node.Arguments[1]);

            var expression = node.Method.Name switch
            {
                "Any" => $"ANY(x IN {collectionArg} WHERE {predicate})",
                "All" => $"ALL(x IN {collectionArg} WHERE {predicate})",
                "None" => $"NONE(x IN {collectionArg} WHERE {predicate})",
                "Single" => $"SINGLE(x IN {collectionArg} WHERE {predicate})",
                _ => throw new NotSupportedException($"Collection method {node.Method.Name} is not supported")
            };

            Logger.LogDebug("Collection method result: {Expression}", expression);
            return expression;
        }
        else
        {
            var expression = node.Method.Name switch
            {
                "Any" => $"SIZE({collectionArg}) > 0",
                "Count" => $"SIZE({collectionArg})",
                _ => throw new NotSupportedException($"Collection method {node.Method.Name} is not supported")
            };

            Logger.LogDebug("Collection method result: {Expression}", expression);
            return expression;
        }
    }

    public override string VisitBinary(BinaryExpression node) => NextVisitor!.VisitBinary(node);
    public override string VisitUnary(UnaryExpression node) => NextVisitor!.VisitUnary(node);
    public override string VisitMember(MemberExpression node) => NextVisitor!.VisitMember(node);
    public override string VisitConstant(ConstantExpression node) => NextVisitor!.VisitConstant(node);
    public override string VisitParameter(ParameterExpression node) => NextVisitor!.VisitParameter(node);
}