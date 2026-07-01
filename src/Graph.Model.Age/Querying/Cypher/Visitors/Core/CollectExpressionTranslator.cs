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

namespace Cvoya.Graph.Model.Age.Querying.Cypher.Visitors.Core;

using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using Cvoya.Graph.Model;

/// <summary>
/// Shared helper for translating inner Select lambda expressions to Cypher collect() expressions.
/// Used by both AgeExpressionToCypherVisitor and ProjectionFragmentVisitor.
/// </summary>
internal static class CollectExpressionTranslator
{
    /// <summary>
    /// Translates an inner Select lambda body to a Cypher expression suitable for collect().
    /// </summary>
    /// <param name="body">The body of the inner Select lambda.</param>
    /// <param name="innerParam">The parameter of the inner lambda (e.g., <c>p</c> in <c>g.Select(p => p.FirstName)</c>).</param>
    /// <param name="srcAlias">The Cypher alias for the source node.</param>
    /// <param name="relAlias">The Cypher alias for the relationship.</param>
    /// <param name="tgtAlias">The Cypher alias for the target node.</param>
    /// <param name="isPathContext">Whether the collect is inside a path segment traversal context.</param>
    public static string TranslateInnerSelectBody(
        Expression body,
        ParameterExpression innerParam,
        string srcAlias,
        string relAlias,
        string tgtAlias,
        bool isPathContext = false)
    {
        if (body is NewExpression newExpr)
        {
            var mapParts = new List<string>();
            for (int i = 0; i < newExpr.Arguments.Count; i++)
            {
                var memberName = newExpr.Members?[i]?.Name ?? $"Prop{i}";
                var argCypher = TranslateInnerExpression(newExpr.Arguments[i], innerParam, srcAlias, relAlias, tgtAlias, isPathContext);
                mapParts.Add($"{memberName}: {argCypher}");
            }
            return $"{{{string.Join(", ", mapParts)}}}";
        }

        if (body is MemberExpression)
        {
            return TranslateInnerExpression(body, innerParam, srcAlias, relAlias, tgtAlias, isPathContext);
        }

        if (body is BinaryExpression binary)
        {
            var left = TranslateInnerExpression(binary.Left, innerParam, srcAlias, relAlias, tgtAlias, isPathContext);
            var right = TranslateInnerExpression(binary.Right, innerParam, srcAlias, relAlias, tgtAlias, isPathContext);
            var op = binary.NodeType switch
            {
                ExpressionType.Add => "+",
                ExpressionType.Subtract => "-",
                ExpressionType.Multiply => "*",
                ExpressionType.Divide => "/",
                ExpressionType.GreaterThan => ">",
                ExpressionType.GreaterThanOrEqual => ">=",
                ExpressionType.LessThan => "<",
                ExpressionType.LessThanOrEqual => "<=",
                ExpressionType.Equal => "=",
                ExpressionType.NotEqual => "<>",
                ExpressionType.AndAlso => "AND",
                ExpressionType.OrElse => "OR",
                _ => throw new NotSupportedException($"Binary operator {binary.NodeType} in inner select")
            };
            return $"({left} {op} {right})";
        }

        if (body is UnaryExpression unary && unary.NodeType == ExpressionType.Convert)
        {
            return TranslateInnerExpression(unary.Operand, innerParam, srcAlias, relAlias, tgtAlias, isPathContext);
        }

        // Fallback: try to evaluate as constant
        try
        {
            var lambda = Expression.Lambda<Func<object>>(Expression.Convert(body, typeof(object)));
            var val = lambda.Compile()();
            return val?.ToString() ?? "null";
        }
        catch
        {
            return body.ToString() ?? "unknown";
        }
    }

