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
using System.Linq.Expressions;
using Cvoya.Graph.Model.Age.Querying.Cypher.Visitors.Core.Modular;
using Microsoft.Extensions.Logging;

/// <summary>
/// Handles graph traversal operations for AGE Cypher queries.
/// Supports PathSegments, WithDepth, and Direction LINQ methods.
/// </summary>
internal sealed class PathSegmentHandler
{
    private readonly CypherQueryContext _context;
    private readonly ILogger _logger;
    private readonly Func<Expression, Expression> _visitExpression;
    private readonly TraversalFragmentVisitor _traversalVisitor;

    public PathSegmentHandler(
        CypherQueryContext context,
        ILogger logger,
        Func<Expression, Expression> visitExpression,
        TraversalFragmentVisitor traversalVisitor)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _visitExpression = visitExpression ?? throw new ArgumentNullException(nameof(visitExpression));
        _traversalVisitor = traversalVisitor ?? throw new ArgumentNullException(nameof(traversalVisitor));
    }

    public Expression HandlePathSegments(MethodCallExpression node)
    {
        // Visit the source expression first so upstream operations (e.g., Traverse, Where) build their state before
        // we append this hop. This preserves MATCH ordering for nested PathSegments chains.
        var sourceExpression = _visitExpression(node.Arguments[0]);

        // Delegate to specialized traversal visitor which handles pattern generation and fragment emission
        _traversalVisitor.HandlePathSegments(node);

        return sourceExpression;
    }

    public Expression HandleWithDepth(MethodCallExpression node)
    {
        _logger.LogDebug("Processing WithDepth method call");

        if (node.Arguments.Count >= 2)
        {
            // Extract depth parameters
            if (node.Arguments.Count == 2)
            {
                // WithDepth(maxDepth)
                var maxDepth = AgeCypherQueryVisitor.EvaluateConstantExpression<int>(node.Arguments[1]);
                _context.Scope.SetTraversalDepth(1, maxDepth); // Default minDepth to 1
                _logger.LogDebug("Set traversal max depth: {MaxDepth}", maxDepth);
            }
            else if (node.Arguments.Count == 3)
            {
                // WithDepth(minDepth, maxDepth)
                var minDepth = AgeCypherQueryVisitor.EvaluateConstantExpression<int>(node.Arguments[1]);
                var maxDepth = AgeCypherQueryVisitor.EvaluateConstantExpression<int>(node.Arguments[2]);
                _context.Scope.SetTraversalDepth(minDepth, maxDepth);
                _logger.LogDebug("Set traversal depth range: {MinDepth}-{MaxDepth}", minDepth, maxDepth);
            }
        }

        // Continue processing the expression tree
        return _visitExpression(node.Arguments[0]);
    }

    public Expression HandleDirection(MethodCallExpression node)
    {
        _logger.LogDebug("Processing Direction method call");

        if (node.Arguments.Count >= 2)
        {
            var direction = AgeCypherQueryVisitor.EvaluateConstantExpression<GraphTraversalDirection>(node.Arguments[1]);

            // Persist the traversal direction on the scope so PathSegments can use it
            _context.Scope.SetTraversalDirection(direction);
            _logger.LogDebug("Set traversal direction: {Direction}", direction);
        }

        // Continue processing the expression tree
        return _visitExpression(node.Arguments[0]);
    }
}
