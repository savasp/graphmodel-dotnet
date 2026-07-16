// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Querying;

using System.Linq.Expressions;
using System.Reflection;

/// <summary>
/// Shared structural validation for scalar-key grouping (grouping a node set by a scalar key and
/// projecting per-group aggregates). Both the Cypher planner and the in-memory interpreter route
/// non-path-segment groupings here so that a query rejected by one provider is rejected by the
/// other with the same reason, keeping the supported surface identical across providers.
/// </summary>
/// <remarks>
/// This covers only the model-level (structural) constraints that both providers can observe. The
/// set of per-group projection <em>shapes</em> a provider can lower (which aggregate operations it
/// supports) is a separate, provider-specific planning concern.
/// </remarks>
public static class ScalarGroupByValidation
{
    /// <summary>
    /// Describes why the scalar grouping carried by <paramref name="model"/> cannot be translated,
    /// or <see langword="null"/> when it is structurally supported. Only call this for a model whose
    /// <see cref="GraphQueryModel.GroupBy"/> is not the path-segment start-node shape
    /// (<see cref="GroupByFragment.GroupsByPathSegmentStartNode"/>).
    /// </summary>
    /// <param name="model">The query model whose grouping to inspect.</param>
    /// <returns>An actionable reason string, or <see langword="null"/> when supported.</returns>
    public static string? DescribeUnsupported(GraphQueryModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        var groupBy = model.GroupBy
            ?? throw new ArgumentException("The model does not carry a grouping.", nameof(model));

        if (model.Root is not NodeRoot and not DynamicRoot)
        {
            return "search and relationship roots are not supported as a grouping source";
        }

        if (model.SearchFilter is not null || model.Join is not null || model.SelectMany is not null ||
            model.Union is not null || model.PathShape is not null)
        {
            return "search, joins, unions, and additional query fragments cannot be combined with grouping";
        }

        if (!IsScalarKeyType(groupBy.KeySelector.ReturnType))
        {
            return "the grouping key must be a scalar value; grouping by an entity, node, or composite key is not supported";
        }

        if (model.Traversal.Count > 0 || ContainsComplexPropertyNavigation(groupBy.KeySelector))
        {
            return "grouping after a traversal is only supported for the path-segment start-node collection shape; " +
                "scalar grouping must start from a node root with an optional Where";
        }

        var sourceType = model.Root switch
        {
            NodeRoot node => node.ElementType,
            DynamicRoot { ElementType: { } elementType } => elementType,
            DynamicRoot => typeof(DynamicNode),
            _ => null,
        };
        if (sourceType is not null && model.Predicates.Any(predicate =>
            predicate.Predicate.Parameters.Count != 1 || predicate.Predicate.Parameters[0].Type != sourceType))
        {
            return "filtering a grouped result is not supported; apply Where before GroupBy";
        }

        if (groupBy.ElementSelector is not null)
        {
            return "GroupBy element-selector overloads are not supported";
        }

        if (model.Distinct || model.Ordering.Count > 0 || model.Paging.Skip is not null ||
            model.Paging.Take is not null || model.PostPaging is not null)
        {
            return "Distinct, ordering, and paging over a grouped result are not supported; materialize the grouped query first";
        }

        if (groupBy.ResultSelector is null && model.Projection?.Selector is null)
        {
            return "a group projection (a Select over the grouping or a result-selector overload) is required";
        }

        if (groupBy.ResultSelector is not null && model.Projection?.Selector is not null)
        {
            return "a Select after a result-selector GroupBy is not supported; use a single projection form";
        }

        if (ValidateProjectionShape(groupBy, model.Projection?.Selector) is { } projectionReason)
        {
            return projectionReason;
        }

        return null;
    }

    /// <summary>
    /// Formats a stable, provider-neutral message for a rejection <paramref name="reason"/> from
    /// <see cref="DescribeUnsupported(GraphQueryModel)"/>.
    /// </summary>
    /// <param name="reason">The reason returned by <see cref="DescribeUnsupported(GraphQueryModel)"/>.</param>
    /// <returns>The full, user-facing message.</returns>
    public static string BuildMessage(string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        return $"GroupBy is not supported for this query shape: {reason}.";
    }

    private static bool IsScalarKeyType(Type type)
    {
        var underlying = Nullable.GetUnderlyingType(type) ?? type;
        return underlying.IsPrimitive
            || underlying.IsEnum
            || underlying == typeof(string)
            || underlying == typeof(decimal)
            || underlying == typeof(DateTime)
            || underlying == typeof(DateTimeOffset)
            || underlying == typeof(Guid)
            || underlying == typeof(DateOnly)
            || underlying == typeof(TimeOnly)
            || underlying == typeof(TimeSpan);
    }

    private static bool ContainsComplexPropertyNavigation(LambdaExpression selector)
    {
        var finder = new ComplexPropertyNavigationFinder();
        finder.Visit(selector.Body);
        return finder.Found;
    }