    /// <summary>
    /// Translates a single expression within an inner Select lambda to Cypher text.
    /// Handles path segment property chains like p.EndNode.FirstName → tgt0.FirstName.
    /// </summary>
    /// <param name="expr">The expression to translate.</param>
    /// <param name="innerParam">The parameter of the inner lambda.</param>
    /// <param name="srcAlias">The Cypher alias for the source node.</param>
    /// <param name="relAlias">The Cypher alias for the relationship.</param>
    /// <param name="tgtAlias">The Cypher alias for the target node.</param>
    /// <param name="isPathContext">Whether we are in a path segment traversal context. When false, simple node member access resolves against srcAlias.</param>
    public static string TranslateInnerExpression(
        Expression expr,
        ParameterExpression innerParam,
        string srcAlias,
        string relAlias,
        string tgtAlias,
        bool isPathContext = false)
    {
        // p.EndNode.FirstName → tgt0.FirstName
        if (expr is MemberExpression memberExpr)
        {
            // Static DateTime members FIRST (before path segment checks)
            // Map to Cypher datetime() function instead of evaluating as a string literal,
            // which would break DateTime arithmetic like (datetime() - p.Relationship.Since).
            if (memberExpr.Expression == null && memberExpr.Member.DeclaringType == typeof(DateTime))
            {
                return memberExpr.Member.Name switch
                {
                    "UtcNow" => "datetime()",
                    "Now" => "localdatetime()",
                    "Today" => "date()",
                    _ => ExpressionTranslationHelper.TryCompileEval(expr)
                };
            }

            // Other static members
            if (memberExpr.Expression == null)
                return ExpressionTranslationHelper.TryCompileEval(expr);

            // Check for path segment property: p.EndNode, p.StartNode, p.Relationship
            if (memberExpr.Expression == innerParam &&
                typeof(IGraphPathSegment).IsAssignableFrom(innerParam.Type))
            {
                return memberExpr.Member.Name switch
                {
                    nameof(IGraphPathSegment.StartNode) => srcAlias,
                    nameof(IGraphPathSegment.EndNode) => tgtAlias,
                    nameof(IGraphPathSegment.Relationship) => relAlias,
                    _ => memberExpr.Member.Name
                };
            }

            // Check for nested access: p.EndNode.FirstName
            if (memberExpr.Expression is MemberExpression innerMem &&
                innerMem.Expression == innerParam &&
                typeof(IGraphPathSegment).IsAssignableFrom(innerParam.Type))
            {
                var alias = innerMem.Member.Name switch
                {
                    nameof(IGraphPathSegment.StartNode) => srcAlias,
                    nameof(IGraphPathSegment.EndNode) => tgtAlias,
                    nameof(IGraphPathSegment.Relationship) => relAlias,
                    _ => throw new NotSupportedException($"Unknown path component {innerMem.Member.Name}")
                };
                var propName = ExpressionTranslationHelper.MapPropertyName(memberExpr.Member.Name);
                return $"{alias}.{propName}";
            }

            // Check for deeper nesting by walking up the member expression chain
            var (resolvedAlias, remainingPath) = WalkPathSegmentChain(memberExpr, innerParam, srcAlias, relAlias, tgtAlias);
            if (resolvedAlias != null)
            {
                return remainingPath.Count > 0
                    ? $"{resolvedAlias}.{string.Join(".", remainingPath)}"
                    : resolvedAlias;
            }

            // Not a path segment chain — the member may be on a non-param sub-expression
            var innerExpr = memberExpr.Expression;
            if (innerExpr is BinaryExpression || innerExpr is MethodCallExpression ||
                innerExpr is UnaryExpression || innerExpr is ConditionalExpression)
            {
                var baseCypher = TranslateInnerExpression(innerExpr, innerParam, srcAlias, relAlias, tgtAlias, isPathContext);
                var prop = ExpressionTranslationHelper.MapPropertyName(memberExpr.Member.Name);

                if (memberExpr.Member.DeclaringType == typeof(TimeSpan))
                    return TranslateTimeSpanProperty(memberExpr, baseCypher, innerExpr, innerParam, srcAlias, relAlias, tgtAlias);

                return $"{baseCypher}.{prop}";
            }

            // Handle member access on a non-path-segment parameter (e.g., p.FirstName where p is Person)
            // In path context: the node is the traversal target → use tgtAlias
            // In plain node query: the node is the source entity → use srcAlias
            if (memberExpr.Expression == innerParam && typeof(INode).IsAssignableFrom(innerParam.Type))
            {
                var effectiveAlias = isPathContext ? tgtAlias : srcAlias;
                return $"{effectiveAlias}.{ExpressionTranslationHelper.MapPropertyName(memberExpr.Member.Name)}";
            }

            // Try constant evaluation
            try
            {
                var lambda = Expression.Lambda<Func<object>>(Expression.Convert(memberExpr, typeof(object)));
                var val = lambda.Compile()();
                return val?.ToString() ?? "null";
            }
            catch
            {
                return memberExpr.Member.Name;
            }
        }

        // DateTime.UtcNow etc.
        if (expr is MemberExpression staticMember && staticMember.Expression == null)
        {
            if (staticMember.Member.DeclaringType == typeof(DateTime))
            {
                return staticMember.Member.Name switch
                {
                    "UtcNow" => "datetime()",
                    "Now" => "localdatetime()",
                    "Today" => "date()",
                    _ => ExpressionTranslationHelper.TryCompileEval(expr)
                };
            }
            return ExpressionTranslationHelper.TryCompileEval(expr);
        }

        // Binary, Unary, etc.
        if (expr is BinaryExpression bin)
        {
            var left = TranslateInnerExpression(bin.Left, innerParam, srcAlias, relAlias, tgtAlias, isPathContext);
            var right = TranslateInnerExpression(bin.Right, innerParam, srcAlias, relAlias, tgtAlias, isPathContext);
            var op = bin.NodeType switch
            {
                ExpressionType.Add => "+",
                ExpressionType.Subtract => "-",
                ExpressionType.Multiply => "*",
                ExpressionType.Divide => "/",
                ExpressionType.GreaterThan => ">",
                ExpressionType.GreaterThanOrEqual => ">=",
                ExpressionType.LessThan => "<",
                ExpressionType.LessThanOrEqual => "<=",
                ExpressionType.Equal => "=",
                ExpressionType.NotEqual => "<>",
                ExpressionType.AndAlso => "AND",
                ExpressionType.OrElse => "OR",
                _ => throw new NotSupportedException($"Binary operator {bin.NodeType} in inner expression")
            };
            return $"({left} {op} {right})";
        }

        if (expr is UnaryExpression ue && ue.NodeType == ExpressionType.Convert)
            return TranslateInnerExpression(ue.Operand, innerParam, srcAlias, relAlias, tgtAlias, isPathContext);

        if (expr is ConstantExpression ce)
            return ce.Value?.ToString() ?? "null";

        // When the inner param itself is referenced:
        // In path context, the param is a path segment → resolve to tgtAlias
        // In plain node query, the param is the source node → resolve to srcAlias
        if (expr == innerParam)
        {
            return isPathContext ? tgtAlias : srcAlias;
        }

        if (expr is MethodCallExpression mc)
            return TranslateInnerMethodCall(mc, innerParam, srcAlias, relAlias, tgtAlias, isPathContext);

        // Fallback evaluation
        try
        {
            var lambda = Expression.Lambda<Func<object>>(Expression.Convert(expr, typeof(object)));
            var val = lambda.Compile()();
            return val?.ToString() ?? "null";
        }
        catch
        {
            return expr.ToString() ?? "unknown";
        }
    }

