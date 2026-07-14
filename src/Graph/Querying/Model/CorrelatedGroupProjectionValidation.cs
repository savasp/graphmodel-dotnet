// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Querying;

using System.Linq.Expressions;

/// <summary>
/// Shared, provider-neutral boundary for the correlated grouped-projection shape (#120):
/// <c>PathSegments&lt;…&gt;().GroupBy(seg =&gt; seg.StartNode).Select(g =&gt; new { … })</c>. Each projected
/// member may only reference the group through the recognized correlated-collection grammar
/// (<c>Select</c>/<c>Where</c>/<c>OrderBy</c>/<c>OrderByDescending</c>/<c>Count</c>/<c>Average</c>/
/// <c>Sum</c>/<c>Min</c>/<c>Max</c>/nested <c>GroupBy</c>, optionally terminated by
/// <c>ToList</c>/<c>ToArray</c>/<c>AsEnumerable</c>) or read the group key. Any other operation over
/// the group (for example <c>First</c>, <c>Take</c>, <c>Skip</c>, <c>Distinct</c>, <c>ElementAt</c>)
/// cannot be lowered to a Cypher pattern comprehension or subquery.
/// </summary>
/// <remarks>
/// This is the single source of truth for that boundary so every provider rejects the same shapes
/// identically. The Cypher planner and the in-memory interpreter both consult it up-front, and its
/// accepted grammar exactly mirrors the planner's per-member lowering so no supported #120 shape is
/// rejected. The recognition is intentionally structural and provider-agnostic: it inspects only the
/// shared query model.
/// </remarks>
public static class CorrelatedGroupProjectionValidation
{
    // The Enumerable/Queryable operators the correlated-collection lowering can represent. Any other
    // method over the group escapes the grammar and cannot be planned.
    private static readonly HashSet<string> RecognizedGroupOperations = new(StringComparer.Ordinal)
    {
        nameof(Enumerable.Select),
        nameof(Enumerable.Where),
        nameof(Enumerable.OrderBy),
        nameof(Enumerable.OrderByDescending),
        nameof(Enumerable.GroupBy),
        nameof(Enumerable.Count),
        nameof(Enumerable.Average),
        nameof(Enumerable.Sum),
        nameof(Enumerable.Min),
        nameof(Enumerable.Max),
        nameof(Enumerable.ToList),
        nameof(Enumerable.ToArray),
        nameof(Enumerable.AsEnumerable),
    };

    /// <summary>
    /// Validates the correlated grouped-projection members of <paramref name="model"/> and returns an
    /// actionable reason for the first member that references the group outside the recognized grammar,
    /// or <see langword="null"/> when the projection is supported (or is not the correlated
    /// grouped-projection shape this validator governs).
    /// </summary>
    /// <param name="model">The provider-independent query model to inspect.</param>
    /// <returns>A reason string when an unsupported member is found; otherwise <see langword="null"/>.</returns>
    public static string? Validate(GraphQueryModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        if (model.GroupBy is not { GroupsByPathSegmentStartNode: true })
        {
            return null;
        }

        if (model.Projection?.Selector is not { } selector ||
            StripConvert(selector.Body) is not NewExpression projection ||
            projection.Members is null)
        {
            // Not the anonymous-type projection shape; leave rejection to the existing guards.
            return null;
        }

        var group = selector.Parameters[0];
        for (var index = 0; index < projection.Arguments.Count; index++)
        {
            if (!IsSupportedMember(projection.Arguments[index], group, out var operation))
            {
                var member = projection.Members[index].Name;
                return operation is { } method
                    ? $"projection member '{member}' applies '.{method}(...)' to the group, which is not a " +
                      "supported correlated-collection operation"
                    : $"projection member '{member}' references the group in a form that is not a supported " +
                      "collection operation or a group-key member access";
            }
        }

        return null;
    }

    /// <summary>
    /// Produces the stable, provider-neutral rejection message for a <paramref name="reason"/> returned
    /// by <see cref="Validate"/>. Both providers throw a
    /// <see cref="GraphQueryTranslationException"/> carrying this exact message so the failure is
    /// identical everywhere.
    /// </summary>
    /// <param name="reason">The reason returned by <see cref="Validate"/>.</param>
    /// <returns>The canonical rejection message.</returns>
    public static string BuildMessage(string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        return $"Cannot translate the correlated grouped projection: {reason}. Supported operations over " +
            "the group are Select, Where, OrderBy, OrderByDescending, Count, Average, Sum, Min, Max, and " +
            "nested grouping (optionally terminated by ToList/ToArray/AsEnumerable), or a projection of the " +
            "group key.";
    }

    private static bool IsSupportedMember(
        Expression argument,
        ParameterExpression group,
        out string? unsupportedOperation)
    {
        unsupportedOperation = null;

        var item = StripToList(StripConvert(argument));
        var current = item;
        while (current is MethodCallExpression call &&
            (call.Method.DeclaringType == typeof(Enumerable) || call.Method.DeclaringType == typeof(Queryable)))
        {
            if (!RecognizedGroupOperations.Contains(call.Method.Name))
            {
                unsupportedOperation = call.Method.Name;
                break;
            }

            current = call.Method.IsStatic ? call.Arguments[0] : call.Object!;
        }

        // A chain of recognized operations terminating at the group is a supported correlated collection.
        if (unsupportedOperation is null && current == group)
        {
            return true;
        }

        // Otherwise the member is supported only if it reads the group key: rewriting group.Key to a free
        // parameter must leave no residual reference to the group itself. The free parameter must carry
        // the key type (IGrouping's first type argument) so member accesses on it stay well-formed.
        var keyType = group.Type.IsGenericType ? group.Type.GetGenericArguments()[0] : group.Type;
        var keyParameter = Expression.Parameter(keyType, "key");
        var rewritten = new GroupKeyRewriter(group, keyParameter).Visit(item)!;
        return !ReferencesParameter(rewritten, group);
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

    private static Expression StripToList(Expression expression)
    {
        while (expression is MethodCallExpression
            {
                Method.Name: nameof(Enumerable.ToList) or nameof(Enumerable.ToArray) or nameof(Enumerable.AsEnumerable),
            } call &&
            (call.Method.DeclaringType == typeof(Enumerable) || call.Method.DeclaringType == typeof(Queryable)))
        {
            expression = call.Method.IsStatic ? call.Arguments[0] : call.Object!;
        }

        return expression;
    }

    private static bool ReferencesParameter(Expression body, ParameterExpression parameter)
    {
        var finder = new ParameterReferenceFinder(parameter);
        finder.Visit(body);
        return finder.Found;
    }

    private sealed class GroupKeyRewriter(
        ParameterExpression groupParameter,
        ParameterExpression keyParameter) : ExpressionVisitor
    {
        protected override Expression VisitMember(MemberExpression node)
        {
            if (node.Expression == groupParameter &&
                node.Member.Name == nameof(IGrouping<object, object>.Key))
            {
                return keyParameter;
            }

            return base.VisitMember(node);
        }
    }

    private sealed class ParameterReferenceFinder(ParameterExpression parameter) : ExpressionVisitor
    {
        public bool Found { get; private set; }

        protected override Expression VisitParameter(ParameterExpression node)
        {
            if (node == parameter)
            {
                Found = true;
            }

            return base.VisitParameter(node);
        }
    }
}
