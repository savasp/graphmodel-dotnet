using System.Collections.Immutable;
using System.Linq.Expressions;
using Microsoft.Extensions.Logging;
using Cvoya.Graph.Model;
using Cvoya.Graph.Model.Age.Querying.Cypher.Builders;

namespace Cvoya.Graph.Model.Age.Querying.Cypher.Visitors.Core.Utilities;

/// <summary>
/// Helper class for resolving Where expressions to Cypher WHERE conditions.
/// Handles special cases like path segment lambdas, relationship filters,
/// node filters in path segment context, and determines correct alias usage.
/// </summary>
internal static class WhereExpressionResolver
{
    /// <summary>
    /// Determines the semantic position of a WHERE clause relative to traversal operations.
    /// </summary>
    public enum WherePosition
    {
        /// <summary>WHERE clause appears before PathSegments - applies to source nodes</summary>
        PreTraversal,
        /// <summary>WHERE clause appears after PathSegments - applies to target nodes</summary>
        PostTraversal,
        /// <summary>Position cannot be determined - use context fallback</summary>
        Unknown
    }

    /// <summary>
    /// Context information needed to resolve WHERE expressions correctly.
    /// </summary>
    public record WhereContext(
        LambdaExpression Lambda,
        Expression SourceExpression,
        CypherQueryContext QueryContext,
        ILogger Logger,
        bool ContainsPathSegments)
    {
        /// <summary>
        /// Optional explicit alias context from the previous operation (e.g., PathSegments).
        /// When provided, this takes precedence over reading CurrentAlias from the scope.
        /// Enables explicit alias passing instead of implicit mutation.
        /// </summary>
        public AliasResolutionResult? ExplicitAliasContext { get; init; }
    }

    /// <summary>
    /// Result of WHERE expression resolution containing both the condition and metadata.
    /// </summary>
    public record WhereResolution(
        string Condition,
        string ExpressionAlias,
        ImmutableArray<string> ConsumedAliases);

    /// <summary>
    /// Resolves a WHERE lambda expression to a Cypher WHERE condition string.
    /// </summary>
    public static WhereResolution ResolveWhereExpression(WhereContext context)
    {
        var lambda = context.Lambda;
        var logger = context.Logger;

        logger.LogDebug("Processing WHERE clause - lambda body type: {LambdaBodyType}", 
            lambda.Body.GetType().Name);
        
        if (lambda.Parameters.Count >= 1)
        {
            logger.LogDebug("Lambda parameter [0] type: {Type}, name: {Name}", 
                lambda.Parameters[0].Type, lambda.Parameters[0].Name);
        }

        // Determine the type of WHERE clause and create appropriate expression visitor
        var (expressionVisitor, expressionAlias) = CreateExpressionVisitorForWhere(context);
        
        // Visit the lambda body to get the Cypher condition
        var whereCondition = expressionVisitor.VisitAndReturnCypher(lambda.Body);
        logger.LogDebug("Expression visitor returned: {WhereCondition}", whereCondition);

        var consumedAliases = ImmutableArray.Create(expressionAlias);
        return new WhereResolution(whereCondition, expressionAlias, consumedAliases);
    }

    private static (AgeExpressionToCypherVisitor Visitor, string Alias) CreateExpressionVisitorForWhere(
        WhereContext context)
    {
        var lambda = context.Lambda;
        var logger = context.Logger;
        var queryContext = context.QueryContext;

        if (lambda.Parameters.Count != 1)
        {
            logger.LogDebug("Using regular expression visitor (no lambda parameter)");
            var visitor = CreateRegularExpressionVisitor(queryContext, logger);
            var alias = GetContextualAlias(queryContext);
            return (visitor, alias);
        }

        var parameterType = lambda.Parameters[0].Type;

        // Check if this is a path segment lambda (e.g., ps => ps.EndNode.Age > 35)
        if (typeof(IGraphPathSegment).IsAssignableFrom(parameterType))
        {
            return CreatePathSegmentVisitor(context);
        }

        // Check if this is a relationship WHERE clause
        if (typeof(IRelationship).IsAssignableFrom(parameterType))
        {
            return CreateRelationshipVisitor(context);
        }

        // Check if this is a node WHERE clause in a path segment context
        if (typeof(INode).IsAssignableFrom(parameterType) &&
            (context.ContainsPathSegments || queryContext.Builder.HasMatchPatterns))
        {
            return CreateNodeInPathSegmentContextVisitor(context);
        }

        // Default: regular expression visitor
        logger.LogDebug("Using regular expression visitor (parameter type: {Type})", parameterType.Name);
        var regularVisitor = CreateRegularExpressionVisitor(queryContext, logger);
        var regularAlias = GetContextualAlias(queryContext);
        return (regularVisitor, regularAlias);
    }