    /// <summary>
    /// Walks a chain of member expressions starting from path segment parameter access.
    /// </summary>
    public static (string? alias, List<string> remainingPath) WalkPathSegmentChain(
        MemberExpression expr,
        ParameterExpression innerParam,
        string srcAlias,
        string relAlias,
        string tgtAlias)
    {
        var members = new List<MemberExpression>();
        Expression? current = expr;
        while (current is MemberExpression currentMem)
        {
            members.Add(currentMem);
            current = currentMem.Expression;
        }

        if (current != innerParam || !typeof(IGraphPathSegment).IsAssignableFrom(innerParam.Type))
            return (null, []);

        string alias;
        int startIndex;
        if (members.Count >= 2 && members[^2].Expression == innerParam)
        {
            alias = members[^1].Member.Name switch
            {
                nameof(IGraphPathSegment.StartNode) => srcAlias,
                nameof(IGraphPathSegment.EndNode) => tgtAlias,
                nameof(IGraphPathSegment.Relationship) => relAlias,
                _ => throw new NotSupportedException($"Unknown path component {members[^1].Member.Name}")
            };
            startIndex = members.Count - 3;
        }
        else if (members.Count >= 1 && members[^1].Expression == innerParam)
        {
            alias = members[^1].Member.Name switch
            {
                nameof(IGraphPathSegment.StartNode) => srcAlias,
                nameof(IGraphPathSegment.EndNode) => tgtAlias,
                nameof(IGraphPathSegment.Relationship) => relAlias,
                _ => throw new NotSupportedException($"Unknown path component {members[^1].Member.Name}")
            };
            startIndex = members.Count - 2;
        }
        else
        {
            return (null, []);
        }

        var remainingPath = new List<string>();
        for (int i = startIndex; i >= 0; i--)
        {
            remainingPath.Add(ExpressionTranslationHelper.MapPropertyName(members[i].Member.Name));
        }

        return (alias, remainingPath);
    }

