using System.Collections.Immutable;
using System.Linq.Expressions;
using Microsoft.Extensions.Logging;
using Cvoya.Graph.Model;
using Cvoya.Graph.Model.Age.Querying.Cypher.Builders;

namespace Cvoya.Graph.Model.Age.Querying.Cypher.Visitors.Core.Utilities;

/// <summary>
/// Helper class for resolving Select projection expressions to Cypher RETURN clauses.
/// Handles PathSegment projections, anonymous types, simple properties, complex expressions,
/// and chained Select operations.
/// </summary>
internal static class SelectProjectionResolver
{
    /// <summary>
    /// Type of projection being performed.
    /// </summary>
    public enum ProjectionType
    {
        /// <summary>PathSegments context with complex projection</summary>
        PathSegmentComplex,
        /// <summary>PathSegments context with simple property (StartNode/EndNode/Relationship)</summary>
        PathSegmentSimple,
        /// <summary>Anonymous type projection (new { ... })</summary>
        AnonymousType,
        /// <summary>Simple property access (p => p.Name)</summary>
        SimpleProperty,
        /// <summary>Complex expression (p => p.Age * 2)</summary>
        ComplexExpression
    }

    /// <summary>
    /// Context information needed to resolve Select projections.
    /// </summary>
    public record SelectContext(
        LambdaExpression Lambda,
        Expression SourceExpression,
        CypherQueryContext QueryContext,
        ILogger Logger,
        bool IsPathSegmentSource,
        int? PathSegmentHop,
        AliasResolutionResult? ExplicitAliasContext = null);

    /// <summary>
    /// Result of Select projection resolution.
    /// </summary>
    public record SelectResolution(
        ProjectionType Type,
        ImmutableArray<string> Projections,
        string? UpdatedAlias,
        bool ExitPathSegmentContext,
        bool DisableComplexPropertyLoading);

    /// <summary>
    /// Resolves a Select lambda expression to Cypher RETURN clause(s).
    /// </summary>
    public static SelectResolution ResolveSelectProjection(SelectContext context)
    {
        var lambda = context.Lambda;
        var body = lambda.Body;
        var logger = context.Logger;

        logger.LogDebug("Resolving Select projection - body type: {BodyType}", body.GetType().Name);

        // Check if this is a PathSegment source
        if (context.IsPathSegmentSource)
        {
            return ResolvePathSegmentProjection(context);
        }

        // Handle different projection types
        if (body is NewExpression newExpr)
        {
            return ResolveAnonymousTypeProjection(newExpr, context);
        }

        if (body is MemberExpression memberExpr && memberExpr.Expression is ParameterExpression)
        {
            return ResolveSimplePropertyProjection(memberExpr, lambda, context);
        }

        return ResolveComplexExpressionProjection(body, context);
    }

    private static SelectResolution ResolvePathSegmentProjection(SelectContext context)
    {
        var lambda = context.Lambda;
        var body = lambda.Body;
        var logger = context.Logger;
        var queryContext = context.QueryContext;

        // Check for simple PathSegment property access (StartNode, EndNode, Relationship)
        if (body is MemberExpression memberExpr && 
            memberExpr.Expression is ParameterExpression paramExpr &&
            paramExpr == lambda.Parameters[0])
        {
            var propertyName = memberExpr.Member.Name;
            
            if (propertyName == "EndNode")
            {
                return ResolvePathSegmentEndNode(context);
            }
            
            if (propertyName == "StartNode")
            {
                return ResolvePathSegmentStartNode(context);
            }
            
            if (propertyName == "Relationship")
            {
                return ResolvePathSegmentRelationship(context);
            }

            // Regular property on path segment
            return ResolvePathSegmentProperty(propertyName, context);
        }

        // Complex projection within path segments
        return ResolvePathSegmentComplexProjection(context);
    }

