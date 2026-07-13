// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph;

/// <summary>
/// Optional provider capabilities. Names describe user-visible features or query constructs and
/// are stable across releases: members are only ever added, never renamed or removed, so a
/// provider's declared <see cref="CapabilitySet"/> keeps meaning what it meant when it was
/// written. Consumers include query-surface dialects (which use these members to describe what a
/// backing store can translate) and the compatibility test suite (which uses them to decide which
/// contract tests to skip for a provider that has not declared a given capability).
/// </summary>
public enum GraphCapability
{
    /// <summary>
    /// Server-side full-text search over indexed string properties (e.g. Neo4j's
    /// <c>db.index.fulltext</c>).
    /// </summary>
    FullTextSearch,

    /// <summary>
    /// Explicit, provider-managed transactions via <see cref="IGraphTransaction"/>.
    /// </summary>
    Transactions,

    /// <summary>
    /// Transactions nested within an already-open transaction (savepoint-style semantics where an
    /// inner transaction can commit or roll back independently of its enclosing one). Reserved for
    /// a future transaction API that exposes an enclosing transaction or savepoint.
    /// </summary>
    NestedTransactions,

    /// <summary>
    /// Cascading create/delete of complex-property subtrees (nested owned objects persisted as
    /// part of their owning node or relationship).
    /// </summary>
    ComplexPropertyCascade,

    /// <summary>
    /// Nested subquery execution equivalent to Cypher's <c>CALL {}</c>, used for constructs such
    /// as grouped aggregation over a correlated subquery.
    /// </summary>
    CallSubqueries,

    /// <summary>
    /// Projection of relationship-pattern sizes (e.g. Cypher's <c>size(&lt;pattern&gt;)</c>).
    /// </summary>
    PatternSizeProjection,

    /// <summary>
    /// Matching against multiple labels/types in a single pattern (e.g. Cypher's <c>:A|B</c>).
    /// </summary>
    MultiLabelMatch,

    /// <summary>
    /// Ordering results by an entire node or relationship variable, rather than by one of its
    /// properties.
    /// </summary>
    OrderByEntity,

    /// <summary>
    /// Shortest-path traversal queries. Reserved for future use: no query construct references it
    /// yet, so there is no user-drivable surface to certify.
    /// </summary>
    ShortestPath,

    /// <summary>
    /// Traversals where a relationship hop may be absent from the result without excluding the
    /// matched entity (optional/outer-join-style traversal). Backs optional complex-property
    /// navigation: a query that reads through a complex property some owners may lack lowers to an
    /// <c>OPTIONAL MATCH</c>, so owners without the property survive (with a null leaf) rather than
    /// being filtered out.
    /// </summary>
    OptionalTraversal
}
