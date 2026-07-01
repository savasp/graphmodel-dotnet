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

/// <summary>
/// Handles translation of .NET System.Math methods to Cypher expressions for AGE.
/// </summary>
internal sealed class MathMethodHandler
{
    private readonly Func<Expression, string> _visitAndReturnCypher;

    public MathMethodHandler(Func<Expression, string> visitAndReturnCypher)
    {
        _visitAndReturnCypher = visitAndReturnCypher;
    }

    public Expression HandleMathMethod(MethodCallExpression node)
    {
        return node.Method.Name switch
        {
            "Abs" when node.Arguments.Count == 1 =>
                Expression.Constant($"abs({_visitAndReturnCypher(node.Arguments[0])})"),

            "Ceiling" when node.Arguments.Count == 1 =>
                Expression.Constant($"ceil({_visitAndReturnCypher(node.Arguments[0])})"),

            "Floor" when node.Arguments.Count == 1 =>
                Expression.Constant($"floor({_visitAndReturnCypher(node.Arguments[0])})"),

            "Round" when node.Arguments.Count == 1 =>
                Expression.Constant($"round({_visitAndReturnCypher(node.Arguments[0])})"),

            "Round" when node.Arguments.Count == 2 =>
                Expression.Constant($"round({_visitAndReturnCypher(node.Arguments[0])}, {_visitAndReturnCypher(node.Arguments[1])})"),

            "Sqrt" when node.Arguments.Count == 1 =>
                Expression.Constant($"sqrt({_visitAndReturnCypher(node.Arguments[0])})"),

            "Exp" when node.Arguments.Count == 1 =>
                Expression.Constant($"exp({_visitAndReturnCypher(node.Arguments[0])})"),

            "Log" when node.Arguments.Count == 1 =>
                Expression.Constant($"log({_visitAndReturnCypher(node.Arguments[0])})"),

            "Log10" when node.Arguments.Count == 1 =>
                Expression.Constant($"log10({_visitAndReturnCypher(node.Arguments[0])})"),

            "Sign" when node.Arguments.Count == 1 =>
                Expression.Constant($"sign({_visitAndReturnCypher(node.Arguments[0])})"),

            "Sin" when node.Arguments.Count == 1 =>
                Expression.Constant($"sin({_visitAndReturnCypher(node.Arguments[0])})"),

            "Cos" when node.Arguments.Count == 1 =>
                Expression.Constant($"cos({_visitAndReturnCypher(node.Arguments[0])})"),

            "Tan" when node.Arguments.Count == 1 =>
                Expression.Constant($"tan({_visitAndReturnCypher(node.Arguments[0])})"),

            "Asin" when node.Arguments.Count == 1 =>
                Expression.Constant($"asin({_visitAndReturnCypher(node.Arguments[0])})"),

            "Acos" when node.Arguments.Count == 1 =>
                Expression.Constant($"acos({_visitAndReturnCypher(node.Arguments[0])})"),

            "Atan" when node.Arguments.Count == 1 =>
                Expression.Constant($"atan({_visitAndReturnCypher(node.Arguments[0])})"),

            "Max" when node.Arguments.Count == 2 =>
                Expression.Constant($"CASE WHEN {_visitAndReturnCypher(node.Arguments[0])} > {_visitAndReturnCypher(node.Arguments[1])} THEN {_visitAndReturnCypher(node.Arguments[0])} ELSE {_visitAndReturnCypher(node.Arguments[1])} END"),

            "Min" when node.Arguments.Count == 2 =>
                Expression.Constant($"CASE WHEN {_visitAndReturnCypher(node.Arguments[0])} < {_visitAndReturnCypher(node.Arguments[1])} THEN {_visitAndReturnCypher(node.Arguments[0])} ELSE {_visitAndReturnCypher(node.Arguments[1])} END"),

            "Pow" when node.Arguments.Count == 2 =>
                Expression.Constant($"({_visitAndReturnCypher(node.Arguments[0])}) ^ ({_visitAndReturnCypher(node.Arguments[1])})"),

            _ => throw new NotSupportedException($"Math method {node.Method.Name} is not supported")
        };
    }
}