    private static SelectResolution ResolvePathSegmentEndNode(SelectContext context)
    {
        var logger = context.Logger;
        var queryContext = context.QueryContext;
        var isInChainedContext = queryContext.Builder.HasMatchPatterns;

        logger.LogDebug("PathSegment EndNode projection - hasMatchPatterns={HasPatterns}, skipping={Skip}",
            isInChainedContext, isInChainedContext);

        if (isInChainedContext)
        {
            logger.LogDebug("Skipped intermediate EndNode projection in chained PathSegments context");
            return new SelectResolution(
                ProjectionType.PathSegmentSimple,
                ImmutableArray<string>.Empty,
                null,
                ExitPathSegmentContext: false,
                DisableComplexPropertyLoading: true);
        }

        var targetAlias = DetermineTargetAlias(queryContext, logger);
        
        return new SelectResolution(
            ProjectionType.PathSegmentSimple,
            ImmutableArray.Create(targetAlias),
            UpdatedAlias: targetAlias,
            ExitPathSegmentContext: false,
            DisableComplexPropertyLoading: true);
    }

    private static SelectResolution ResolvePathSegmentStartNode(SelectContext context)
    {
        var logger = context.Logger;
        var queryContext = context.QueryContext;
        var isInChainedContext = queryContext.Builder.HasMatchPatterns && queryContext.Scope.CurrentHop > 0;

        logger.LogDebug("PathSegment StartNode projection - hasMatchPatterns={HasPatterns}, currentHop={Hop}, skipping={Skip}",
            queryContext.Builder.HasMatchPatterns, queryContext.Scope.CurrentHop, isInChainedContext);

        var sourceType = queryContext.Scope.TraversalInfo?.SourceNodeType;
        var sourceAlias = sourceType != null
            ? (queryContext.Scope.GetAliasForType(sourceType) ?? "src0")
            : queryContext.Scope.GetNumberedAlias("src");

        if (isInChainedContext)
        {
            logger.LogDebug("Skipped intermediate StartNode projection in chained PathSegments context");
            return new SelectResolution(
                ProjectionType.PathSegmentSimple,
                ImmutableArray<string>.Empty,
                UpdatedAlias: sourceAlias,
                ExitPathSegmentContext: true,
                DisableComplexPropertyLoading: true);
        }

        return new SelectResolution(
            ProjectionType.PathSegmentSimple,
            ImmutableArray.Create(sourceAlias),
            UpdatedAlias: sourceAlias,
            ExitPathSegmentContext: true,
            DisableComplexPropertyLoading: true);
    }

    private static SelectResolution ResolvePathSegmentRelationship(SelectContext context)
    {
        var logger = context.Logger;
        var queryContext = context.QueryContext;
        var relationshipType = queryContext.Scope.TraversalInfo?.RelationshipType;

        string relAlias;
        if (relationshipType != null)
        {
            if (queryContext.Scope.CurrentHop > 0)
            {
                var lastHop = queryContext.Scope.CurrentHop - 1;
                relAlias = queryContext.Scope.GetNumberedAliasForHop("r", lastHop);
            }
            else
            {
                relAlias = queryContext.Scope.GetAliasForType(relationshipType) ?? "r0";
            }
        }
        else
        {
            relAlias = queryContext.Scope.CurrentHop > 0
                ? queryContext.Scope.GetNumberedAliasForHop("r", 0)
                : "r0";
        }

        logger.LogDebug("PathSegment Relationship projection: {Alias}", relAlias);

        return new SelectResolution(
            ProjectionType.PathSegmentSimple,
            ImmutableArray.Create(relAlias),
            UpdatedAlias: null,
            ExitPathSegmentContext: false,
            DisableComplexPropertyLoading: true);
    }

    private static SelectResolution ResolvePathSegmentProperty(string propertyName, SelectContext context)
    {
        var logger = context.Logger;
        var queryContext = context.QueryContext;
        var alias = queryContext.Scope.CurrentAlias ?? GetContextualAlias(queryContext);
        var projection = $"{alias}.{propertyName}";

        logger.LogDebug("PathSegment property projection: {Property}", propertyName);

        return new SelectResolution(
            ProjectionType.PathSegmentSimple,
            ImmutableArray.Create(projection),
            UpdatedAlias: null,
            ExitPathSegmentContext: false,
            DisableComplexPropertyLoading: true);
    }

