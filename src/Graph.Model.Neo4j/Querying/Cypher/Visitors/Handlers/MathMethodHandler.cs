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
/// Handles mathematical functions for Cypher queries.
/// </summary>
internal record MathMethodHandler : MethodHandlerBase
{
    public override bool Handle(CypherQueryContext context, MethodCallExpression node, Expression result)
    {
        if (node.Method.DeclaringType != typeof(Math))
        {
            return false;
        }

        var methodName = node.Method.Name;
        var expressionVisitor = CreateExpressionVisitor(context);

        var arguments = node.Arguments.Select(arg => expressionVisitor.Visit(arg)).ToList();

        var cypherExpression = methodName switch
        {
            "Abs" when arguments.Count == 1 => $"abs({arguments[0]})",
            "Floor" when arguments.Count == 1 => $"floor({arguments[0]})",
            "Ceiling" when arguments.Count == 1 => $"ceil({arguments[0]})",
            "Round" when arguments.Count == 1 => $"round({arguments[0]})",
            "Round" when arguments.Count == 2 => $"round({arguments[0]}, {arguments[1]})",
            "Min" when arguments.Count == 2 => $"min({arguments[0]}, {arguments[1]})",
            "Max" when arguments.Count == 2 => $"max({arguments[0]}, {arguments[1]})",
            "Pow" when arguments.Count == 2 => $"pow({arguments[0]}, {arguments[1]})",
            "Sqrt" when arguments.Count == 1 => $"sqrt({arguments[0]})",
            "Sign" when arguments.Count == 1 => $"sign({arguments[0]})",
            "Sin" when arguments.Count == 1 => $"sin({arguments[0]})",
            "Cos" when arguments.Count == 1 => $"cos({arguments[0]})",
            "Tan" when arguments.Count == 1 => $"tan({arguments[0]})",
            "Asin" when arguments.Count == 1 => $"asin({arguments[0]})",
            "Acos" when arguments.Count == 1 => $"acos({arguments[0]})",
            "Atan" when arguments.Count == 1 => $"atan({arguments[0]})",
            "Atan2" when arguments.Count == 2 => $"atan2({arguments[0]}, {arguments[1]})",
            "Log" when arguments.Count == 1 => $"log({arguments[0]})",
            "Log10" when arguments.Count == 1 => $"log10({arguments[0]})",
            "Exp" when arguments.Count == 1 => $"exp({arguments[0]})",
            _ => throw new GraphException($"Math method '{methodName}' is not supported in Cypher queries")
        };

        // For methods used in expressions, we don't add to builder directly
        // The expression visitor chain will handle the result
        return true;
    }

    private static ICypherExpressionVisitor CreateExpressionVisitor(CypherQueryContext context)
    {
        return new CollectionMethodVisitor(
            context,
            new StringMethodVisitor(
                context,
                new BinaryExpressionVisitor(
                    context,
                    new BaseExpressionVisitor(context))));
    }
}
