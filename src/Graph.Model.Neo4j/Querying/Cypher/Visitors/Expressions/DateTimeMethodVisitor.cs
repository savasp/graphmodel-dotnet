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

        if (node.Object is MemberExpression member &&
                member.Member.DeclaringType == typeof(DateTime) &&
                member.Expression == null) // static property
        {
            Logger.LogDebug("Processing method call on DateTime static property: {Property}.{Method}",
                member.Member.Name, node.Method.Name);

            // First translate the static property
            var dateTimeExpr = member.Member.Name switch
            {
                "Now" => "datetime()",
                "UtcNow" => "datetime.realtime()",
                "Today" => "date()",
                _ => throw new NotSupportedException($"DateTime static property {member.Member.Name} is not supported")
            };

            // Then apply the method
            var args = node.Arguments.Select(arg => Visit(arg)).ToList();

            var result = node.Method.Name switch
            {
                "AddYears" => $"{dateTimeExpr} + duration({{years: {args[0]}}})",
                "AddMonths" => $"{dateTimeExpr} + duration({{months: {args[0]}}})",
                "AddDays" => $"{dateTimeExpr} + duration({{days: {args[0]}}})",
                "AddHours" => $"{dateTimeExpr} + duration({{hours: {args[0]}}})",
                "AddMinutes" => $"{dateTimeExpr} + duration({{minutes: {args[0]}}})",
                "AddSeconds" => $"{dateTimeExpr} + duration({{seconds: {args[0]}}})",
                _ => throw new NotSupportedException($"DateTime method {node.Method.Name} is not supported")
            };

            Logger.LogDebug("Translated DateTime.{Property}.{Method} to {Result}",
                member.Member.Name, node.Method.Name, result);
            return result;
        }

        // Handle static methods/properties
        if (node.Object == null)
        {
            var expr = node.Method.Name switch
            {
                "get_Now" => "datetime()",
                "get_UtcNow" => "datetime.realtime()",
                "get_Today" => "date()",
                _ => throw new NotSupportedException($"Static DateTime method {node.Method.Name} is not supported")
            };

            Logger.LogDebug("Static DateTime method result: {Expression}", expr);
            return expr;
        }

        var target = NextVisitor?.Visit(node.Object!)
            ?? throw new NotSupportedException("Cannot process DateTime target");

        var arguments = node.Arguments.Select(arg => NextVisitor?.Visit(arg)
            ?? throw new NotSupportedException("Cannot process DateTime argument")).ToList();

        var expression = node.Method.Name switch
        {
            "AddYears" when arguments.Count == 1 => $"datetime({target}) + duration({{years: {arguments[0]}}})",
            "AddMonths" when arguments.Count == 1 => $"datetime({target}) + duration({{months: {arguments[0]}}})",
            "AddDays" when arguments.Count == 1 => $"datetime({target}) + duration({{days: {arguments[0]}}})",
            "AddHours" when arguments.Count == 1 => $"datetime({target}) + duration({{hours: {arguments[0]}}})",
            "AddMinutes" when arguments.Count == 1 => $"datetime({target}) + duration({{minutes: {arguments[0]}}})",
            "AddSeconds" when arguments.Count == 1 => $"datetime({target}) + duration({{seconds: {arguments[0]}}})",
            "AddMilliseconds" when arguments.Count == 1 => $"datetime({target}) + duration({{milliseconds: {arguments[0]}}})",

            // Property accessors
            "get_Year" => $"datetime({target}).year",
            "get_Month" => $"datetime({target}).month",
            "get_Day" => $"datetime({target}).day",
            "get_Hour" => $"datetime({target}).hour",
            "get_Minute" => $"datetime({target}).minute",
            "get_Second" => $"datetime({target}).second",
            "get_Millisecond" => $"datetime({target}).millisecond",
            "get_DayOfWeek" => $"datetime({target}).dayOfWeek",
            "get_DayOfYear" => $"datetime({target}).ordinalDay",
            "get_Date" => $"date({target})",
            "get_TimeOfDay" => $"time({target})",

            // Conversion methods
            "ToUniversalTime" => $"datetime({target})",
            "ToLocalTime" => $"datetime({target})",
            "ToString" => arguments.Count == 0
                ? $"toString({target})"
                : $"toString({target})",

            _ => throw new NotSupportedException($"DateTime method {node.Method.Name} is not supported")
        };

        Logger.LogDebug("DateTime method result: {Expression}", expression);
        return expression;
    }

    public override string VisitMember(MemberExpression node)
    {
        // Handle DateTime static properties like DateTime.UtcNow
        if (node.Member.DeclaringType == typeof(DateTime))
        {
            var expression = node.Member.Name switch
            {
                "Now" => "datetime()",
                "UtcNow" => "datetime.realtime()",
                "Today" => "date()",
                _ => null
            };

            if (expression != null)
            {
                Logger.LogDebug("DateTime static property {Property} translated to {Expression}", node.Member.Name, expression);
                return expression;
            }
        }

        return NextVisitor?.VisitMember(node)
            ?? throw new NotSupportedException($"Member expression {node.Member.Name} is not supported");
    }

    public override string VisitBinary(BinaryExpression node)
    {
        // Handle comparisons involving DateTime
        if (node.Left.Type == typeof(DateTime) || node.Right.Type == typeof(DateTime))
        {
            var left = Visit(node.Left);
            var right = Visit(node.Right);

            var operatorSymbol = node.NodeType switch
            {
                ExpressionType.LessThan => "<",
                ExpressionType.LessThanOrEqual => "<=",
                ExpressionType.GreaterThan => ">",
                ExpressionType.GreaterThanOrEqual => ">=",
                ExpressionType.Equal => "=",
                ExpressionType.NotEqual => "<>",
                _ => throw new NotSupportedException($"Binary operator {node.NodeType} is not supported for DateTime")
            };

            Logger.LogDebug("Translated DateTime binary expression: {Left} {Operator} {Right}", left, operatorSymbol, right);
            return $"{left} {operatorSymbol} {right}";
        }

        // Delegate to the next visitor if the binary expression is not related to DateTime
        return NextVisitor?.VisitBinary(node)
            ?? throw new NotSupportedException($"Binary expression {node.NodeType} is not supported");
    }

    public override string VisitUnary(UnaryExpression node) => NextVisitor?.VisitUnary(node)
        ?? throw new NotSupportedException($"Unary expression {node.NodeType} is not supported");

    public override string VisitConstant(ConstantExpression node) => NextVisitor?.VisitConstant(node)
        ?? throw new NotSupportedException($"Constant expression {node.NodeType} is not supported");

    public override string VisitParameter(ParameterExpression node) => NextVisitor?.VisitParameter(node)
        ?? throw new NotSupportedException($"Parameter expression {node.NodeType} is not supported");
}