    private static SelectResolution ResolvePathSegmentComplexProjection(SelectContext context)
    {
        var logger = context.Logger;
        var queryContext = context.QueryContext;
        var lambda = context.Lambda;

        bool containsPathSegments = ContainsPathSegmentsCall(context.SourceExpression);
        if (!containsPathSegments && !queryContext.Scope.IsPathSegmentContext)
        {
            // Not actually in path segment context
            var visitor = CreateRegularExpressionVisitor(queryContext, logger);
            var selectExpression = visitor.VisitAndReturnCypher(lambda.Body);
            
            return new SelectResolution(
                ProjectionType.ComplexExpression,
                ImmutableArray.Create(selectExpression),
                UpdatedAlias: null,
                ExitPathSegmentContext: false,
                DisableComplexPropertyLoading: true);
        }

        // Use the hop number from context
        var targetHop = context.PathSegmentHop ?? Math.Max(0, queryContext.Scope.CurrentHop - 1);
        
        // Determine aliases based on chained pattern
        var (srcAlias, tgtAlias, rAlias) = DeterminePathSegmentAliases(targetHop, queryContext, logger);

        logger.LogDebug("PathSegment complex projection: src={SrcAlias}, tgt={TgtAlias}, r={RelAlias}",
            srcAlias, tgtAlias, rAlias);

        // Create visitor with path segment context
        var dummyParam = Expression.Parameter(typeof(object), "ps");
        var pathSegmentVisitor = new AgeExpressionToCypherVisitor(
            queryContext.Builder, logger, "ps", dummyParam, srcAlias, rAlias, tgtAlias);

        var pathSegmentExpression = pathSegmentVisitor.VisitAndReturnCypher(lambda.Body);

        return new SelectResolution(
            ProjectionType.PathSegmentComplex,
            ImmutableArray.Create(pathSegmentExpression),
            UpdatedAlias: null,
            ExitPathSegmentContext: false,
            DisableComplexPropertyLoading: true);
    }

    private static SelectResolution ResolveAnonymousTypeProjection(
        NewExpression newExpr,
        SelectContext context)
    {
        var logger = context.Logger;
        var queryContext = context.QueryContext;
        var projections = new List<string>();
        var alias = queryContext.Scope.CurrentAlias ?? GetContextualAlias(queryContext);

        for (int i = 0; i < newExpr.Arguments.Count; i++)
        {
            var arg = newExpr.Arguments[i];
            var member = newExpr.Members?[i];
            var projectionAlias = member?.Name ?? $"field{i}";
            var safeAlias = $"c_{projectionAlias}";

            // Handle PathSegment parameter projections
            if (arg is ParameterExpression paramExpr && IsPathSegmentType(paramExpr.Type))
            {
                var sourceAlias = queryContext.Scope.GetNumberedAlias("src");
                var relationshipAlias = queryContext.Scope.GetNumberedAlias("r");
                var targetAlias = queryContext.Scope.GetNumberedAlias("tgt");

                var pathSegmentProjection = $"{sourceAlias} AS {safeAlias}_{sourceAlias}, " +
                    $"{relationshipAlias} AS {safeAlias}_{relationshipAlias}, " +
                    $"{targetAlias} AS {safeAlias}_{targetAlias}";
                projections.Add(pathSegmentProjection);

                logger.LogDebug("Added PathSegment projection: {Projection}", pathSegmentProjection);
            }
            else if (arg is MemberExpression argMemberExpr && 
                     argMemberExpr.Expression is ParameterExpression paramExpr2)
            {
                // Check if this is an IGrouping parameter
                if (paramExpr2.Type.IsGenericType && 
                    paramExpr2.Type.GetGenericTypeDefinition().Name.Contains("IGrouping"))
                {
                    var visitor = CreateRegularExpressionVisitor(queryContext, logger);
                    var cypherExpr = visitor.VisitAndReturnCypher(arg);
                    projections.Add($"{cypherExpr} AS {safeAlias}");
                }
                else
                {
                    var propertyName = argMemberExpr.Member.Name;
                    projections.Add($"{alias}.{propertyName} AS {safeAlias}");
                }
            }
            else
            {
                bool involvesPathSegment = ContainsPathSegmentAccess(arg);
                var visitor = involvesPathSegment
                    ? CreatePathSegmentExpressionVisitor(null!, queryContext, logger)
                    : CreateRegularExpressionVisitor(queryContext, logger);
                var cypherExpr = visitor.VisitAndReturnCypher(arg);
                projections.Add($"{cypherExpr} AS {safeAlias}");
            }
        }

        logger.LogDebug("Added anonymous projection: {Count} fields", projections.Count);

        return new SelectResolution(
            ProjectionType.AnonymousType,
            projections.ToImmutableArray(),
            UpdatedAlias: null,
            ExitPathSegmentContext: false,
            DisableComplexPropertyLoading: true);
    }

