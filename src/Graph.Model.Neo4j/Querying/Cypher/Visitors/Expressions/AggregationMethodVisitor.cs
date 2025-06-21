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

internal class AggregationMethodVisitor(
    CypherQueryContext context,
    ICypherExpressionVisitor? innerVisitor)
    : CypherExpressionVisitorBase<AggregationMethodVisitor>(context, innerVisitor)
{
    public override string VisitMethodCall(MethodCallExpression node)
    {
        // Check if it's an aggregation method
        if (IsAggregationMethod(node.Method.Name))
        {
            return node.Method.Name.ToLower() switch
            {
                "count" or "longcount" => HandleCount(node),
                "sum" => HandleAggregate(node, "sum"),
                "average" => HandleAggregate(node, "avg"),
                "min" => HandleAggregate(node, "min"),
                "max" => HandleAggregate(node, "max"),
                _ => NextVisitor?.VisitMethodCall(node) ?? throw new NotSupportedException($"Aggregation {node.Method.Name} not supported")
            };
        }

        return NextVisitor?.VisitMethodCall(node) ?? throw new NotSupportedException($"Method {node.Method.Name} not supported");
    }

    private static bool IsAggregationMethod(string methodName) =>
        methodName is "Count" or "LongCount" or "Sum" or "Average" or "Min" or "Max";

    private string HandleCount(MethodCallExpression node)
    {
        if (node.Arguments.Count == 0)
        {
            // Count() on the collection itself
            var target = node.Object != null ? Visit(node.Object) : Scope.CurrentAlias
                ?? throw new InvalidOperationException("No current alias set when building Count() function");
            return $"count({target})";
        }

        // Count with predicate would be handled differently
        return $"count({Visit(node.Arguments[0])})";
    }

    private string HandleAggregate(MethodCallExpression node, string cypherFunction)
    {
        var target = node.Arguments.Count > 0
            ? Visit(node.Arguments[0])
            : Scope.CurrentAlias
              ?? throw new InvalidOperationException($"No current alias set when building {cypherFunction} function");

        return $"{cypherFunction}({target})";
    }

    public override string VisitBinary(BinaryExpression node) => NextVisitor!.VisitBinary(node);
    public override string VisitUnary(UnaryExpression node) => NextVisitor!.VisitUnary(node);
    public override string VisitMember(MemberExpression node) => NextVisitor!.VisitMember(node);
    public override string VisitConstant(ConstantExpression node) => NextVisitor!.VisitConstant(node);
    public override string VisitParameter(ParameterExpression node) => NextVisitor!.VisitParameter(node);
}