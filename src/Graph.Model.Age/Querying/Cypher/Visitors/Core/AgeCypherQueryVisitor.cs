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

using System.Linq.Expressions;
using Cvoya.Graph.Model;
using Cvoya.Graph.Model.Age.Querying.Cypher.Visitors;
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
        _logger.LogDebug("HandleWhere called with {ArgumentCount} arguments", node.Arguments.Count);
        if (node.Arguments.Count != 2)
            throw new ArgumentException("Where method must have exactly 2 arguments");

        // IMPORTANT: Process the source expression FIRST (bottom-up evaluation)
        // This ensures that PathSegments and other operations are processed before WHERE clauses are applied
        var source = Visit(node.Arguments[0]);

        var lambda = ExtractLambda(node.Arguments[1]);
        if (lambda == null)
            throw new ArgumentException("Where method requires a lambda expression");

        _logger.LogDebug("Processing WHERE clause - lambda body type: {LambdaBodyType}", lambda.Body.GetType().Name);

        // Debug logging for path segment parameter detection
        _logger.LogDebug("Lambda parameter count: {Count}", lambda.Parameters.Count);
        if (lambda.Parameters.Count >= 1)
        {
            _logger.LogDebug("Lambda parameter [0] type: {Type}", lambda.Parameters[0].Type);
            _logger.LogDebug("Lambda parameter [0] name: {Name}", lambda.Parameters[0].Name);
            _logger.LogDebug("Is IGraphPathSegment assignable from parameter type: {IsAssignable}", 
                typeof(IGraphPathSegment).IsAssignableFrom(lambda.Parameters[0].Type));
        }

        // Check if this is a path segment lambda (e.g., ps => ps.EndNode.Age > 35)
        AgeExpressionToCypherVisitor expressionVisitor;
        if (lambda.Parameters.Count == 1 && 
            typeof(IGraphPathSegment).IsAssignableFrom(lambda.Parameters[0].Type))
        {
            _logger.LogDebug("Detected path segment WHERE clause - using path segment context");
            
            // Find which hop this WHERE is associated with by checking the source expression
            // The WHERE is applied to the closest PathSegments call in the chain
            int targetHop = FindPathSegmentHopForWhere(node.Arguments[0]);
            _logger.LogDebug("WHERE clause targets hop {Hop}", targetHop);
            
            // For path segments, create a visitor that's aware of the path segment parameter
            expressionVisitor = CreatePathSegmentExpressionVisitor(lambda.Parameters[0], targetHop);
        }
        // Check if this is a relationship WHERE clause (check parameter type directly)
        else if (lambda.Parameters.Count == 1 && 
                 typeof(IRelationship).IsAssignableFrom(lambda.Parameters[0].Type))
        {
            var relAlias = _context.Scope.GetNumberedAlias("r");
            _logger.LogDebug("Detected relationship WHERE clause - parameter type: {Type}, using '{Alias}' alias", lambda.Parameters[0].Type.Name, relAlias);
            expressionVisitor = new AgeExpressionToCypherVisitor(_context.Builder, _logger, alias: relAlias);
        }
        // Check if this is a node WHERE clause in a path segment context
        else if (lambda.Parameters.Count == 1 && 
                 typeof(INode).IsAssignableFrom(lambda.Parameters[0].Type) &&
                 (ContainsPathSegmentsCall(node) || _context.Builder.HasMatchPatterns))
        {
            // Determine semantic position: is this WHERE before or after the traversal?
            var wherePosition = DetermineWherePosition(node);
            var parameterType = lambda.Parameters[0].Type;
            
            // Determine the alias based on WHERE position and current hop
            string targetAlias;
            int hopNumber = Math.Max(0, _context.Scope.CurrentHop - 1);
            
            if (wherePosition == WherePosition.PreTraversal)
            {
                // PreTraversal filters source nodes
                // For chained multi-hop patterns, use the OUTERMOST source (highest hop number)
                // This is because the chain is: Nodes<T> WHERE ... PathSegments (hop N) ... PathSegments (hop 0)
                // The WHERE at the beginning should filter the starting nodes (hop N's source)
                bool isChainedPattern = _context.Builder.HasMatchPatterns && _context.Scope.CurrentHop > 0;
                if (isChainedPattern)
                {
                    // Use the highest hop number (the outermost source in the chain)
                    hopNumber = _context.Scope.CurrentHop - 1;
                    _logger.LogDebug("PreTraversal WHERE in chained pattern: using outermost source at hop {HopNumber}", hopNumber);
                }
                targetAlias = _context.Scope.GetNumberedAliasForHop("src", hopNumber);
                _logger.LogDebug("PreTraversal WHERE: isChained={IsChained}, hopNumber={HopNumber}, alias={Alias}", 
                    isChainedPattern, hopNumber, targetAlias);
            }
            else
            {
                // PostTraversal filters target nodes
                // Use CurrentAlias which always points to the current query result
                // This handles both cases:
                // - After Traverse: CurrentAlias points to the result (e.g., src0)
                // - After PathSegments: CurrentAlias points to the target (e.g., tgt0)
                targetAlias = _context.Scope.CurrentAlias ?? _context.Scope.GetNumberedAliasForHop("tgt", hopNumber);
                _logger.LogDebug("PostTraversal WHERE: using CurrentAlias={Alias} (CurrentHop={CurrentHop})", 
                    targetAlias, _context.Scope.CurrentHop);
            }
            
            _logger.LogDebug("Detected node WHERE clause in path segment context - position: {Position}, paramType: {ParamType}, mapping to: {Alias}", 
                wherePosition, parameterType.Name, targetAlias);
            
            expressionVisitor = new AgeExpressionToCypherVisitor(_context.Builder, _logger, alias: targetAlias);
        }
        else
        {
            _logger.LogDebug("Using regular expression visitor (not a path segment WHERE clause)");
            // Use regular expression visitor for normal node queries
            expressionVisitor = CreateExpressionVisitor();
        }
        
        _logger.LogDebug("Created expression visitor, calling VisitAndReturnCypher...");
        var whereCondition = expressionVisitor.VisitAndReturnCypher(lambda.Body);
        _logger.LogDebug("Expression visitor returned: {WhereCondition}", whereCondition);
        _context.Builder.AddWhere(whereCondition);

        _logger.LogDebug("Added WHERE condition: {Condition}", whereCondition);

        // Return the source expression (already visited above)
        return source;
    }

    private Expression HandleSelect(MethodCallExpression node)
    {
        if (node.Arguments.Count != 2)
            throw new ArgumentException("Select method must have exactly 2 arguments");

        var lambda = ExtractLambda(node.Arguments[1]);
        if (lambda == null)
            throw new ArgumentException("Select method requires a lambda expression");

        _logger.LogDebug("Processing SELECT projection");

        // Check if the source is PathSegments - if so, handle this as a special case
        var sourceExpression = node.Arguments[0];
        
        // Special case: if the source is GroupBy, we need to visit it FIRST before processing the Select lambda
        // This ensures that the GROUP BY expression is set in the context before we try to use g.Key
        if (sourceExpression is MethodCallExpression groupByMethod && groupByMethod.Method.Name == "GroupBy")
        {
            _logger.LogDebug("Detected GroupBy + Select pattern - visiting GroupBy first");
            Visit(sourceExpression);
            
            // Now process the Select lambda with the GROUP BY context in place
            // The lambda will have access to the GroupByExpression via g.Key
            
            // Check if this is an anonymous type projection
            if (lambda.Body is NewExpression anonymousNewExpr)
            {
                HandleAnonymousProjection(anonymousNewExpr);
            }
            else
            {
                // Simple projection
                var expressionVisitor = CreateExpressionVisitor();
                var selectExpression = expressionVisitor.VisitAndReturnCypher(lambda.Body);
                _context.Builder.AddReturn(selectExpression);
                _logger.LogDebug("Added GROUP BY Select projection: {Expression}", selectExpression);
            }
            
            // Return without visiting the source again (we already did it above)
            return node;
        }
        
        // Check if the source is also a Select - if so, we need to clear its returns after visiting
        // because chained Selects should only keep the outermost projection
        bool isChainedSelect = sourceExpression is MethodCallExpression sourceMethod && 
                              sourceMethod.Method.Name == "Select";
        if (sourceExpression is MethodCallExpression pathSegmentsMethod && 
            pathSegmentsMethod.Method.Name == nameof(GraphTraversalExtensions.PathSegments))
        {
            _logger.LogDebug("Detected PathSegments + Select pattern - processing PathSegments first");
            
            // Remember the hop number BEFORE visiting PathSegments
            // PathSegments will create its hop at CurrentHop, then advance
            var pathSegmentHop = _context.Scope.CurrentHop;
            _logger.LogDebug("PathSegment will be created at hop {Hop}", pathSegmentHop);
            
            // First, visit the PathSegments to set up the context
            Visit(sourceExpression);
            
            // Now process the Select with the path segment context in place
            var body = lambda.Body;
            
            _logger.LogDebug("PathSegments + Select: body type = {BodyType}, IsPathSegmentContext = {IsPathSegmentContext}", 
                body.GetType().Name, _context.Scope.IsPathSegmentContext);
            
            if (body is MemberExpression memberExpr && memberExpr.Expression is ParameterExpression)
            {
                var propertyName = memberExpr.Member.Name;
                _logger.LogDebug("PathSegments + Select: processing property {PropertyName}", propertyName);
                
                // Handle path segment projections with context now properly set
                if (propertyName == "EndNode")
                {
                    // In chained multi-hop PathSegments context, skip individual EndNode projections
                    // The final complex SELECT will handle all projections  
                    var isInChainedContext = _context.Builder.HasMatchPatterns;
                    
                    _logger.LogDebug("DEBUG: EndNode projection - hasMatchPatterns={HasPatterns}, skipping={Skip}", 
                        isInChainedContext, isInChainedContext);
                    
                    if (!isInChainedContext)
                    {
                        var targetType = _context.Scope.TraversalInfo?.TargetNodeType;
                        string targetAlias;
                        
                        if (targetType != null)
                        {
                            var aliasFromType = _context.Scope.GetAliasForType(targetType);
                            _logger.LogDebug("DEBUG: GetAliasForType returned: {Alias} for type {Type}", aliasFromType, targetType.Name);
                            targetAlias = aliasFromType ?? "tgt";
                            if (aliasFromType == null)
                            {
                                _logger.LogDebug("DEBUG: GetAliasForType returned null, using fallback 'tgt'");
                                // In multi-hop context, use numbered alias instead of 'tgt'
                                if (_context.Scope.CurrentHop > 0)
                                {
                                    var hopNumber = _context.Scope.CurrentHop - 1;
                                    targetAlias = _context.Scope.GetNumberedAliasForHop("tgt", hopNumber);
                                    _logger.LogDebug("DEBUG: Replaced fallback with numbered alias: {Alias}", targetAlias);
                                }
                            }
                        }
                        else
                        {
                            // Always use numbered alias for path segments
                            _logger.LogDebug("DEBUG: CurrentHop={CurrentHop} when adding EndNode projection", _context.Scope.CurrentHop);
                            if (_context.Scope.CurrentHop > 0)
                            {
                                var hopNumber = _context.Scope.CurrentHop - 1;
                                targetAlias = _context.Scope.GetNumberedAliasForHop("tgt", hopNumber);
                                _logger.LogDebug("DEBUG: Using numbered alias tgt{HopNumber} = {Alias}", hopNumber, targetAlias);
                            }
                            else
                            {
                                // Even for hop 0, use numbered alias (tgt0)
                                targetAlias = _context.Scope.GetNumberedAliasForHop("tgt", 0);
                                _logger.LogDebug("DEBUG: CurrentHop is 0, using numbered alias tgt0 = {Alias}", targetAlias);
                            }
                        }
                        
                        _context.Builder.AddReturn(targetAlias);
                        _logger.LogDebug("Added path segment EndNode projection: {Alias}", targetAlias);
                        
                        // After projecting to EndNode, we exit PathSegment context and update current alias
                        // so subsequent operations (like chained Selects) use the correct alias
                        _context.Scope.IsPathSegmentContext = false;
                        _context.Scope.CurrentAlias = targetAlias;
                        _logger.LogDebug("Exited PathSegment context after EndNode projection, set CurrentAlias to {Alias}", targetAlias);
                    }
                    else
                    {
                        _logger.LogDebug("Skipped intermediate EndNode projection in chained PathSegments context");
                    }
                }
                else if (propertyName == "StartNode")
                {
                    string sourceAlias;
                    var sourceType = _context.Scope.TraversalInfo?.SourceNodeType;
                    if (sourceType != null)
                    {
                        sourceAlias = _context.Scope.GetAliasForType(sourceType) ?? "src";
                        _context.Builder.AddReturn(sourceAlias);
                        _logger.LogDebug("Added path segment StartNode projection: {Alias}", sourceAlias);
                    }
                    else
                    {
                        sourceAlias = _context.Scope.GetNumberedAlias("src");
                        _context.Builder.AddReturn(sourceAlias);
                        _logger.LogDebug("Added default path segment StartNode projection: {Alias}", sourceAlias);
                    }
                    
                    // After projecting to StartNode, we exit PathSegment context and update current alias
                    // so subsequent operations (like chained Selects) use the correct alias
                    _context.Scope.IsPathSegmentContext = false;
                    _context.Scope.CurrentAlias = sourceAlias;
                    _logger.LogDebug("Exited PathSegment context after StartNode projection, set CurrentAlias to {Alias}", sourceAlias);
                }
                else if (propertyName == "Relationship")
                {
                    var relationshipType = _context.Scope.TraversalInfo?.RelationshipType;
                    if (relationshipType != null)
                    {
                        // For multi-hop scenarios, use the numbered alias from the most recent hop
                        string relAlias;
                        if (_context.Scope.CurrentHop > 0)
                        {
                            // We're in a multi-hop context - use the numbered alias from the last completed hop
                            var lastHop = _context.Scope.CurrentHop - 1;
                            relAlias = _context.Scope.GetNumberedAliasForHop("r", lastHop);
                        }
                        else
                        {
                            // Fallback for non-multi-hop scenarios
                            relAlias = _context.Scope.GetAliasForType(relationshipType) ?? "r0";
                        }
                        
                        _context.Builder.AddReturn(relAlias);
                        _logger.LogDebug("Added path segment Relationship projection: {Alias}", relAlias);
                    }
                    else
                    {
                        // Default fallback - use r0 for multi-hop consistency
                        var defaultAlias = _context.Scope.CurrentHop > 0 ? 
                            _context.Scope.GetNumberedAliasForHop("r", 0) : "r0";
                        _context.Builder.AddReturn(defaultAlias);
                        _logger.LogDebug("Added default path segment Relationship projection: {Alias}", defaultAlias);
                    }
                }
                else
                {
                    // Regular property on path segment
                    var alias = _context.Scope.CurrentAlias ?? GetContextualAlias();
                    _context.Builder.AddReturn($"{alias}.{propertyName}");
                    _logger.LogDebug("Added path segment property projection: {Property}", propertyName);
                }
            }
            else
            {
                // Complex projection within path segments
                bool containsPathSegments = ContainsPathSegmentsCall(node);
                
                if (containsPathSegments || _context.Scope.IsPathSegmentContext)
                {
                    _logger.LogDebug("Complex SELECT in path segment context - using context-aware visitor");

                    // Use the hop number we saved before visiting PathSegments
                    // This is the hop that PathSegments created, which is what we want to project
                    var targetHop = pathSegmentHop;
                    
                    // For chained patterns, adjust aliases to match the chained structure
                    string srcAlias, tgtAlias, rAlias;
                    bool isChainedPattern = _context.Builder.HasMatchPatterns && targetHop > 0;
                    
                    if (isChainedPattern)
                    {
                        // In chained patterns:
                        // - PathSegment.StartNode maps to the previous hop's target (becomes this hop's source)
                        // - PathSegment.EndNode maps to this hop's target
                        srcAlias = _context.Scope.GetNumberedAliasForHop("tgt", targetHop - 1);  // Previous hop's target
                        tgtAlias = _context.Scope.GetNumberedAliasForHop("tgt", targetHop);      // This hop's target
                        rAlias = _context.Scope.GetNumberedAliasForHop("r", targetHop);         // This hop's relationship
                        _logger.LogDebug("Chained pattern projection: StartNode->{StartAlias}, EndNode->{EndAlias}, Relationship->{RelAlias}", 
                            srcAlias, tgtAlias, rAlias);
                    }
                    else
                    {
                        // Independent hop uses standard numbered aliases
                        srcAlias = _context.Scope.GetNumberedAliasForHop("src", targetHop);
                        tgtAlias = _context.Scope.GetNumberedAliasForHop("tgt", targetHop);
                        rAlias = _context.Scope.GetNumberedAliasForHop("r", targetHop);
                        _logger.LogDebug("Independent pattern projection: src={SrcAlias}, tgt={TgtAlias}, r={RelAlias}", 
                            srcAlias, tgtAlias, rAlias);
                    }

                    _logger.LogDebug($"DEBUG: CurrentHop={_context.Scope.CurrentHop}, using targetHop={targetHop}, aliases: src='{srcAlias}', tgt='{tgtAlias}', r='{rAlias}'");

                    // Create a dummy parameter for the path segment parameter (not used since we pass the aliases directly)
                    var dummyParam = Expression.Parameter(typeof(object), "ps");
                    
                    // Create a visitor that maps path segment properties to current hop aliases
                    var pathSegmentVisitor = new AgeExpressionToCypherVisitor(_context.Builder, _logger, "ps", dummyParam,
                        srcAlias, rAlias, tgtAlias);

                    _logger.LogDebug($"Created PathSegmentExpressionVisitor with aliases: src={srcAlias}, tgt={tgtAlias}, r={rAlias}");

                    var selectExpression = pathSegmentVisitor.VisitAndReturnCypher(body);
                    _context.Builder.AddReturn(selectExpression);
                    _logger.LogDebug("Added path segment expression projection: {Expression}", selectExpression);
                }
                else
                {
                    var expressionVisitor = CreateExpressionVisitor();
                    var selectExpression = expressionVisitor.VisitAndReturnCypher(body);
                    _context.Builder.AddReturn(selectExpression);
                    _logger.LogDebug("Added path segment expression projection: {Expression}", selectExpression);
                }
            }
            
            // For projections, disable complex property loading since we're not returning full entities
            _context.Builder.DisableComplexPropertyLoading();
            
            // Return the expression (source already processed)
            return node;
        }

        // Handle regular Select (non-PathSegments source)
        var regularBody = lambda.Body;

        if (regularBody is NewExpression newExpr)
        {
            // Anonymous type projection: Select(p => new { p.FirstName, p.LastName })
            HandleAnonymousProjection(newExpr);
        }
        else if (regularBody is MemberExpression memberExpr && memberExpr.Expression is ParameterExpression)
        {
            // Simple property or entity projection: Select(p => p.FirstName) or Select(ps => ps.Relationship)
            var propertyName = memberExpr.Member.Name;
            var parameterType = ((ParameterExpression)memberExpr.Expression).Type;
            
            // Check if this Select is projecting from PathSegment properties (StartNode, EndNode, Relationship)
            // We detect this by checking if the parameter type is a PathSegment type
            bool isProjectingFromPathSegment = propertyName == "StartNode" || propertyName == "EndNode" || propertyName == "Relationship";
            bool isParameterPathSegmentType = parameterType.IsGenericType && 
                                               parameterType.GetGenericTypeDefinition() == typeof(IGraphPathSegment<,,>);
            bool isPathSegmentContext = isProjectingFromPathSegment && isParameterPathSegmentType;
            
            _logger.LogDebug("Select processing property {PropertyName}: isProjectingFromPathSegment={IsProjectingFromPathSegment}, isParameterPathSegmentType={IsParameterPathSegmentType}, isPathSegmentContext={IsPathSegmentContext}",
                propertyName, isProjectingFromPathSegment, isParameterPathSegmentType, isPathSegmentContext);

            // Special handling for IRelationship projections: always return the full relationship entity
            if (typeof(IRelationship).IsAssignableFrom(parameterType) && propertyName == "Relationship")
            {
                // Use the correct alias for the relationship entity
                var relAlias = _context.Scope.GetAliasForType(parameterType) ?? GetContextualAlias();
                _context.Builder.AddReturn(relAlias);
                _logger.LogDebug("[PATCH] Added full relationship entity projection for IRelationship: {Alias}", relAlias);
            }
            else if (isPathSegmentContext && propertyName == "EndNode")
            {
                // In chained multi-hop PathSegments context, skip individual EndNode projections  
                // The final complex SELECT will handle all projections
                var isInChainedContext = _context.Builder.HasMatchPatterns;
                _logger.LogDebug("DEBUG: Location2 EndNode projection - hasMatchPatterns={HasPatterns}, skipping={Skip}", 
                    isInChainedContext, isInChainedContext);
                if (!isInChainedContext)
                {
                    var targetType = _context.Scope.TraversalInfo?.TargetNodeType;
                    string targetAlias;
                    if (targetType != null)
                    {
                        var aliasFromType = _context.Scope.GetAliasForType(targetType);
                        _logger.LogDebug("DEBUG: Location2 GetAliasForType returned: {Alias} for type {Type}", aliasFromType, targetType.Name);
                        targetAlias = aliasFromType ?? "tgt";
                        if (aliasFromType == null)
                        {
                            _logger.LogDebug("DEBUG: Location2 GetAliasForType returned null, using fallback 'tgt'");
                            if (_context.Scope.CurrentHop > 0)
                            {
                                var hopNumber = _context.Scope.CurrentHop - 1;
                                targetAlias = _context.Scope.GetNumberedAliasForHop("tgt", hopNumber);
                                _logger.LogDebug("DEBUG: Location2 Replaced fallback with numbered alias: {Alias}", targetAlias);
                            }
                        }
                    }
                    else
                    {
                        _logger.LogDebug("DEBUG: Location2 CurrentHop={CurrentHop} when adding EndNode projection", _context.Scope.CurrentHop);
                        if (_context.Scope.CurrentHop > 0)
                        {
                            var hopNumber = _context.Scope.CurrentHop - 1;
                            targetAlias = _context.Scope.GetNumberedAliasForHop("tgt", hopNumber);
                            _logger.LogDebug("DEBUG: Location2 Using numbered alias tgt{HopNumber} = {Alias}", hopNumber, targetAlias);
                        }
                        else
                        {
                            // Even for hop 0, use numbered alias
                            targetAlias = _context.Scope.GetNumberedAliasForHop("tgt", 0);
                            _logger.LogDebug("DEBUG: Location2 CurrentHop is 0, using numbered alias tgt0 = {Alias}", targetAlias);
                        }
                    }
                    _context.Builder.AddReturn(targetAlias);
                    _logger.LogDebug("Added path segment EndNode projection: {Alias}", targetAlias);
                    
                    // Update current alias for subsequent operations
                    _context.Scope.CurrentAlias = targetAlias;
                    _logger.LogDebug("Set CurrentAlias to {Alias} after EndNode projection", targetAlias);
                }
                else
                {
                    _logger.LogDebug("Skipped intermediate EndNode projection in chained PathSegments context");
                }
            }
            else if (isPathSegmentContext && propertyName == "StartNode")
            {
                _logger.LogDebug("General Select: Processing StartNode in PathSegment context");
                
                // In chained multi-hop PathSegments context, skip intermediate StartNode projections
                // The final complex SELECT will handle all projections
                var isInChainedContext = _context.Builder.HasMatchPatterns && _context.Scope.CurrentHop > 0;
                
                _logger.LogDebug("DEBUG: StartNode projection - hasMatchPatterns={HasPatterns}, currentHop={Hop}, skipping={Skip}", 
                    _context.Builder.HasMatchPatterns, _context.Scope.CurrentHop, isInChainedContext);
                
                if (!isInChainedContext)
                {
                    string sourceAlias;
                    var sourceType = _context.Scope.TraversalInfo?.SourceNodeType;
                    if (sourceType != null)
                    {
                        sourceAlias = _context.Scope.GetAliasForType(sourceType) ?? "src";
                        _context.Builder.AddReturn(sourceAlias);
                        _logger.LogDebug("Added path segment StartNode projection: {Alias}", sourceAlias);
                    }
                    else
                    {
                        sourceAlias = _context.Scope.GetNumberedAlias("src");
                        _context.Builder.AddReturn(sourceAlias);
                        _logger.LogDebug("Added default path segment StartNode projection: {Alias}", sourceAlias);
                    }
                    
                    // After projecting to StartNode, we exit PathSegment context and update current alias
                    _context.Scope.IsPathSegmentContext = false;
                    _context.Scope.CurrentAlias = sourceAlias;
                    _logger.LogDebug("Exited PathSegment context after StartNode projection (general path), set CurrentAlias to {Alias}", sourceAlias);
                }
                else
                {
                    _logger.LogDebug("Skipped intermediate StartNode projection in chained PathSegments context");
                    // Still exit path segment context and set current alias for the chain to continue
                    var sourceType = _context.Scope.TraversalInfo?.SourceNodeType;
                    var sourceAlias = sourceType != null ? (_context.Scope.GetAliasForType(sourceType) ?? "src") : _context.Scope.GetNumberedAlias("src");
                    _context.Scope.IsPathSegmentContext = false;
                    _context.Scope.CurrentAlias = sourceAlias;
                }
            }
            else if (isPathSegmentContext && propertyName == "Relationship")
            {
                // Use numbered alias for relationship in path segment queries
                var relAlias = _context.Scope.GetNumberedAlias("r");
                _context.Builder.AddReturn(relAlias);
                _logger.LogDebug("Added path segment Relationship projection: {Alias}", relAlias);
            }
            else if (!isPathSegmentContext)
            {
                _logger.LogDebug("General Select: Simple property projection for {PropertyName}, isPathSegmentContext={IsPathSegmentContext}", 
                    propertyName, isPathSegmentContext);
                
                // Check if the source expression is a Select that projects a PathSegment property
                // If so, we need to use the alias that Select will establish, not the current one
                string alias;
                if (sourceExpression is MethodCallExpression sourceSelect && 
                    sourceSelect.Method.Name == "Select" &&
                    sourceSelect.Arguments.Count == 2)
                {
                    var sourceLambda = ExtractLambda(sourceSelect.Arguments[1]);
                    if (sourceLambda?.Body is MemberExpression sourceMember && 
                        sourceMember.Expression is ParameterExpression sourceParam)
                    {
                        var sourcePropertyName = sourceMember.Member.Name;
                        var sourceParamType = sourceParam.Type;
                        bool isSourcePathSegmentProjection = 
                            (sourcePropertyName == "StartNode" || sourcePropertyName == "EndNode" || sourcePropertyName == "Relationship") &&
                            sourceParamType.IsGenericType && 
                            sourceParamType.GetGenericTypeDefinition() == typeof(IGraphPathSegment<,,>);
                        
                        if (isSourcePathSegmentProjection && sourcePropertyName == "StartNode")
                        {
                            // The source Select projects StartNode, so use src alias
                            var sourceType = _context.Scope.TraversalInfo?.SourceNodeType;
                            alias = sourceType != null ? 
                                (_context.Scope.GetAliasForType(sourceType) ?? _context.Scope.GetNumberedAlias("src")) :
                                _context.Scope.GetNumberedAlias("src");
                            _logger.LogDebug("Chained Select after StartNode: using source alias {Alias}", alias);
                        }
                        else if (isSourcePathSegmentProjection && sourcePropertyName == "EndNode")
                        {
                            // The source Select projects EndNode, so use tgt alias
                            var targetType = _context.Scope.TraversalInfo?.TargetNodeType;
                            alias = targetType != null ?
                                (_context.Scope.GetAliasForType(targetType) ?? _context.Scope.GetNumberedAlias("tgt")) :
                                _context.Scope.GetNumberedAlias("tgt");
                            _logger.LogDebug("Chained Select after EndNode: using target alias {Alias}", alias);
                        }
                        else if (isSourcePathSegmentProjection && sourcePropertyName == "Relationship")
                        {
                            // The source Select projects Relationship, so use r alias
                            var relationshipType = _context.Scope.TraversalInfo?.RelationshipType;
                            alias = relationshipType != null ?
                                (_context.Scope.GetAliasForType(relationshipType) ?? _context.Scope.GetNumberedAlias("r")) :
                                _context.Scope.GetNumberedAlias("r");
                            _logger.LogDebug("Chained Select after Relationship: using relationship alias {Alias}", alias);
                        }
                        else
                        {
                            // Not a PathSegment projection, use current/contextual alias
                            var contextualAlias = GetContextualAlias();
                            var currentAlias = _context.Scope.CurrentAlias;
                            alias = currentAlias ?? contextualAlias;
                            _logger.LogDebug("General Select: Using alias={Alias} (current={CurrentAlias}, contextual={ContextualAlias})", 
                                alias, currentAlias, contextualAlias);
                        }
                    }
                    else
                    {
                        // Source Select is not a simple property projection
                        var contextualAlias = GetContextualAlias();
                        var currentAlias = _context.Scope.CurrentAlias;
                        alias = currentAlias ?? contextualAlias;
                        _logger.LogDebug("General Select: Using alias={Alias} (current={CurrentAlias}, contextual={ContextualAlias})", 
                            alias, currentAlias, contextualAlias);
                    }
                }
                else
                {
                    // Source is not a Select, use current/contextual alias
                    var contextualAlias = GetContextualAlias();
                    var currentAlias = _context.Scope.CurrentAlias;
                    alias = currentAlias ?? contextualAlias;
                    _logger.LogDebug("General Select: Using alias={Alias} (current={CurrentAlias}, contextual={ContextualAlias})", 
                        alias, currentAlias, contextualAlias);
                }
                
                // Map property name (e.g., Id -> user_id) to match AGE storage
                var mappedPropertyName = MapPropertyNameForAge(propertyName);
                var projectionExpression = $"{alias}.{mappedPropertyName}";
                _context.Builder.AddReturn(projectionExpression);
                _context.Scope.LastProjectedExpression = projectionExpression; // Track for OrderBy(x => x) cases
                _logger.LogDebug("Added simple property projection: {Alias}.{Property} (mapped to {MappedProperty})", alias, propertyName, mappedPropertyName);
            }
            else
            {
                _logger.LogDebug("Skipping property projection in path segment context: {Property}, isPathSegmentContext={IsPathSegmentContext}", 
                    propertyName, isPathSegmentContext);
            }
        }
        else
        {
            // Complex expression projection: Select(p => p.Age * 2)
            bool containsPathSegments = ContainsPathSegmentsCall(node);
            bool isPathSegmentContext = _context.Scope.IsPathSegmentContext || containsPathSegments;
            
            if (!isPathSegmentContext)
            {
                // Only add complex projections if not in path segment context
                var expressionVisitor = CreateExpressionVisitor();
                var selectExpression = expressionVisitor.VisitAndReturnCypher(regularBody);
                _context.Builder.AddReturn(selectExpression);
                _logger.LogDebug("Added expression projection: {Expression}", selectExpression);
            }
            else
            {
                _logger.LogDebug("Skipping complex expression projection in path segment context");
            }
        }

        // For projections, disable complex property loading since we're not returning full entities
        _context.Builder.DisableComplexPropertyLoading();

        // If this is a chained Select, save our return clause before visiting the source
        string? ourReturn = null;
        if (isChainedSelect && _context.Builder.HasReturnClauses)
        {
            ourReturn = _context.Builder.GetLastReturnClause();
            _logger.LogDebug("Chained Select: saved our return '{Return}' before visiting source", ourReturn);
        }

        // Continue processing the source expression
        var result = Visit(node.Arguments[0]);
        
        // After visiting the source, if it was a chained Select and we had saved a return,
        // clear all returns and restore only our return (the outer projection)
        if (isChainedSelect && ourReturn != null && _context.Builder.HasReturnClauses)
        {
            _context.Builder.ClearReturn();
            _context.Builder.AddReturn(ourReturn);
            _logger.LogDebug("Chained Select: cleared inner Select returns and restored our return '{Return}'", ourReturn);
        }
        
        return result;
    }

    private Expression HandleGroupBy(MethodCallExpression node)
    {
        _logger.LogDebug("Processing GroupBy");
        
        if (node.Arguments.Count != 2)
            throw new ArgumentException("GroupBy method must have exactly 2 arguments");

        var lambda = ExtractLambda(node.Arguments[1]);
        if (lambda == null)
            throw new ArgumentException("GroupBy method requires a lambda expression");

        // Get the current alias for the parameter
        var currentAlias = _context.Scope.CurrentAlias ?? GetContextualAlias();
        _logger.LogDebug("GroupBy: Using alias '{Alias}' for parameter '{Parameter}'", currentAlias, lambda.Parameters[0].Name);
        
        // Register the parameter type with the current alias
        if (lambda.Parameters.Count > 0)
        {
            var parameterType = lambda.Parameters[0].Type;
            _context.Scope.RegisterTypeAlias(parameterType, currentAlias);
            _logger.LogDebug("GroupBy: Registered type {Type} with alias '{Alias}'", parameterType.Name, currentAlias);
        }
        
        // Extract the grouping key expression (e.g., p => p.FirstName)
        var expressionVisitor = CreateExpressionVisitor();
        var groupByExpression = expressionVisitor.VisitAndReturnCypher(lambda.Body);
        
        // Store the GROUP BY expression in the scope for later use in Select
        _context.Scope.SetGroupByExpression(groupByExpression);
        _logger.LogDebug("Set GROUP BY expression: {Expression}", groupByExpression);

        // Continue processing the source expression
        return Visit(node.Arguments[0]);
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
            }
            else if (parameterIndex == 1)
            {
                // Returning the inner parameter (node) - use target node alias
                var targetAlias = _context.Scope.GetNumberedAlias("tgt");
                _context.Builder.AddReturn(targetAlias);
                _logger.LogDebug("JOIN: Returning inner parameter (target node): {Alias}", targetAlias);
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
        Console.WriteLine($"DEBUG: HandleAnonymousProjection called with {newExpr.Arguments.Count} arguments");
        
        var projections = new List<string>();
        var alias = _context.Scope.CurrentAlias ?? GetContextualAlias();

        for (int i = 0; i < newExpr.Arguments.Count; i++)
        {
            var arg = newExpr.Arguments[i];
            var member = newExpr.Members?[i];
            var projectionAlias = member?.Name ?? $"field{i}";

            // Use safe alias to avoid reserved word conflicts in AGE
            var safeAlias = $"c_{projectionAlias}";

            Console.WriteLine($"DEBUG: HandleAnonymousProjection - processing argument {i}: {arg}, Type: {arg.Type}");
            
            // Special handling for PathSegment parameter projections
            if (arg is ParameterExpression paramExpr && IsPathSegmentType(paramExpr.Type))
            {
                Console.WriteLine($"DEBUG: Detected PathSegment parameter projection: {paramExpr.Name}");
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
        if (node.Arguments.Count != 2)
            throw new ArgumentException("OrderBy method must have exactly 2 arguments");

        // Extract lambda from quote expression if necessary
        var lambdaArg = node.Arguments[1];
        var lambda = ExtractLambda(lambdaArg);
        
        if (lambda == null)
            throw new ArgumentException("OrderBy method requires a lambda expression");

        _logger.LogDebug("Processing ORDER BY clause, descending: {Descending}", descending);

        // Check if we have aggregation in RETURN clause - ORDER BY is not allowed with aggregation without GROUP BY
        if (_context.Builder.HasAggregationInReturn)
        {
            _logger.LogDebug("Skipping ORDER BY because aggregation functions are present in RETURN clause");
            // Continue processing the source expression without adding ORDER BY
            return Visit(node.Arguments[0]);
        }

        // Check if we need PathSegments detection for ORDER BY
        bool containsPathSegments = ContainsPathSegmentsCall(node);
        
        // Special case: if the OrderBy lambda is just a parameter (e.g., content => content),
        // it means we're ordering by a projected value. Use the tracked projection expression.
        string orderExpression;
        if (lambda.Body is ParameterExpression)
        {
            // First, try to use the tracked last projected expression (most reliable)
            if (_context.Scope.LastProjectedExpression != null)
            {
                orderExpression = _context.Scope.LastProjectedExpression;
                _logger.LogDebug("OrderBy on parameter resolved to tracked projection: {Expression}", orderExpression);
            }
            // Fallback: search through the source chain to find a Select operation
            else
            {
                MethodCallExpression? selectMethod = null;
                var currentExpr = node.Arguments[0];
                
                // Walk up the chain to find the Select
                while (currentExpr is MethodCallExpression methodCall)
                {
                    if (methodCall.Method.Name == "Select")
                    {
                        selectMethod = methodCall;
                        break;
                    }
                    // Continue up the chain
                    if (methodCall.Arguments.Count > 0)
                    {
                        currentExpr = methodCall.Arguments[0];
                    }
                    else
                    {
                        break;
                    }
                }
                
                if (selectMethod != null)
                {
                    // Extract the Select's projection lambda
                    var selectLambda = ExtractLambda(selectMethod.Arguments[1]);
                    if (selectLambda != null)
                    {
                        _logger.LogDebug("OrderBy on projection parameter - extracting order expression from Select projection");
                        
                        // Visit the Select's projection body to get the Cypher expression
                        AgeExpressionToCypherVisitor expressionVisitor;
                        if (containsPathSegments || _context.Scope.IsPathSegmentContext)
                        {
                            _logger.LogDebug("ORDER BY in path segment context - using context-aware visitor for projection");
                            bool isChainedPattern = _context.Builder.HasMatchPatterns && _context.Scope.CurrentHop > 0;
                            int hopNumber = isChainedPattern ? 0 : Math.Max(0, _context.Scope.CurrentHop - 1);
                            var sourceAlias = _context.Scope.GetNumberedAliasForHop("src", hopNumber);
                            expressionVisitor = new AgeExpressionToCypherVisitor(_context.Builder, _logger, sourceAlias);
                        }
                        else
                        {
                            expressionVisitor = CreateExpressionVisitor();
                        }
                        
                        orderExpression = expressionVisitor.VisitAndReturnCypher(selectLambda.Body);
                        _logger.LogDebug("OrderBy using projection expression: {Expression}", orderExpression);
                    }
                    else
                    {
                        throw new InvalidOperationException($"Cannot order by parameter without a valid Select projection in the chain");
                    }
                }
                else
                {
                    throw new InvalidOperationException($"Cannot order by parameter - no Select projection found in the chain");
                }
            }
        }
        else
        {
            // Normal expression (not just a parameter), visit it
            // Special handling: if the lambda body is a property access on the parameter (e.g., x => x.FirstName)
            // and we have RETURN clauses with column aliases, map to the alias instead
            if (lambda.Body is MemberExpression memberExpr && 
                memberExpr.Expression is ParameterExpression paramExpr &&
                paramExpr == lambda.Parameters[0] &&
                _context.Builder.HasReturnClauses)
            {
                // Try to find the column alias for this property in the RETURN clauses
                var propertyName = memberExpr.Member.Name;
                var columnAlias = FindColumnAliasForProperty(propertyName);
                
                if (columnAlias != null)
                {
                    orderExpression = columnAlias;
                    _logger.LogDebug("OrderBy property access '{Property}' mapped to column alias: {Alias}", 
                        propertyName, columnAlias);
                }
                else
                {
                    // Fallback: visit normally
                    AgeExpressionToCypherVisitor expressionVisitor;
                    if (containsPathSegments || _context.Scope.IsPathSegmentContext)
                    {
                        _logger.LogDebug("ORDER BY in path segment context - using context-aware visitor");
                        bool isChainedPattern = _context.Builder.HasMatchPatterns && _context.Scope.CurrentHop > 0;
                        int hopNumber = isChainedPattern ? 0 : Math.Max(0, _context.Scope.CurrentHop - 1);
                        var sourceAlias = _context.Scope.GetNumberedAliasForHop("src", hopNumber);
                        expressionVisitor = new AgeExpressionToCypherVisitor(_context.Builder, _logger, sourceAlias);
                    }
                    else
                    {
                        expressionVisitor = CreateExpressionVisitor();
                    }
                    
                    orderExpression = expressionVisitor.VisitAndReturnCypher(lambda.Body);
                    _logger.LogDebug("OrderBy property access '{Property}' not found in RETURN aliases, using expression: {Expression}", 
                        propertyName, orderExpression);
                }
            }
            else
            {
                // Not a simple property access, visit normally
                AgeExpressionToCypherVisitor expressionVisitor;
                if (containsPathSegments || _context.Scope.IsPathSegmentContext)
                {
                    _logger.LogDebug("ORDER BY in path segment context - using context-aware visitor");
                    bool isChainedPattern = _context.Builder.HasMatchPatterns && _context.Scope.CurrentHop > 0;
                    int hopNumber = isChainedPattern ? 0 : Math.Max(0, _context.Scope.CurrentHop - 1);
                    var sourceAlias = _context.Scope.GetNumberedAliasForHop("src", hopNumber);
                    expressionVisitor = new AgeExpressionToCypherVisitor(_context.Builder, _logger, sourceAlias);
                }
                else
                {
                    expressionVisitor = CreateExpressionVisitor();
                }
                
                orderExpression = expressionVisitor.VisitAndReturnCypher(lambda.Body);
            }
        }
        
        _context.Builder.AddOrderBy(orderExpression, descending);

        _logger.LogDebug("Added ORDER BY: {Expression} {Direction}", orderExpression, descending ? "DESC" : "ASC");

        // Continue processing the source expression
        return Visit(node.Arguments[0]);
    }

    private Expression HandleThenBy(MethodCallExpression node, bool descending)
    {
        // ThenBy works the same as OrderBy for Cypher - just adds another sorting criterion
        return HandleOrderBy(node, descending);
    }

    private Expression HandleTake(MethodCallExpression node)
    {
        if (node.Arguments.Count != 2)
            throw new ArgumentException("Take method must have exactly 2 arguments");

        var limitValue = EvaluateConstantExpression<int>(node.Arguments[1]);
        _context.Builder.SetLimit(limitValue);
        _logger.LogDebug("Set LIMIT: {Limit}", limitValue);

        // Continue processing the source expression
        return Visit(node.Arguments[0]);
    }

    private Expression HandleSkip(MethodCallExpression node)
    {
        if (node.Arguments.Count != 2)
            throw new ArgumentException("Skip method must have exactly 2 arguments");

        var skipValue = EvaluateConstantExpression<int>(node.Arguments[1]);
        _context.Builder.SetSkip(skipValue);
        _logger.LogDebug("Set SKIP: {Skip}", skipValue);

        // Continue processing the source expression
        return Visit(node.Arguments[0]);
    }

    private Expression HandleDistinct(MethodCallExpression node)
    {
        _context.Builder.SetDistinct(true);
        _logger.LogDebug("Set DISTINCT flag");

        // Continue processing the source expression
        return Visit(node.Arguments[0]);
    }

    private Expression HandleCount(MethodCallExpression node)
    {
        // Handle optional predicate: Count(p => p.Age > 18)
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

        // Use context-aware alias for count aggregation
        var countAlias = GetContextualAlias();
        
        // Check if we're in a path segment context
        if (ContainsPathSegmentsCall(node))
        {
            // Path segments count the target nodes - use numbered alias
            countAlias = _context.Scope.GetNumberedAlias("tgt");
        }
        
        _context.Builder.AddReturn($"count({countAlias})");
        _logger.LogDebug("Added COUNT aggregation with alias: {Alias}", countAlias);

        // Continue processing the source expression
        var result = Visit(node.Arguments[0]);
        
        // Clear ORDER BY clauses after processing source
        // Aggregations return a single value, so ordering doesn't make sense
        _context.Builder.ClearOrderBy();
        _logger.LogDebug("Cleared ORDER BY clauses for COUNT aggregation");
        
        return result;
    }

    private Expression HandleAny(MethodCallExpression node)
    {
        // Handle optional predicate: Any(p => p.Age > 18)
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

        // Use context-aware alias for any aggregation
        var countAlias = GetContextualAlias();
        
        // Check if we're in a path segment context
        if (ContainsPathSegmentsCall(node))
        {
            countAlias = "tgt"; // Path segments count the target nodes
        }
        
        _context.Builder.AddReturn($"count({countAlias}) > 0");
        _logger.LogDebug("Added ANY aggregation with alias: {Alias}", countAlias);

        // Continue processing the source expression
        return Visit(node.Arguments[0]);
    }

    private Expression HandleAll(MethodCallExpression node)
    {
        if (node.Arguments.Count != 2)
            throw new ArgumentException("All method must have exactly 2 arguments");

        var lambda = ExtractLambda(node.Arguments[1]);
        if (lambda == null)
            throw new ArgumentException("All method requires a lambda expression");

        // All(predicate) means: there are NO elements that DON'T match the predicate
        // This is achieved by negating the condition and checking count = 0
        var expressionVisitor = CreateExpressionVisitor();
        var condition = expressionVisitor.VisitAndReturnCypher(lambda.Body);
        _context.Builder.AddWhere($"NOT ({condition})");
        
        // Use context-aware alias for all aggregation 
        var countAlias = GetContextualAlias();
        
        // Check if we're in a path segment context
        if (ContainsPathSegmentsCall(node))
        {
            countAlias = "tgt"; // Path segments count the target nodes
        }
        
        _context.Builder.AddReturn($"count({countAlias}) = 0");
        _logger.LogDebug("Added ALL aggregation with negated condition using alias: {Alias}", countAlias);

        // Continue processing the source expression
        return Visit(node.Arguments[0]);
    }

    private Expression HandleSum(MethodCallExpression node)
    {
        return HandleAggregationWithSelector(node, "sum", "SUM");
    }

    private Expression HandleAverage(MethodCallExpression node)
    {
        return HandleAggregationWithSelector(node, "avg", "AVERAGE");
    }

    private Expression HandleMin(MethodCallExpression node)
    {
        return HandleAggregationWithSelector(node, "min", "MIN");
    }

    private Expression HandleMax(MethodCallExpression node)
    {
        return HandleAggregationWithSelector(node, "max", "MAX");
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
        return HandleElementAccess(node, 1, "First");
    }

    private Expression HandleLast(MethodCallExpression node)
    {
        // For Last, set a flag to reverse order by clauses when building the query
        _context.Builder.SetShouldReverseOrderBy(true);
        return HandleElementAccess(node, 1, "Last");
    }

    private Expression HandleSingle(MethodCallExpression node)
    {
        // Single needs LIMIT 2 to detect if there's more than one
        return HandleElementAccess(node, 2, "Single");
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
                // Use numbered aliases from the current hop context
                var sourceAlias = _context.Scope.GetNumberedAlias("src");
                var relAlias = _context.Scope.GetNumberedAlias("r");
                var targetAlias = _context.Scope.GetNumberedAlias("tgt");
                var pathSegmentReturn = $"{sourceAlias}, {relAlias}, {targetAlias}";
                _context.Builder.AddReturn(pathSegmentReturn);
                _logger.LogDebug("Added default path segment return: {Return}", pathSegmentReturn);
            }
            else
            {
                _logger.LogDebug("Skipping default path segment return - projection already exists");
            }
        }
        else if (resultType != null && typeof(INode).IsAssignableFrom(resultType))
        {
            _logger.LogDebug("Processing simple node query of type {Type} (complex properties disabled for testing)", resultType.Name);
            
            // For node queries, add context-aware return clause ONLY if no projection exists and not in path context
            if (!containsPathSegments && !_context.Scope.IsPathSegmentContext && !_context.Builder.HasReturnClauses)
            {
                var nodeAlias = GetContextualAlias();
                _context.Builder.AddReturn(nodeAlias);
                _logger.LogDebug("Added default node return with alias: {Alias}", nodeAlias);
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

        // Continue processing the source expression
        return Visit(node.Arguments[0]);
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
            // Relationship query: MATCH (src)-[r:Label]->(tgt)
            // Use numbered variables for source and target nodes to support StartNodeId/EndNodeId queries
            var label = Labels.GetLabelFromType(elementType);
            var alias = _context.Scope.CurrentAlias ?? _context.Scope.GetNumberedAlias("r");
            var srcAlias = _context.Scope.GetNumberedAlias("src");
            var tgtAlias = _context.Scope.GetNumberedAlias("tgt");
            _context.Builder.AddMatchPattern($"({srcAlias})-[{alias}:{label}]->({tgtAlias})");
            
            _logger.LogDebug("Set up relationship match for type {Type} with label {Label} using aliases: {SrcAlias}-[{RelAlias}]->{TgtAlias}", 
                elementType.Name, label, srcAlias, alias, tgtAlias);
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
        if (relationshipType.IsInterface)
        {
            var interfaceLabel = Labels.GetLabelFromType(relationshipType);
            
            // Use standard Cypher IN syntax for AGE compatibility
            // Generate: WHERE 'IRelationship' IN r0.inheritance_labels
            var whereClause = $"'{interfaceLabel}' IN {relationshipAlias}.inheritance_labels";
            _context.Builder.AddWhere(whereClause);
            _logger.LogDebug("Added inheritance-based relationship filter: {WhereClause}", whereClause);
        }
    }

    private Expression HandlePathSegments(MethodCallExpression node)
    {
        _logger.LogDebug("Processing PathSegments method call");

        // Check if this PathSegments is chained from another PathSegments
        // We need to traverse through intermediate Select calls to find the original PathSegments
        bool isChainedPathSegments = ContainsPathSegmentsInChain(node.Arguments[0]);
        
        // ADDITIONAL: If we already have patterns in the builder and this is not the first hop,
        // it's very likely this is a chained call even if expression tree analysis fails
        bool hasExistingPatterns = _context.Builder.HasMatchPatterns && _context.Scope.CurrentHop > 0;
        
        // For chaining, either the expression analysis finds PathSegments OR we have existing patterns
        isChainedPathSegments = isChainedPathSegments || hasExistingPatterns;
        
        _logger.LogDebug("Chain detection for hop {Hop}: checking argument type {ArgType}, expressionChain={ExprChain}, hasPatterns={HasPatterns}, isChained={IsChained}", 
            _context.Scope.CurrentHop, node.Arguments[0].GetType().Name, ContainsPathSegmentsInChain(node.Arguments[0]), hasExistingPatterns, isChainedPathSegments);
            
        if (isChainedPathSegments)
        {
            _logger.LogDebug("Detected chained PathSegments - this is hop {Hop}", _context.Scope.CurrentHop);
        }
        else
        {
            _logger.LogDebug("No chaining detected for hop {Hop} - will create independent pattern", _context.Scope.CurrentHop);
        }

        // PathSegments method typically has generic arguments that specify the path structure
        var method = node.Method;
        if (method.IsGenericMethod)
        {
            var genericArgs = method.GetGenericArguments();
            if (genericArgs.Length == 3)
            {
                var sourceType = genericArgs[0];
                var relationshipType = genericArgs[1];
                var targetType = genericArgs[2];

                _logger.LogDebug("PathSegments: Source={Source}, Relationship={Relationship}, Target={Target}",
                    sourceType.Name, relationshipType.Name, targetType.Name);

                // Set up path segment context in the scope
                _context.Scope.SetTraversalInfo(sourceType, relationshipType, targetType);
                _context.Scope.IsPathSegmentContext = true;

                // Generate numbered aliases for multi-hop support
                var sourceAlias = _context.Scope.GetNumberedAlias("src");
                var relAlias = _context.Scope.GetNumberedAlias("r");
                string targetAlias = _context.Scope.GetNumberedAlias("tgt"); // Default value
                
                // For chained hops > 0, check if the target connects to a previous hop's source
                // For multi-hop chains, connect to the PREVIOUS hop (CurrentHop - 1), not hop 0
                // Example: Hop 0: Charlie->David, Hop 1: Bob->Charlie (connects to hop 0), Hop 2: Alice->Bob (connects to hop 1)
                if (_context.Scope.CurrentHop > 0 && isChainedPathSegments)
                {
                    // Check previous hops starting from the most recent (CurrentHop - 1) and working backwards
                    for (int prevHop = _context.Scope.CurrentHop - 1; prevHop >= 0; prevHop--)
                    {
                        var prevHopTypes = _context.Scope.GetHopTypes(prevHop);
                        if (prevHopTypes.HasValue && targetType == prevHopTypes.Value.src)
                        {
                            // Target of this hop connects to source of previous hop
                            targetAlias = _context.Scope.GetNumberedAliasForHop("src", prevHop);
                            _logger.LogDebug("Chained hop {Hop}: target type {TargetType} matches hop {PrevHop} source, using alias {Alias}", 
                                _context.Scope.CurrentHop, targetType.Name, prevHop, targetAlias);
                            break;
                        }
                    }
                }

                // Register type-to-alias mappings so WHERE clauses can find the correct alias for each type
                _context.Scope.RegisterTypeAlias(sourceType, sourceAlias);
                _context.Scope.RegisterTypeAlias(relationshipType, relAlias);
                // Only register targetType if it's not the same as an already registered type (chained case)
                if (!isChainedPathSegments || _context.Scope.CurrentHop == 0)
                {
                    _context.Scope.RegisterTypeAlias(targetType, targetAlias);
                }

                _logger.LogDebug("Generated hop {Hop} aliases: src={SrcAlias}, r={RelAlias}, tgt={TgtAlias}", 
                    _context.Scope.CurrentHop, sourceAlias, relAlias, targetAlias);
                _logger.LogDebug("Registered type aliases: {SourceType}={SourceAlias}, {RelType}={RelAlias}, {TargetType}={TargetAlias}",
                    sourceType.Name, sourceAlias, relationshipType.Name, relAlias, targetType.Name, targetAlias);

                // Store hop aliases and types for later use in WHERE clauses and chaining logic
                _context.Scope.StoreHopAliases(_context.Scope.CurrentHop, sourceAlias, relAlias, targetAlias);
                _context.Scope.StoreHopTypes(_context.Scope.CurrentHop, sourceType, relationshipType, targetType);

                // Build the path pattern for AGE
                var sourceLabel = Labels.GetBaseTypeLabel(sourceType);
                var relationshipLabel = GetRelationshipLabel(relationshipType);
                var targetLabel = Labels.GetBaseTypeLabel(targetType);

                // Determine relationship direction arrows based on traversal direction
                var direction = _context.Builder.TraversalDirection ?? GraphTraversalDirection.Outgoing;
                string leftArrow, rightArrow;
                switch (direction)
                {
                    case GraphTraversalDirection.Incoming:
                        // Incoming: target -> source (we flip perspective)
                        leftArrow = "<-";
                        rightArrow = "-";
                        break;
                    case GraphTraversalDirection.Both:
                        // Both directions: no arrows
                        leftArrow = "-";
                        rightArrow = "-";
                        break;
                    case GraphTraversalDirection.Outgoing:
                    default:
                        // Outgoing: source -> target
                        leftArrow = "-";
                        rightArrow = "->";
                        break;
                }

                // Check if this is the first hop or a continuation
                string pathPattern;
                if (_context.Scope.CurrentHop == 0)
                {
                    // First hop: start with source node
                    var relPattern = string.IsNullOrEmpty(relationshipLabel) ? relAlias : $"{relAlias}:{relationshipLabel}";
                    
                    // Add depth range if specified (for variable-length paths)
                    if (_context.Scope.TraversalMinDepth.HasValue || _context.Scope.TraversalMaxDepth.HasValue)
                    {
                        var minDepth = _context.Scope.TraversalMinDepth ?? 1;
                        var maxDepth = _context.Scope.TraversalMaxDepth ?? minDepth;
                        relPattern = $"{relPattern}*{minDepth}..{maxDepth}";
                        _logger.LogDebug("Added depth range to relationship pattern: {Pattern}", relPattern);
                    }
                    
                    pathPattern = $"({sourceAlias}:{sourceLabel}){leftArrow}[{relPattern}]{rightArrow}({targetAlias}:{targetLabel})";
                    _logger.LogDebug("First hop pattern: {Pattern} (direction: {Direction})", pathPattern, direction);
                }
                else
                {
                    // Subsequent hops: these will be prepended to the existing pattern
                    // For chained PathSegments, generate a complete pattern since target connects to next hop's source
                    var relPattern = string.IsNullOrEmpty(relationshipLabel) ? relAlias : $"{relAlias}:{relationshipLabel}";
                    
                    if (isChainedPathSegments)
                    {
                        // For chained patterns, we need to prepend this hop to the existing pattern
                        // The pattern will be prepended to the existing one, connecting via the target alias
                        // Example: If existing is "(src0:Person)-[r0:KNOWS]->(tgt0:Person)"
                        // and we want to add hop 1, we create "(src1:Person)-[r1:KNOWS]->" 
                        // which prepends to become "(src1:Person)-[r1:KNOWS]->(src0:Person)-[r0:KNOWS]->(tgt0:Person)"
                        // For incoming direction, we need to include the target alias (connection point)
                        pathPattern = $"({sourceAlias}:{sourceLabel}){leftArrow}[{relPattern}]{rightArrow}({targetAlias})";
                        _logger.LogDebug("Chained hop {Hop} pattern (to be prepended): {Pattern} (direction: {Direction})", 
                            _context.Scope.CurrentHop, pathPattern, direction);
                    }
                    else
                    {
                        // Non-chained multi-hop: independent pattern
                        pathPattern = $"({sourceAlias}:{sourceLabel}){leftArrow}[{relPattern}]{rightArrow}({targetAlias}:{targetLabel})";
                        _logger.LogDebug("Independent hop {Hop} pattern: {Pattern} (direction: {Direction})", 
                            _context.Scope.CurrentHop, pathPattern, direction);
                    }
                }

                // Add the relationship pattern using AddMatchPattern
                _context.Builder.AddMatchPattern(pathPattern);
                _logger.LogDebug("Added path segment pattern: {Pattern}", pathPattern);

                // Add relationship type filter for interface types
                AddRelationshipTypeFilter(relationshipType, relAlias);

                // NOTE: PathSegments does NOT add any RETURN clause.
                // The RETURN clause is determined by:
                // 1. Explicit Select projections (handled by HandleSelect)
                // 2. The final return type (handled by ToList/FirstOrDefault/etc.)
                // This ensures queries like .PathSegments().Select(ps => ps.StartNode.Name)
                // only return "src0.Name", not "src0, r0, tgt0, src0.Name"
                _logger.LogDebug("PathSegments completed - no default RETURN added (will be determined by outer Select or final operator)");

                // Update CurrentAlias to point to the traversal target
                // This ensures subsequent operations (e.g., AverageAsync) use the target node
                _context.Scope.CurrentAlias = targetAlias;
                _logger.LogDebug("Updated CurrentAlias to target: {TargetAlias}", targetAlias);

                // Advance to next hop for potential chained PathSegments
                _context.Scope.AdvanceHop();
            }
        }

        // Continue processing the expression tree
        return Visit(node.Arguments[0]);
    }

    private bool ContainsPathSegmentsInChain(Expression expression)
    {
        _logger.LogDebug("ContainsPathSegmentsInChain: checking {ExprType}", expression.GetType().Name);
        
        switch (expression)
        {
            case MethodCallExpression methodCall:
                _logger.LogDebug("ContainsPathSegmentsInChain: MethodCall {MethodName}", methodCall.Method.Name);
                
                if (methodCall.Method.Name == nameof(GraphTraversalExtensions.PathSegments))
                {
                    _logger.LogDebug("ContainsPathSegmentsInChain: Found PathSegments!");
                    return true;
                }
                // For WHERE clauses in the chain, they don't break chaining - they're just filters
                // Continue looking for PathSegments in the arguments
                if (methodCall.Method.Name == "Where" || methodCall.Method.Name == "Select")
                {
                    _logger.LogDebug("ContainsPathSegmentsInChain: {MethodName} - checking args recursively", methodCall.Method.Name);
                    return methodCall.Arguments.Any(ContainsPathSegmentsInChain);
                }
                // For other method calls, also check recursively  
                _logger.LogDebug("ContainsPathSegmentsInChain: {MethodName} - checking args recursively", methodCall.Method.Name);
                return methodCall.Arguments.Any(ContainsPathSegmentsInChain);
            
            case UnaryExpression unary:
                _logger.LogDebug("ContainsPathSegmentsInChain: UnaryExpression - checking operand");
                return ContainsPathSegmentsInChain(unary.Operand);
            
            default:
                _logger.LogDebug("ContainsPathSegmentsInChain: {ExprType} - no PathSegments", expression.GetType().Name);
                return false;
        }
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