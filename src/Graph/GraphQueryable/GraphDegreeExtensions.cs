// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph;

/// <summary>
/// Projection-only extension methods for counting a node's relationships (its degree) by
/// relationship type and direction.
/// </summary>
public static class GraphDegreeExtensions
{
    /// <summary>
    /// Counts a node's relationships of type <typeparamref name="TRel"/> in the given
    /// <paramref name="direction"/> — the node's degree restricted to one relationship type.
    /// </summary>
    /// <typeparam name="TRel">The relationship type to count.</typeparam>
    /// <param name="node">The node whose relationships are counted.</param>
    /// <param name="direction">
    /// Which relationships to count relative to <paramref name="node"/>:
    /// <see cref="GraphTraversalDirection.Outgoing"/> counts relationships that start at the node,
    /// <see cref="GraphTraversalDirection.Incoming"/> counts relationships that end at the node, and
    /// <see cref="GraphTraversalDirection.Both"/> counts relationships in either direction.
    /// </param>
    /// <returns>The number of matching relationships.</returns>
    /// <remarks>
    /// <para>
    /// This method is a <b>translation marker</b>: it is only meaningful inside a graph query
    /// projection (a <c>Select</c> selector), where the provider recognizes the call and rewrites
    /// it into a native relationship-count subquery rather than executing this method body. In
    /// Cypher it lowers to a <c>COUNT { }</c> / <c>size((node)-[:REL]-&gt;())</c> pattern subquery
    /// gated on <see cref="GraphCapability.PatternSizeProjection"/>; the in-memory provider computes
    /// the count directly against the store snapshot.
    /// </para>
    /// <para>
    /// Example:
    /// <code>
    /// graph.Nodes&lt;Person&gt;().Select(p =&gt; new
    /// {
    ///     p.FirstName,
    ///     OutgoingKnows = p.CountRelationships&lt;Knows&gt;(GraphTraversalDirection.Outgoing),
    ///     IncomingKnows = p.CountRelationships&lt;Knows&gt;(GraphTraversalDirection.Incoming),
    ///     TotalKnows    = p.CountRelationships&lt;Knows&gt;(GraphTraversalDirection.Both),
    /// });
    /// </code>
    /// </para>
    /// <para>
    /// The <paramref name="direction"/> argument must be a compile-time constant so it can be
    /// resolved while translating the query.
    /// </para>
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// Always thrown if the method is invoked directly rather than translated by a provider inside a
    /// query projection.
    /// </exception>
    public static int CountRelationships<TRel>(
        this INode node,
        GraphTraversalDirection direction = GraphTraversalDirection.Outgoing)
        where TRel : IRelationship =>
        throw new InvalidOperationException(
            "CountRelationships is only valid inside a graph query projection; it is recognized and " +
            "translated by the provider and must not be invoked directly.");
}
