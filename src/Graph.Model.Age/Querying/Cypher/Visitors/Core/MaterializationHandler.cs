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
using System.Collections.Immutable;
using System.Linq;
using System.Linq.Expressions;
using Cvoya.Graph.Model;
using Cvoya.Graph.Model.Age.Querying.Cypher.Visitors;
using Cvoya.Graph.Model.Cypher.Querying.Cypher.Visitors.Core;
using Microsoft.Extensions.Logging;

/// <summary>
/// Handles materialization operations for AGE Cypher queries.
/// Supports First/Last/Single, ToList/ToArray, and query finalization.
/// </summary>
internal sealed class MaterializationHandler
{
    private readonly CypherQueryContext _context;
    private readonly ILogger _logger;
    private readonly Func<Expression, Expression> _visit;
    private readonly Func<string> _getContextualAlias;
    private readonly Action<string, string?, ImmutableArray<string>> _emitWhereFragment;
    private readonly Func<Expression, LambdaExpression?> _extractLambda;

    public MaterializationHandler(
        CypherQueryContext context,
        ILogger logger,
        Func<Expression, Expression> visit,
        Func<string> getContextualAlias,
        Action<string, string?, ImmutableArray<string>> emitWhereFragment,
        Func<Expression, LambdaExpression?> extractLambda)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _visit = visit ?? throw new ArgumentNullException(nameof(visit));
        _getContextualAlias = getContextualAlias ?? throw new ArgumentNullException(nameof(getContextualAlias));
        _emitWhereFragment = emitWhereFragment ?? throw new ArgumentNullException(nameof(emitWhereFragment));
        _extractLambda = extractLambda ?? throw new ArgumentNullException(nameof(extractLambda));
    }

    public Expression HandleFirst(MethodCallExpression node)
    {
        // Visit source FIRST (proper visitor pattern: children before parent)
        _visit(node.Arguments[0]);

        // Emit LimitFragment for LIMIT 1 directly
        var limitFragment = new LimitFragment(1, _context.Scope.CurrentAlias);
        _context.AddFragment(limitFragment);
        _logger.LogDebug("Emitted LimitFragment for First/FirstOrDefault with limit 1");

        // Handle optional predicate: First(p => p.Age > 18)
        if (node.Arguments.Count == 2)
        {
            var lambda = _extractLambda(node.Arguments[1]);
            if (lambda != null)
            {
                var expressionVisitor = CreateExpressionVisitor();
                var whereCondition = expressionVisitor.VisitAndReturnCypher(lambda.Body);
                _emitWhereFragment(whereCondition, null, default);
            }
        }

        return node;
    }

    public Expression HandleLast(MethodCallExpression node)
    {
        // Visit source FIRST (proper visitor pattern: children before parent)
        _visit(node.Arguments[0]);

        // Emit ReverseOrderFragment to reverse ORDER BY
        var reverseFragment = new ReverseOrderFragment();
        _context.AddFragment(reverseFragment);
        _logger.LogDebug("Emitted ReverseOrderFragment for Last()");

        // Emit LimitFragment for LIMIT 1
        var limitFragment = new LimitFragment(1, _context.Scope.CurrentAlias);
        _context.AddFragment(limitFragment);
        _logger.LogDebug("Emitted LimitFragment for Last()");

        // Handle optional predicate: Last(p => p.Age > 18)
        if (node.Arguments.Count == 2)
        {
            var lambda = _extractLambda(node.Arguments[1]);
            if (lambda != null)
            {
                var expressionVisitor = CreateExpressionVisitor();
                var whereCondition = expressionVisitor.VisitAndReturnCypher(lambda.Body);
                _emitWhereFragment(whereCondition, null, default);
            }
        }

        return node;
    }

    public Expression HandleSingle(MethodCallExpression node)
    {
        // Visit source FIRST (proper visitor pattern: children before parent)
        _visit(node.Arguments[0]);

        // Single needs LIMIT 2 to detect if there's more than one
        var limitFragment = new LimitFragment(2, _context.Scope.CurrentAlias);
        _context.AddFragment(limitFragment);
        _logger.LogDebug("Emitted LimitFragment for Single()");

        // Handle optional predicate: Single(p => p.Age > 18)
        if (node.Arguments.Count == 2)
        {
            var lambda = _extractLambda(node.Arguments[1]);
            if (lambda != null)
            {
                var expressionVisitor = CreateExpressionVisitor();
                var whereCondition = expressionVisitor.VisitAndReturnCypher(lambda.Body);
                _emitWhereFragment(whereCondition, null, default);
            }
        }

        return node;
    }

    public Expression HandleToList(MethodCallExpression node)
    {
        _logger.LogDebug("Processing ToList/ToArray method");

        // Visit source FIRST (proper visitor pattern)
        var result = _visit(node.Arguments[0]);

        // Check if we need to enable complex property loading for node queries
        var resultType = node.Type.GetGenericArguments().FirstOrDefault();

        // Check if this expression tree contains PathSegments calls
        bool containsPathSegments = ContainsPathSegmentsCall(node);
        var hasExplicitReturns = _context.HasExplicitReturnFragments();

        if (resultType != null && resultType.IsGenericType &&
            resultType.GetGenericTypeDefinition().Name.Contains("IGraphPathSegment"))
        {
            _logger.LogDebug("Processing path segment query of type {Type}", resultType.Name);

            // For path segment queries, return the path components ONLY if no projection exists
            if (!hasExplicitReturns)
            {
                // Path segments should reference the LAST hop (where traversal ends), not CurrentHop
                var lastHop = Math.Max(0, _context.Scope.CurrentHop - 1);
                var sourceAlias = _context.Scope.GetNumberedAliasForHop("src", lastHop);
                var relAlias = _context.Scope.GetNumberedAliasForHop("r", lastHop);
                var targetAlias = _context.Scope.GetNumberedAliasForHop("tgt", lastHop);
                var pathSegmentReturn = $"{sourceAlias}, {relAlias}, {targetAlias}";
                var returns = ImmutableArray.Create(sourceAlias, relAlias, targetAlias);
                var projectionFragment = new ProjectionFragment(returns, targetAlias);
                _context.AddFragment(projectionFragment);
                _logger.LogDebug("Emitted default path segment ProjectionFragment for hop {Hop}: {Return}", lastHop, pathSegmentReturn);
            }
            else
            {
                _logger.LogDebug("Skipping default path segment return - projection already exists");
            }
        }
        else if (resultType != null && typeof(INode).IsAssignableFrom(resultType))
        {
            _logger.LogDebug("Processing simple node query of type {Type}", resultType.Name);

            // For node queries, add context-aware return clause ONLY if no projection exists and not in path context
            bool isInPathContext = _context.FragmentSequence.OfType<MatchSegmentFragment>().Any();
            if (!containsPathSegments && !isInPathContext && !hasExplicitReturns)
            {
                var nodeAlias = _getContextualAlias();
                var nodeProjection = new ProjectionFragment(ImmutableArray.Create(nodeAlias), nodeAlias);
                _context.AddFragment(nodeProjection);
                _logger.LogDebug("Emitted default node ProjectionFragment with alias: {Alias}", nodeAlias);

                // Check if the node type has complex properties (INode properties)
                var hasComplexProperties = resultType.GetProperties()
                    .Any(p => typeof(INode).IsAssignableFrom(p.PropertyType));

                if (hasComplexProperties)
                {
                    // Enable complex property loading for nodes with INode properties
                    var optionalPattern = $"({nodeAlias})-[prop_rel]->(prop_node)";
                    var optionalFragment = new OptionalMatchFragment(
                        optionalPattern,
                        ImmutableArray.Create("prop_rel", "prop_node"),
                        ImmutableArray.Create(nodeAlias),
                        nodeAlias);
                    _context.AddFragment(optionalFragment);
                    _logger.LogDebug("Emitted OptionalMatchFragment for complex property loading");

                    // Always emit ComplexPropertyLoadingFragment to track state
                    var complexPropertyFragment = new ComplexPropertyLoadingFragment(true, nodeAlias);
                    _context.AddFragment(complexPropertyFragment);
                    _logger.LogDebug("Emitted ComplexPropertyLoadingFragment (enabled) for node query with complex properties");
                }
            }
            else
            {
                _logger.LogDebug("Skipping default node return - projection already exists or path context detected");
            }
        }
        else if (resultType != null && typeof(IRelationship).IsAssignableFrom(resultType))
        {
            _logger.LogDebug("Processing relationship query of type {Type}", resultType.Name);

            // For relationship queries, add default return ONLY if:
            // 1. No projection exists AND
            // 2. This is not part of a PathSegments chain (which handles its own returns)
            if (!hasExplicitReturns && !containsPathSegments)
            {
                var relAlias = _context.Scope.GetNumberedAlias("r");
                var relationshipProjection = new ProjectionFragment(ImmutableArray.Create(relAlias), relAlias);
                _context.AddFragment(relationshipProjection);
                _logger.LogDebug("Emitted default relationship ProjectionFragment with alias: {Alias}", relAlias);
            }
            else
            {
                if (containsPathSegments)
                {
                    _logger.LogDebug("Skipping default relationship return - PathSegments chain will handle returns");
                }
                else
                {
                    _logger.LogDebug("Skipping default relationship return - projection already exists");
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Finalizes the query by adding any missing default projections.
    /// Should be called after Visit() completes to ensure path segment queries
    /// without explicit ToList/ToArray calls still get proper projections.
    /// </summary>
    public void FinalizeQuery(Type elementType)
    {
        // Check if the element type is a path segment and if no projection has been added yet
        if (IsPathSegmentType(elementType) && !_context.HasExplicitReturnFragments())
        {
            // Add the default path segment projection
            var lastHop = Math.Max(0, _context.Scope.CurrentHop - 1);
            var sourceAlias = _context.Scope.GetNumberedAliasForHop("src", lastHop);
            var relAlias = _context.Scope.GetNumberedAliasForHop("r", lastHop);
            var targetAlias = _context.Scope.GetNumberedAliasForHop("tgt", lastHop);
            var returns = ImmutableArray.Create(sourceAlias, relAlias, targetAlias);
            var projectionFragment = new ProjectionFragment(returns, targetAlias);
            _context.AddFragment(projectionFragment);
            _logger.LogDebug("Finalized path segment query with default projection for hop {Hop}", lastHop);
        }
    }

    private bool ContainsPathSegmentsCall(Expression expression)
    {
        _logger.LogDebug("Scanning expression tree for PathSegments calls - Expression type: {Type}", expression.GetType().Name);

        if (expression is MethodCallExpression methodCall)
        {
            _logger.LogDebug("Found method call: {MethodName}", methodCall.Method.Name);

            // Check if this is a PathSegments call
            if (methodCall.Method.Name == "PathSegments")
            {
                _logger.LogDebug("Found PathSegments call!");
                return true;
            }

            // Recursively check arguments
            foreach (var arg in methodCall.Arguments)
            {
                if (ContainsPathSegmentsCall(arg))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool IsPathSegmentType(Type elementType)
    {
        return elementType.IsGenericType &&
               elementType.GetGenericTypeDefinition().Name.Contains("IGraphPathSegment");
    }

    private AgeExpressionToCypherVisitor CreateExpressionVisitor()
    {
        var alias = _context.Scope.CurrentAlias ?? _getContextualAlias();
        _logger.LogDebug("MaterializationHandler.CreateExpressionVisitor: Using alias '{Alias}'", alias);
        return new AgeExpressionToCypherVisitor(_context, _logger, alias);
    }
}
