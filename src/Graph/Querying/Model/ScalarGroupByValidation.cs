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

        if (!IsScalarKeyType(groupBy.KeySelector.ReturnType))
        {
            return "the grouping key must be a scalar value; grouping by an entity, node, or composite key is not supported";
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
}
