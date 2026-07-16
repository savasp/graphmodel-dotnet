// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph;

using System.Linq.Expressions;
using static ExtensionUtils;

/// <summary>Extension methods for filtering node queries by stored labels.</summary>
public static class GraphLabelExtensions
{
    /// <summary>Filters nodes to those carrying <paramref name="label"/>.</summary>
    /// <remarks>The label is treated as a value and is never interpolated as a query identifier.</remarks>
    public static IGraphQueryable<TSource> OfLabel<TSource>(
        this IGraphQueryable<TSource> source,
        string label)
        where TSource : class, INode
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        var method = GetGenericExtensionMethod(typeof(GraphLabelExtensions), nameof(OfLabel), 1, 2)
            .MakeGenericMethod(typeof(TSource));
        return source.Provider.CreateQuery<TSource>(Expression.Call(
            null,
            method,
            source.Expression,
            Expression.Constant(label)));
    }

    /// <summary>
    /// Filters nodes using any/all semantics over <paramref name="labels"/>. <see cref="GraphLabelMatch.Any"/>
    /// requires at least one requested label; <see cref="GraphLabelMatch.All"/> requires every
    /// requested label. An empty label list is an identity operation.
    /// </summary>
    public static IGraphQueryable<TSource> OfLabels<TSource>(
        this IGraphQueryable<TSource> source,
        GraphLabelMatch match,
        params string[] labels)
        where TSource : class, INode
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(labels);
        if (!Enum.IsDefined(match))
            throw new ArgumentOutOfRangeException(nameof(match));
        if (labels.Any(string.IsNullOrWhiteSpace))
            throw new ArgumentException("Labels cannot contain null, empty, or whitespace values.", nameof(labels));
        if (labels.Length == 0)
            return source;

        var method = GetGenericExtensionMethod(typeof(GraphLabelExtensions), nameof(OfLabels), 1, 3)
            .MakeGenericMethod(typeof(TSource));
        return source.Provider.CreateQuery<TSource>(Expression.Call(
            null,
            method,
            source.Expression,
            Expression.Constant(match),
            Expression.Constant(labels)));
    }
}
