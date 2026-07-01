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

using System.Collections.Immutable;
using System.Linq.Expressions;
using Cvoya.Graph.Model;
using Cvoya.Graph.Model.Age.Querying.Cypher.Visitors.Core.Modular;
using Cvoya.Graph.Model.Age.Querying.Linq.Queryables;
using Cvoya.Graph.Model.Cypher.Querying.Cypher.Visitors.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// AGE implementation of CypherQueryVisitor that translates LINQ expressions to Cypher queries.
/// Modeled after Neo4j's CypherQueryVisitor but adapted for AGE's specific Cypher dialect and infrastructure.
/// This visitor replaces the primitive type checking approach with comprehensive LINQ method routing.
/// </summary>
internal sealed class AgeCypherQueryVisitor : ExpressionVisitor
{
    private readonly CypherQueryContext _context;
    private readonly ILogger<AgeCypherQueryVisitor> _logger;

    // Modular specialized visitors for different query concerns
    private readonly TraversalFragmentVisitor _traversalVisitor;
    private readonly FilteringFragmentVisitor _filteringVisitor;
    private readonly ProjectionFragmentVisitor _projectionVisitor;
    private readonly AggregationFragmentVisitor _aggregationVisitor;
    private readonly JoinHandler _joinHandler;
    private readonly SearchHandler _searchHandler;
    private readonly MaterializationHandler _materializationHandler;
    private readonly QueryInitializationHandler _queryInitHandler;
    private readonly PathSegmentHandler _pathSegmentHandler;
    private readonly DegreeQueryHandler _degreeQueryHandler;

    public AgeCypherQueryVisitor(CypherQueryContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = context.LoggerFactory?.CreateLogger<AgeCypherQueryVisitor>() ?? NullLogger<AgeCypherQueryVisitor>.Instance;

        // Initialize modular specialized visitors
        _traversalVisitor = new TraversalFragmentVisitor(_context, _logger);
        _filteringVisitor = new FilteringFragmentVisitor(_context, _logger);
        _projectionVisitor = new ProjectionFragmentVisitor(_context, _logger);
        _aggregationVisitor = new AggregationFragmentVisitor(_context, _logger);

        // QueryInitializationHandler must be initialized before JoinHandler since JoinHandler
        // references SetupAdditionalMatch on the init handler.
        _queryInitHandler = new QueryInitializationHandler(_context, _logger, GetContextualAlias, EmitWhereFragment);
        _joinHandler = new JoinHandler(_context, _logger, Visit, ExtractLambda, _queryInitHandler.SetupAdditionalMatch);
        _searchHandler = new SearchHandler(_context, _logger, Visit, _queryInitHandler.SetupInitialMatch, EmitWhereFragment);
        _materializationHandler = new MaterializationHandler(
            _context, _logger, Visit, GetContextualAlias, EmitWhereFragment, ExtractLambda);
        _pathSegmentHandler = new PathSegmentHandler(_context, _logger, Visit, _traversalVisitor);
        _degreeQueryHandler = new DegreeQueryHandler(_context, _logger);
    }

