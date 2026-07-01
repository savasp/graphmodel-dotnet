// Copyright 2025 Savas Parastatidis

namespace Cvoya.Graph.Model.Age.Querying.Cypher.Visitors.Core.Modular;

using System.Linq.Expressions;
using System.Collections.Immutable;
using Cvoya.Graph.Model.Cypher.Querying.Cypher.Visitors.Core;
using Microsoft.Extensions.Logging;

/// <summary>
/// Specialized visitor for handling aggregation operations (Count, Sum, Average, Min, Max).
/// </summary>
internal sealed class AggregationFragmentVisitor : FragmentEmittingVisitorBase
{
    public AggregationFragmentVisitor(CypherQueryContext context, ILogger logger)
        : base(context, logger)
    {
    }

    public Expression HandleCount(MethodCallExpression node)
    {
        Logger.LogDebug("Processing COUNT aggregation");

        var sourceExpression = node.Arguments[0];
        var hasWhereClause = node.Arguments.Count == 2;
        var alias = Context.Scope.CurrentAlias ?? "src0";

        // If there's a predicate, handle it as a WHERE clause
        if (hasWhereClause)
        {
            var lambda = ExtractLambda(node.Arguments[1])
                ?? throw new InvalidOperationException("Failed to extract lambda expression from Count predicate. " +
                    "The predicate must be a lambda expression (e.g., p => p.IsActive).");

            var expressionVisitor = CreateExpressionVisitor();
            var whereCondition = expressionVisitor.VisitAndReturnCypher(lambda.Body);
            var whereFragment = new WhereFragment(
                whereCondition,
                ImmutableArray.Create(alias),
                alias);
            Context.AddFragment(whereFragment);
            Logger.LogDebug("Emitted WHERE condition for Count predicate: {Condition}", whereCondition);
        }

        Context.AddFragment(new AggregationFragment("count", alias, IsScalar: true));
        Logger.LogDebug("Emitted COUNT aggregation for alias {Alias}", alias);

        return sourceExpression;
    }

    public Expression HandleAny(MethodCallExpression node)
    {
        Logger.LogDebug("Processing ANY aggregation");

        var sourceExpression = node.Arguments[0];
        var hasWhereClause = node.Arguments.Count == 2;
        var alias = Context.Scope.CurrentAlias ?? "src0";

        if (hasWhereClause)
        {
            var lambda = ExtractLambda(node.Arguments[1])
                ?? throw new InvalidOperationException("Failed to extract lambda expression from Any predicate. " +
                    "The predicate must be a lambda expression (e.g., p => p.IsActive).");

            var expressionVisitor = CreateExpressionVisitor();
            var whereCondition = expressionVisitor.VisitAndReturnCypher(lambda.Body);
            var whereFragment = new WhereFragment(
                whereCondition,
                ImmutableArray.Create(alias),
                alias);
            Context.AddFragment(whereFragment);
            Logger.LogDebug("Emitted WHERE condition for Any predicate: {Condition}", whereCondition);
        }

        Context.AddFragment(new AggregationFragment("any", alias, IsScalar: true));
        Logger.LogDebug("Emitted ANY aggregation for alias {Alias}", alias);

        return sourceExpression;
    }

    public Expression HandleAll(MethodCallExpression node)
    {
        Logger.LogDebug("Processing ALL aggregation");

        var sourceExpression = node.Arguments[0];
        var alias = Context.Scope.CurrentAlias ?? "src0";

        if (node.Arguments.Count == 2)
        {
            var lambda = ExtractLambda(node.Arguments[1])
                ?? throw new InvalidOperationException("Failed to extract lambda expression from All predicate. " +
                    "The predicate must be a lambda expression (e.g., p => p.IsActive).");

            if (lambda.Body is Expression condition)
            {
                var expressionVisitor = CreateExpressionVisitor();
                var conditionCypher = expressionVisitor.VisitAndReturnCypher(condition);

                // All() means: count where NOT condition = 0
                var whereFragment = new WhereFragment(
                    $"NOT ({conditionCypher})",
                    ImmutableArray.Create(alias),
                    alias);
                Context.AddFragment(whereFragment);
                Logger.LogDebug("Emitted WHERE condition for All predicate: NOT ({Condition})", conditionCypher);
            }
        }

        Context.AddFragment(new AggregationFragment("all", alias, IsScalar: true));
        Logger.LogDebug("Emitted ALL aggregation for alias {Alias}", alias);

        return sourceExpression;
    }

    public Expression HandleSum(MethodCallExpression node)
    {
        Logger.LogDebug("Processing SUM aggregation");
        Context.AddFragment(new AggregationFragment("sum", "*", IsScalar: true));
        return node.Arguments[0];
    }

    public Expression HandleAverage(MethodCallExpression node)
    {
        Logger.LogDebug("Processing AVG aggregation");
        Context.AddFragment(new AggregationFragment("avg", "*", IsScalar: true));
        return node.Arguments[0];
    }

    public Expression HandleMin(MethodCallExpression node)
    {
        Logger.LogDebug("Processing MIN aggregation");
        Context.AddFragment(new AggregationFragment("min", "*", IsScalar: true));
        return node.Arguments[0];
    }

    public Expression HandleMax(MethodCallExpression node)
    {
        Logger.LogDebug("Processing MAX aggregation");
        Context.AddFragment(new AggregationFragment("max", "*", IsScalar: true));
        return node.Arguments[0];
    }

    /// <summary>
    /// Processes Sum/Average/Min/Max operations and emits the corresponding fragment.
    /// Extracts the property selector lambda and uses it for the aggregation expression.
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
                Context.AddFragment(new AggregationFragment(functionName.ToLowerInvariant(), $"{alias}.{propertyName}", IsScalar: true));
                Logger.LogDebug("Emitted {Function} aggregation over property {Property}", functionName, $"{alias}.{propertyName}");
            }
            else if (lambda != null)
            {
                var expressionVisitor = CreateExpressionVisitor();
                var expression = expressionVisitor.VisitAndReturnCypher(lambda.Body);
                Context.AddFragment(new AggregationFragment(functionName.ToLowerInvariant(), expression, IsScalar: true));
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
}
