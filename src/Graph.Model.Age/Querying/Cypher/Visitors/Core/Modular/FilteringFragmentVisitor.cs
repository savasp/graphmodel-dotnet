// Copyright 2025 Savas Parastatidis

namespace Cvoya.Graph.Model.Age.Querying.Cypher.Visitors.Core.Modular;

using System.Collections.Immutable;
using System.Linq.Expressions;
using Cvoya.Graph.Model.Cypher.Querying.Cypher.Visitors.Core;
using Microsoft.Extensions.Logging;

/// <summary>
/// Specialized visitor for handling filtering and ordering operations
/// (Where, OrderBy, ThenBy, OrderByDescending, ThenByDescending, Skip, Take, Distinct, Reverse).
/// Emits WhereFragment, OrderFragment, SkipFragment, LimitFragment, DistinctFragment, and ReverseOrderFragment.
/// </summary>
internal sealed class FilteringFragmentVisitor : FragmentEmittingVisitorBase
{
    public FilteringFragmentVisitor(CypherQueryContext context, ILogger logger)
        : base(context, logger)
    {
    }

    /// <summary>
    /// Processes a Where() operation and emits WhereFragment.
    /// </summary>
    public Expression HandleWhere(MethodCallExpression node)
    {
        Logger.LogDebug("Processing WHERE clause");

        var sourceExpression = node.Arguments[0];
        var lambda = ExtractLambda(node.Arguments[1]);

        if (lambda == null)
        {
            Logger.LogWarning("Where lambda could not be extracted");
            return sourceExpression;
        }

        // Check if lambda parameter is a PathSegment type
        var parameter = lambda.Parameters.FirstOrDefault();
        AgeExpressionToCypherVisitor expressionVisitor;
        
        if (parameter != null && typeof(IGraphPathSegment).IsAssignableFrom(parameter.Type))
        {
            // Path segment context - use numbered aliases
            // CurrentHop has already been incremented by PathSegments, so use CurrentHop - 1
            Logger.LogDebug("WHERE clause in PathSegment context, using numbered aliases");
            var hopNumber = Math.Max(0, Context.Scope.CurrentHop - 1);
            var sourceAlias = Context.Scope.GetNumberedAliasForHop("src", hopNumber);
            var relationshipAlias = Context.Scope.GetNumberedAliasForHop("r", hopNumber);
            var targetAlias = Context.Scope.GetNumberedAliasForHop("tgt", hopNumber);
            
            expressionVisitor = new AgeExpressionToCypherVisitor(
                Context.Builder,
                Logger,
                Context.Scope.CurrentAlias ?? "src0",
                parameter,
                sourceAlias,
                relationshipAlias,
                targetAlias);
        }
        else
        {
            expressionVisitor = CreateExpressionVisitor();
        }
        
        var whereCondition = expressionVisitor.VisitAndReturnCypher(lambda.Body);

        Context.Builder.AddWhere(whereCondition);
        Logger.LogDebug("Added WHERE condition: {Condition}", whereCondition);

        // Emit WhereFragment
        var consumedAliases = ImmutableArray<string>.Empty; // TODO: Track actual consumed aliases
        var currentAlias = Context.Scope.CurrentAlias ?? "src0";
        var fragment = new WhereFragment(whereCondition, consumedAliases, currentAlias);
        EmitFragment(fragment, "WhereFragment");

        return sourceExpression;
    }