    private static SelectResolution ResolveSimplePropertyProjection(
        MemberExpression memberExpr,
        LambdaExpression lambda,
        SelectContext context)
    {
        var logger = context.Logger;
        var queryContext = context.QueryContext;
        var propertyName = memberExpr.Member.Name;
        var parameterType = ((ParameterExpression)memberExpr.Expression!).Type;

        // Check if this is a PathSegment property projection
        bool isProjectingFromPathSegment = propertyName is "StartNode" or "EndNode" or "Relationship";
        bool isParameterPathSegmentType = parameterType.IsGenericType &&
            parameterType.GetGenericTypeDefinition() == typeof(IGraphPathSegment<,,>);
        bool isPathSegmentContext = isProjectingFromPathSegment && isParameterPathSegmentType;

        // Special handling for IRelationship projections
        if (typeof(IRelationship).IsAssignableFrom(parameterType) && propertyName == "Relationship")
        {
            var relAlias = queryContext.Scope.GetAliasForType(parameterType) ?? GetContextualAlias(queryContext);
            logger.LogDebug("Added full relationship entity projection: {Alias}", relAlias);

            return new SelectResolution(
                ProjectionType.SimpleProperty,
                ImmutableArray.Create(relAlias),
                UpdatedAlias: null,
                ExitPathSegmentContext: false,
                DisableComplexPropertyLoading: true);
        }

        // PathSegment context property projections
        if (isPathSegmentContext)
        {
            if (propertyName == "EndNode")
            {
                return ResolvePathSegmentEndNode(context);
            }
            if (propertyName == "StartNode")
            {
                return ResolvePathSegmentStartNode(context);
            }
            if (propertyName == "Relationship")
            {
                return ResolvePathSegmentRelationship(context);
            }
        }

        // Regular simple property projection
        if (!isPathSegmentContext)
        {
            var alias = DetermineAliasForChainedSelect(context.SourceExpression, queryContext, logger);
            var mappedPropertyName = MapPropertyNameForAge(propertyName);
            var projectionExpression = $"{alias}.{mappedPropertyName}";

            logger.LogDebug("Simple property projection: {Alias}.{Property} (mapped to {MappedProperty})",
                alias, propertyName, mappedPropertyName);

            return new SelectResolution(
                ProjectionType.SimpleProperty,
                ImmutableArray.Create(projectionExpression),
                UpdatedAlias: null,
                ExitPathSegmentContext: false,
                DisableComplexPropertyLoading: true);
        }

        // Skip projection in path segment context
        logger.LogDebug("Skipping property projection in path segment context: {Property}", propertyName);
        return new SelectResolution(
            ProjectionType.SimpleProperty,
            ImmutableArray<string>.Empty,
            UpdatedAlias: null,
            ExitPathSegmentContext: false,
            DisableComplexPropertyLoading: true);
    }

    private static SelectResolution ResolveComplexExpressionProjection(
        Expression body,
        SelectContext context)
    {
        var logger = context.Logger;
        var queryContext = context.QueryContext;

        bool containsPathSegments = ContainsPathSegmentsCall(context.SourceExpression);
        bool isPathSegmentContext = queryContext.Scope.IsPathSegmentContext || containsPathSegments;

        if (!isPathSegmentContext)
        {
            var visitor = CreateRegularExpressionVisitor(queryContext, logger);
            var selectExpression = visitor.VisitAndReturnCypher(body);
            logger.LogDebug("Complex expression projection: {Expression}", selectExpression);

            return new SelectResolution(
                ProjectionType.ComplexExpression,
                ImmutableArray.Create(selectExpression),
                UpdatedAlias: null,
                ExitPathSegmentContext: false,
                DisableComplexPropertyLoading: true);
        }

        logger.LogDebug("Skipping complex expression projection in path segment context");
        return new SelectResolution(
            ProjectionType.ComplexExpression,
            ImmutableArray<string>.Empty,
            UpdatedAlias: null,
            ExitPathSegmentContext: false,
            DisableComplexPropertyLoading: true);
    }