    /// <summary>
    /// Translates a method call expression within an inner Select lambda.
    /// </summary>
    public static string TranslateInnerMethodCall(
        MethodCallExpression mc,
        ParameterExpression innerParam,
        string srcAlias,
        string relAlias,
        string tgtAlias,
        bool isPathContext = false)
    {
        if (mc.Method.Name == "Subtract" && mc.Method.DeclaringType == typeof(DateTime) && mc.Arguments.Count == 1)
        {
            var left = TranslateInnerExpression(mc.Object!, innerParam, srcAlias, relAlias, tgtAlias, isPathContext);
            var right = TranslateInnerExpression(mc.Arguments[0], innerParam, srcAlias, relAlias, tgtAlias, isPathContext);
            return $"({left} - {right})";
        }

        if (mc.Method.DeclaringType == typeof(DateTime) && mc.Arguments.Count == 1 && mc.Object != null)
        {
            return mc.Method.Name switch
            {
                "AddDays" or "AddMonths" or "AddYears" or "AddHours" or "AddMinutes" or "AddSeconds" => TryCompileDateTime(mc),
                _ => TryCompileEval(mc)
            };
        }

        if (mc.Method.Name == "get_Days" && mc.Object != null)
        {
            try
            {
                var lambda = Expression.Lambda<Func<object>>(Expression.Convert(mc, typeof(object)));
                var val = lambda.Compile()();
                return val?.ToString() ?? "0";
            }
            catch
            {
                var obj = TranslateInnerExpression(mc.Object, innerParam, srcAlias, relAlias, tgtAlias, isPathContext);
                return $"toInteger({obj} / 86400000)";
            }
        }

        // Fallback evaluation
        try
        {
            var lambda = Expression.Lambda<Func<object>>(Expression.Convert(mc, typeof(object)));
            var val = lambda.Compile()();
            return val?.ToString() ?? "null";
        }
        catch
        {
            return mc.ToString() ?? "unknown";
        }
    }

    private static string TranslateTimeSpanProperty(
        MemberExpression memberExpr,
        string baseCypher,
        Expression innerExpr,
        ParameterExpression innerParam,
        string srcAlias,
        string relAlias,
        string tgtAlias)
    {
        try
        {
            var lambda = Expression.Lambda<Func<object>>(Expression.Convert(memberExpr, typeof(object)));
            var val = lambda.Compile()();
            return val?.ToString() ?? "0";
        }
        catch
        {
            return "0";
        }
    }

    private static string TryCompileDateTime(MethodCallExpression mc)
    {
        try
        {
            var lambda = Expression.Lambda<Func<object>>(Expression.Convert(mc, typeof(object)));
            var val = lambda.Compile()();
            return val switch
            {
                DateTime dt => $"'{dt:yyyy-MM-ddTHH:mm:ss}'",
                _ => val?.ToString() ?? "null"
            };
        }
        catch { return "null"; }
    }

    private static string TryCompileEval(Expression expr)
        => ExpressionTranslationHelper.TryCompileEval(expr);
}
