using System.Linq.Expressions;
using Microsoft.Extensions.Logging;
using Cvoya.Graph.Model.Age.Querying.Cypher.Builders;

namespace Cvoya.Graph.Model.Age.Querying.Cypher.Visitors.Core.Utilities;

/// <summary>
/// Helper class for resolving OrderBy expressions to Cypher expressions.
/// Handles special cases like parameter-only lambdas, projection tracking,
/// column alias mapping, and PathSegment context awareness.
/// </summary>
internal static class OrderByExpressionResolver
{
    /// <summary>
    /// Resolves an OrderBy lambda expression to a Cypher order expression string.
    /// </summary>
    /// <param name="lambda">The OrderBy lambda expression</param>
    /// <param name="context">The query context</param>
    /// <param name="logger">Logger for diagnostics</param>
    /// <param name="sourceExpression">The source expression for Select chain traversal</param>
    /// <param name="containsPathSegments">Whether the expression tree contains PathSegments calls</param>
    /// <param name="explicitAliasContext">Optional explicit alias context from previous operations. When provided, takes precedence over CurrentAlias.</param>
    /// <returns>The resolved Cypher order expression</returns>
    public static string ResolveOrderExpression(
        LambdaExpression lambda,
        CypherQueryContext context,
        ILogger logger,
        Expression sourceExpression,
        bool containsPathSegments,
        AliasResolutionResult? explicitAliasContext = null)
    {
        // Special case: if the OrderBy lambda is just a parameter (e.g., content => content),
        // it means we're ordering by a projected value. Use the tracked projection expression.
        if (lambda.Body is ParameterExpression)
        {
            return ResolveParameterOnlyExpression(lambda, context, logger, sourceExpression, containsPathSegments);
        }

        // Special handling: if the lambda body is a property access on the parameter (e.g., x => x.FirstName)
        // and we have RETURN clauses with column aliases, map to the alias instead
        if (lambda.Body is MemberExpression memberExpr &&
            memberExpr.Expression is ParameterExpression paramExpr &&
            paramExpr == lambda.Parameters[0] &&
            context.Builder.HasReturnClauses)
        {
            return ResolvePropertyAccessWithAliases(memberExpr, lambda, context, logger, containsPathSegments);
        }

        // Normal expression (not a special case), visit it
        return ResolveNormalExpression(lambda.Body, context, logger, containsPathSegments);
    }

    private static string ResolveParameterOnlyExpression(
        LambdaExpression lambda,
        CypherQueryContext context,
        ILogger logger,
        Expression sourceExpression,
        bool containsPathSegments)
    {
        // First, try to use the tracked last projected expression (most reliable)
        if (context.Scope.LastProjectedExpression != null)
        {
            logger.LogDebug("OrderBy on parameter resolved to tracked projection: {Expression}",
                context.Scope.LastProjectedExpression);
            return context.Scope.LastProjectedExpression;
        }

        // Fallback: search through the source chain to find a Select operation
        var selectMethod = FindSelectInChain(sourceExpression);
        if (selectMethod == null)
        {
            throw new InvalidOperationException(
                "Cannot order by parameter - no Select projection found in the chain");
        }

        // Extract the Select's projection lambda
        var selectLambda = ExtractLambda(selectMethod.Arguments[1]);
        if (selectLambda == null)
        {
            throw new InvalidOperationException(
                "Cannot order by parameter without a valid Select projection in the chain");
        }

        logger.LogDebug("OrderBy on projection parameter - extracting order expression from Select projection");

        // Visit the Select's projection body to get the Cypher expression
        var expressionVisitor = CreateExpressionVisitor(context, logger, containsPathSegments);
        var orderExpression = expressionVisitor.VisitAndReturnCypher(selectLambda.Body);

        logger.LogDebug("OrderBy using projection expression: {Expression}", orderExpression);
        return orderExpression;
    }

    private static string ResolvePropertyAccessWithAliases(
        MemberExpression memberExpr,
        LambdaExpression lambda,
        CypherQueryContext context,
        ILogger logger,
        bool containsPathSegments)
    {
        // Try to find the column alias for this property in the RETURN clauses
        var propertyName = memberExpr.Member.Name;
        var columnAlias = FindColumnAliasForProperty(propertyName, context, logger);

        if (columnAlias != null)
        {
            logger.LogDebug("OrderBy property access '{Property}' mapped to column alias: {Alias}",
                propertyName, columnAlias);
            return columnAlias;
        }

        // Fallback: visit normally
        logger.LogDebug("OrderBy property access '{Property}' not found in RETURN aliases, visiting expression",
            propertyName);
        return ResolveNormalExpression(lambda.Body, context, logger, containsPathSegments);
    }

    private static string ResolveNormalExpression(
        Expression expression,
        CypherQueryContext context,
        ILogger logger,
        bool containsPathSegments)
    {
        var expressionVisitor = CreateExpressionVisitor(context, logger, containsPathSegments);
        return expressionVisitor.VisitAndReturnCypher(expression);
    }

    private static AgeExpressionToCypherVisitor CreateExpressionVisitor(
        CypherQueryContext context,
        ILogger logger,
        bool containsPathSegments)
    {
        if (containsPathSegments || context.Scope.IsPathSegmentContext)
        {
            logger.LogDebug("ORDER BY in path segment context - using context-aware visitor");
            bool isChainedPattern = context.Builder.HasMatchPatterns && context.Scope.CurrentHop > 0;
            int hopNumber = isChainedPattern ? 0 : Math.Max(0, context.Scope.CurrentHop - 1);
            var sourceAlias = context.Scope.GetNumberedAliasForHop("src", hopNumber);
            return new AgeExpressionToCypherVisitor(context.Builder, logger, sourceAlias);
        }

        // Pass the query builder so parameters are added directly to it
        var alias = context.Scope.CurrentAlias ?? GetContextualAlias(context);
        logger.LogDebug("CreateExpressionVisitor: Using alias '{Alias}'", alias);
        return new AgeExpressionToCypherVisitor(context.Builder, logger, alias);
    }

    private static MethodCallExpression? FindSelectInChain(Expression expression)
    {
        var current = expression;

        // Walk up the chain to find the Select
        while (current is MethodCallExpression methodCall)
        {
            if (methodCall.Method.Name == "Select")
            {
                return methodCall;
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

        return null;
    }

    private static string? FindColumnAliasForProperty(string propertyName, CypherQueryContext context, ILogger logger)
    {
        var returnClauses = context.Builder.GetReturnClauses();

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
        // Simple fallback alias logic
        if (context.Scope.CurrentHop > 0)
        {
            return context.Scope.GetNumberedAliasForHop("src", Math.Max(0, context.Scope.CurrentHop - 1));
        }

        return "src0";
    }
}