    // Helper methods

    private static string DetermineTargetAlias(CypherQueryContext context, ILogger logger)
    {
        var targetType = context.Scope.TraversalInfo?.TargetNodeType;
        
        if (targetType != null)
        {
            var aliasFromType = context.Scope.GetAliasForType(targetType);
            logger.LogDebug("GetAliasForType returned: {Alias} for type {Type}",
                aliasFromType, targetType.Name);
            
            if (aliasFromType != null)
                return aliasFromType;
        }

        var hopNumber = Math.Max(0, context.Scope.CurrentHop - 1);
        var targetAlias = context.Scope.GetNumberedAliasForHop("tgt", hopNumber);
        logger.LogDebug("Using numbered alias: {Alias}", targetAlias);
        
        return targetAlias;
    }

    private static (string Src, string Tgt, string Rel) DeterminePathSegmentAliases(
        int targetHop,
        CypherQueryContext context,
        ILogger logger)
    {
        bool isChainedPattern = context.Builder.HasMatchPatterns && targetHop > 0;

        if (isChainedPattern)
        {
            var srcAlias = context.Scope.GetNumberedAliasForHop("tgt", targetHop - 1);
            var tgtAlias = context.Scope.GetNumberedAliasForHop("tgt", targetHop);
            var rAlias = context.Scope.GetNumberedAliasForHop("r", targetHop);

            logger.LogDebug("Chained pattern: StartNode->{Src}, EndNode->{Tgt}, Rel->{Rel}",
                srcAlias, tgtAlias, rAlias);

            return (srcAlias, tgtAlias, rAlias);
        }

        var src = context.Scope.GetNumberedAliasForHop("src", targetHop);
        var tgt = context.Scope.GetNumberedAliasForHop("tgt", targetHop);
        var rel = context.Scope.GetNumberedAliasForHop("r", targetHop);

        logger.LogDebug("Independent pattern: src={Src}, tgt={Tgt}, r={Rel}", src, tgt, rel);

        return (src, tgt, rel);
    }