    /// <summary>
    /// Main entry point - visits the root expression and builds the complete Cypher query.
    /// </summary>
    protected override Expression VisitMethodCall(MethodCallExpression node)
    {
        _logger.LogDebug("Processing LINQ method: {Method}", node.Method.Name);

        // Route LINQ methods to specific handlers using a single dispatch helper.
        // This eliminates 16 repetitive Handle* methods that all followed the pattern:
        //   Visit(node.Arguments[0]); handler.HandleXxx(node); return node;
        return node.Method.Name switch
        {
            // Core LINQ methods
            "Where" => HandleWhereWithDegreeDetection(node),
            "Select" => VisitThenSelect(node),
            "GroupBy" => VisitThen(node, n => _projectionVisitor.HandleGroupBy(n)),
            "Join" => _joinHandler.HandleJoin(node),
            "OrderBy" => VisitThen(node, n => _filteringVisitor.HandleOrderBy(n, descending: false, isThenBy: false)),
            "OrderByDescending" => VisitThen(node, n => _filteringVisitor.HandleOrderBy(n, descending: true, isThenBy: false)),
            "ThenBy" => VisitThen(node, n => _filteringVisitor.HandleOrderBy(n, descending: false, isThenBy: true)),
            "ThenByDescending" => VisitThen(node, n => _filteringVisitor.HandleOrderBy(n, descending: true, isThenBy: true)),
            "Take" => VisitThen(node, n => _filteringVisitor.HandleTake(n)),
            "Skip" => VisitThen(node, n => _filteringVisitor.HandleSkip(n)),
            "Distinct" => VisitThen(node, n => _filteringVisitor.HandleDistinct(n)),

            // Graph traversal methods
            "PathSegments" => _pathSegmentHandler.HandlePathSegments(node),
            "WithDepth" => _pathSegmentHandler.HandleWithDepth(node),
            "Direction" => _pathSegmentHandler.HandleDirection(node),

            // Full-text search from LINQ chain (e.g., .Where().Search("text"))
            "Search" => _searchHandler.HandleSearch(node),

            // Aggregation methods
            "Count" or "CountAsync" or "CountAsyncMarker" => VisitThen(node, n => _aggregationVisitor.HandleCount(n)),
            "LongCount" or "LongCountAsync" or "LongCountAsyncMarker" => VisitThen(node, n => _aggregationVisitor.HandleCount(n)),
            "Any" or "AnyAsync" or "AnyAsyncMarker" => VisitThen(node, n => _aggregationVisitor.HandleAny(n)),
            "All" or "AllAsync" or "AllAsyncMarker" => VisitThen(node, n => _aggregationVisitor.HandleAll(n)),
            "Sum" or "SumAsync" or "SumAsyncMarker" => VisitThen(node, n => _aggregationVisitor.HandleAggregationFunction(n, "SUM")),
            "Average" or "AverageAsync" or "AverageAsyncMarker" => VisitThen(node, n => _aggregationVisitor.HandleAggregationFunction(n, "AVG")),
            "Min" or "MinAsync" or "MinAsyncMarker" => VisitThen(node, n => _aggregationVisitor.HandleAggregationFunction(n, "MIN")),
            "Max" or "MaxAsync" or "MaxAsyncMarker" => VisitThen(node, n => _aggregationVisitor.HandleAggregationFunction(n, "MAX")),

            // Element access methods
            "First" or "FirstAsync" or "FirstAsyncMarker" => _materializationHandler.HandleFirst(node),
            "FirstOrDefault" or "FirstOrDefaultAsync" or "FirstOrDefaultAsyncMarker" => _materializationHandler.HandleFirst(node),
            "Last" or "LastAsync" or "LastAsyncMarker" => _materializationHandler.HandleLast(node),
            "LastOrDefault" or "LastOrDefaultAsync" or "LastOrDefaultAsyncMarker" => _materializationHandler.HandleLast(node),
            "Single" or "SingleAsync" or "SingleAsyncMarker" => _materializationHandler.HandleSingle(node),
            "SingleOrDefault" or "SingleOrDefaultAsync" or "SingleOrDefaultAsyncMarker" => _materializationHandler.HandleSingle(node),

            // Materialization methods
            "ToList" or "ToListAsync" or "ToListAsyncMarker" => _materializationHandler.HandleToList(node),
            "ToArray" or "ToArrayAsync" or "ToArrayAsyncMarker" => _materializationHandler.HandleToList(node),

            // If this is not a LINQ method, continue traversing
            _ => base.VisitMethodCall(node)
        };
    }

    /// <summary>
    /// Visits the source expression first, then delegates to the handler action.
    /// This replaces 16 repetitive Handle* methods with a single dispatch pattern.
    /// </summary>
    private Expression VisitThen(MethodCallExpression node, Action<MethodCallExpression> handler)
    {
        Visit(node.Arguments[0]);
        handler(node);
        return node;
    }

    /// <summary>
    /// Special handling for Select: visits source, delegates projection, then disables complex property loading.
    /// </summary>
    private Expression VisitThenSelect(MethodCallExpression node)
    {
        Visit(node.Arguments[0]);
        _projectionVisitor.HandleSelect(node);
        return node;
    }

    /// <summary>
    /// Handles Where clauses with degree query detection.
    /// Visits the source first, then checks if the predicate contains a
    /// closure-captured Count(lambda) pattern on a navigation property.
    /// If detected, delegates to DegreeQueryHandler; otherwise falls through
    /// to normal FilteringFragmentVisitor.HandleWhere.
    /// </summary>
    private Expression HandleWhereWithDegreeDetection(MethodCallExpression node)
    {
        // Visit source first (ensures MATCH is set up via QueryInitializationHandler)
        Visit(node.Arguments[0]);

        // Check if this is a degree query (Count in predicate on navigation property)
        if (_degreeQueryHandler.TryHandleDegreeWhereClause(node))
        {
            _logger.LogDebug("HandleWhereWithDegreeDetection: handled as degree query");
            return node;
        }

        // Fall through to normal Where handling
        _filteringVisitor.HandleWhere(node);
        _logger.LogDebug("HandleWhereWithDegreeDetection: handled as normal Where");
        return node;
    }