    private static (AgeExpressionToCypherVisitor, string) CreatePathSegmentVisitor(WhereContext context)
    {
        var logger = context.Logger;
        var queryContext = context.QueryContext;
        var lambda = context.Lambda;

        logger.LogDebug("Detected path segment WHERE clause - using path segment context");

        // Find which hop this WHERE is associated with by checking the source expression
        int targetHop = FindPathSegmentHopForWhere(context.SourceExpression, logger);
        logger.LogDebug("WHERE clause targets hop {Hop}", targetHop);

        // Create a visitor that's aware of the path segment parameter
        var visitor = CreatePathSegmentExpressionVisitor(
            lambda.Parameters[0], 
            targetHop, 
            queryContext, 
            logger);

        return (visitor, "ps");
    }

    private static (AgeExpressionToCypherVisitor, string) CreateRelationshipVisitor(WhereContext context)
    {
        var logger = context.Logger;
        var queryContext = context.QueryContext;
        var parameterType = context.Lambda.Parameters[0].Type;

        var relAlias = queryContext.Scope.GetNumberedAlias("r");
        logger.LogDebug("Detected relationship WHERE clause - parameter type: {Type}, using '{Alias}' alias",
            parameterType.Name, relAlias);

        var visitor = new AgeExpressionToCypherVisitor(
            queryContext.Builder, 
            logger, 
            alias: relAlias);

        return (visitor, relAlias);
    }

    private static (AgeExpressionToCypherVisitor, string) CreateNodeInPathSegmentContextVisitor(
        WhereContext context)
    {
        var logger = context.Logger;
        var queryContext = context.QueryContext;
        var parameterType = context.Lambda.Parameters[0].Type;

        // Determine semantic position: is this WHERE before or after the traversal?
        var wherePosition = DetermineWherePosition(context.SourceExpression, queryContext, logger);

        // Determine the alias based on WHERE position and current hop
        string targetAlias;
        int hopNumber = Math.Max(0, queryContext.Scope.CurrentHop - 1);

        if (wherePosition == WherePosition.PreTraversal)
        {
            // PreTraversal filters source nodes
            bool isChainedPattern = queryContext.Builder.HasMatchPatterns && 
                                   queryContext.Scope.CurrentHop > 0;
            if (isChainedPattern)
            {
                hopNumber = queryContext.Scope.CurrentHop - 1;
                logger.LogDebug("PreTraversal WHERE in chained pattern: using outermost source at hop {HopNumber}",
                    hopNumber);
            }
            targetAlias = queryContext.Scope.GetNumberedAliasForHop("src", hopNumber);
            logger.LogDebug("PreTraversal WHERE: isChained={IsChained}, hopNumber={HopNumber}, alias={Alias}",
                isChainedPattern, hopNumber, targetAlias);
        }
        else
        {
            // PostTraversal filters target nodes
            targetAlias = queryContext.Scope.CurrentAlias ?? 
                         queryContext.Scope.GetNumberedAliasForHop("tgt", hopNumber);
            logger.LogDebug("PostTraversal WHERE: using CurrentAlias={Alias} (CurrentHop={CurrentHop})",
                targetAlias, queryContext.Scope.CurrentHop);
        }

        logger.LogDebug("Detected node WHERE clause in path segment context - position: {Position}, " +
                       "paramType: {ParamType}, mapping to: {Alias}",
            wherePosition, parameterType.Name, targetAlias);

        var visitor = new AgeExpressionToCypherVisitor(
            queryContext.Builder, 
            logger, 
            alias: targetAlias);

        return (visitor, targetAlias);
    }

