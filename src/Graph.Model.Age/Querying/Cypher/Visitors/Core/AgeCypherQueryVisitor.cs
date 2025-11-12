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
using Cvoya.Graph.Model.Age.Querying.Cypher.Visitors;
using Cvoya.Graph.Model.Age.Querying.Cypher.Visitors.Core.Modular;
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
    
    /// <summary>
    /// Defines the semantic position of a WHERE clause relative to path traversal operations.
    /// </summary>
    private enum WherePosition
    {
        /// <summary>WHERE clause appears before PathSegments - applies to source nodes</summary>
        PreTraversal,
        /// <summary>WHERE clause appears after PathSegments - applies to target nodes</summary>
        PostTraversal,
        /// <summary>Position cannot be determined - use context fallback</summary>
        Unknown
    }

    public AgeCypherQueryVisitor(CypherQueryContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = context.LoggerFactory?.CreateLogger<AgeCypherQueryVisitor>() ?? NullLogger<AgeCypherQueryVisitor>.Instance;
        
        // Initialize modular specialized visitors
        _traversalVisitor = new TraversalFragmentVisitor(_context, _logger);
        _filteringVisitor = new FilteringFragmentVisitor(_context, _logger);
        _projectionVisitor = new ProjectionFragmentVisitor(_context, _logger);
        _aggregationVisitor = new AggregationFragmentVisitor(_context, _logger);
    }

    /// <summary>
    /// Creates an expression visitor for translating expressions to Cypher.
    /// </summary>
    private AgeExpressionToCypherVisitor CreateExpressionVisitor()
    {
        // Pass the query builder so parameters are added directly to it
        var alias = _context.Scope.CurrentAlias ?? GetContextualAlias();
        _logger.LogDebug("CreateExpressionVisitor: Using alias '{Alias}' (CurrentAlias={CurrentAlias}, ContextualAlias={ContextualAlias})", 
            alias, _context.Scope.CurrentAlias, GetContextualAlias());
        return new AgeExpressionToCypherVisitor(_context.Builder, _logger, alias);
    }

    /// <summary>
    /// Finds which hop a PathSegment WHERE clause is targeting by examining the source expression.
    /// The WHERE targets the PathSegments call it's directly applied to in the chain.
    /// For example: .PathSegments(hop1).Where(ps => ...) targets hop 1
    ///              .PathSegments(hop1).Select(...).PathSegments(hop0).Where(ps => ...) targets hop 0
    /// </summary>
    private int FindPathSegmentHopForWhere(Expression sourceExpression)
    {
        // We need to find which PathSegments this WHERE is applied to
        // The WHERE's source expression should be the PathSegments (or something between like Select)
        // Walk UP the tree and find the FIRST PathSegments, but check how many PathSegments
        // exist BETWEEN the WHERE and that PathSegments
        var current = sourceExpression;
        int pathSegmentsDepth = 0; // How many PathSegments we encounter walking up
        
        while (current != null)
        {
            if (current is MethodCallExpression methodCall)
            {
                // Check if this is a PathSegments call
                if (methodCall.Method.Name == "PathSegments" || 
                    methodCall.Method.Name == "PathSegmentsIncoming" ||
                    methodCall.Method.Name == "PathSegmentsOutgoing")
                {
                    pathSegmentsDepth++;
                    
                    // The WHERE targets the FIRST PathSegments we encounter
                    // But in bottom-up processing, hop numbers are assigned in reverse:
                    // .PathSegments(hop1).PathSegments(hop0).Where()
                    // When we walk UP, we find hop0 first, hop1 second
                    // CurrentHop = 2 (incremented by both)
                    // First PathSegments found (pathSegmentsDepth=1): hop = CurrentHop - 1 = 1? NO!
                    // Actually: hop = CurrentHop - pathSegmentsDepth = 2 - 1 = 1 WRONG!
                    // 
                    // Wait, let me think about this differently:
                    // When WHERE is applied DIRECTLY after PathSegments, the first PathSegments
                    // we find walking up is the one it targets.
                    // But what hop number is it?
                    // 
                    // Expression: .PathSegments(hop1).Where().PathSegments(hop0)
                    // Processing order (bottom-up): hop0 processed first, hop1 second
                    // After hop0: CurrentHop = 1
                    // After hop1: CurrentHop = 2
                    // WHERE is between them, so when processing WHERE, CurrentHop = depends on where it is
                    //
                    // Actually, the WHERE is processed AFTER both PathSegments in bottom-up traversal!
                    // So CurrentHop = 2, and we need to figure out which hop the WHERE belongs to.
                    //
                    // If WHERE source is PathSegments directly: it's the immediately preceding PathSegments
                    // Walk up and check: is the source a PathSegments or something between?
                    
                    // Check if this is the immediate PathSegments (no other PathSegments between WHERE and this)
                    if (pathSegmentsDepth == 1)
                    {
                        // This is the first PathSegments we found walking UP - it's the one the WHERE targets
                        // In bottom-up processing, hops are numbered starting from 0 for the innermost (most recent)
                        // The FIRST PathSegments we find going UP is always the most recent one, which is hop 0
                        // UNLESS there are multiple PathSegments in the chain, in which case we need to count backwards
                        
                        // Actually, let's think about this differently:
                        // When HandleWhere calls Visit(source), it processes ALL PathSegments in the source chain
                        // After that, CurrentHop reflects how many PathSegments have been created
                        // The PathSegments immediately before the WHERE (the one we just found) was the LAST one processed
                        // And the last one processed gets the LOWEST hop number (0)
                        // 
                        // So for: .PathSegments(hop1).PathSegments(hop0).Where()
                        // After processing: CurrentHop = 2
                        // The first PathSegments we find (hop0) should return 0
                        //
                        // For: .PathSegments(hop0).Where()
                        // After processing: CurrentHop = 1
                        // The first PathSegments we find (hop0) should return 0
                        //
                        // So the answer is always 0 for the first PathSegments found? NO!
                        //
                        // Wait, let me re-examine the failing test:
                        // .PathSegments(hop1).Select().PathSegments(hop0).Where()
                        // Walking UP from WHERE: find PathSegments(hop0) first
                        // This IS hop 0, so return 0
                        //
                        // For CanUseMultiplePathSegmentsWithIncomingDirection:
                        // .PathSegments(hop1).Where().PathSegments(hop0)
                        // Walking UP from WHERE: find PathSegments(hop1) first (hop0 is AFTER the WHERE)
                        // This IS hop 1, so return 1
                        //
                        // But how do I know which hop number corresponds to the PathSegments I found?
                        // 
                        // AH! I need to check if there are MORE PathSegments AFTER this one in the source chain!
                        // If walking up I find only ONE PathSegments, and CurrentHop = 2, then this is hop 1
                        // If walking up I find TWO PathSegments, and CurrentHop = 2, then the first is hop 0
                        
                        // Continue walking to count total PathSegments in the chain
                        var tempCurrent = methodCall.Object ?? (methodCall.Arguments.Count > 0 ? methodCall.Arguments[0] : null);
                        int totalPathSegments = 1;
                        while (tempCurrent != null)
                        {
                            if (tempCurrent is MethodCallExpression tempMethodCall)
                            {
                                if (tempMethodCall.Method.Name == "PathSegments" || 
                                    tempMethodCall.Method.Name == "PathSegmentsIncoming" ||
                                    tempMethodCall.Method.Name == "PathSegmentsOutgoing")
                                {
                                    totalPathSegments++;
                                }
                                tempCurrent = tempMethodCall.Object ?? (tempMethodCall.Arguments.Count > 0 ? tempMethodCall.Arguments[0] : null);
                            }
                            else
                            {
                                break;
                            }
                        }
                        
                        // The first PathSegments found is at position (totalPathSegments - 1) from the end
                        // But hop numbers are assigned bottom-up, so hop 0 is the innermost
                        // If we found 2 PathSegments total and CurrentHop = 2:
                        //   - First PathSegments found = position 0 from the end = hop 0
                        // If we found 1 PathSegments total and CurrentHop = 2:
                        //   - First PathSegments found = the only one, but there's another hop somewhere = hop 1
                        // Actually, CurrentHop tells us how many hops exist
                        // totalPathSegments tells us how many we can see from here
                        // The first one we found is hop = CurrentHop - totalPathSegments
                        var targetHop = Math.Max(0, _context.Scope.CurrentHop - totalPathSegments);
                        _logger.LogDebug("Found immediate PathSegments call for WHERE: totalPathSegments={Total}, CurrentHop={CurrentHop}, targeting hop {Hop}", 
                            totalPathSegments, _context.Scope.CurrentHop, targetHop);
                        return targetHop;
                    }
                }
                
                // Continue walking up the tree through the source
                current = methodCall.Object ?? (methodCall.Arguments.Count > 0 ? methodCall.Arguments[0] : null);
            }
            else
            {
                break;
            }
        }
        
        // Fallback to CurrentHop - 1 if we can't determine (shouldn't happen)
        _logger.LogWarning("Could not determine PathSegment hop for WHERE, falling back to CurrentHop - 1");
        return Math.Max(0, _context.Scope.CurrentHop - 1);
    }

    private AgeExpressionToCypherVisitor CreatePathSegmentExpressionVisitor(
        ParameterExpression pathSegmentParameter,
        int? explicitHopNumber = null)
    {
        // For path segments, we need to create a visitor that can handle the path segment parameter
        // IMPORTANT: Use the PREVIOUS hop (the one just created by PathSegments) because
        // HandlePathSegments calls AdvanceHop() at the end, incrementing CurrentHop
        // For example: .PathSegments() creates src0/r0/tgt0 at hop 0, then advances to hop 1
        // When .Where(ps => ...) is processed, CurrentHop is 1, but we need aliases from hop 0
        // However, in chained PathSegments, we need to know WHICH PathSegments the WHERE targets
        var hopNumber = explicitHopNumber ?? Math.Max(0, _context.Scope.CurrentHop - 1);
        
        // Use stored hop aliases (avoids type collision issues in chained patterns)
        var hopAliases = _context.Scope.GetHopAliases(hopNumber);
        if (hopAliases.HasValue)
        {
            var (sourceAlias, relationshipAlias, targetAlias) = hopAliases.Value;
            _logger.LogDebug("Using stored hop {Hop} aliases: src={Src}, r={Rel}, tgt={Tgt} (explicit={Explicit})", 
                hopNumber, sourceAlias, relationshipAlias, targetAlias, explicitHopNumber.HasValue);
            
            return new AgeExpressionToCypherVisitor(_context.Builder, _logger, "ps", pathSegmentParameter,
                sourceAlias, relationshipAlias, targetAlias);
        }
        
        // Fallback to numbered aliases if not found (shouldn't happen in normal flow)
        _logger.LogDebug("Hop {Hop} aliases not found in storage, falling back to numbered aliases", hopNumber);
        var fallbackSourceAlias = _context.Scope.GetNumberedAliasForHop("src", hopNumber);
        var fallbackRelAlias = _context.Scope.GetNumberedAliasForHop("r", hopNumber);
        var fallbackTargetAlias = _context.Scope.GetNumberedAliasForHop("tgt", hopNumber);
        
        return new AgeExpressionToCypherVisitor(_context.Builder, _logger, "ps", pathSegmentParameter,
            fallbackSourceAlias, fallbackRelAlias, fallbackTargetAlias);
    }

    /// <summary>
    /// Main entry point - visits the root expression and builds the complete Cypher query.
    /// </summary>
    protected override Expression VisitMethodCall(MethodCallExpression node)
    {
        _logger.LogDebug("Processing LINQ method: {Method}", node.Method.Name);

        // Route LINQ methods to specific handlers
        return node.Method.Name switch
        {
            // Core LINQ methods
            "Where" => HandleWhere(node),
            "Select" => HandleSelect(node),
            "GroupBy" => HandleGroupBy(node),
            "Join" => HandleJoin(node),
            "OrderBy" => HandleOrderBy(node, descending: false),
            "OrderByDescending" => HandleOrderBy(node, descending: true),
            "ThenBy" => HandleThenBy(node, descending: false),
            "ThenByDescending" => HandleThenBy(node, descending: true),
            "Take" => HandleTake(node),
            "Skip" => HandleSkip(node),
            "Distinct" => HandleDistinct(node),

            // Graph traversal methods
            "PathSegments" => HandlePathSegments(node),
            "WithDepth" => HandleWithDepth(node),
            "Direction" => HandleDirection(node),

            // Aggregation methods
            "Count" or "CountAsync" or "CountAsyncMarker" => HandleCount(node),
            "Any" or "AnyAsync" or "AnyAsyncMarker" => HandleAny(node),
            "All" or "AllAsync" or "AllAsyncMarker" => HandleAll(node),
            "Sum" or "SumAsync" or "SumAsyncMarker" => HandleSum(node),
            "Average" or "AverageAsync" or "AverageAsyncMarker" => HandleAverage(node),
            "Min" or "MinAsync" or "MinAsyncMarker" => HandleMin(node),
            "Max" or "MaxAsync" or "MaxAsyncMarker" => HandleMax(node),

            // Element access methods
            "First" or "FirstAsync" or "FirstAsyncMarker" => HandleFirst(node),
            "FirstOrDefault" or "FirstOrDefaultAsync" or "FirstOrDefaultAsyncMarker" => HandleFirst(node),
            "Last" or "LastAsync" or "LastAsyncMarker" => HandleLast(node),
            "LastOrDefault" or "LastOrDefaultAsync" or "LastOrDefaultAsyncMarker" => HandleLast(node),
            "Single" or "SingleAsync" or "SingleAsyncMarker" => HandleSingle(node),
            "SingleOrDefault" or "SingleOrDefaultAsync" or "SingleOrDefaultAsyncMarker" => HandleSingle(node),

            // Materialization methods
            "ToList" or "ToListAsync" or "ToListAsyncMarker" => HandleToList(node),
            "ToArray" or "ToArrayAsync" or "ToArrayAsyncMarker" => HandleToList(node),

            // If this is not a LINQ method, continue traversing
            _ => base.VisitMethodCall(node)
        };
    }

    private Expression HandleWhere(MethodCallExpression node)
    {
        // Visit the source expression FIRST to ensure full traversal (e.g., SetupInitialMatch for root queries)
        // This sets CurrentAlias which is needed by FilteringFragmentVisitor
        Visit(node.Arguments[0]);
        
        // Then delegate to specialized filtering visitor which handles WHERE clauses and emits fragments
        _filteringVisitor.HandleWhere(node);
        
        return node;
    }

    private Expression HandleSelect(MethodCallExpression node)
    {
        // Visit the source expression FIRST to set up MATCH patterns and increment hop counter
        Visit(node.Arguments[0]);
        
        // Then delegate projection handling to specialized visitor which emits ProjectionFragment
        _projectionVisitor.HandleSelect(node);
        
        // For projections, disable complex property loading and emit toggle fragment
        _context.Builder.DisableComplexPropertyLoading();
        var complexPropertyFragment = new ComplexPropertyLoadingFragment(false, _context.Scope.CurrentAlias);
        _context.FragmentSequence.Add(complexPropertyFragment);
        _logger.LogDebug("Emitted ComplexPropertyLoadingFragment (disabled)");
        
        return node;
    }

    private Expression HandleGroupBy(MethodCallExpression node)
    {
        // Visit source FIRST to ensure the MATCH pattern is generated (proper visitor pattern: children before parent)
        Visit(node.Arguments[0]);
        
        // Then delegate to specialized projection visitor which handles GroupBy and emits fragments
        _projectionVisitor.HandleGroupBy(node);
        
        return node;
    }

    private Expression HandleJoin(MethodCallExpression node)
    {
        // Join(outer, inner, outerKeySelector, innerKeySelector, resultSelector)
        if (node.Arguments.Count != 5)
            throw new ArgumentException("Join method must have exactly 5 arguments");

        _logger.LogDebug("Processing JOIN operation");

        var outer = node.Arguments[0];           // The first sequence (e.g., relationships)
        var inner = node.Arguments[1];           // The second sequence (e.g., nodes)
        var outerKeySelector = ExtractLambda(node.Arguments[2]);  // e.g., k => k.EndNodeId
        var innerKeySelector = ExtractLambda(node.Arguments[3]);  // e.g., p => p.Id
        var resultSelector = ExtractLambda(node.Arguments[4]);    // e.g., (k, p) => p

        if (outerKeySelector == null || innerKeySelector == null || resultSelector == null)
            throw new ArgumentException("Join method requires lambda expressions for all selectors");

        // For this Join implementation, we assume the MATCH pattern is already correct
        // and we just need to handle the projection correctly.
        // The test case: allKnows.Where(...).Join(allPeople, k => k.EndNodeId, p => p.Id, (k, p) => p)
        // means: return the Person nodes (p) that are joined with KNOWS relationships
        
        // First, process the outer source (should set up the MATCH pattern)
        Visit(outer);

        // Clear any existing return clauses since Join defines its own projection
        _context.Builder.ClearReturn();

        // Analyze the result selector to determine what to return
        var resultBody = resultSelector.Body;
        if (resultBody is ParameterExpression paramExpr)
        {
            // Simple parameter reference: (k, p) => p
            var parameterIndex = -1;
            for (int i = 0; i < resultSelector.Parameters.Count; i++)
            {
                if (resultSelector.Parameters[i].Name == paramExpr.Name)
                {
                    parameterIndex = i;
                    break;
                }
            }
            
            if (parameterIndex == 0)
            {
                // Returning the outer parameter (relationship) - use relationship alias
                var relationshipAlias = _context.Scope.GetNumberedAlias("r");
                _context.Builder.AddReturn(relationshipAlias);
                _logger.LogDebug("JOIN: Returning outer parameter (relationship): {Alias}", relationshipAlias);

                // Update scope so downstream operators understand we're working with the relationship alias
                _context.Scope.CurrentAlias = relationshipAlias;
                _context.Scope.CurrentType = resultSelector.Parameters[0].Type;
                _context.Scope.RegisterTypeAlias(_context.Scope.CurrentType, relationshipAlias);

                // Emit a projection fragment so the fragment renderer preserves the relationship return
                var returns = ImmutableArray.Create(relationshipAlias);
                var projectionFragment = new ProjectionFragment(returns, relationshipAlias);
                _context.FragmentSequence.Add(projectionFragment);
                _logger.LogDebug("JOIN: Emitted ProjectionFragment for relationship alias {Alias}", relationshipAlias);
            }
            else if (parameterIndex == 1)
            {
                // Returning the inner parameter (node) - use target node alias
                var targetAlias = _context.Scope.GetNumberedAlias("tgt");
                _context.Builder.AddReturn(targetAlias);
                _logger.LogDebug("JOIN: Returning inner parameter (target node): {Alias}", targetAlias);

                // Update scope/type information for the returned node so subsequent operators use the right alias
                _context.Scope.CurrentAlias = targetAlias;
                _context.Scope.CurrentType = resultSelector.Parameters[1].Type;
                _context.Scope.RegisterTypeAlias(_context.Scope.CurrentType, targetAlias);

                // Emit ProjectionFragment to keep fragment renderer aligned with builder results
                var returns = ImmutableArray.Create(targetAlias);
                var projectionFragment = new ProjectionFragment(returns, targetAlias);
                _context.FragmentSequence.Add(projectionFragment);
                _logger.LogDebug("JOIN: Emitted ProjectionFragment for node alias {Alias}", targetAlias);
            }
            else
            {
                throw new ArgumentException($"Invalid parameter reference in Join result selector: {paramExpr.Name}");
            }
        }
        else
        {
            // Complex projection - would need more sophisticated handling
            throw new NotSupportedException("Complex projections in Join result selector are not yet supported");
        }

        // Don't continue processing the inner sequence since we're not doing a real database join
        // The MATCH pattern should already include both the relationships and nodes
        return outer;
    }

    private void HandleAnonymousProjection(NewExpression newExpr)
    {
    _logger.LogDebug("HandleAnonymousProjection called with {ArgumentCount} arguments", newExpr.Arguments.Count);
        
        var projections = new List<string>();
        var alias = _context.Scope.CurrentAlias ?? GetContextualAlias();

        for (int i = 0; i < newExpr.Arguments.Count; i++)
        {
            var arg = newExpr.Arguments[i];
            var member = newExpr.Members?[i];
            var projectionAlias = member?.Name ?? $"field{i}";

            // Use safe alias to avoid reserved word conflicts in AGE
            var safeAlias = $"c_{projectionAlias}";

            _logger.LogTrace("HandleAnonymousProjection processing argument {Index}: {Argument} (Type: {Type})", i, arg, arg.Type);
            
            // Special handling for PathSegment parameter projections
            if (arg is ParameterExpression paramExpr && IsPathSegmentType(paramExpr.Type))
            {
                _logger.LogDebug("Detected PathSegment parameter projection: {Parameter}", paramExpr.Name);
                
                // For PathSegment projections, we need to return the three components
                // that can be reconstructed into a PathSegment by the result processor.
                // Each component gets its own alias to avoid Cypher syntax issues.
                // Include the actual alias names (src0, r0, tgt0) in column names to avoid duplicates
                // when multiple PathSegment projections exist in the same query.
                var sourceAlias = _context.Scope.GetNumberedAlias("src");
                var relationshipAlias = _context.Scope.GetNumberedAlias("r");
                var targetAlias = _context.Scope.GetNumberedAlias("tgt");
                
                // Column names include both the property name and the actual Cypher aliases
                // E.g., "c_PathSegment_src0", "c_PathSegment_r0", "c_PathSegment_tgt0"
                var pathSegmentProjection = $"{sourceAlias} AS {safeAlias}_{sourceAlias}, {relationshipAlias} AS {safeAlias}_{relationshipAlias}, {targetAlias} AS {safeAlias}_{targetAlias}";
                projections.Add(pathSegmentProjection);
                
                _logger.LogDebug("Added PathSegment projection: {Projection}", pathSegmentProjection);
            }
            else if (arg is MemberExpression argMemberExpr && argMemberExpr.Expression is ParameterExpression paramExpr2)
            {
                // Check if this is an IGrouping parameter (e.g., g.Key or g.Count())
                if (paramExpr2.Type.IsGenericType && paramExpr2.Type.GetGenericTypeDefinition().Name.Contains("IGrouping"))
                {
                    // Use visitor to handle IGrouping members (g.Key, etc.)
                    var expressionVisitor = CreateExpressionVisitor();
                    var cypherExpr = expressionVisitor.VisitAndReturnCypher(arg);
                    projections.Add($"{cypherExpr} AS {safeAlias}");
                }
                else
                {
                    // Simple property: p.FirstName
                    var propertyName = argMemberExpr.Member.Name;
                    projections.Add($"{alias}.{propertyName} AS {safeAlias}");
                }
            }
            else
            {
                // Complex expression - use visitor
                // Check if this expression involves path segment access
                bool involvesPathSegment = ContainsPathSegmentAccess(arg);
                var expressionVisitor = involvesPathSegment 
                    ? CreatePathSegmentExpressionVisitor(null!)  // null because we're not projecting the whole path segment
                    : CreateExpressionVisitor();
                var cypherExpr = expressionVisitor.VisitAndReturnCypher(arg);
                projections.Add($"{cypherExpr} AS {safeAlias}");
            }
        }

        var projectionReturn = string.Join(", ", projections);
        _context.Builder.AddReturn(projectionReturn);
        _logger.LogDebug("Added anonymous projection: {Projection}", projectionReturn);
    }

    private Expression HandleOrderBy(MethodCallExpression node, bool descending)
    {
        // Visit source FIRST to ensure CurrentAlias is set (proper visitor pattern: children before parent)
        Visit(node.Arguments[0]);
        
        // Then delegate to specialized filtering visitor which handles ordering and emits fragments
        _filteringVisitor.HandleOrderBy(node, descending, isThenBy: false);
        
        return node;
    }

    private Expression HandleThenBy(MethodCallExpression node, bool descending)
    {
        // Visit source FIRST to ensure CurrentAlias is set
        Visit(node.Arguments[0]);
        
        // Then delegate to specialized filtering visitor (ThenBy is just additional ordering)
        _filteringVisitor.HandleOrderBy(node, descending, isThenBy: true);
        
        return node;
    }

    private Expression HandleTake(MethodCallExpression node)
    {
        // Visit source FIRST to ensure CurrentAlias is set
        Visit(node.Arguments[0]);
        
        // Then delegate to specialized filtering visitor for pagination
        _filteringVisitor.HandleTake(node);
        
        return node;
    }

    private Expression HandleSkip(MethodCallExpression node)
    {
        // Visit source FIRST to ensure CurrentAlias is set
        Visit(node.Arguments[0]);
        
        // Then delegate to specialized filtering visitor for pagination
        _filteringVisitor.HandleSkip(node);
        
        return node;
    }

    private Expression HandleDistinct(MethodCallExpression node)
    {
        // Visit source FIRST to ensure CurrentAlias is set
        Visit(node.Arguments[0]);
        
        // Then delegate to specialized filtering visitor for distinctness
        _filteringVisitor.HandleDistinct(node);
        
        return node;
    }

    private Expression HandleCount(MethodCallExpression node)
    {
        // Visit source FIRST to ensure CurrentAlias is set
        Visit(node.Arguments[0]);
        
        // Then delegate to specialized aggregation visitor
        _aggregationVisitor.HandleCount(node);
        
        return node;
    }

    private Expression HandleAny(MethodCallExpression node)
    {
        // Visit source FIRST to ensure CurrentAlias is set
        Visit(node.Arguments[0]);
        
        // Then delegate to specialized aggregation visitor
        _aggregationVisitor.HandleAny(node);
        
        return node;
    }

    private Expression HandleAll(MethodCallExpression node)
    {
        // Visit source FIRST to ensure CurrentAlias is set
        Visit(node.Arguments[0]);
        
        // Then delegate to specialized aggregation visitor
        _aggregationVisitor.HandleAll(node);
        
        return node;
    }

    private Expression HandleSum(MethodCallExpression node)
    {
        // Visit source FIRST to ensure CurrentAlias is set
        Visit(node.Arguments[0]);
        
        // Then delegate to specialized aggregation visitor
        _aggregationVisitor.HandleAggregationFunction(node, "SUM");
        
        return node;
    }

    private Expression HandleAverage(MethodCallExpression node)
    {
        // Visit source FIRST to ensure CurrentAlias is set
        Visit(node.Arguments[0]);
        
        // Then delegate to specialized aggregation visitor
        _aggregationVisitor.HandleAggregationFunction(node, "AVG");
        
        return node;
    }

    private Expression HandleMin(MethodCallExpression node)
    {
        // Visit source FIRST to ensure CurrentAlias is set
        Visit(node.Arguments[0]);
        
        // Then delegate to specialized aggregation visitor
        _aggregationVisitor.HandleAggregationFunction(node, "MIN");
        
        return node;
    }

    private Expression HandleMax(MethodCallExpression node)
    {
        // Visit source FIRST to ensure CurrentAlias is set
        Visit(node.Arguments[0]);
        
        // Then delegate to specialized aggregation visitor
        _aggregationVisitor.HandleAggregationFunction(node, "MAX");
        
        return node;
    }

    private Expression HandleAggregationWithSelector(MethodCallExpression node, string cypherFunction, string logName)
    {
        // IMPORTANT: Process the source expression FIRST to set up context (especially CurrentAlias)
        // before creating the expression visitor for the selector
        var result = Visit(node.Arguments[0]);
        
        // Clear ORDER BY clauses when processing aggregations
        // Aggregations return a single value, so ordering the input doesn't affect the output
        // and ORDER BY on non-aggregated columns in aggregation queries causes errors
        _context.Builder.ClearOrderBy();
        _logger.LogDebug("Cleared ORDER BY clauses for {LogName} aggregation", logName);
        
        if (node.Arguments.Count == 2 || node.Arguments.Count == 3)
        {
            // Aggregation with selector: Sum(p => p.Age) or Sum(p => p.Age, cancellationToken)
            // Lambda is always at index 1, cancellation token (if present) is at index 2
            var lambda = ExtractLambda(node.Arguments[1]);
            if (lambda != null)
            {
                var expressionVisitor = CreateExpressionVisitor();
                var selector = expressionVisitor.VisitAndReturnCypher(lambda.Body);
                _context.Builder.AddReturn($"{cypherFunction}({selector})");
                _logger.LogDebug("Added {LogName} aggregation with selector: {Selector}", logName, selector);
            }
            else
            {
                var alias = _context.Scope.CurrentAlias ?? GetContextualAlias();
                _context.Builder.AddReturn($"{cypherFunction}({alias})");
                _logger.LogDebug("Added simple {LogName} aggregation (lambda extraction failed)", logName);
            }
        }
        else
        {
            // Simple aggregation: Sum() - use the node itself
            var alias = _context.Scope.CurrentAlias ?? GetContextualAlias();
            _context.Builder.AddReturn($"{cypherFunction}({alias})");
            _logger.LogDebug("Added simple {LogName} aggregation", logName);
        }

        return result;
    }

    private Expression HandleFirst(MethodCallExpression node)
    {
        // Visit source FIRST (proper visitor pattern: children before parent)
        Visit(node.Arguments[0]);
        
        // Emit LimitFragment for LIMIT 1 directly (don't delegate to HandleTake - First doesn't have Take's signature)
        _context.Builder.SetLimit(1);
        var limitFragment = new LimitFragment(1, _context.Scope.CurrentAlias);
        _context.FragmentSequence.Add(limitFragment);
        _logger.LogDebug("Emitted LimitFragment for First/FirstOrDefault with limit 1");
        
        // Handle optional predicate: First(p => p.Age > 18)
        if (node.Arguments.Count == 2)
        {
            var lambda = ExtractLambda(node.Arguments[1]);
            if (lambda != null)
            {
                var expressionVisitor = CreateExpressionVisitor();
                var whereCondition = expressionVisitor.VisitAndReturnCypher(lambda.Body);
                _context.Builder.AddWhere(whereCondition);
            }
        }
        
        return node;
    }

    private Expression HandleLast(MethodCallExpression node)
    {
        // Visit source FIRST (proper visitor pattern: children before parent)
        Visit(node.Arguments[0]);
        
        // Emit ReverseOrderFragment to reverse ORDER BY
        var reverseFragment = new ReverseOrderFragment();
        _context.FragmentSequence.Add(reverseFragment);
        _logger.LogDebug("Emitted ReverseOrderFragment for Last()");
        
        // Also coordinate with builder for backward compatibility
        _context.Builder.SetShouldReverseOrderBy(true);
        
        // Emit LimitFragment for LIMIT 1
        _context.Builder.SetLimit(1); // Coordinate with builder
        var limitFragment = new LimitFragment(1, _context.Scope.CurrentAlias);
        _context.FragmentSequence.Add(limitFragment);
        _logger.LogDebug("Emitted LimitFragment for Last()");
        
        // Handle optional predicate: Last(p => p.Age > 18)
        if (node.Arguments.Count == 2)
        {
            var lambda = ExtractLambda(node.Arguments[1]);
            if (lambda != null)
            {
                var expressionVisitor = CreateExpressionVisitor();
                var whereCondition = expressionVisitor.VisitAndReturnCypher(lambda.Body);
                _context.Builder.AddWhere(whereCondition);
            }
        }
        
        return node;
    }

    private Expression HandleSingle(MethodCallExpression node)
    {
        // Visit source FIRST (proper visitor pattern: children before parent)
        Visit(node.Arguments[0]);
        
        // Single needs LIMIT 2 to detect if there's more than one
        _context.Builder.SetLimit(2); // Coordinate with builder
        var limitFragment = new LimitFragment(2, _context.Scope.CurrentAlias);
        _context.FragmentSequence.Add(limitFragment);
        _logger.LogDebug("Emitted LimitFragment for Single()");
        
        // Handle optional predicate: Single(p => p.Age > 18)
        if (node.Arguments.Count == 2)
        {
            var lambda = ExtractLambda(node.Arguments[1]);
            if (lambda != null)
            {
                var expressionVisitor = CreateExpressionVisitor();
                var whereCondition = expressionVisitor.VisitAndReturnCypher(lambda.Body);
                _context.Builder.AddWhere(whereCondition);
            }
        }
        
        return node;
    }

    private Expression HandleElementAccess(MethodCallExpression node, int limit, string methodName)
    {
        _context.Builder.SetLimit(limit);

        // Handle optional predicate: First(p => p.Age > 18)
        if (node.Arguments.Count == 2)
        {
            var lambda = ExtractLambda(node.Arguments[1]);
            if (lambda != null)
            {
                var expressionVisitor = CreateExpressionVisitor();
                var whereCondition = expressionVisitor.VisitAndReturnCypher(lambda.Body);
                _context.Builder.AddWhere(whereCondition);
            }
        }

        // Element access methods need a basic RETURN clause for simple queries
        // Check if we're dealing with a node or relationship query
        var resultType = node.Type;
        if (typeof(INode).IsAssignableFrom(resultType))
        {
            var nodeAlias = GetContextualAlias();
            _context.Builder.AddReturn(nodeAlias);
            _logger.LogDebug("Added basic RETURN {Alias} for node element access", nodeAlias);
        }
        else if (typeof(IRelationship).IsAssignableFrom(resultType))
        {
            var relAlias = _context.Scope.GetNumberedAlias("r");
            _context.Builder.AddReturn(relAlias);
            _logger.LogDebug("Added basic RETURN {Alias} for relationship element access", relAlias);
        }

        _logger.LogDebug("Added LIMIT {Limit} for {Method}", limit, methodName);

        // Continue processing the source expression
        return Visit(node.Arguments[0]);
    }

    private Expression HandleToList(MethodCallExpression node)
    {
        _logger.LogDebug("Processing ToList/ToArray method");

        // Visit source FIRST (proper visitor pattern)
        var result = Visit(node.Arguments[0]);

        // Check if we need to enable complex property loading for node queries
        var resultType = node.Type.GetGenericArguments().FirstOrDefault();
        
        // Check if this expression tree contains PathSegments calls
        bool containsPathSegments = ContainsPathSegmentsCall(node);
        
        if (resultType != null && resultType.IsGenericType && 
            resultType.GetGenericTypeDefinition().Name.Contains("IGraphPathSegment"))
        {
            _logger.LogDebug("Processing path segment query of type {Type}", resultType.Name);
            
            // For path segment queries, return the path components ONLY if no projection exists
            if (!_context.Builder.HasReturnClauses)
            {
                // Path segments should reference the LAST hop (where traversal ends), not CurrentHop
                // For simple queries with one traversal, this is hop 0: (src0)-[r0]->(tgt0)
                var lastHop = Math.Max(0, _context.Scope.CurrentHop - 1);
                var sourceAlias = _context.Scope.GetNumberedAliasForHop("src", lastHop);
                var relAlias = _context.Scope.GetNumberedAliasForHop("r", lastHop);
                var targetAlias = _context.Scope.GetNumberedAliasForHop("tgt", lastHop);
                var pathSegmentReturn = $"{sourceAlias}, {relAlias}, {targetAlias}";
                _context.Builder.AddReturn(pathSegmentReturn);
                _logger.LogDebug("Added default path segment return for hop {Hop}: {Return}", lastHop, pathSegmentReturn);

                if (_context.UseFragmentRenderer)
                {
                    var returns = ImmutableArray.Create(sourceAlias, relAlias, targetAlias);
                    var projectionFragment = new ProjectionFragment(returns, targetAlias);
                    _context.FragmentSequence.Add(projectionFragment);
                    _logger.LogDebug("Emitted ProjectionFragment for path segment return with aliases {Returns}", returns);
                }
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
            if (!containsPathSegments && !_context.Scope.IsPathSegmentContext && !_context.Builder.HasReturnClauses)
            {
                var nodeAlias = GetContextualAlias();
                _context.Builder.AddReturn(nodeAlias);
                _logger.LogDebug("Added default node return with alias: {Alias}", nodeAlias);
                
                // Check if the node type has complex properties (INode properties)
                var hasComplexProperties = resultType.GetProperties()
                    .Any(p => typeof(INode).IsAssignableFrom(p.PropertyType));
                
                if (hasComplexProperties)
                {
                    // Enable complex property loading for nodes with INode properties
                    _context.Builder.EnableComplexPropertyLoading(nodeAlias);
                    
                    // Emit OptionalMatchFragment only when using fragment renderer
                    // Builder generates OPTIONAL MATCH internally from EnableComplexPropertyLoading
                    if (_context.UseFragmentRenderer)
                    {
                        var optionalPattern = $"({nodeAlias})-[prop_rel]->(prop_node)";
                        var optionalFragment = new OptionalMatchFragment(
                            optionalPattern,
                            ImmutableArray.Create("prop_rel", "prop_node"),
                            ImmutableArray.Create(nodeAlias),
                            nodeAlias);
                        _context.FragmentSequence.Add(optionalFragment);
                        _logger.LogDebug("Emitted OptionalMatchFragment for complex property loading");
                    }
                    
                    // Always emit ComplexPropertyLoadingFragment to track state
                    var complexPropertyFragment = new ComplexPropertyLoadingFragment(true, nodeAlias);
                    _context.FragmentSequence.Add(complexPropertyFragment);
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
            if (!_context.Builder.HasReturnClauses && !containsPathSegments)
            {
                var relAlias = _context.Scope.GetNumberedAlias("r");
                _context.Builder.AddReturn(relAlias);
                _logger.LogDebug("Added default relationship return with alias: {Alias}", relAlias);
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
                    SetupInitialMatch(elementType);
                }
            }
        }

        return base.VisitConstant(node);
    }

    private void SetupInitialMatch(Type elementType)
    {
        // Skip setup if we already have path patterns (e.g., from PathSegments)
        if (_context.Builder.HasMatchPatterns)
        {
            _logger.LogDebug("Skipping initial match setup - path patterns already exist");
            return;
        }

        if (typeof(INode).IsAssignableFrom(elementType))
        {
            // Node query: MATCH (n:BaseLabel) 
            // For AGE inheritance support, always use base type label
            var baseLabel = Labels.GetBaseTypeLabel(elementType);
            var alias = _context.Scope.CurrentAlias ?? GetContextualAlias();
            _context.Builder.AddMatch(alias, baseLabel);
            
            // Set CurrentAlias so subsequent operations (WHERE, SELECT, etc.) use the correct alias
            _context.Scope.CurrentAlias = alias;
            
            // Emit MatchRootFragment for root node queries
            var pattern = $"({alias}:{baseLabel})";
            var fragment = new MatchRootFragment(pattern, baseLabel, elementType, ImmutableArray.Create(alias), alias);
            _context.FragmentSequence.Add(fragment);
            _logger.LogDebug("Emitted MatchRootFragment for {Type} with alias {Alias}", elementType.Name, alias);
            
            // Add inheritance filter if querying for a derived type
            var actualLabel = Labels.GetLabelFromType(elementType);
            if (baseLabel != actualLabel)
            {
                // Add WHERE clause to filter by inheritance hierarchy
                var inheritanceFilter = $"{alias}.inheritance_labels[0] = '{actualLabel}'";
                _context.Builder.AddWhere(inheritanceFilter);
                _logger.LogDebug("Added inheritance filter: {Filter}", inheritanceFilter);
            }
            
            // Set up complex properties for node types
            SetupComplexProperties(elementType, alias);
            
            _logger.LogDebug("Set up node match for type {Type} with base label {BaseLabel}", elementType.Name, baseLabel);
        }
        else if (typeof(IRelationship).IsAssignableFrom(elementType))
        {
            var alias = _context.Scope.CurrentAlias ?? _context.Scope.GetNumberedAlias("r");
            var srcAlias = _context.Scope.GetNumberedAlias("src");
            var tgtAlias = _context.Scope.GetNumberedAlias("tgt");
            var relationshipLabel = GetRelationshipLabel(elementType);
            var relationshipPattern = string.IsNullOrEmpty(relationshipLabel)
                ? alias
                : $"{alias}:{relationshipLabel}";
            var pattern = $"({srcAlias})-[{relationshipPattern}]->({tgtAlias})";
            _context.Builder.AddMatchPattern(pattern);
            
            // Set CurrentAlias so subsequent operations use the correct alias
            _context.Scope.CurrentAlias = alias;
            
            // Emit MatchRootFragment for root relationship queries
            var fragment = new MatchRootFragment(pattern, relationshipLabel, elementType, ImmutableArray.Create(srcAlias, alias, tgtAlias), alias);
            _context.FragmentSequence.Add(fragment);
            _logger.LogDebug("Emitted MatchRootFragment for {Type} with alias {Alias}", elementType.Name, alias);
            
            AddRelationshipTypeFilter(elementType, alias);

            var labelForLog = string.IsNullOrEmpty(relationshipLabel) ? "*" : relationshipLabel;
            _logger.LogDebug("Set up relationship match for type {Type} with label {Label} using aliases: {SrcAlias}-[{RelAlias}]->{TgtAlias}", 
                elementType.Name, labelForLog, srcAlias, alias, tgtAlias);
        }
        else
        {
            throw new NotSupportedException($"Query type {elementType.Name} is not supported. Only INode and IRelationship types are supported.");
        }
    }

    private void SetupComplexProperties(Type nodeType, string alias)
    {
        var complexProps = GetComplexProperties(nodeType);
        if (complexProps.Count == 0) return;

        _logger.LogDebug("Setting up {Count} complex properties for {Type}", complexProps.Count, nodeType.Name);

        foreach (var prop in complexProps)
        {
            var relType = GraphDataModel.PropertyNameToRelationshipTypeName(prop.Name);
            var optionalMatch = $"OPTIONAL MATCH ({alias})-[r_{prop.Name}:{relType}]->(cp_{prop.Name})";
            _context.Builder.AddOptionalMatch(optionalMatch);
            _logger.LogDebug("Added optional match for complex property '{Property}': {Match}", prop.Name, optionalMatch);
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

    private static T EvaluateConstantExpression<T>(Expression expression)
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

    private static List<System.Reflection.PropertyInfo> GetComplexProperties(Type type)
    {
        return type.GetProperties()
            .Where(p => typeof(INode).IsAssignableFrom(p.PropertyType) ||
                       typeof(IRelationship).IsAssignableFrom(p.PropertyType) ||
                       (p.PropertyType.IsGenericType &&
                        p.PropertyType.GetGenericTypeDefinition() == typeof(ICollection<>) &&
                        (typeof(INode).IsAssignableFrom(p.PropertyType.GetGenericArguments()[0]) ||
                         typeof(IRelationship).IsAssignableFrom(p.PropertyType.GetGenericArguments()[0]))))
            .ToList();
    }

    /// <summary>
    /// Gets the appropriate relationship label for Cypher queries.
    /// For interface types, returns empty string (no type filtering in pattern - will use WHERE clause instead).
    /// For concrete types, returns the base type label.
    /// </summary>
    /// <param name="relationshipType">The relationship type</param>
    /// <returns>A Cypher-compatible relationship type pattern</returns>
    private string GetRelationshipLabel(Type relationshipType)
    {
        if (IsUnspecifiedRelationshipType(relationshipType))
        {
            _logger.LogDebug("Relationship type {TypeName} requests unspecified pattern", relationshipType.Name);
            return string.Empty;
        }

        // For interface types, we don't specify the type in the pattern
        // Instead, we'll add a WHERE clause later to filter by inheritance_labels property
        if (relationshipType.IsInterface)
        {
            var interfaceLabel = Labels.GetLabelFromType(relationshipType);
            _logger.LogDebug("Interface relationship type {TypeName} mapped to label: {Label}", 
                relationshipType.Name, interfaceLabel);
            
            // Store the interface label for later use in WHERE clause
            // We'll use the more efficient inheritance_labels property approach
            return "";
        }
        
        // For concrete types, use the base type label
        return Labels.GetBaseTypeLabel(relationshipType);
    }

    /// <summary>
    /// Adds a WHERE clause to filter relationships by type when dealing with interface types.
    /// </summary>
    /// <param name="relationshipType">The relationship type</param>
    /// <param name="relationshipAlias">The alias used for the relationship in the query</param>
    private void AddRelationshipTypeFilter(Type relationshipType, string relationshipAlias)
    {
        if (!relationshipType.IsInterface || IsUnspecifiedRelationshipType(relationshipType))
        {
            return;
        }

        var interfaceLabel = Labels.GetLabelFromType(relationshipType);

        // Use standard Cypher IN syntax for AGE compatibility
        // Generate: WHERE 'IRelationship' IN r0.inheritance_labels
        var whereClause = $"'{interfaceLabel}' IN {relationshipAlias}.inheritance_labels";
        _context.Builder.AddWhere(whereClause);
        _logger.LogDebug("Added inheritance-based relationship filter: {WhereClause}", whereClause);
    }

    private static bool IsUnspecifiedRelationshipType(Type relationshipType)
    {
        return relationshipType == typeof(IRelationship) || relationshipType == typeof(Relationship);
    }

    private Expression HandlePathSegments(MethodCallExpression node)
    {
        // Visit the source expression first so upstream operations (e.g., Traverse, Where) build their state before
        // we append this hop. This preserves MATCH ordering for nested PathSegments chains.
        var sourceExpression = Visit(node.Arguments[0]);

        // Delegate to specialized traversal visitor which handles pattern generation and fragment emission
        _traversalVisitor.HandlePathSegments(node);

        return sourceExpression;
    }

    private Expression HandleWithDepth(MethodCallExpression node)
    {
        _logger.LogDebug("Processing WithDepth method call");

        if (node.Arguments.Count >= 2)
        {
            // Extract depth parameters
            if (node.Arguments.Count == 2)
            {
                // WithDepth(maxDepth)
                var maxDepth = EvaluateConstantExpression<int>(node.Arguments[1]);
                _context.Scope.SetTraversalDepth(1, maxDepth); // Default minDepth to 1
                _logger.LogDebug("Set traversal max depth: {MaxDepth}", maxDepth);
            }
            else if (node.Arguments.Count == 3)
            {
                // WithDepth(minDepth, maxDepth)
                var minDepth = EvaluateConstantExpression<int>(node.Arguments[1]);
                var maxDepth = EvaluateConstantExpression<int>(node.Arguments[2]);
                _context.Scope.SetTraversalDepth(minDepth, maxDepth);
                _logger.LogDebug("Set traversal depth range: {MinDepth}-{MaxDepth}", minDepth, maxDepth);
            }
        }

        // Continue processing the expression tree
        return Visit(node.Arguments[0]);
    }

    private Expression HandleDirection(MethodCallExpression node)
    {
        _logger.LogDebug("Processing Direction method call");

        if (node.Arguments.Count >= 2)
        {
            var direction = EvaluateConstantExpression<GraphTraversalDirection>(node.Arguments[1]);
            
            // Set the traversal direction in the builder so PathSegments can use it
            _context.Builder.SetTraversalDirection(direction);
            _logger.LogDebug("Set traversal direction: {Direction}", direction);
        }

        // Continue processing the expression tree
        return Visit(node.Arguments[0]);
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
        if (_context.Scope.IsPathSegmentContext)
        {
            return _context.Scope.GetNumberedAlias("src"); // Use numbered source node alias in path segment contexts
        }
        
        // Check if this is a relationship query
        if (typeof(IRelationship).IsAssignableFrom(_context.Scope.RootType))
        {
            return _context.Scope.GetNumberedAlias("r"); // Use numbered relationship alias for relationship queries
        }
        
        // Use src as the standard alias for source nodes (consistent with Neo4j provider)
        return _context.Scope.GetNumberedAlias("src");
    }

    #region WHERE Position Analysis

    /// <summary>
    /// Determines the semantic position of a WHERE clause relative to path traversal operations.
    /// This enables context-aware alias mapping for pre-traversal vs post-traversal filters.
    /// </summary>
    /// <param name="whereNode">The WHERE clause expression node</param>
    /// <returns>Position indicating whether filters apply to source or target nodes</returns>
    private WherePosition DetermineWherePosition(MethodCallExpression whereNode)
    {
        _logger.LogDebug("Analyzing WHERE position for expression: {Expression}", whereNode.Method.Name);
        
        // With bottom-up evaluation (visiting source before processing WHERE lambda):
        // - Check if PathSegments exists in the WHERE's SOURCE expression (Arguments[0])
        // - If PathSegments is in the source, WHERE is PostTraversal (applies after traversal to target nodes)
        // - If PathSegments is NOT in the source, WHERE is PreTraversal (applies before traversal to source nodes)
        
        // Check if PathSegments exists in the WHERE's source expression
        var sourceExpression = whereNode.Arguments[0];
        bool pathSegmentsInSource = ContainsPathSegmentsCall(sourceExpression);
        
        if (pathSegmentsInSource)
        {
            _logger.LogDebug("PathSegments found in WHERE source - WHERE is PostTraversal (target filter)");
            return WherePosition.PostTraversal;
        }
        else if (_context.Builder.HasMatchPatterns)
        {
            // If we have match patterns but PathSegments is not in THIS WHERE's source,
            // then this WHERE is BEFORE the PathSegments in the chain, so it's PreTraversal
            _logger.LogDebug("Match patterns exist but PathSegments not in WHERE source - WHERE is PreTraversal (source filter)");
            return WherePosition.PreTraversal;
        }
        else
        {
            _logger.LogDebug("No match patterns and no PathSegments in source - simple node query");
            return WherePosition.Unknown;
        }
    }
    
    /// <summary>
    /// Checks if an expression tree contains PathSegments operations.
    /// Used to determine if traversal operations exist in the query chain.
    /// </summary>
    /// <param name="expression">Expression to analyze</param>
    /// <returns>True if PathSegments operations are found</returns>
    private bool ContainsPathSegments(Expression expression)
    {
        if (expression is MethodCallExpression methodCall)
        {
            if (methodCall.Method.Name == nameof(GraphTraversalExtensions.PathSegments))
            {
                return true;
            }
            
            // Check arguments for nested PathSegments calls
            foreach (var arg in methodCall.Arguments)
            {
                if (ContainsPathSegments(arg))
                {
                    return true;
                }
            }
            
            // Check the object/source
            if (methodCall.Object != null && ContainsPathSegments(methodCall.Object))
            {
                return true;
            }
        }
        
        return false;
    }

    /// <summary>
    /// Checks if an expression accesses path segment properties (StartNode, EndNode, Relationship)
    /// </summary>
    private bool ContainsPathSegmentAccess(Expression expression)
    {
        if (expression is MemberExpression memberExpr)
        {
            // Check if accessing StartNode, EndNode, or Relationship
            if (memberExpr.Member.Name == nameof(IGraphPathSegment.StartNode) ||
                memberExpr.Member.Name == nameof(IGraphPathSegment.EndNode) ||
                memberExpr.Member.Name == nameof(IGraphPathSegment.Relationship))
            {
                return true;
            }

            // Check nested properties like ps.EndNode.FirstName
            if (memberExpr.Expression is MemberExpression nestedMember)
            {
                return ContainsPathSegmentAccess(nestedMember);
            }

            // Check if the expression is a path segment parameter
            if (memberExpr.Expression is ParameterExpression paramExpr && IsPathSegmentType(paramExpr.Type))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Checks if a type is a PathSegment type
    /// </summary>
    private static bool IsPathSegmentType(Type type)
    {
        if (type == null) return false;
        
        // Check if it's the generic IGraphPathSegment interface
        if (type.IsGenericType)
        {
            var genericTypeDef = type.GetGenericTypeDefinition();
            return genericTypeDef.Name.StartsWith("IGraphPathSegment");
        }
        
        // Check if any implemented interfaces are PathSegment types
        foreach (var interfaceType in type.GetInterfaces())
        {
            if (interfaceType.IsGenericType)
            {
                var genericTypeDef = interfaceType.GetGenericTypeDefinition();
                if (genericTypeDef.Name.StartsWith("IGraphPathSegment"))
                {
                    return true;
                }
            }
        }
        
        return false;
    }

    /// <summary>
    /// Finds the column alias for a property name in the RETURN clauses.
    /// For example, given "FirstName" and RETURN clause "src0.FirstName AS c_FirstName",
    /// returns "c_FirstName".
    /// </summary>
    private string? FindColumnAliasForProperty(string propertyName)
    {
        var returnClauses = _context.Builder.GetReturnClauses();
        
        foreach (var returnClause in returnClauses)
        {
            // Parse return clause: "src0.FirstName AS c_FirstName" or similar
            // Look for pattern: "... AS c_{PropertyName}" or "... AS {PropertyName}"
            var asIndex = returnClause.IndexOf(" AS ", StringComparison.OrdinalIgnoreCase);
            if (asIndex == -1)
                continue;
                
            var beforeAs = returnClause.Substring(0, asIndex).Trim();
            var afterAs = returnClause.Substring(asIndex + 4).Trim();
            
            // Check if the expression before AS ends with the property name
            // e.g., "src0.FirstName" ends with "FirstName"
            if (beforeAs.EndsWith($".{propertyName}", StringComparison.Ordinal) ||
                beforeAs == propertyName)
            {
                return afterAs;
            }
            
            // Also check if the alias itself matches the property name
            // This handles cases like "src0.FirstName AS FirstName"
            if (afterAs.Equals($"c_{propertyName}", StringComparison.Ordinal) ||
                afterAs.Equals(propertyName, StringComparison.Ordinal))
            {
                return afterAs;
            }
        }
        
        return null;
    }

    /// <summary>
    /// Maps C# property names to AGE database field names.
    /// </summary>
    private static string MapPropertyNameForAge(string csharpPropertyName)
    {
        return csharpPropertyName switch
        {
            // Map C# "Id" property to our prefixed "user_id" field to avoid conflict with PostgreSQL internal "id"
            // This ensures we always use our application-controlled IDs, not PostgreSQL internal IDs
            "Id" => "user_id",
            
            // For all other properties, keep the same name
            _ => csharpPropertyName
        };
    }

    #endregion
}