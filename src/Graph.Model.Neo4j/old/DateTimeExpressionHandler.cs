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

using System.Linq.Expressions;

namespace Cvoya.Graph.Model.Neo4j.Linq;

internal static class DateTimeExpressionHandler
{
    public static bool TryHandleDateTimeMethod(MethodCallExpression node, out string cypherExpression)
    {
        cypherExpression = string.Empty;

        if (node.Object?.Type != typeof(DateTime) && node.Method.DeclaringType != typeof(DateTime))
            return false;

        if (!node.Method.Name.StartsWith("Add"))
            return false;

        var durationUnit = node.Method.Name switch
        {
            "AddDays" => "days",
            "AddHours" => "hours",
            "AddMinutes" => "minutes",
            "AddSeconds" => "seconds",
            "AddMilliseconds" => "milliseconds",
            _ => null
        };

        if (durationUnit == null)
            return false;

        // Handle the base datetime (could be DateTime.UtcNow or another expression)
        string baseDateTime;
        if (node.Object is MemberExpression memberExpr &&
            TryHandleDateTimeExpression(memberExpr, out string baseCypher))
        {
            baseDateTime = baseCypher;
        }
        else
        {
            // For now, default to datetime() - you might want to handle this differently
            baseDateTime = "datetime()";
        }

        // Get the value being added (for now, handle constants)
        var valueExpr = ExtractConstantValue(node.Arguments[0]);

        cypherExpression = $"({baseDateTime} + duration({{{durationUnit}: {valueExpr}}}))";
        return true;
    }

    public static bool TryHandleDateTimeExpression(MemberExpression node, out string cypherExpression)
    {
        cypherExpression = string.Empty;

        if (node.Member.DeclaringType != typeof(DateTime))
            return false;

        cypherExpression = node.Member.Name switch
        {
            nameof(DateTime.UtcNow) => "datetime()",
            nameof(DateTime.Now) => "localdatetime()",
            nameof(DateTime.Today) => "date()",
            _ => string.Empty
        };

        return !string.IsNullOrEmpty(cypherExpression);
    }

    private static string ExtractConstantValue(Expression expression)
    {
        return expression switch
        {
            ConstantExpression constant => constant.Value?.ToString() ?? "0",
            _ => "0" // Fallback for now
        };
    }
}