    private static AgeExpressionToCypherVisitor CreateRegularExpressionVisitor(
        CypherQueryContext context,
        ILogger logger)
    {
        var alias = context.Scope.CurrentAlias ?? GetContextualAlias(context);
        logger.LogDebug("CreateExpressionVisitor: Using alias '{Alias}'", alias);
        return new AgeExpressionToCypherVisitor(context.Builder, logger, alias);
    }

    private static WherePosition DetermineWherePosition(
        Expression sourceExpression,
        CypherQueryContext context,
        ILogger logger)
    {
        logger.LogDebug("Analyzing WHERE position");

        // Check if PathSegments exists in the WHERE's source expression
        bool pathSegmentsInSource = ContainsPathSegmentsCall(sourceExpression);

        if (pathSegmentsInSource)
        {
            logger.LogDebug("PathSegments found in WHERE source - WHERE is PostTraversal (target filter)");
            return WherePosition.PostTraversal;
        }
        
        if (context.Builder.HasMatchPatterns)
        {
            logger.LogDebug("Match patterns exist but PathSegments not in WHERE source - " +
                          "WHERE is PreTraversal (source filter)");
            return WherePosition.PreTraversal;
        }

        logger.LogDebug("No match patterns and no PathSegments in source - simple node query");
        return WherePosition.Unknown;
    }

    private static int FindPathSegmentHopForWhere(Expression sourceExpression, ILogger logger)
    {
        var current = sourceExpression;
        int pathSegmentsDepth = 0;

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

                    // The WHERE targets the FIRST PathSegments encountered
                    // Return the depth - this will be used as the hop number
                    logger.LogDebug("Found PathSegments at depth {Depth}", pathSegmentsDepth);
                    return pathSegmentsDepth - 1;
                }

                // Continue up the chain
                if (methodCall.Arguments.Count > 0)
                {
                    current = methodCall.Arguments[0];
                }
                else
                {
                    break;
                }
            }
            else
            {
                break;
            }
        }

        logger.LogDebug("No PathSegments found in source expression");
        return 0;
    }

    private static AgeExpressionToCypherVisitor CreatePathSegmentExpressionVisitor(
        ParameterExpression pathSegmentParameter,
        int hopNumber,
        CypherQueryContext context,
        ILogger logger)
    {
        // Use stored hop aliases (avoids type collision issues in chained patterns)
        var hopAliases = context.Scope.GetHopAliases(hopNumber);
        if (hopAliases.HasValue)
        {
            var (sourceAlias, relationshipAlias, targetAlias) = hopAliases.Value;
            logger.LogDebug("Using stored hop {Hop} aliases: src={Src}, r={Rel}, tgt={Tgt}",
                hopNumber, sourceAlias, relationshipAlias, targetAlias);

            return new AgeExpressionToCypherVisitor(context.Builder, logger, "ps", pathSegmentParameter,
                sourceAlias, relationshipAlias, targetAlias);
        }

        // Fallback to numbered aliases if not found
        logger.LogDebug("Hop {Hop} aliases not found in storage, falling back to numbered aliases", hopNumber);
        var fallbackSourceAlias = context.Scope.GetNumberedAliasForHop("src", hopNumber);
        var fallbackRelAlias = context.Scope.GetNumberedAliasForHop("r", hopNumber);
        var fallbackTargetAlias = context.Scope.GetNumberedAliasForHop("tgt", hopNumber);

        return new AgeExpressionToCypherVisitor(context.Builder, logger, "ps", pathSegmentParameter,
            fallbackSourceAlias, fallbackRelAlias, fallbackTargetAlias);
    }

    private static bool ContainsPathSegmentsCall(Expression expression)
    {
        if (expression is MethodCallExpression methodCall)
        {
            // Check if this is a PathSegments call
            if (methodCall.Method.Name == "PathSegments" ||
                methodCall.Method.Name == "PathSegmentsIncoming" ||
                methodCall.Method.Name == "PathSegmentsOutgoing")
            {
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

    private static string GetContextualAlias(CypherQueryContext context)
    {
        if (context.Scope.CurrentHop > 0)
        {
            return context.Scope.GetNumberedAliasForHop("src", Math.Max(0, context.Scope.CurrentHop - 1));
        }

        return "src0";
    }
}
