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

namespace Cvoya.Graph.Model.Neo4j.Querying.Cypher.Visitors.Handlers;

using System.Linq.Expressions;
using Cvoya.Graph.Model.Neo4j.Querying.Cypher.Visitors.Core;
using Cvoya.Graph.Model.Neo4j.Querying.Cypher.Visitors.Expressions;

/// <summary>
/// Handles DateTime manipulation methods for Cypher queries.
/// </summary>
internal record DateTimeMethodHandler : MethodHandlerBase
{
    public override bool Handle(CypherQueryContext context, MethodCallExpression node, Expression result)
    {
        if (node.Method.DeclaringType != typeof(DateTime) &&
            node.Method.DeclaringType != typeof(DateTimeOffset))
        {
            return false;
        }

        var methodName = node.Method.Name;
        var expressionVisitor = CreateExpressionVisitor(context);

        var target = node.Object != null ? expressionVisitor.Visit(node.Object) : null;
        var arguments = node.Arguments.Select(arg => expressionVisitor.Visit(arg)).ToList();

        var cypherExpression = methodName switch
        {
            "AddDays" when arguments.Count == 1 => $"date({target}) + duration({{days: {arguments[0]}}})",
            "AddMonths" when arguments.Count == 1 => $"date({target}) + duration({{months: {arguments[0]}}})",
            "AddYears" when arguments.Count == 1 => $"date({target}) + duration({{years: {arguments[0]}}})",
            "AddHours" when arguments.Count == 1 => $"datetime({target}) + duration({{hours: {arguments[0]}}})",
            "AddMinutes" when arguments.Count == 1 => $"datetime({target}) + duration({{minutes: {arguments[0]}}})",
            "AddSeconds" when arguments.Count == 1 => $"datetime({target}) + duration({{seconds: {arguments[0]}}})",
            "AddMilliseconds" when arguments.Count == 1 => $"datetime({target}) + duration({{milliseconds: {arguments[0]}}})",

            // DateTime static methods
            "Now" when target == null => "datetime()",
            "UtcNow" when target == null => "datetime('UTC')",
            "Today" when target == null => "date()",

            // Property-like methods
            "Date" => $"date({target})",
            "TimeOfDay" => $"time({target})",
            "Year" => $"datetime({target}).year",
            "Month" => $"datetime({target}).month",
            "Day" => $"datetime({target}).day",
            "Hour" => $"datetime({target}).hour",
            "Minute" => $"datetime({target}).minute",
            "Second" => $"datetime({target}).second",
            "Millisecond" => $"datetime({target}).millisecond",
            "DayOfWeek" => $"datetime({target}).dayOfWeek",
            "DayOfYear" => $"datetime({target}).dayOfYear",

            _ => throw new GraphException($"DateTime method '{methodName}' is not supported in Cypher queries")
        };

        // For methods used in expressions, we don't add to builder directly
        // The expression visitor chain will handle the result
        return true;
    }

    private static ICypherExpressionVisitor CreateExpressionVisitor(CypherQueryContext context)
    {
        return new ExpressionVisitorChainFactory(context).CreateStandardChain();
    }
}
