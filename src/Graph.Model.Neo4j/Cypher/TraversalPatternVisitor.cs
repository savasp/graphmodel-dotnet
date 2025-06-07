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

namespace Cvoya.Graph.Model.Neo4j.Cypher;

internal class TraversalPatternVisitor(QueryScope scope, CypherQueryBuilder builder) : ExpressionVisitor
{
    private readonly Stack<string> _patterns = new();
    private int _nodeCounter = 0;

    public void VisitTraversalPattern(Expression expression)
    {
        Visit(expression);

        // Build the complete pattern from collected segments
        if (_patterns.Count > 0)
        {
            var fullPattern = string.Join("", _patterns.Reverse());
            builder.AddMatch(scope.Alias, pattern: fullPattern);
        }
    }

    protected override Expression VisitMethodCall(MethodCallExpression node)
    {
        return node.Method.Name switch
        {
            "Out" => HandleDirectionalTraversal(node, "->"),
            "In" => HandleDirectionalTraversal(node, "<-"),
            "Both" => HandleDirectionalTraversal(node, "-"),
            "HasLabel" => HandleLabelFilter(node),
            "HasRelationship" => HandleRelationshipFilter(node),
            "WithDepth" => HandleDepthConstraint(node),
            _ => base.VisitMethodCall(node)
        };
    }

    private Expression HandleDirectionalTraversal(MethodCallExpression node, string direction)
    {
        // Visit the source first
        Visit(node.Arguments[0]);

        var relationshipType = "";
        var minDepth = 1;
        var maxDepth = 1;

        // Extract relationship type if provided
        if (node.Arguments.Count > 1 && node.Arguments[1] is ConstantExpression { Value: string relType })
        {
            relationshipType = $":{relType.ToUpperInvariant()}";
        }

        // Extract depth constraints if provided
        if (node.Arguments.Count > 2)
        {
            if (node.Arguments[2] is ConstantExpression { Value: int min })
                minDepth = min;

            if (node.Arguments.Count > 3 && node.Arguments[3] is ConstantExpression { Value: int max })
                maxDepth = max;
        }

        var depthPattern = BuildDepthPattern(minDepth, maxDepth);
        var targetAlias = $"n{++_nodeCounter}";

        var pattern = direction switch
        {
            "->" => $"-[{relationshipType}{depthPattern}]->({targetAlias})",
            "<-" => $"<-[{relationshipType}{depthPattern}]-({targetAlias})",
            "-" => $"-[{relationshipType}{depthPattern}]-({targetAlias})",
            _ => throw new InvalidOperationException($"Unknown direction: {direction}")
        };

        _patterns.Push(pattern);

        // Update scope for next traversal
        scope = scope with { Alias = targetAlias };

        return node;
    }

    private Expression HandleLabelFilter(MethodCallExpression node)
    {
        Visit(node.Arguments[0]);

        if (node.Arguments[1] is ConstantExpression { Value: string label })
        {
            // Add label constraint to the last node in the pattern
            var lastPattern = _patterns.Pop();
            var modifiedPattern = lastPattern.Replace($"({scope.Alias})", $"({scope.Alias}:{label})");
            _patterns.Push(modifiedPattern);
        }

        return node;
    }

    private Expression HandleRelationshipFilter(MethodCallExpression node)
    {
        Visit(node.Arguments[0]);

        if (node.Arguments[1] is ConstantExpression { Value: string relType })
        {
            // Modify the last relationship in the pattern
            var lastPattern = _patterns.Pop();
            var modifiedPattern = lastPattern.Replace("-[", $"-[:{relType.ToUpperInvariant()}");
            _patterns.Push(modifiedPattern);
        }

        return node;
    }

    private Expression HandleDepthConstraint(MethodCallExpression node)
    {
        Visit(node.Arguments[0]);

        var minDepth = 1;
        var maxDepth = int.MaxValue;

        if (node.Arguments.Count > 1 && node.Arguments[1] is ConstantExpression { Value: int min })
            minDepth = min;

        if (node.Arguments.Count > 2 && node.Arguments[2] is ConstantExpression { Value: int max })
            maxDepth = max;

        // Modify the last relationship pattern with depth
        var lastPattern = _patterns.Pop();
        var depthPattern = BuildDepthPattern(minDepth, maxDepth);

        // Insert depth pattern before the closing bracket
        var modifiedPattern = lastPattern.Replace("]", $"{depthPattern}]");
        _patterns.Push(modifiedPattern);

        return node;
    }

    private static string BuildDepthPattern(int minDepth, int maxDepth)
    {
        if (minDepth == 1 && maxDepth == 1)
            return ""; // No depth pattern needed for single hop

        if (maxDepth == int.MaxValue)
            return $"*{minDepth}..";

        if (minDepth == maxDepth)
            return $"*{minDepth}";

        return $"*{minDepth}..{maxDepth}";
    }
}