    /// <summary>
    /// Processes OrderBy/ThenBy operations and emits OrderFragment.
    /// </summary>
    public Expression HandleOrderBy(MethodCallExpression node, bool isDescending, bool isThenBy)
    {
        var direction = isDescending ? "DESC" : "ASC";
        var operation = isThenBy ? "THEN BY" : "ORDER BY";
        Logger.LogDebug("Processing {Operation} {Direction}", operation, direction);

        var sourceExpression = node.Arguments[0];
        var lambda = ExtractLambda(node.Arguments[1]);

        if (lambda == null)
        {
            Logger.LogWarning("OrderBy lambda could not be extracted");
            return sourceExpression;
        }

        string orderExpression;
        
        // Special case: if the lambda body is just a parameter (e.g., OrderBy(x => x) after a projection),
        // use the last return clause as the order expression
        if (lambda.Body is ParameterExpression)
        {
            var lastReturn = Context.Builder.GetLastReturnClause();
            if (lastReturn != null)
            {
                // Extract the expression from the return clause (e.g., "src0.FirstName" from "src0.FirstName AS Name")
                var returnExpression = lastReturn.Contains(" AS ") 
                    ? lastReturn.Substring(0, lastReturn.IndexOf(" AS "))
                    : lastReturn;
                orderExpression = returnExpression;
                Logger.LogDebug("OrderBy on parameter after projection, using return expression: {Expression}", orderExpression);
            }
            else
            {
                // Fallback to using the expression visitor
                var expressionVisitor = CreateExpressionVisitor();
                orderExpression = expressionVisitor.VisitAndReturnCypher(lambda.Body);
            }
        }
        else
        {
            var expressionVisitor = CreateExpressionVisitor();
            orderExpression = expressionVisitor.VisitAndReturnCypher(lambda.Body);
        }

        Context.Builder.AddOrderBy(orderExpression, isDescending);
        Logger.LogDebug("Added {Operation}: {Expression} {Direction}", operation, orderExpression, direction);

        // Emit OrderFragment with current alias
        var fragment = new OrderFragment(orderExpression, isDescending, Context.Scope.CurrentAlias);
        EmitFragment(fragment, "OrderFragment");

        return sourceExpression;
    }

    /// <summary>
    /// Processes Skip() operation and emits SkipFragment.
    /// </summary>
    public Expression HandleSkip(MethodCallExpression node)
    {
        Logger.LogDebug("Processing SKIP clause");

        var sourceExpression = node.Arguments[0];

        if (node.Arguments[1] is ConstantExpression constantExpr && constantExpr.Value is int skipCount)
        {
            Context.Builder.SetSkip(skipCount);
            Logger.LogDebug("Added SKIP: {Count}", skipCount);

            // Emit SkipFragment with current alias
            var fragment = new SkipFragment(skipCount, Context.Scope.CurrentAlias);
            EmitFragment(fragment, "SkipFragment");
        }
        else
        {
            Logger.LogWarning("Skip count could not be extracted as constant integer");
        }

        return sourceExpression;
    }

    /// <summary>
    /// Processes Take() operation and emits LimitFragment.
    /// </summary>
    public Expression HandleTake(MethodCallExpression node)
    {
        Logger.LogDebug("Processing TAKE/LIMIT clause");

        var sourceExpression = node.Arguments[0];

        if (node.Arguments[1] is ConstantExpression constantExpr && constantExpr.Value is int takeCount)
        {
            Context.Builder.SetLimit(takeCount);
            Logger.LogDebug("Added LIMIT: {Count}", takeCount);

            // Emit LimitFragment with current alias
            var fragment = new LimitFragment(takeCount, Context.Scope.CurrentAlias);
            EmitFragment(fragment, "LimitFragment");
        }
        else
        {
            Logger.LogWarning("Take count could not be extracted as constant integer");
        }

        return sourceExpression;
    }

    /// <summary>
    /// Processes Distinct() operation and emits DistinctFragment.
    /// </summary>
    public Expression HandleDistinct(MethodCallExpression node)
    {
        Logger.LogDebug("Processing DISTINCT clause");

        var sourceExpression = node.Arguments[0];

        Context.Builder.SetDistinct(true);
        Logger.LogDebug("Added DISTINCT");

        // Emit DistinctFragment with current alias
        var fragment = new DistinctFragment(Context.Scope.CurrentAlias);
        EmitFragment(fragment, "DistinctFragment");

        return sourceExpression;
    }

    /// <summary>
    /// Processes Reverse() operation and emits ReverseOrderFragment.
    /// </summary>
    public Expression HandleReverse(MethodCallExpression node)
    {
        Logger.LogDebug("Processing REVERSE clause");

        var sourceExpression = node.Arguments[0];

        Context.Builder.ReverseOrderBy();
        Logger.LogDebug("Added REVERSE (reversed ORDER BY)");

        // Emit ReverseOrderFragment
        var fragment = new ReverseOrderFragment();
        EmitFragment(fragment, "ReverseOrderFragment");

        return sourceExpression;
    }
}
