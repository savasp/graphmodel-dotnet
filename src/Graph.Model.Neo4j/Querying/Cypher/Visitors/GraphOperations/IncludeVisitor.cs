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

namespace Cvoya.Graph.Model.Neo4j.Querying.Cypher.Visitors;

using System.Linq.Expressions;
using System.Reflection;
using Cvoya.Graph.Model.Neo4j.Querying.Cypher.Visitors.Core;

internal class IncludeVisitor(CypherQueryContext context) : CypherVisitorBase<IncludeVisitor>(context)
{
    private readonly Stack<string> _pathSegments = new();
    private int _includeCounter = 0;

    protected override Expression VisitMember(MemberExpression node)
    {
        // Build the relationship path from the member expression
        var pathSegments = new Stack<string>();
        var current = node;

        while (current != null)
        {
            pathSegments.Push(current.Member.Name);
            current = current.Expression as MemberExpression;
        }

        // Generate the relationship pattern
        var currentAlias = Scope.CurrentAlias
            ?? throw new InvalidOperationException("No current alias set when processing Include clause");

        var patterns = new List<string>();

        while (pathSegments.TryPop(out var segment))
        {
            var relationshipAlias = $"r{_includeCounter}";
            var targetAlias = $"src{++_includeCounter}";

            // Check if this is a collection or single relationship
            var memberType = GetMemberType(node.Type, segment);
            var isCollection = IsCollectionType(memberType);

            // Build the relationship pattern
            var pattern = BuildRelationshipPattern(
                currentAlias,
                relationshipAlias,
                targetAlias,
                segment,
                isCollection);

            patterns.Add(pattern);
            currentAlias = targetAlias;

            // Add to the MATCH clause
            Builder.AddMatch(currentAlias, pattern: pattern);

            // Add to the RETURN clause to ensure we fetch the related data
            Builder.AddReturn(targetAlias);
        }

        return node;
    }

    protected override Expression VisitMethodCall(MethodCallExpression node)
    {
        // Handle ThenInclude scenarios
        if (node.Method.Name == "ThenInclude")
        {
            // Process the previous Include first
            Visit(node.Arguments[0]);

            // Then process the ThenInclude
            if (node.Arguments[1] is UnaryExpression { Operand: LambdaExpression lambda })
            {
                Visit(lambda.Body);
            }

            return node;
        }

        return base.VisitMethodCall(node);
    }

    private string BuildRelationshipPattern(
        string sourceAlias,
        string relationshipAlias,
        string targetAlias,
        string relationshipName,
        bool isCollection)
    {
        // Convert property name to relationship type (e.g., "Friends" -> "FRIEND", "BestFriend" -> "BEST_FRIEND")
        var relationshipType = ConvertToRelationshipType(relationshipName);

        // For now, assume outgoing relationships. In a real implementation,
        // we'd need metadata to determine direction
        return $"-[{relationshipAlias}:{relationshipType}]->({targetAlias})";
    }

    private string ConvertToRelationshipType(string propertyName)
    {
        // Simple conversion - in practice, this would use metadata/attributes
        if (propertyName.EndsWith("s"))
        {
            propertyName = propertyName[..^1]; // Remove plural 's'
        }

        // Convert camelCase/PascalCase to UPPER_SNAKE_CASE
        var result = string.Concat(propertyName.Select((c, i) =>
            i > 0 && char.IsUpper(c) ? $"_{c}" : c.ToString()
        ));

        return result.ToUpperInvariant();
    }

    private static MemberInfo? GetMemberInfo(Type type, string memberName)
    {
        return type.GetProperty(memberName) ?? (MemberInfo?)type.GetField(memberName);
    }

    private static bool IsCollectionType(Type? type)
    {
        if (type == null) return false;

        return type.IsGenericType &&
               (type.GetGenericTypeDefinition() == typeof(ICollection<>) ||
                type.GetGenericTypeDefinition() == typeof(IList<>) ||
                type.GetGenericTypeDefinition() == typeof(List<>) ||
                type.GetGenericTypeDefinition() == typeof(IEnumerable<>));
    }

    private static Type? GetMemberType(Type type, string memberName)
    {
        var property = type.GetProperty(memberName);
        if (property != null) return property.PropertyType;

        var field = type.GetField(memberName);
        return field?.FieldType;
    }
}