    private static string? ValidateProjectionShape(GroupByFragment groupBy, LambdaExpression? selector)
    {
        ParameterExpression? keyParameter;
        ParameterExpression groupParameter;
        Expression body;
        if (groupBy.ResultSelector is { } resultSelector)
        {
            keyParameter = resultSelector.Parameters[0];
            groupParameter = resultSelector.Parameters[1];
            body = StripConvert(resultSelector.Body);
        }
        else
        {
            if (selector is null)
            {
                return "a group projection (a Select over the grouping or a result-selector overload) is required";
            }

            keyParameter = null;
            groupParameter = selector.Parameters[0];
            body = StripConvert(selector.Body);
        }

        IReadOnlyList<Expression> items;
        if (body is NewExpression { Members: not null } projection)
        {
            items = projection.Arguments;
        }
        else if (body is NewExpression or MemberInitExpression or ListInitExpression or NewArrayExpression)
        {
            return "the group projection must be a constructor projection with named members, a key expression, " +
                "or a supported aggregate";
        }
        else
        {
            items = [body];
        }

        foreach (var item in items)
        {
            if (ValidateProjectionItem(item, keyParameter, groupParameter) is { } reason)
            {
                return reason;
            }
        }

        return null;
    }

    private static string? ValidateProjectionItem(
        Expression expression,
        ParameterExpression? keyParameter,
        ParameterExpression groupParameter)
    {
        var item = StripConvert(expression);
        var current = item;
        var hasSelector = false;
        var hasAggregate = false;
        while (current is MethodCallExpression call &&
            (call.Method.DeclaringType == typeof(Enumerable) || call.Method.DeclaringType == typeof(Queryable)))
        {
            switch (call.Method.Name)
            {
                case nameof(Enumerable.Select):
                    if (hasSelector)
                    {
                        return "multiple Select operations over a scalar group are not supported";
                    }

                    hasSelector = true;
                    break;
                case nameof(Queryable.AsQueryable):
                    // IGrouping implements IEnumerable, so AsQueryable is the adapter required to
                    // express the Queryable aggregate overloads inside a group projection.
                    break;
                case nameof(Enumerable.Count) or nameof(Enumerable.LongCount):
                    if (hasAggregate)
                    {
                        return "multiple terminal aggregates over a scalar group are not supported";
                    }

                    if (call.Arguments.Count > 1)
                    {
                        return $"{call.Method.Name}(predicate) over a scalar group is not supported; " +
                            "filter before GroupBy";
                    }

                    hasAggregate = true;
                    break;
                case nameof(Enumerable.Average) or nameof(Enumerable.Sum) or
                    nameof(Enumerable.Min) or nameof(Enumerable.Max):
                    if (hasAggregate)
                    {
                        return "multiple terminal aggregates over a scalar group are not supported";
                    }

                    hasAggregate = true;
                    break;
                case nameof(Enumerable.Where):
                    return "filtering inside a scalar group is not supported; filter before GroupBy";
                case nameof(Enumerable.ToList) or nameof(Enumerable.ToArray) or nameof(Enumerable.AsEnumerable):
                    return "projecting a scalar group as a collection is not supported; project only the key and aggregates";
                default:
                    return $"operation '{call.Method.Name}' over a scalar group is not supported";
            }

            if (call.Arguments.Skip(1).Any(argument => ReferencesParameter(argument, groupParameter)))
            {
                return $"operation '{call.Method.Name}' captures the outer scalar group in its selector";
            }

            current = call.Method.IsStatic ? call.Arguments[0] : call.Object!;
        }

        if (current == groupParameter)
        {
            return hasAggregate
                ? null
                : "the scalar group may only be referenced through Key, Count, LongCount, Sum, Average, Min, or Max";
        }

        var rewritten = keyParameter is null
            ? new ScalarGroupKeyRewriter(groupParameter).Visit(item)!
            : item;
        return ReferencesParameter(rewritten, groupParameter)
            ? "the projection references the scalar group outside Key, Count, LongCount, Sum, Average, Min, or Max"
            : null;
    }

    private static Expression StripConvert(Expression expression)
    {
        while (expression is UnaryExpression
            { NodeType: ExpressionType.Convert or ExpressionType.ConvertChecked } unary)
        {
            expression = unary.Operand;
        }

        return expression;
    }

    private static bool ReferencesParameter(Expression expression, ParameterExpression parameter)
    {
        var finder = new ParameterReferenceFinder(parameter);
        finder.Visit(expression);
        return finder.Found;
    }

    private sealed class ComplexPropertyNavigationFinder : ExpressionVisitor
    {
        public bool Found { get; private set; }

        protected override Expression VisitMember(MemberExpression node)
        {
            if (node.Member is PropertyInfo property && GraphDataModel.IsComplex(property.PropertyType))
            {
                Found = true;
                return node;
            }

            return base.VisitMember(node);
        }
    }

    private sealed class ScalarGroupKeyRewriter(ParameterExpression groupParameter) : ExpressionVisitor
    {
        protected override Expression VisitMember(MemberExpression node)
        {
            if (node.Expression == groupParameter && node.Member.Name == nameof(IGrouping<object, object>.Key))
            {
                return Expression.Default(node.Type);
            }

            return base.VisitMember(node);
        }
    }

    private sealed class ParameterReferenceFinder(ParameterExpression parameter) : ExpressionVisitor
    {
        public bool Found { get; private set; }

        protected override Expression VisitParameter(ParameterExpression node)
        {
            Found |= node == parameter;
            return base.VisitParameter(node);
        }
    }
}
