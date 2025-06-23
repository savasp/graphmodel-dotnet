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

internal class StringMethodVisitor(CypherQueryContext context, ICypherExpressionVisitor nextVisitor)
    : CypherExpressionVisitorBase<StringMethodVisitor>(context, nextVisitor)
{
    public override string VisitMethodCall(MethodCallExpression node)
    {
        if (node.Method.DeclaringType != typeof(string))
        {
            return NextVisitor?.VisitMethodCall(node)
                ?? throw new NotSupportedException($"String method {node.Method.Name} is not supported");
        }

        Logger.LogDebug("Visiting string method: {MethodName}", node.Method.Name);

        // Handle static methods
        if (node.Method.IsStatic)
        {
            var args = node.Arguments.Select(arg => Visit(arg)
                ?? throw new NotSupportedException("Cannot process string argument")).ToList();

            var expr = node.Method.Name switch
            {
                "IsNullOrEmpty" when args.Count == 1 =>
                    $"({args[0]} IS NULL OR size({args[0]}) = 0)",
                "IsNullOrWhiteSpace" when args.Count == 1 =>
                    $"({args[0]} IS NULL OR size(trim({args[0]})) = 0)",
                "Concat" => args.Count switch
                {
                    2 => $"{args[0]} + {args[1]}",
                    _ => $"apoc.text.join([{string.Join(", ", args)}], '')"
                },
                "Join" when args.Count >= 2 =>
                    $"apoc.text.join([{string.Join(", ", args.Skip(1))}], {args[0]})",
                _ => throw new NotSupportedException($"Static string method {node.Method.Name} is not supported")
            };

            Logger.LogDebug("Static string method result: {Expression}", expr);
            return expr;
        }

        var target = Visit(node.Object!)
            ?? throw new NotSupportedException("Cannot process string target");

        var arguments = node.Arguments.Select(arg => Visit(arg)
            ?? throw new NotSupportedException("Cannot process string argument")).ToList();

        var expression = node.Method.Name switch
        {
            "Contains" => $"{target} CONTAINS {arguments[0]}",
            "StartsWith" => $"{target} STARTS WITH {arguments[0]}",
            "EndsWith" => $"{target} ENDS WITH {arguments[0]}",
            "ToLower" => $"toLower({target})",
            "ToUpper" => $"toUpper({target})",
            "Trim" => $"trim({target})",
            "TrimStart" => $"ltrim({target})",
            "TrimEnd" => $"rtrim({target})",
            "Replace" => $"replace({target}, {arguments[0]}, {arguments[1]})",
            "Substring" => arguments.Count == 1
                ? $"substring({target}, {arguments[0]})"
                : $"substring({target}, {arguments[0]}, {arguments[1]})",
            "Length" => $"length({target})",
            "IndexOf" when arguments.Count == 1 => $"apoc.text.indexOf({target}, {arguments[0]}, 0)",
            "LastIndexOf" when arguments.Count == 1 => $"apoc.text.lastIndexOf({target}, {arguments[0]})",
            "PadLeft" when arguments.Count == 1 => $"apoc.text.lpad({target}, {arguments[0]}, ' ')",
            "PadLeft" when arguments.Count == 2 => $"apoc.text.lpad({target}, {arguments[0]}, {arguments[1]})",
            "PadRight" when arguments.Count == 1 => $"apoc.text.rpad({target}, {arguments[0]}, ' ')",
            "PadRight" when arguments.Count == 2 => $"apoc.text.rpad({target}, {arguments[0]}, {arguments[1]})",
            "CompareTo" when arguments.Count == 1 => $"apoc.text.compareTo({target}, {arguments[0]})",
            _ => throw new NotSupportedException($"String method {node.Method.Name} is not supported")
        };

        Logger.LogDebug("String method result: {Expression}", expression);
        return expression;
    }

    public override string VisitBinary(BinaryExpression node)
    {
        // Handle logical operations like OR (||) and AND (&&)
        if (node.NodeType == ExpressionType.OrElse || node.NodeType == ExpressionType.AndAlso)
        {
            var left = Visit(node.Left);
            var right = Visit(node.Right);

            var operatorSymbol = node.NodeType == ExpressionType.OrElse ? "OR" : "AND";

            Logger.LogDebug("Translated logical binary expression: ({Left} {Operator} {Right})", left, operatorSymbol, right);
            return $"({left} {operatorSymbol} {right})";
        }

        // Delegate to the next visitor if the binary expression is not related to strings
        return NextVisitor?.VisitBinary(node)
            ?? throw new NotSupportedException($"Binary expression {node.NodeType} is not supported");
    }

    public override string VisitUnary(UnaryExpression node) => NextVisitor?.Visit(node)
        ?? throw new NotSupportedException($"Unary expression {node.NodeType} is not supported");

    public override string VisitMember(MemberExpression node) => NextVisitor?.Visit(node)
        ?? throw new NotSupportedException($"Member expression {node.NodeType} is not supported");

    public override string VisitConstant(ConstantExpression node) => NextVisitor?.Visit(node)
        ?? throw new NotSupportedException($"Constant expression {node.NodeType} is not supported");

    public override string VisitParameter(ParameterExpression node) => NextVisitor?.Visit(node)
        ?? throw new NotSupportedException($"Parameter expression {node.NodeType} is not supported");
}