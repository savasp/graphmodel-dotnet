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
/// Handles graph-specific operations like Include, Traverse, etc.
/// </summary>
internal record GraphOperationMethodHandler : MethodHandlerBase
{
    public override bool Handle(CypherQueryContext context, MethodCallExpression node, Expression result)
    {
        var methodName = node.Method.Name;

        return methodName switch
        {
            "Include" => HandleInclude(context, node),
            "Traverse" => HandleTraverse(context, node),
            "Relationships" => HandleRelationships(context, node),
            "PathSegments" => HandlePathSegments(context, node),
            "WithTransaction" => HandleWithTransaction(context, node),
            _ => false
        };
    }

    private static bool HandleInclude(CypherQueryContext context, MethodCallExpression node)
    {
        if (node.Arguments.Count != 2)
        {
            return false;
        }

        if (node.Arguments[1] is UnaryExpression { Operand: LambdaExpression lambda })
        {
            var expressionVisitor = CreateExpressionVisitor(context);

            // Process the include expression to add appropriate MATCH clauses
            if (lambda.Body is MemberExpression member)
            {
                var currentAlias = context.Scope.CurrentAlias ?? "n";
                var relationshipType = GetRelationshipType(member);
                var targetAlias = context.Scope.GetOrCreateAlias(member.Type,
                    GetPreferredAlias(member.Type));

                // Add OPTIONAL MATCH for the relationship
                context.Builder.AddOptionalMatch(
                    $"({currentAlias})-[:{relationshipType}]->({targetAlias})");
            }
        }

        return true;
    }

    private static bool HandleTraverse(CypherQueryContext context, MethodCallExpression node)
    {
        if (node.Arguments.Count < 2)
        {
            return false;
        }

        // Traverse operations typically involve path patterns
        var currentAlias = context.Scope.CurrentAlias ?? "n";
        var pathAlias = context.Scope.GetOrCreateAlias(typeof(object), "p");

        // Add a path pattern - this is a simplified implementation
        context.Builder.AddMatch($"({currentAlias})-[*]->({pathAlias})");
        context.Scope.CurrentAlias = pathAlias;

        return true;
    }

    private static bool HandleRelationships(CypherQueryContext context, MethodCallExpression node)
    {
        // This would return relationships in a path or from a node
        var currentAlias = context.Scope.CurrentAlias ?? "n";
        context.Builder.AddReturn($"relationships({currentAlias})");
        return true;
    }

    private static bool HandlePathSegments(CypherQueryContext context, MethodCallExpression node)
    {
        // This would return path segments
        var currentAlias = context.Scope.CurrentAlias ?? "n";
        context.Builder.AddReturn($"nodes({currentAlias})");
        return true;
    }

    private static bool HandleWithTransaction(CypherQueryContext context, MethodCallExpression node)
    {
        // Transaction handling would be done at a higher level
        // For now, just pass through
        return true;
    }

    private static string GetRelationshipType(MemberExpression member)
    {
        // Extract relationship type from property name or attributes
        // This is a simplified implementation
        return member.Member.Name.ToUpper();
    }

    private static string GetPreferredAlias(Type type)
    {
        var name = type.Name;

        // Remove generic type markers
        var genericIndex = name.IndexOf('`');
        if (genericIndex > 0)
        {
            name = name[..genericIndex];
        }

        // Remove interface prefix
        if (name.StartsWith("I") && name.Length > 1 && char.IsUpper(name[1]))
        {
            name = name[1..];
        }

        return char.ToLower(name[0]).ToString();
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
