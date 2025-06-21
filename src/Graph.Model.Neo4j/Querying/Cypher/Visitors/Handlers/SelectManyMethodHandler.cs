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
/// Handles SelectMany LINQ method for flattening collections and graph traversals.
/// </summary>
internal record SelectManyMethodHandler : MethodHandlerBase
{
    public override bool Handle(CypherQueryContext context, MethodCallExpression node, Expression result)
    {
        if (node.Method.Name != "SelectMany")
        {
            return false;
        }

        // SelectMany can have 2 or 3 arguments
        if (node.Arguments.Count < 2 || node.Arguments.Count > 3)
        {
            return false;
        }

        // Get the collection selector (lambda expression)
        if (node.Arguments[1] is not UnaryExpression { Operand: LambdaExpression collectionSelector })
        {
            throw new GraphException("SelectMany method requires a lambda expression collection selector");
        }

        // For graph traversals, this typically involves relationships
        if (collectionSelector.Body is MemberExpression member)
        {
            var currentAlias = context.Scope.CurrentAlias
                ?? throw new InvalidOperationException("No current alias set when processing SelectMany");
            var relationshipType = GetRelationshipType(member);
            var targetType = GetElementType(member.Type);
            var targetAlias = context.Scope.GetOrCreateAlias(targetType, GetPreferredAlias(targetType));

            // Add MATCH for the relationship traversal
            context.Builder.AddMatch($"({currentAlias})-[:{relationshipType}]->({targetAlias})");
            context.Scope.CurrentAlias = targetAlias;
            context.Scope.CurrentType = targetType;

            // Handle result selector if present (3-argument form)
            if (node.Arguments.Count == 3 && node.Arguments[2] is UnaryExpression { Operand: LambdaExpression resultSelector })
            {
                var expressionVisitor = CreateExpressionVisitor(context);
                var resultExpression = expressionVisitor.Visit(resultSelector.Body);
                context.Builder.AddReturn(resultExpression);
            }
        }
        else
        {
            // Handle other collection expressions
            var expressionVisitor = CreateExpressionVisitor(context);
            var collectionExpression = expressionVisitor.Visit(collectionSelector.Body);

            // Use UNWIND for flattening collections
            var unwoundAlias = context.Scope.GetOrCreateAlias(typeof(object), "item");
            context.Builder.AddUnwind($"{collectionExpression} AS {unwoundAlias}");
            context.Scope.CurrentAlias = unwoundAlias;
        }

        return true;
    }

    private static string GetRelationshipType(MemberExpression member)
    {
        // Extract relationship type from property name or attributes
        // This is a simplified implementation - in a real scenario, you'd use attributes or conventions
        return member.Member.Name.ToUpper();
    }

    private static Type GetElementType(Type collectionType)
    {
        // Extract the element type from collection types
        if (collectionType.IsGenericType)
        {
            var genericArgs = collectionType.GetGenericArguments();
            if (genericArgs.Length > 0)
            {
                return genericArgs[0];
            }
        }

        // Fallback to object if we can't determine the type
        return typeof(object);
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
        return new ExpressionVisitorChainFactory(context).CreateStandardChain();
    }
}
