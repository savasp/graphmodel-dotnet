// Copyright 2025 Savas Parastatidis

namespace Cvoya.Graph.Model.Age.Querying.Cypher.Visitors.Core.Modular;

using System.Collections.Immutable;
using System.Linq.Expressions;
using Cvoya.Graph.Model.Cypher.Querying.Cypher.Visitors.Core;
using Microsoft.Extensions.Logging;

/// <summary>
/// Specialized visitor for handling aggregation operations (Count, Sum, Average, Min, Max, Any, All).
/// Emits AggregationFragment instances and manages ORDER BY clearing for scalar aggregations.
/// </summary>
internal sealed class AggregationFragmentVisitor : FragmentEmittingVisitorBase
{
    public AggregationFragmentVisitor(CypherQueryContext context, ILogger logger)
        : base(context, logger)
    {
    }

    /// <summary>
    /// Processes a Count() operation and emits the corresponding fragment.
    /// </summary>
    public Expression HandleCount(MethodCallExpression node)
    {
        Logger.LogDebug("Processing COUNT aggregation");

        var sourceExpression = node.Arguments[0];
        var hasWhereClause = node.Arguments.Count == 2;

        // If there's a predicate, handle it as a WHERE clause
        if (hasWhereClause)
        {
            var lambda = ExtractLambda(node.Arguments[1]);
            if (lambda != null)
            {
                var expressionVisitor = CreateExpressionVisitor();
                var whereCondition = expressionVisitor.VisitAndReturnCypher(lambda.Body);
                Context.Builder.AddWhere(whereCondition);
                Logger.LogDebug("Added WHERE condition for Count predicate: {Condition}", whereCondition);
            }
        }

    // Aggregations should override any prior projections
    Context.Builder.ClearReturn();

    // Build COUNT expression
        var alias = Context.Scope.CurrentAlias ?? "src0";
        var countExpression = $"count({alias})";
        Context.Builder.AddReturn(countExpression);

        // Emit aggregation fragment
        EmitAggregationFragment("count", alias, isScalar: true);

        // Clear ORDER BY for scalar aggregations
        Context.Builder.ClearOrderBy();

        return sourceExpression;
    }

    /// <summary>
    /// Processes an Any() operation and emits the corresponding fragment.
    /// </summary>
    public Expression HandleAny(MethodCallExpression node)
    {
        Logger.LogDebug("Processing ANY aggregation");

        var sourceExpression = node.Arguments[0];
        var hasWhereClause = node.Arguments.Count == 2;

        if (hasWhereClause)
        {
            var lambda = ExtractLambda(node.Arguments[1]);
            if (lambda != null)
            {
                var expressionVisitor = CreateExpressionVisitor();
                var whereCondition = expressionVisitor.VisitAndReturnCypher(lambda.Body);
                Context.Builder.AddWhere(whereCondition);
            }
        }

    // Aggregations should override any prior projections
    Context.Builder.ClearReturn();

    var alias = Context.Scope.CurrentAlias ?? "src0";
        var anyExpression = $"count({alias}) > 0";
        Context.Builder.AddReturn(anyExpression);

        EmitAggregationFragment("any", alias, isScalar: true);
        Context.Builder.ClearOrderBy();

        return sourceExpression;
    }

    /// <summary>
    /// Processes an All() operation and emits the corresponding fragment.
    /// </summary>
    public Expression HandleAll(MethodCallExpression node)
    {
        Logger.LogDebug("Processing ALL aggregation");

        var sourceExpression = node.Arguments[0];

        if (node.Arguments.Count == 2)
        {
            var lambda = ExtractLambda(node.Arguments[1]);
            if (lambda?.Body is Expression condition)
            {
                var expressionVisitor = CreateExpressionVisitor();
                var conditionCypher = expressionVisitor.VisitAndReturnCypher(condition);
                
                // All() means: count where NOT condition = 0
                Context.Builder.AddWhere($"NOT ({conditionCypher})");
            }
        }

    // Aggregations should override any prior projections
    Context.Builder.ClearReturn();

    var alias = Context.Scope.CurrentAlias ?? "src0";
        var allExpression = $"count({alias}) = 0";
        Context.Builder.AddReturn(allExpression);

        EmitAggregationFragment("all", alias, isScalar: true);
        Context.Builder.ClearOrderBy();

        return sourceExpression;
    }

    /// <summary>
    /// Processes Sum/Average/Min/Max operations and emits the corresponding fragment.
    /// </summary>
    public Expression HandleAggregationFunction(MethodCallExpression node, string functionName)
    {
        Logger.LogDebug("Processing {Function} aggregation", functionName);

    var sourceExpression = node.Arguments[0];
    Context.Builder.ClearOrderBy();

    // Aggregations should override any prior projections
    Context.Builder.ClearReturn();

        // Handle both 2-arg (selector) and 3-arg (selector + cancellationToken) cases
        // SumAsync(p => p.Age) has 2 args: [source, selector]
        // SumAsync(p => p.Age, cancellationToken) has 3 args: [source, selector, cancellationToken]
        if (node.Arguments.Count >= 2 && node.Arguments.Count <= 3)
        {
            var lambda = ExtractLambda(node.Arguments[1]);
            if (lambda?.Body is MemberExpression memberExpr)
            {
                var alias = Context.Scope.CurrentAlias ?? "src0";
                var propertyName = memberExpr.Member.Name;
                var aggregationExpression = $"{functionName.ToLowerInvariant()}({alias}.{propertyName})";
                Context.Builder.AddReturn(aggregationExpression);

                EmitAggregationFragment(functionName.ToLowerInvariant(), $"{alias}.{propertyName}", isScalar: true);
            }
            else if (lambda != null)
            {
                var expressionVisitor = CreateExpressionVisitor();
                var expression = expressionVisitor.VisitAndReturnCypher(lambda.Body);
                var aggregationExpression = $"{functionName.ToLowerInvariant()}({expression})";
                Context.Builder.AddReturn(aggregationExpression);

                EmitAggregationFragment(functionName.ToLowerInvariant(), expression, isScalar: true);
            }
            else
            {
                // Lambda extraction failed - this is an error
                throw new NotSupportedException(
                    $"{functionName} aggregation requires a property selector lambda. " +
                    $"Example: .{functionName}Async(p => p.PropertyName)");
            }
        }
        else
        {
            // No selector provided - this is invalid for node/relationship aggregations
            throw new NotSupportedException(
                $"{functionName} aggregation requires a property selector. " +
                $"Example: .{functionName}Async(p => p.PropertyName). " +
                $"You cannot aggregate an entire node or relationship.");
        }

        return sourceExpression;
    }

    /// <summary>
    /// Emits an AggregationFragment with the specified aggregation type and expression.
    /// </summary>
    private void EmitAggregationFragment(string aggregationType, string expression, bool isScalar)
    {
        var fragment = new AggregationFragment(aggregationType, expression, isScalar);
        EmitFragment(fragment, "AggregationFragment");
    }
}
