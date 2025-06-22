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

internal class DateTimeMethodVisitor(
    CypherQueryContext context, ICypherExpressionVisitor nextVisitor)
    : CypherExpressionVisitorBase<DateTimeMethodVisitor>(context, nextVisitor)
{
    public override string VisitMethodCall(MethodCallExpression node)
    {
        if (node.Method.DeclaringType != typeof(DateTime) &&
            node.Method.DeclaringType != typeof(DateTimeOffset) &&
            node.Method.DeclaringType != typeof(DateOnly) &&
            node.Method.DeclaringType != typeof(TimeOnly))
        {
            return NextVisitor?.VisitMethodCall(node)
                ?? throw new NotSupportedException($"DateTime method {node.Method.Name} is not supported");
        }

        Logger.LogDebug("Visiting DateTime method: {MethodName}", node.Method.Name);

        var target = NextVisitor?.Visit(node.Object!)
            ?? throw new NotSupportedException("Cannot process DateTime target");

        var arguments = node.Arguments.Select(arg => NextVisitor?.Visit(arg)
            ?? throw new NotSupportedException("Cannot process DateTime argument")).ToList();

        var expression = node.Method.Name switch
        {
            "AddYears" => $"date.addYears({target}, {arguments[0]})",
            "AddMonths" => $"date.addMonths({target}, {arguments[0]})",
            "AddDays" => $"date.addDays({target}, {arguments[0]})",
            "AddHours" => $"date.addHours({target}, {arguments[0]})",
            "AddMinutes" => $"date.addMinutes({target}, {arguments[0]})",
            "AddSeconds" => $"date.addSeconds({target}, {arguments[0]})",
            "ToUniversalTime" => $"date.toUtc({target})",
            "ToLocalTime" => $"date.toLocal({target})",
            "ToString" => arguments.Count == 0
                ? $"date.format({target}, 'yyyy-MM-dd')"
                : $"date.format({target}, {arguments[0]})",
            _ => throw new NotSupportedException($"DateTime method {node.Method.Name} is not supported")
        };

        Logger.LogDebug("DateTime method result: {Expression}", expression);
        return expression;
    }

    public override string VisitBinary(BinaryExpression node) => NextVisitor?.VisitBinary(node)
        ?? throw new NotSupportedException($"Binary expression {node.NodeType} is not supported");

    public override string VisitUnary(UnaryExpression node) => NextVisitor?.VisitUnary(node)
        ?? throw new NotSupportedException($"Unary expression {node.NodeType} is not supported");

    public override string VisitMember(MemberExpression node) => NextVisitor?.VisitMember(node)
        ?? throw new NotSupportedException($"Member expression {node.NodeType} is not supported");

    public override string VisitConstant(ConstantExpression node) => NextVisitor?.VisitConstant(node)
        ?? throw new NotSupportedException($"Constant expression {node.NodeType} is not supported");

    public override string VisitParameter(ParameterExpression node) => NextVisitor?.VisitParameter(node)
        ?? throw new NotSupportedException($"Parameter expression {node.NodeType} is not supported");
}