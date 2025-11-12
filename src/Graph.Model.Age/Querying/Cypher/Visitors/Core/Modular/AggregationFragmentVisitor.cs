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
        var alias = Context.Scope.CurrentAlias ?? "src0";

        // If there's a predicate, handle it as a WHERE clause
        if (hasWhereClause)
        {
            var lambda = ExtractLambda(node.Arguments[1]);
            if (lambda != null)
            {
                var expressionVisitor = CreateExpressionVisitor();
                var whereCondition = expressionVisitor.VisitAndReturnCypher(lambda.Body);
                var whereFragment = new WhereFragment(
                    whereCondition,
                    ImmutableArray.Create(alias),
                    alias);
                EmitFragment(whereFragment, "WhereFragment");
                Logger.LogDebug("Emitted WHERE condition for Count predicate: {Condition}", whereCondition);
            }
        }

        var countExpression = $"count({alias})";
        EmitAggregationFragment("count", alias, isScalar: true);

    Logger.LogDebug("Emitted COUNT aggregation for alias {Alias}", alias);

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
        var alias = Context.Scope.CurrentAlias ?? "src0";

        if (hasWhereClause)
        {
            var lambda = ExtractLambda(node.Arguments[1]);
            if (lambda != null)
            {
                var expressionVisitor = CreateExpressionVisitor();
                var whereCondition = expressionVisitor.VisitAndReturnCypher(lambda.Body);
                var whereFragment = new WhereFragment(
                    whereCondition,
                    ImmutableArray.Create(alias),
                    alias);
                EmitFragment(whereFragment, "WhereFragment");
                Logger.LogDebug("Emitted WHERE condition for Any predicate: {Condition}", whereCondition);
            }
        }

        var anyExpression = $"count({alias}) > 0";
        EmitAggregationFragment("any", alias, isScalar: true);
        Logger.LogDebug("Emitted ANY aggregation for alias {Alias}", alias);

        return sourceExpression;
    }

    /// <summary>
    /// Processes an All() operation and emits the corresponding fragment.
    /// </summary>
    public Expression HandleAll(MethodCallExpression node)
    {
        Logger.LogDebug("Processing ALL aggregation");

        var sourceExpression = node.Arguments[0];
        var alias = Context.Scope.CurrentAlias ?? "src0";

        if (node.Arguments.Count == 2)
        {
            var lambda = ExtractLambda(node.Arguments[1]);
            if (lambda?.Body is Expression condition)
            {
                var expressionVisitor = CreateExpressionVisitor();
                var conditionCypher = expressionVisitor.VisitAndReturnCypher(condition);
                
                // All() means: count where NOT condition = 0
                var whereFragment = new WhereFragment(
                    $"NOT ({conditionCypher})",
                    ImmutableArray.Create(alias),
                    alias);
                EmitFragment(whereFragment, "WhereFragment");
                Logger.LogDebug("Emitted WHERE condition for All predicate: NOT ({Condition})", conditionCypher);
            }
        }

        var allExpression = $"count({alias}) = 0";
        EmitAggregationFragment("all", alias, isScalar: true);
        Logger.LogDebug("Emitted ALL aggregation for alias {Alias}", alias);

        return sourceExpression;
    }

    /// <summary>
    /// Processes Sum/Average/Min/Max operations and emits the corresponding fragment.
    /// </summary>
    public Expression HandleAggregationFunction(MethodCallExpression node, string functionName)
    {
        Logger.LogDebug("Processing {Function} aggregation", functionName);

        var sourceExpression = node.Arguments[0];

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
                EmitAggregationFragment(functionName.ToLowerInvariant(), $"{alias}.{propertyName}", isScalar: true);
                Logger.LogDebug("Emitted {Function} aggregation over property {Property}", functionName, $"{alias}.{propertyName}");
            }
            else if (lambda != null)
            {
                var expressionVisitor = CreateExpressionVisitor();
                var expression = expressionVisitor.VisitAndReturnCypher(lambda.Body);
                EmitAggregationFragment(functionName.ToLowerInvariant(), expression, isScalar: true);
                Logger.LogDebug("Emitted {Function} aggregation over expression {Expression}", functionName, expression);
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
