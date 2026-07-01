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

namespace Cvoya.Graph.Model.Age.Querying.Cypher.Visitors;

using System;
using System.Linq.Expressions;
using Microsoft.Extensions.Logging;

/// <summary>
/// Handles translation of .NET string methods to Cypher expressions for AGE.
/// </summary>
internal sealed class StringMethodHandler
{
    private readonly Func<Expression, string> _visitAndReturnCypher;
    private readonly Func<object?, string> _addParameter;
    private readonly ILogger _logger;

    public StringMethodHandler(
        Func<Expression, string> visitAndReturnCypher,
        Func<object?, string> addParameter,
        ILogger logger)
    {
        _visitAndReturnCypher = visitAndReturnCypher;
        _addParameter = addParameter;
        _logger = logger;
    }

    public Expression HandleStringMethod(MethodCallExpression node)
    {
        var obj = node.Object != null ? _visitAndReturnCypher(node.Object) : null;

        return node.Method.Name switch
        {
            "StartsWith" when node.Arguments.Count == 1 =>
                HandleStringStartsWith(obj, node.Arguments[0]),

            "EndsWith" when node.Arguments.Count == 1 =>
                HandleStringEndsWith(obj, node.Arguments[0]),

            "Contains" when node.Arguments.Count == 1 =>
                HandleStringContains(obj, node.Arguments[0]),

            "ToLower" when node.Arguments.Count == 0 =>
                Expression.Constant($"toLower({obj})"),

            "ToUpper" when node.Arguments.Count == 0 =>
                Expression.Constant($"toUpper({obj})"),

            "Trim" when node.Arguments.Count == 0 =>
                Expression.Constant($"trim({obj})"),

            "Substring" when node.Arguments.Count == 1 =>
                Expression.Constant($"substring({obj}, {_visitAndReturnCypher(node.Arguments[0])})"),

            "Substring" when node.Arguments.Count == 2 =>
                Expression.Constant($"substring({obj}, {_visitAndReturnCypher(node.Arguments[0])}, {_visitAndReturnCypher(node.Arguments[1])})"),

            "Replace" when node.Arguments.Count == 2 =>
                Expression.Constant($"replace({obj}, {_visitAndReturnCypher(node.Arguments[0])}, {_visitAndReturnCypher(node.Arguments[1])})"),

            // size() is used for both strings (character count) and lists (element count)
            // in Cypher. This matches .NET's .Length for strings. If AGE ever differentiates
            // size_string() and size_list(), this would need updating.
            "get_Length" when node.Arguments.Count == 0 =>
                Expression.Constant($"size({obj})"),

            _ => throw new NotSupportedException($"String method {node.Method.Name} is not supported")
        };
    }

    private Expression HandleStringContains(string? obj, Expression arg)
        => BuildStringMatchExpression(obj, arg, "CONTAINS");

    private Expression HandleStringStartsWith(string? obj, Expression arg)
        => BuildStringMatchExpression(obj, arg, "STARTS WITH");

    private Expression HandleStringEndsWith(string? obj, Expression arg)
        => BuildStringMatchExpression(obj, arg, "ENDS WITH");

    /// <summary>
    /// Builds a Cypher string match expression using the specified native operator keyword
    /// (CONTAINS, STARTS WITH, or ENDS WITH). The match value is parameterized via
    /// <c>NpgsqlParameter</c> to prevent Cypher injection.
    /// </summary>
    private Expression BuildStringMatchExpression(string? obj, Expression arg, string operatorKeyword)
    {
        if (obj is null)
        {
            return Expression.Constant(null, typeof(string));
        }

        object? argValue = null;

        if (arg is ConstantExpression constantExpr)
        {
            argValue = constantExpr.Value;
        }
        else if (arg is MemberExpression memberExpr)
        {
            try
            {
                var lambda = Expression.Lambda<Func<object>>(Expression.Convert(memberExpr, typeof(object)));
                argValue = lambda.Compile()();
            }
            catch { }
        }

        if (argValue is string strValue)
        {
            // Parameterize the value to prevent Cypher injection
            var paramName = _addParameter(strValue);
            return Expression.Constant($"{obj} {operatorKeyword} {paramName}");
        }

        _logger.LogWarning("Could not evaluate {Operator} argument, attempting fallback", operatorKeyword);
        var argCypher = _visitAndReturnCypher(arg);
        return Expression.Constant($"{obj} {operatorKeyword} {argCypher}");
    }
}
