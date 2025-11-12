using System;
using System.Linq.Expressions;
using Microsoft.Extensions.Logging;

namespace Cvoya.Graph.Model.Age.Querying.Cypher.Visitors.Core.Utilities;

/// <summary>
/// Utility class for resolving GroupBy expressions into Cypher GROUP BY clauses.
/// Handles parameter registration, expression resolution, and scope management.
/// </summary>
internal static class GroupByExpressionResolver
{
    /// <summary>
    /// Context required for resolving GroupBy expressions.
    /// </summary>
    internal record GroupByContext(
        LambdaExpression Lambda,
        CypherQueryContext QueryContext,
        ILogger Logger,
        Func<AgeExpressionToCypherVisitor> CreateExpressionVisitor,
        Func<string> GetContextualAlias,
        AliasResolutionResult? ExplicitAliasContext = null);

    /// <summary>
    /// Result of resolving a GroupBy expression.
    /// </summary>
    internal record GroupByResolution(
        string GroupByExpression,
        string GroupingAlias);

    /// <summary>
    /// Resolves a GroupBy lambda expression into a Cypher GROUP BY clause.
    /// </summary>
    /// <param name="context">Context containing the lambda and query state.</param>
    /// <returns>Resolution containing the GROUP BY expression and grouping alias.</returns>
    internal static GroupByResolution ResolveGroupBy(GroupByContext context)
    {
        var lambda = context.Lambda;
        var queryContext = context.QueryContext;
        var logger = context.Logger;

        // Get the current alias for the parameter
        var currentAlias = queryContext.Scope.CurrentAlias ?? context.GetContextualAlias();
        logger.LogDebug("GroupBy: Using alias '{Alias}' for parameter '{Parameter}'", 
            currentAlias, lambda.Parameters[0].Name);
        
        // Register the parameter type with the current alias
        if (lambda.Parameters.Count > 0)
        {
            var parameterType = lambda.Parameters[0].Type;
            queryContext.Scope.RegisterTypeAlias(parameterType, currentAlias);
            logger.LogDebug("GroupBy: Registered type {Type} with alias '{Alias}'", 
                parameterType.Name, currentAlias);
        }
        
        // Extract the grouping key expression (e.g., p => p.FirstName)
        var expressionVisitor = context.CreateExpressionVisitor();
        var groupByExpression = expressionVisitor.VisitAndReturnCypher(lambda.Body);
        
        // Store the GROUP BY expression in the scope for later use in Select
        queryContext.Scope.SetGroupByExpression(groupByExpression);
        logger.LogDebug("Set GROUP BY expression: {Expression}", groupByExpression);

        // Determine the grouping alias (used for fragments)
        var groupingAlias = queryContext.Scope.CurrentAlias ?? context.GetContextualAlias();

        return new GroupByResolution(groupByExpression, groupingAlias);
    }
}
