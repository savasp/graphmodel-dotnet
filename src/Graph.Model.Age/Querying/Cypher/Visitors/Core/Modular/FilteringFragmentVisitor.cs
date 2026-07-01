// Copyright 2025 Savas Parastatidis

namespace Cvoya.Graph.Model.Age.Querying.Cypher.Visitors.Core.Modular;

using System.Collections.Immutable;
using System.Linq.Expressions;
using Cvoya.Graph.Model;
using Cvoya.Graph.Model.Cypher.Querying.Cypher.Visitors.Core;
using Microsoft.Extensions.Logging;

/// <summary>
/// Specialized visitor for handling filtering and ordering operations.
/// </summary>
internal sealed class FilteringFragmentVisitor : FragmentEmittingVisitorBase
{
    public FilteringFragmentVisitor(CypherQueryContext context, ILogger logger)
        : base(context, logger)
    {
    }

    public Expression HandleWhere(MethodCallExpression node)
    {
        Logger.LogDebug("Processing WHERE clause");
        var sourceExpression = node.Arguments[0];
        var lambda = ExtractLambda(node.Arguments[1])
            ?? throw new InvalidOperationException("Failed to extract lambda expression from WHERE clause. " +
                "The predicate must be a lambda expression (e.g., p => p.Name == \"value\").");

        var parameter = lambda.Parameters.FirstOrDefault();
        AgeExpressionToCypherVisitor expressionVisitor;

        if (parameter != null && typeof(IGraphPathSegment).IsAssignableFrom(parameter.Type))
        {
            Logger.LogDebug("WHERE clause in PathSegment context, using numbered aliases");
            var hopNumber = Math.Max(0, Context.Scope.CurrentHop - 1);
            var sourceAlias = Context.Scope.GetNumberedAliasForHop("src", hopNumber);
            var relationshipAlias = Context.Scope.GetNumberedAliasForHop("r", hopNumber);
            var targetAlias = Context.Scope.GetNumberedAliasForHop("tgt", hopNumber);
            expressionVisitor = new AgeExpressionToCypherVisitor(Context, Logger, Context.Scope.CurrentAlias ?? "src0",
                parameter, sourceAlias, relationshipAlias, targetAlias);
        }
        else
        {
            expressionVisitor = CreateExpressionVisitor();
        }

        var whereCondition = expressionVisitor.VisitAndReturnCypher(lambda.Body);
        Logger.LogDebug("Resolved WHERE condition: {Condition}", whereCondition);

        var consumedAliases = ImmutableArray<string>.Empty;
        var currentAlias = Context.Scope.CurrentAlias ?? "src0";
        Context.AddFragment(new WhereFragment(whereCondition, consumedAliases, currentAlias));
        return sourceExpression;
    }

    public Expression HandleOrderBy(MethodCallExpression node, bool descending, bool isThenBy = false)
    {
        Logger.LogDebug("Processing ORDER BY clause");
        var sourceExpression = node.Arguments[0];
        var lambda = ExtractLambda(node.Arguments[1])
            ?? throw new InvalidOperationException("Failed to extract lambda expression from ORDER BY clause. " +
                "The key selector must be a lambda expression (e.g., p => p.Name).");

        var expressionVisitor = CreateExpressionVisitor();
        var orderExpression = expressionVisitor.VisitAndReturnCypher(lambda.Body);
        Logger.LogDebug("Resolved ORDER BY expression: {Expression}", orderExpression);

        // If ORDER BY resolves to just the node alias (e.g., OrderBy(name => name) after Select(p => p.Name)),
        // use the last projected expression instead. This is critical for DISTINCT queries where
        // PostgreSQL requires ORDER BY expressions to appear in the SELECT list.
        var currentAlias = Context.Scope.CurrentAlias ?? "src0";
        if (orderExpression == currentAlias && Context.Scope.LastProjectedExpression != null)
        {
            orderExpression = Context.Scope.LastProjectedExpression;
            Logger.LogDebug("Using last projected expression for ORDER BY: {Expression}", orderExpression);
        }

        Context.AddFragment(new OrderFragment(orderExpression, descending));
        return sourceExpression;
    }

    public Expression HandleThenBy(MethodCallExpression node, bool descending)
    {
        Logger.LogDebug("Processing THEN BY clause");
        return HandleOrderBy(node, descending, isThenBy: true);
    }

    public Expression HandleTake(MethodCallExpression node)
    {
        Logger.LogDebug("Processing TAKE/LIMIT clause");
        if (node.Arguments.Count >= 2 && node.Arguments[1] is ConstantExpression constant)
        {
            Context.AddFragment(new LimitFragment((int)constant.Value!));
        }
        return node.Arguments[0];
    }

    public Expression HandleSkip(MethodCallExpression node)
    {
        Logger.LogDebug("Processing SKIP clause");
        if (node.Arguments.Count >= 2 && node.Arguments[1] is ConstantExpression constant)
        {
            Context.AddFragment(new SkipFragment((int)constant.Value!));
        }
        return node.Arguments[0];
    }

    public Expression HandleDistinct(MethodCallExpression node)
    {
        Logger.LogDebug("Processing DISTINCT clause");
        Context.AddFragment(new DistinctFragment());
        return node.Arguments[0];
    }
}
