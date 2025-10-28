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
        return new AgeExpressionToCypherVisitor(_context.Builder, _logger, alias);
    }

    private AgeExpressionToCypherVisitor CreatePathSegmentExpressionVisitor(ParameterExpression pathSegmentParameter)
    {
        // For path segments, we need to create a visitor that can handle the path segment parameter
        // Pass the current hop's numbered aliases for proper mapping
        var sourceAlias = _context.Scope.GetNumberedAlias("src");
        var relationshipAlias = _context.Scope.GetNumberedAlias("r");
        var targetAlias = _context.Scope.GetNumberedAlias("tgt");
        
        _logger.LogDebug("Creating path segment visitor with aliases: src={Src}, r={Rel}, tgt={Tgt}", 
            sourceAlias, relationshipAlias, targetAlias);
        
        return new AgeExpressionToCypherVisitor(_context.Builder, _logger, "ps", pathSegmentParameter,
            sourceAlias, relationshipAlias, targetAlias);
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
            // For path segments, create a visitor that's aware of the path segment parameter
            expressionVisitor = CreatePathSegmentExpressionVisitor(lambda.Parameters[0]);
        }
        // Check if this is a relationship WHERE clause (check parameter type directly)
        else if (lambda.Parameters.Count == 1 && 
                 typeof(IRelationship).IsAssignableFrom(lambda.Parameters[0].Type))
        {
            _logger.LogDebug("Detected relationship WHERE clause - parameter type: {Type}, using 'r' alias", lambda.Parameters[0].Type.Name);
            expressionVisitor = new AgeExpressionToCypherVisitor(_context.Builder, _logger, alias: "r");
        }
        // Check if this is a node WHERE clause in a path segment context
        else if (lambda.Parameters.Count == 1 && 
                 typeof(INode).IsAssignableFrom(lambda.Parameters[0].Type) &&
                 (ContainsPathSegmentsCall(node) || _context.Builder.HasMatchPatterns))
        {
            // Determine semantic position: is this WHERE before or after the traversal?
            var wherePosition = DetermineWherePosition(node);
            
            // For multi-hop scenarios, we need to determine which hop this WHERE applies to
            string targetAlias;
            if (_context.Scope.CurrentHop == 0)
            {
                // Single hop or initial hop scenario
                targetAlias = wherePosition == WherePosition.PreTraversal ? "src0" : "tgt0";
            }
            else
            {
                // Multi-hop scenario - determine the appropriate hop number
                if (wherePosition == WherePosition.PreTraversal)
                {
                    // For chained patterns, PreTraversal always applies to the first hop (src0)
                    // For independent hops, use the most recent hop
                    bool isChainedPattern = _context.Builder.HasMatchPatterns && _context.Scope.CurrentHop > 0;
                    int hopNumber = isChainedPattern ? 0 : Math.Max(0, _context.Scope.CurrentHop - 1);
                    targetAlias = _context.Scope.GetNumberedAliasForHop("src", hopNumber);
                    _logger.LogDebug("PreTraversal WHERE: isChained={IsChained}, hopNumber={HopNumber}, alias={Alias}", 
                        isChainedPattern, hopNumber, targetAlias);
                }
                else
                {
                    // PostTraversal uses the current hop's target
                    int hopNumber = _context.Scope.CurrentHop - 1;
                    targetAlias = _context.Scope.GetNumberedAliasForHop("tgt", hopNumber);
                }
            }
            
            _logger.LogDebug("Detected node WHERE clause in path segment context - position: {Position}, mapping to: {Alias}", 
                wherePosition, targetAlias);
            
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

        // Continue processing the source expression
        return Visit(node.Arguments[0]);
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
        if (sourceExpression is MethodCallExpression sourceMethod && 
            sourceMethod.Method.Name == nameof(GraphTraversalExtensions.PathSegments))
        {
            _logger.LogDebug("Detected PathSegments + Select pattern - processing PathSegments first");
            
            // First, visit the PathSegments to set up the context
            Visit(sourceExpression);
            
            // Now process the Select with the path segment context in place
            var body = lambda.Body;
            
            if (body is MemberExpression memberExpr && memberExpr.Expression is ParameterExpression)
            {
                var propertyName = memberExpr.Member.Name;
                
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
                            // Use numbered alias if in multi-hop context
                            _logger.LogDebug("DEBUG: CurrentHop={CurrentHop} when adding EndNode projection", _context.Scope.CurrentHop);
                            if (_context.Scope.CurrentHop > 0)
                            {
                                var hopNumber = _context.Scope.CurrentHop - 1;
                                targetAlias = _context.Scope.GetNumberedAliasForHop("tgt", hopNumber);
                                _logger.LogDebug("DEBUG: Using numbered alias tgt{HopNumber} = {Alias}", hopNumber, targetAlias);
                            }
                            else
                            {
                                targetAlias = "tgt";
                                _logger.LogDebug("DEBUG: CurrentHop is 0, using fallback 'tgt'");
                            }
                        }
                        
                        _context.Builder.AddReturn(targetAlias);
                        _logger.LogDebug("Added path segment EndNode projection: {Alias}", targetAlias);
                    }
                    else
                    {
                        _logger.LogDebug("Skipped intermediate EndNode projection in chained PathSegments context");
                    }
                }
                else if (propertyName == "StartNode")
                {
                    var sourceType = _context.Scope.TraversalInfo?.SourceNodeType;
                    if (sourceType != null)
                    {
                        var sourceAlias = _context.Scope.GetAliasForType(sourceType) ?? "src";
                        _context.Builder.AddReturn(sourceAlias);
                        _logger.LogDebug("Added path segment StartNode projection: {Alias}", sourceAlias);
                    }
                    else
                    {
                        var sourceAlias = _context.Scope.GetNumberedAlias("src");
                        _context.Builder.AddReturn(sourceAlias);
                        _logger.LogDebug("Added default path segment StartNode projection: {Alias}", sourceAlias);
                    }
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

                    // Use previous hop aliases for projection (since AdvanceHop was called after last PathSegments)
                    var lastHop = _context.Scope.CurrentHop - 1;
                    
                    // For chained patterns, adjust aliases to match the chained structure
                    string srcAlias, tgtAlias, rAlias;
                    bool isChainedPattern = _context.Builder.HasMatchPatterns && lastHop > 0;
                    
                    if (isChainedPattern)
                    {
                        // In chained patterns:
                        // - PathSegment.StartNode maps to the previous hop's target (becomes this hop's source)
                        // - PathSegment.EndNode maps to this hop's target
                        srcAlias = _context.Scope.GetNumberedAliasForHop("tgt", lastHop - 1);  // Previous hop's target
                        tgtAlias = _context.Scope.GetNumberedAliasForHop("tgt", lastHop);      // This hop's target
                        rAlias = _context.Scope.GetNumberedAliasForHop("r", lastHop);         // This hop's relationship
                        _logger.LogDebug("Chained pattern projection: StartNode->{StartAlias}, EndNode->{EndAlias}, Relationship->{RelAlias}", 
                            srcAlias, tgtAlias, rAlias);
                    }
                    else
                    {
                        // Independent hop uses standard numbered aliases
                        srcAlias = _context.Scope.GetNumberedAliasForHop("src", lastHop);
                        tgtAlias = _context.Scope.GetNumberedAliasForHop("tgt", lastHop);
                        rAlias = _context.Scope.GetNumberedAliasForHop("r", lastHop);
                        _logger.LogDebug("Independent pattern projection: src={SrcAlias}, tgt={TgtAlias}, r={RelAlias}", 
                            srcAlias, tgtAlias, rAlias);
                    }

                    _logger.LogDebug($"DEBUG: CurrentHop={_context.Scope.CurrentHop}, using lastHop={lastHop}, aliases: src='{srcAlias}', tgt='{tgtAlias}', r='{rAlias}'");

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
            // Simple property projection: Select(p => p.FirstName) or Select(ps => ps.EndNode)
            var propertyName = memberExpr.Member.Name;
            
            // Check if we're in a path segment context by also scanning for PathSegments calls
            bool containsPathSegments = ContainsPathSegmentsCall(node);
            bool isPathSegmentContext = _context.Scope.IsPathSegmentContext || containsPathSegments;
            
            // Handle path segment projections specially
            if (isPathSegmentContext && propertyName == "EndNode")
            {
                // In chained multi-hop PathSegments context, skip individual EndNode projections  
                // The final complex SELECT will handle all projections
                var isInChainedContext = _context.Builder.HasMatchPatterns;
                
                _logger.LogDebug("DEBUG: Location2 EndNode projection - hasMatchPatterns={HasPatterns}, skipping={Skip}", 
                    isInChainedContext, isInChainedContext);
                
                if (!isInChainedContext)
                {
                    // In path segment context, EndNode refers to the target node
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
                            // In multi-hop context, use numbered alias instead of 'tgt'
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
                        // Use numbered alias if in multi-hop context
                        _logger.LogDebug("DEBUG: Location2 CurrentHop={CurrentHop} when adding EndNode projection", _context.Scope.CurrentHop);
                        if (_context.Scope.CurrentHop > 0)
                        {
                            var hopNumber = _context.Scope.CurrentHop - 1;
                            targetAlias = _context.Scope.GetNumberedAliasForHop("tgt", hopNumber);
                            _logger.LogDebug("DEBUG: Location2 Using numbered alias tgt{HopNumber} = {Alias}", hopNumber, targetAlias);
                        }
                        else
                        {
                            targetAlias = "tgt";
                            _logger.LogDebug("DEBUG: Location2 CurrentHop is 0, using fallback 'tgt'");
                        }
                    }
                    
                    _context.Builder.AddReturn(targetAlias);
                    _logger.LogDebug("Added path segment EndNode projection: {Alias}", targetAlias);
                }
                else
                {
                    _logger.LogDebug("Skipped intermediate EndNode projection in chained PathSegments context");
                }
            }
            else if (isPathSegmentContext && propertyName == "StartNode")
            {
                // In path segment context, StartNode refers to the source node
                var sourceType = _context.Scope.TraversalInfo?.SourceNodeType;
                if (sourceType != null)
                {
                    var sourceAlias = _context.Scope.GetAliasForType(sourceType) ?? "src";
                    _context.Builder.AddReturn(sourceAlias);
                    _logger.LogDebug("Added path segment StartNode projection: {Alias}", sourceAlias);
                }
                else
                {
                    var sourceAlias = _context.Scope.GetNumberedAlias("src");
                    _context.Builder.AddReturn(sourceAlias);
                    _logger.LogDebug("Added default path segment StartNode projection: {Alias}", sourceAlias);
                }
            }
            else if (isPathSegmentContext && propertyName == "Relationship")
            {
                // In path segment context, Relationship refers to the relationship
                var relationshipType = _context.Scope.TraversalInfo?.RelationshipType;
                if (relationshipType != null)
                {
                    var relAlias = _context.Scope.GetAliasForType(relationshipType) ?? "r";
                    _context.Builder.AddReturn(relAlias);
                    _logger.LogDebug("Added path segment Relationship projection: {Alias}", relAlias);
                }
                else
                {
                    _context.Builder.AddReturn("r");
                    _logger.LogDebug("Added default path segment Relationship projection: r");
                }
            }
            else if (!isPathSegmentContext)
            {
                // Regular property projection - but ONLY if not in path segment context
                var alias = _context.Scope.CurrentAlias ?? GetContextualAlias();
                _context.Builder.AddReturn($"{alias}.{propertyName}");
                _logger.LogDebug("Added simple property projection: {Property}", propertyName);
            }
            else
            {
                _logger.LogDebug("Skipping property projection in path segment context: {Property}", propertyName);
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

        // Continue processing the source expression
        return Visit(node.Arguments[0]);
    }

    private void HandleAnonymousProjection(NewExpression newExpr)
    {
        var projections = new List<string>();
        var alias = _context.Scope.CurrentAlias ?? GetContextualAlias();

        for (int i = 0; i < newExpr.Arguments.Count; i++)
        {
            var arg = newExpr.Arguments[i];
            var member = newExpr.Members?[i];
            var projectionAlias = member?.Name ?? $"field{i}";

            // Use safe alias to avoid reserved word conflicts in AGE
            var safeAlias = $"c_{projectionAlias}";

            if (arg is MemberExpression argMemberExpr && argMemberExpr.Expression is ParameterExpression)
            {
                // Simple property: p.FirstName
                var propertyName = argMemberExpr.Member.Name;
                projections.Add($"{alias}.{propertyName} AS {safeAlias}");
            }
            else
            {
                // Complex expression - use visitor
                var expressionVisitor = CreateExpressionVisitor();
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
        
        AgeExpressionToCypherVisitor expressionVisitor;
        if (containsPathSegments || _context.Scope.IsPathSegmentContext)
        {
            _logger.LogDebug("ORDER BY in path segment context - using context-aware visitor");
            // In path segment context, we need to determine the correct alias
            // For now, assume we're ordering by the start node (src) properties
            var sourceAlias = _context.Scope.GetNumberedAlias("src");
            expressionVisitor = new AgeExpressionToCypherVisitor(_context.Builder, _logger, sourceAlias);
        }
        else
        {
            expressionVisitor = CreateExpressionVisitor();
        }
        
        var orderExpression = expressionVisitor.VisitAndReturnCypher(lambda.Body);
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
            countAlias = "tgt"; // Path segments count the target nodes
        }
        
        _context.Builder.AddReturn($"count({countAlias})");
        _logger.LogDebug("Added COUNT aggregation with alias: {Alias}", countAlias);

        // Continue processing the source expression
        return Visit(node.Arguments[0]);
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

        // Continue processing the source expression
        return Visit(node.Arguments[0]);
    }

    private Expression HandleFirst(MethodCallExpression node)
    {
        return HandleElementAccess(node, 1, "First");
    }

    private Expression HandleLast(MethodCallExpression node)
    {
        // For Last, we need to reverse the order
        _context.Builder.ReverseOrderBy();
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
            _context.Builder.AddReturn("r");
            _logger.LogDebug("Added basic RETURN r for relationship element access");
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
                _context.Builder.AddReturn("src, r, tgt");
                _logger.LogDebug("Added default path segment return: src, r, tgt");
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
                _context.Builder.AddReturn("r");
                _logger.LogDebug("Added default relationship return with alias: r");
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
            // Relationship query: MATCH ()-[r:Label]->()
            var label = Labels.GetLabelFromType(elementType);
            var alias = _context.Scope.CurrentAlias ?? "r";
            _context.Builder.AddMatchPattern($"()-[{alias}:{label}]->()");
            
            _logger.LogDebug("Set up relationship match for type {Type} with label {Label}", elementType.Name, label);
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
                var targetAlias = _context.Scope.GetNumberedAlias("tgt");

                _logger.LogDebug("Generated hop {Hop} aliases: src={SrcAlias}, r={RelAlias}, tgt={TgtAlias}", 
                    _context.Scope.CurrentHop, sourceAlias, relAlias, targetAlias);

                // Build the path pattern for AGE
                var sourceLabel = Labels.GetBaseTypeLabel(sourceType);
                var relationshipLabel = Labels.GetBaseTypeLabel(relationshipType);
                var targetLabel = Labels.GetBaseTypeLabel(targetType);

                // Check if this is the first hop or a continuation
                string pathPattern;
                if (_context.Scope.CurrentHop == 0)
                {
                    // First hop: start with source node
                    pathPattern = $"({sourceAlias}:{sourceLabel})-[{relAlias}:{relationshipLabel}]->({targetAlias}:{targetLabel})";
                    _logger.LogDebug("First hop pattern: {Pattern}", pathPattern);
                }
                else
                {
                    // Subsequent hops: chain from previous target to new target
                    // For chained PathSegments, we don't repeat the source node since it's already the target of the previous hop
                    if (isChainedPathSegments)
                    {
                        pathPattern = $"-[{relAlias}:{relationshipLabel}]->({targetAlias}:{targetLabel})";
                        _logger.LogDebug("Chained hop {Hop} pattern: {Pattern}", _context.Scope.CurrentHop, pathPattern);
                    }
                    else
                    {
                        // Non-chained multi-hop (shouldn't happen in normal cases, but handle gracefully)
                        pathPattern = $"({sourceAlias}:{sourceLabel})-[{relAlias}:{relationshipLabel}]->({targetAlias}:{targetLabel})";
                        _logger.LogDebug("Independent hop {Hop} pattern: {Pattern}", _context.Scope.CurrentHop, pathPattern);
                    }
                }

                // Add the relationship pattern using AddMatchPattern
                _context.Builder.AddMatchPattern(pathPattern);
                _logger.LogDebug("Added path segment pattern: {Pattern}", pathPattern);

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
            
            // For now, just log the direction - we'll implement proper direction handling later
            _logger.LogDebug("Set traversal direction: {Direction}", direction);
            
            // TODO: Implement direction handling in AGE query builder
            // This might require modifying the path pattern to use <- for incoming relationships
        }

        // Continue processing the expression tree
        return Visit(node.Arguments[0]);
    }

    /// <summary>
    /// Gets the appropriate alias for the current context.
    /// In path segment contexts, returns "src" (source node alias).
    /// In regular node contexts, returns "n".
    /// </summary>
    /// <returns>The contextual alias to use for queries.</returns>
    private string GetContextualAlias()
    {
        if (_context.Scope.IsPathSegmentContext)
        {
            return "src"; // Use source node alias in path segment contexts
        }
        return "n"; // Use default node alias in regular contexts
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
        
        // Simple approach: use the current builder state to determine position
        // Since we process bottom-up:
        // - If HasMatchPatterns is true, PathSegments already processed, so WHERE is logically PreTraversal
        // - If HasMatchPatterns is false, this is likely a simple node query
        
        if (_context.Builder.HasMatchPatterns)
        {
            _logger.LogDebug("Builder has match patterns - WHERE is PreTraversal (source filter)");
            return WherePosition.PreTraversal;
        }
        else
        {
            _logger.LogDebug("Builder has no match patterns - checking for PathSegments in expression");
            // Check if PathSegments exists in the overall expression to distinguish between
            // a simple node query vs a traversal query where PathSegments hasn't processed yet
            if (ContainsPathSegmentsCall(whereNode))
            {
                _logger.LogDebug("Found PathSegments in expression - WHERE is PreTraversal");
                return WherePosition.PreTraversal;
            }
            else
            {
                _logger.LogDebug("No PathSegments found - this is a simple node query");
                return WherePosition.Unknown;
            }
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

    #endregion
}