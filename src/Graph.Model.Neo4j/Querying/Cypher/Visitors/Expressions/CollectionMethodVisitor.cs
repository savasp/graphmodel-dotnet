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
using Cvoya.Graph.Model.Neo4j.Querying.Cypher.Builders;
using Microsoft.Extensions.Logging;

internal class CollectionMethodVisitor(
    ICypherExpressionVisitor innerVisitor,
    CypherQueryScope scope,
    CypherQueryBuilder builder) : CypherExpressionVisitorBase<CollectionMethodVisitor>(scope, builder)
{
    private readonly ICypherExpressionVisitor _innerVisitor = innerVisitor;

    public override string VisitMethodCall(MethodCallExpression node)
    {
        if (node.Method.DeclaringType != typeof(Enumerable))
        {
            return _innerVisitor.VisitMethodCall(node);
        }

        Logger.LogDebug("Visiting collection method: {MethodName}", node.Method.Name);

        var collection = _innerVisitor.Visit(node.Arguments[0]);

        if (node.Arguments.Count > 1)
        {
            var predicate = _innerVisitor.Visit(node.Arguments[1]);

            var expression = node.Method.Name switch
            {
                "Any" => $"ANY(x IN {collection} WHERE {predicate})",
                "All" => $"ALL(x IN {collection} WHERE {predicate})",
                "None" => $"NONE(x IN {collection} WHERE {predicate})",
                "Single" => $"SINGLE(x IN {collection} WHERE {predicate})",
                _ => throw new NotSupportedException($"Collection method {node.Method.Name} is not supported")
            };

            Logger.LogDebug("Collection method result: {Expression}", expression);
            return expression;
        }
        else
        {
            var expression = node.Method.Name switch
            {
                "Any" => $"SIZE({collection}) > 0",
                "Count" => $"SIZE({collection})",
                _ => throw new NotSupportedException($"Collection method {node.Method.Name} is not supported")
            };

            Logger.LogDebug("Collection method result: {Expression}", expression);
            return expression;
        }
    }

    public override string VisitBinary(BinaryExpression node) => _innerVisitor.VisitBinary(node);
    public override string VisitUnary(UnaryExpression node) => _innerVisitor.VisitUnary(node);
    public override string VisitMember(MemberExpression node) => _innerVisitor.VisitMember(node);
    public override string VisitConstant(ConstantExpression node) => _innerVisitor.VisitConstant(node);
    public override string VisitParameter(ParameterExpression node) => _innerVisitor.VisitParameter(node);
}