    private static string DetermineAliasForChainedSelect(
        Expression sourceExpression,
        CypherQueryContext context,
        ILogger logger)
    {
        // Check if the source expression is a Select that projects a PathSegment property
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
                    (sourcePropertyName is "StartNode" or "EndNode" or "Relationship") &&
                    sourceParamType.IsGenericType &&
                    sourceParamType.GetGenericTypeDefinition() == typeof(IGraphPathSegment<,,>);

                if (isSourcePathSegmentProjection)
                {
                    return sourcePropertyName switch
                    {
                        "StartNode" => GetSourceNodeAlias(context, logger),
                        "EndNode" => GetTargetNodeAlias(context, logger),
                        "Relationship" => GetRelationshipAlias(context, logger),
                        _ => context.Scope.CurrentAlias ?? GetContextualAlias(context)
                    };
                }
            }
        }

        return context.Scope.CurrentAlias ?? GetContextualAlias(context);
    }

    private static string GetSourceNodeAlias(CypherQueryContext context, ILogger logger)
    {
        var sourceType = context.Scope.TraversalInfo?.SourceNodeType;
        var alias = sourceType != null
            ? (context.Scope.GetAliasForType(sourceType) ?? context.Scope.GetNumberedAlias("src"))
            : context.Scope.GetNumberedAlias("src");
        logger.LogDebug("Chained Select after StartNode: using source alias {Alias}", alias);
        return alias;
    }

    private static string GetTargetNodeAlias(CypherQueryContext context, ILogger logger)
    {
        var targetType = context.Scope.TraversalInfo?.TargetNodeType;
        var alias = targetType != null
            ? (context.Scope.GetAliasForType(targetType) ?? context.Scope.GetNumberedAlias("tgt"))
            : context.Scope.GetNumberedAlias("tgt");
        logger.LogDebug("Chained Select after EndNode: using target alias {Alias}", alias);
        return alias;
    }

    private static string GetRelationshipAlias(CypherQueryContext context, ILogger logger)
    {
        var relationshipType = context.Scope.TraversalInfo?.RelationshipType;
        var alias = relationshipType != null
            ? (context.Scope.GetAliasForType(relationshipType) ?? context.Scope.GetNumberedAlias("r"))
            : context.Scope.GetNumberedAlias("r");
        logger.LogDebug("Chained Select after Relationship: using relationship alias {Alias}", alias);
        return alias;
    }

    private static AgeExpressionToCypherVisitor CreateRegularExpressionVisitor(
        CypherQueryContext context,
        ILogger logger)
    {
        var alias = context.Scope.CurrentAlias ?? GetContextualAlias(context);
        return new AgeExpressionToCypherVisitor(context.Builder, logger, alias);
    }

    private static AgeExpressionToCypherVisitor CreatePathSegmentExpressionVisitor(
        ParameterExpression? pathSegmentParameter,
        CypherQueryContext context,
        ILogger logger)
    {
        var hopNumber = Math.Max(0, context.Scope.CurrentHop - 1);
        var hopAliases = context.Scope.GetHopAliases(hopNumber);

        if (hopAliases.HasValue)
        {
            var (src, rel, tgt) = hopAliases.Value;
            return new AgeExpressionToCypherVisitor(context.Builder, logger, "ps",
                pathSegmentParameter!, src, rel, tgt);
        }

        var fallbackSrc = context.Scope.GetNumberedAliasForHop("src", hopNumber);
        var fallbackRel = context.Scope.GetNumberedAliasForHop("r", hopNumber);
        var fallbackTgt = context.Scope.GetNumberedAliasForHop("tgt", hopNumber);

        return new AgeExpressionToCypherVisitor(context.Builder, logger, "ps",
            pathSegmentParameter!, fallbackSrc, fallbackRel, fallbackTgt);
    }

    private static bool IsPathSegmentType(Type type)
    {
        return type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IGraphPathSegment<,,>);
    }

    private static bool ContainsPathSegmentsCall(Expression expression)
    {
        if (expression is MethodCallExpression methodCall)
        {
            if (methodCall.Method.Name is "PathSegments" or "PathSegmentsIncoming" or "PathSegmentsOutgoing")
                return true;

            foreach (var arg in methodCall.Arguments)
            {
                if (ContainsPathSegmentsCall(arg))
                    return true;
            }
        }

        return false;
    }

    private static bool ContainsPathSegmentAccess(Expression expression)
    {
        if (expression is MemberExpression memberExpr)
        {
            var memberName = memberExpr.Member.Name;
            if (memberName is "StartNode" or "EndNode" or "Relationship")
            {
                if (memberExpr.Expression is ParameterExpression paramExpr &&
                    IsPathSegmentType(paramExpr.Type))
                {
                    return true;
                }
            }

            if (memberExpr.Expression != null)
                return ContainsPathSegmentAccess(memberExpr.Expression);
        }

        if (expression is MethodCallExpression methodCallExpr)
        {
            foreach (var arg in methodCallExpr.Arguments)
            {
                if (ContainsPathSegmentAccess(arg))
                    return true;
            }
        }

        return false;
    }

    private static LambdaExpression? ExtractLambda(Expression expression)
    {
        return expression switch
        {
            UnaryExpression { NodeType: ExpressionType.Quote, Operand: LambdaExpression lambda } => lambda,
            LambdaExpression lambda => lambda,
            _ => null
        };
    }

    private static string GetContextualAlias(CypherQueryContext context)
    {
        if (context.Scope.CurrentHop > 0)
        {
            return context.Scope.GetNumberedAliasForHop("src", Math.Max(0, context.Scope.CurrentHop - 1));
        }

        return "src0";
    }

    private static string MapPropertyNameForAge(string csharpPropertyName)
    {
        return csharpPropertyName switch
        {
            "Id" => "user_id",
            _ => csharpPropertyName
        };
    }
}
