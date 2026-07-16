// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph;

using System.Linq.Expressions;

/// <summary>
/// Options that configure a graph traversal operator (depth range and direction). Built via the
/// fluent <see cref="Depth(int)"/>/<see cref="Depth(int, int)"/>/<see cref="Direction"/> methods
/// and passed as an options lambda to traversal operators, e.g.
/// <c>source.Traverse&lt;R, T&gt;(o =&gt; o.Depth(1, 3).Direction(GraphTraversalDirection.Incoming))</c>.
/// </summary>
public sealed class GraphTraversalOptions
{
    private readonly List<(Type RelationshipType, LambdaExpression Predicate)> relationshipPredicates = [];

    /// <summary>
    /// Gets the minimum traversal depth, or <see langword="null"/> if unspecified (defaults to 1).
    /// </summary>
    public int? MinDepth { get; private set; }

    /// <summary>
    /// Gets the maximum traversal depth, or <see langword="null"/> if unspecified (defaults to 1
    /// for single-hop operators, or unbounded for path operators).
    /// </summary>
    public int? MaxDepth { get; private set; }

    /// <summary>
    /// Gets the traversal direction, or <see langword="null"/> if unspecified (defaults to
    /// <see cref="GraphTraversalDirection.Outgoing"/>).
    /// </summary>
    public GraphTraversalDirection? TraversalDirection { get; private set; }

    /// <summary>
    /// Sets the maximum traversal depth, leaving the minimum at its default (1).
    /// </summary>
    /// <param name="maxDepth">The maximum depth to traverse. Must be non-negative.</param>
    /// <returns>This instance, for chaining.</returns>
    public GraphTraversalOptions Depth(int maxDepth)
    {
        if (maxDepth < 0)
            throw new ArgumentOutOfRangeException(nameof(maxDepth), "Depth must be non-negative.");

        MinDepth = null;
        MaxDepth = maxDepth;
        return this;
    }

    /// <summary>
    /// Sets the minimum and maximum traversal depth.
    /// </summary>
    /// <param name="minDepth">The minimum depth to traverse. Must be non-negative.</param>
    /// <param name="maxDepth">The maximum depth to traverse. Must be greater than or equal to <paramref name="minDepth"/>.</param>
    /// <returns>This instance, for chaining.</returns>
    public GraphTraversalOptions Depth(int minDepth, int maxDepth)
    {
        if (minDepth < 0)
            throw new ArgumentOutOfRangeException(nameof(minDepth), "Minimum depth must be non-negative.");

        if (maxDepth < minDepth)
            throw new ArgumentOutOfRangeException(nameof(maxDepth), "Maximum depth must be greater than or equal to minimum depth.");

        MinDepth = minDepth;
        MaxDepth = maxDepth;
        return this;
    }

    /// <summary>
    /// Sets the traversal direction.
    /// </summary>
    /// <param name="direction">The direction to traverse.</param>
    /// <returns>This instance, for chaining.</returns>
    public GraphTraversalOptions Direction(GraphTraversalDirection direction)
    {
        TraversalDirection = direction;
        return this;
    }

    /// <summary>
    /// Restricts expansion to relationships that satisfy <paramref name="predicate"/>. For a
    /// variable-length traversal every relationship in the path must satisfy the predicate.
    /// </summary>
    /// <typeparam name="TRel">The relationship type traversed by the configured operator.</typeparam>
    /// <param name="predicate">The predicate evaluated while expanding candidate paths.</param>
    /// <returns>This instance, for chaining.</returns>
    public GraphTraversalOptions WhereRelationship<TRel>(Expression<Func<TRel, bool>> predicate)
        where TRel : class, IRelationship
    {
        ArgumentNullException.ThrowIfNull(predicate);
        relationshipPredicates.Add((typeof(TRel), predicate));
        return this;
    }

    internal IReadOnlyList<LambdaExpression> GetRelationshipPredicates(Type relationshipType)
    {
        ArgumentNullException.ThrowIfNull(relationshipType);

        var mismatched = relationshipPredicates
            .FirstOrDefault(item => item.RelationshipType != relationshipType);
        if (mismatched.Predicate is not null)
        {
            throw new ArgumentException(
                $"The traversal is over '{relationshipType.FullName}', but its relationship predicate " +
                $"was declared for '{mismatched.RelationshipType.FullName}'.");
        }

        return relationshipPredicates.Select(item => item.Predicate).ToArray();
    }
}