    private void EmitWhereFragment(string predicate, string? alias, ImmutableArray<string> consumedAliases)
    {
        if (string.IsNullOrWhiteSpace(predicate))
        {
            return;
        }

        var normalizedConsumed = consumedAliases.IsDefault ? ImmutableArray<string>.Empty : consumedAliases;
        var currentAlias = alias ?? _context.Scope.CurrentAlias ?? "src0";
        var fragment = new WhereFragment(predicate, normalizedConsumed, currentAlias);
        _context.AddFragment(fragment);
        _logger.LogDebug("Emitted WhereFragment for alias {Alias}: {Predicate}", currentAlias, predicate);
    }



    /// <summary>
    /// This is called for the root queryable (e.g., context.Nodes&lt;Person&gt;())
    /// Sets up the initial MATCH clause based on the element type.
    /// </summary>
    protected override Expression VisitConstant(ConstantExpression node)
    {
        // Check if this is a queryable root
        if (node.Value != null && node.Type.IsGenericType)
        {
            var genericTypeDefinition = node.Type.GetGenericTypeDefinition();
            if (genericTypeDefinition.Name.Contains("Queryable"))
            {
                var elementType = node.Type.GetGenericArguments().FirstOrDefault();
                if (elementType != null)
                {
                    _queryInitHandler.SetupInitialMatch(elementType);
                }
            }
        }

        return base.VisitConstant(node);
    }

    protected override Expression VisitExtension(Expression node)
    {
        if (node is AgeFullTextSearchExpression searchExpr)
        {
            _logger.LogDebug("Handling AGE full text search expression for query: {Query}", searchExpr.SearchQuery);
            _searchHandler.HandleAgeFullTextSearch(searchExpr);
            return node;
        }

        return base.VisitExtension(node);
    }

    internal static T EvaluateConstantExpression<T>(Expression expression)
    {
        if (expression is ConstantExpression constant && constant.Value is T value)
        {
            return value;
        }

        // Try to evaluate the expression
        try
        {
            var lambda = Expression.Lambda<Func<T>>(Expression.Convert(expression, typeof(T)));
            var compiled = lambda.Compile();
            return compiled();
        }
        catch (Exception ex)
        {
            throw new ArgumentException($"Cannot evaluate expression to {typeof(T).Name}", ex);
        }
    }

    private static LambdaExpression? ExtractLambda(Expression expression)
    {
        // Handle quoted lambda expressions
        if (expression is UnaryExpression { NodeType: ExpressionType.Quote } quote)
        {
            expression = quote.Operand;
        }

        return expression as LambdaExpression;
    }

    /// <summary>
    /// Gets the appropriate alias for the current context.
    /// In path segment contexts, returns "src" (source node alias).
    /// In relationship contexts, returns "r" (relationship alias).
    /// In regular node contexts, returns "n".
    /// </summary>
    /// <returns>The contextual alias to use for queries.</returns>
    private string GetContextualAlias()
    {
        bool isInPathContext = _context.FragmentSequence.OfType<MatchSegmentFragment>().Any();
        if (isInPathContext)
        {
            return _context.Scope.GetNumberedAlias("src"); // Use numbered source node alias in path segment contexts
        }

        // Path segment root types (IGraphPathSegment) should use "src" alias
        if (typeof(IGraphPathSegment).IsAssignableFrom(_context.Scope.RootType))
            return _context.Scope.GetNumberedAlias("src");

        // Check if this is a relationship query
        if (typeof(IRelationship).IsAssignableFrom(_context.Scope.RootType))
        {
            return _context.Scope.GetNumberedAlias("r"); // Use numbered relationship alias for relationship queries
        }

        // Use src as the standard alias for source nodes (consistent with Neo4j provider)
        return _context.Scope.GetNumberedAlias("src");
    }

    /// <summary>
    /// Finalizes the query by adding any missing default projections.
    /// This should be called after Visit() completes to ensure path segment queries
    /// without explicit ToList/ToArray calls still get proper projections.
    /// </summary>
    public void FinalizeQuery(Type elementType)
    {
        _materializationHandler.FinalizeQuery(elementType);
    }
}
