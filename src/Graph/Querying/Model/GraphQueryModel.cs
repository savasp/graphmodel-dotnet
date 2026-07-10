// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Querying;

/// <summary>
/// Describes the provider-independent meaning of a graph query.
/// </summary>
/// <remarks>
/// This model is the level-1 semantic intermediate representation: it captures what the query
/// asks for without choosing a provider-specific execution plan or query language.
/// </remarks>
public sealed record GraphQueryModel
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GraphQueryModel"/> record.
    /// </summary>
    /// <param name="root">The query root.</param>
    /// <param name="predicates">Predicates applied to the root scope.</param>
    /// <param name="traversal">Traversal steps applied after root predicates.</param>
    /// <param name="projection">The projection shape, or <see langword="null"/> for the current element.</param>
    /// <param name="ordering">Ordering keys applied to the current element.</param>
    /// <param name="paging">Skip/take paging information.</param>
    /// <param name="terminal">The terminal operation or terminal modifier for the query.</param>
    public GraphQueryModel(
        QueryRoot root,
        IReadOnlyList<PredicateFragment> predicates,
        IReadOnlyList<TraversalStep> traversal,
        ProjectionShape? projection,
        IReadOnlyList<OrderingKey> ordering,
        Paging paging,
        TerminalOperation terminal)
        : this(root, predicates, traversal, projection, ordering, paging, terminal, false, null, null, null)
    {
    }

    /// <summary>
    /// Initializes a complete provider-independent query model.
    /// </summary>
    /// <param name="root">The query root.</param>
    /// <param name="predicates">Predicates applied to the root scope.</param>
    /// <param name="traversal">Traversal steps applied after root predicates.</param>
    /// <param name="projection">The projection shape, or <see langword="null"/> for the current element.</param>
    /// <param name="ordering">Ordering keys applied to the current element.</param>
    /// <param name="paging">Skip/take paging information.</param>
    /// <param name="terminal">The terminal operation or terminal modifier for the query.</param>
    /// <param name="distinct">Whether the result projection is distinct.</param>
    /// <param name="terminalOperand">The operand carried by a terminal operation such as Contains.</param>
    /// <param name="pathShape">Graph-path materialization metadata, when the query returns paths.</param>
    /// <param name="join">The equijoin description, when present.</param>
    public GraphQueryModel(
        QueryRoot root,
        IReadOnlyList<PredicateFragment> predicates,
        IReadOnlyList<TraversalStep> traversal,
        ProjectionShape? projection,
        IReadOnlyList<OrderingKey> ordering,
        Paging paging,
        TerminalOperation terminal,
        bool distinct,
        object? terminalOperand,
        QueryPathShape? pathShape,
        JoinFragment? join)
        : this(
            root,
            predicates,
            traversal,
            projection,
            ordering,
            paging,
            terminal,
            distinct,
            terminalOperand,
            pathShape,
            join,
            searchFilter: null)
    {
    }

    /// <summary>
    /// Initializes a complete provider-independent query model.
    /// </summary>
    /// <param name="root">The query root.</param>
    /// <param name="predicates">Predicates applied to the root scope.</param>
    /// <param name="traversal">Traversal steps applied after root predicates.</param>
    /// <param name="projection">The projection shape, or <see langword="null"/> for the current element.</param>
    /// <param name="ordering">Ordering keys applied to the current element.</param>
    /// <param name="paging">Skip/take paging information.</param>
    /// <param name="terminal">The terminal operation or terminal modifier for the query.</param>
    /// <param name="distinct">Whether the result projection is distinct.</param>
    /// <param name="terminalOperand">The operand carried by a terminal operation such as Contains.</param>
    /// <param name="pathShape">Graph-path materialization metadata, when the query returns paths.</param>
    /// <param name="join">The equijoin description, when present.</param>
    /// <param name="searchFilter">A full-text search applied to the current query scope after traversal.</param>
    public GraphQueryModel(
        QueryRoot root,
        IReadOnlyList<PredicateFragment> predicates,
        IReadOnlyList<TraversalStep> traversal,
        ProjectionShape? projection,
        IReadOnlyList<OrderingKey> ordering,
        Paging paging,
        TerminalOperation terminal,
        bool distinct,
        object? terminalOperand,
        QueryPathShape? pathShape,
        JoinFragment? join,
        SearchRoot? searchFilter)
        : this(
            root,
            predicates,
            traversal,
            projection,
            ordering,
            paging,
            terminal,
            distinct,
            terminalOperand,
            pathShape,
            join,
            searchFilter,
            groupBy: null,
            selectMany: null,
            union: null)
    {
    }

    /// <summary>
    /// Initializes a complete provider-independent query model.
    /// </summary>
    /// <param name="root">The query root.</param>
    /// <param name="predicates">Predicates applied to the root scope.</param>
    /// <param name="traversal">Traversal steps applied after root predicates.</param>
    /// <param name="projection">The projection shape, or <see langword="null"/> for the current element.</param>
    /// <param name="ordering">Ordering keys applied to the current element.</param>
    /// <param name="paging">Skip/take paging information.</param>
    /// <param name="terminal">The terminal operation for the query.</param>
    /// <param name="distinct">Whether the result projection is distinct.</param>
    /// <param name="terminalOperand">The operand carried by a terminal operation such as Contains or ElementAt.</param>
    /// <param name="pathShape">Graph-path materialization metadata, when the query returns paths.</param>
    /// <param name="join">The equijoin description, when present.</param>
    /// <param name="searchFilter">A full-text search applied to the current query scope after traversal.</param>
    /// <param name="groupBy">The grouping description, when present.</param>
    /// <param name="selectMany">The flattening-projection description, when present.</param>
    /// <param name="union">The set-union description, when present.</param>
    public GraphQueryModel(
        QueryRoot root,
        IReadOnlyList<PredicateFragment> predicates,
        IReadOnlyList<TraversalStep> traversal,
        ProjectionShape? projection,
        IReadOnlyList<OrderingKey> ordering,
        Paging paging,
        TerminalOperation terminal,
        bool distinct,
        object? terminalOperand,
        QueryPathShape? pathShape,
        JoinFragment? join,
        SearchRoot? searchFilter,
        GroupByFragment? groupBy,
        SelectManyFragment? selectMany,
        UnionFragment? union)
    {
        Root = root ?? throw new ArgumentNullException(nameof(root));
        Predicates = QueryModelGuard.CopyRequiredList(predicates, nameof(predicates));
        Traversal = QueryModelGuard.CopyRequiredList(traversal, nameof(traversal));
        Projection = projection;
        Ordering = QueryModelGuard.CopyRequiredList(ordering, nameof(ordering));
        Paging = paging ?? throw new ArgumentNullException(nameof(paging));
        QueryModelGuard.RequireDefinedEnum(terminal, nameof(terminal));
        Terminal = terminal;
        Distinct = distinct;
        TerminalOperand = terminalOperand;
        PathShape = pathShape;
        Join = join;
        SearchFilter = searchFilter;
        GroupBy = groupBy;
        SelectMany = selectMany;
        Union = union;
    }

    /// <summary>
    /// Gets the query root.
    /// </summary>
    public QueryRoot Root { get; }

    /// <summary>
    /// Gets predicates applied to the root scope.
    /// </summary>
    public IReadOnlyList<PredicateFragment> Predicates { get; }

    /// <summary>
    /// Gets traversal steps applied after the root predicates.
    /// </summary>
    public IReadOnlyList<TraversalStep> Traversal { get; }

    /// <summary>
    /// Gets the projection shape, or <see langword="null"/> when the current element is returned.
    /// </summary>
    public ProjectionShape? Projection { get; }

    /// <summary>
    /// Gets ordering keys applied to the current element.
    /// </summary>
    public IReadOnlyList<OrderingKey> Ordering { get; }

    /// <summary>
    /// Gets skip/take paging information.
    /// </summary>
    public Paging Paging { get; }

    /// <summary>
    /// Gets the terminal operation or terminal modifier for the query.
    /// </summary>
    public TerminalOperation Terminal { get; }

    /// <summary>Gets a value indicating whether the result projection is distinct.</summary>
    public bool Distinct { get; }

    /// <summary>Gets the operand carried by a terminal operation such as Contains.</summary>
    public object? TerminalOperand { get; }

    /// <summary>Gets graph-path materialization metadata, when the query returns paths.</summary>
    public QueryPathShape? PathShape { get; }

    /// <summary>Gets the equijoin description, when present.</summary>
    public JoinFragment? Join { get; }

    /// <summary>Gets a full-text search applied to the current query scope after traversal.</summary>
    public SearchRoot? SearchFilter { get; }

    /// <summary>Gets the grouping description, when present.</summary>
    public GroupByFragment? GroupBy { get; }

    /// <summary>Gets the flattening-projection description, when present.</summary>
    public SelectManyFragment? SelectMany { get; }

    /// <summary>Gets the set-union description, when present.</summary>
    public UnionFragment? Union { get; }
}
