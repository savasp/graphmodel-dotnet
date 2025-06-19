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
/// Handles string manipulation methods for Cypher queries.
/// </summary>
internal record StringMethodHandler : MethodHandlerBase
{
    public override bool Handle(CypherQueryContext context, MethodCallExpression node, Expression result)
    {
        if (node.Method.DeclaringType != typeof(string))
        {
            return false;
        }

        var methodName = node.Method.Name;
        var expressionVisitor = CreateExpressionVisitor(context);

        var target = expressionVisitor.Visit(node.Object!);
        var arguments = node.Arguments.Select(arg => expressionVisitor.Visit(arg)).ToList();

        var cypherExpression = methodName switch
        {
            "Contains" => $"{target} CONTAINS {arguments[0]}",
            "StartsWith" => $"{target} STARTS WITH {arguments[0]}",
            "EndsWith" => $"{target} ENDS WITH {arguments[0]}",
            "ToLower" => $"toLower({target})",
            "ToUpper" => $"toUpper({target})",
            "Trim" => $"trim({target})",
            "TrimStart" => $"ltrim({target})",
            "TrimEnd" => $"rtrim({target})",
            "Replace" when arguments.Count == 2 => $"replace({target}, {arguments[0]}, {arguments[1]})",
            "Substring" when arguments.Count == 1 => $"substring({target}, {arguments[0]})",
            "Substring" when arguments.Count == 2 => $"substring({target}, {arguments[0]}, {arguments[1]})",
            "Length" => $"length({target})",
            "IndexOf" when arguments.Count == 1 => $"indexOf({target}, {arguments[0]})",
            "Split" when arguments.Count == 1 => $"split({target}, {arguments[0]})",
            _ => throw new GraphException($"String method '{methodName}' is not supported in Cypher queries")
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
