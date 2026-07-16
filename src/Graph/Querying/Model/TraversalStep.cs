// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Querying;

/// <summary>
/// Describes one graph traversal step.
/// </summary>
public sealed record TraversalStep
{
    private IReadOnlyList<PredicateFragment> targetPredicates = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="TraversalStep"/> record.
    /// </summary>
    /// <param name="relationshipType">The relationship type label to traverse, or <see langword="null"/> for any relationship type.</param>
    /// <param name="direction">The traversal direction.</param>
    /// <param name="depth">The traversal depth range.</param>
    /// <param name="relationshipPredicates">Predicates applied to relationships traversed by the step.</param>
    /// <param name="targetType">The target node type, if known.</param>
    public TraversalStep(
        string? relationshipType,
        GraphTraversalDirection direction,
        DepthRange depth,
        IReadOnlyList<PredicateFragment> relationshipPredicates,
        Type? targetType)
        : this(relationshipType, direction, depth, relationshipPredicates, targetType, null, false)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TraversalStep"/> record.
    /// </summary>
    /// <param name="relationshipType">The relationship type label to traverse, or <see langword="null"/> for any relationship type.</param>
    /// <param name="direction">The traversal direction.</param>
    /// <param name="depth">The traversal depth range.</param>
    /// <param name="relationshipPredicates">Predicates applied to relationships traversed by the step.</param>
    /// <param name="targetType">The target node or value-node type, if known.</param>
    /// <param name="relationshipClrType">The CLR relationship type, if known.</param>
    public TraversalStep(
        string? relationshipType,
        GraphTraversalDirection direction,
        DepthRange depth,
        IReadOnlyList<PredicateFragment> relationshipPredicates,
        Type? targetType,
        Type? relationshipClrType)
        : this(relationshipType, direction, depth, relationshipPredicates, targetType, relationshipClrType, false)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TraversalStep"/> record.
    /// </summary>
    /// <param name="relationshipType">The relationship type label to traverse, or <see langword="null"/> for any relationship type.</param>
    /// <param name="direction">The traversal direction.</param>
    /// <param name="depth">The traversal depth range.</param>
    /// <param name="relationshipPredicates">Predicates applied to relationships traversed by the step.</param>
    /// <param name="targetType">The target node or value-node type, if known.</param>
    /// <param name="relationshipClrType">The CLR relationship type, if known.</param>
    /// <param name="isComplexPropertyTraversal">Whether the step came from a complex-property member access.</param>
    public TraversalStep(
        string? relationshipType,
        GraphTraversalDirection direction,
        DepthRange depth,
        IReadOnlyList<PredicateFragment> relationshipPredicates,
        Type? targetType,
        Type? relationshipClrType,
        bool isComplexPropertyTraversal)
        : this(
            relationshipType,
            direction,
            depth,
            relationshipPredicates,
            targetType,
            relationshipClrType,
            isComplexPropertyTraversal,
            sourceAlias: null)
    {
    }

    /// <summary>
    /// Initializes a traversal step with an explicit source scope.
    /// </summary>
    /// <param name="relationshipType">The relationship type label to traverse, or <see langword="null"/> for any relationship type.</param>
    /// <param name="direction">The traversal direction.</param>
    /// <param name="depth">The traversal depth range.</param>
    /// <param name="relationshipPredicates">Predicates applied to relationships traversed by the step.</param>
    /// <param name="targetType">The target node or value-node type, if known.</param>
    /// <param name="relationshipClrType">The CLR relationship type, if known.</param>
    /// <param name="isComplexPropertyTraversal">Whether the step came from a complex-property member access.</param>
    /// <param name="sourceAlias">The semantic source scope for this step, if known.</param>
    public TraversalStep(
        string? relationshipType,
        GraphTraversalDirection direction,
        DepthRange depth,
        IReadOnlyList<PredicateFragment> relationshipPredicates,
        Type? targetType,
        Type? relationshipClrType,
        bool isComplexPropertyTraversal,
        string? sourceAlias)
        : this(
            relationshipType,
            direction,
            depth,
            relationshipPredicates,
            targetType,
            relationshipClrType,
            isComplexPropertyTraversal,
            sourceAlias,
            targetAlias: null)
    {
    }

    /// <summary>
    /// Initializes a traversal step with explicit source and target scopes.
    /// </summary>
    /// <param name="relationshipType">The relationship type label to traverse, or <see langword="null"/> for any relationship type.</param>
    /// <param name="direction">The traversal direction.</param>
    /// <param name="depth">The traversal depth range.</param>
    /// <param name="relationshipPredicates">Predicates applied to relationships traversed by the step.</param>
    /// <param name="targetType">The target node or value-node type, if known.</param>
    /// <param name="relationshipClrType">The CLR relationship type, if known.</param>
    /// <param name="isComplexPropertyTraversal">Whether the step came from a complex-property member access.</param>
    /// <param name="sourceAlias">The semantic source scope for this step, if known.</param>
    /// <param name="targetAlias">The semantic scope bound by this step's target, if known. Predicates
    /// and ordering keys whose alias equals this value apply to the traversal target.</param>
    public TraversalStep(
        string? relationshipType,
        GraphTraversalDirection direction,
        DepthRange depth,
        IReadOnlyList<PredicateFragment> relationshipPredicates,
        Type? targetType,
        Type? relationshipClrType,
        bool isComplexPropertyTraversal,
        string? sourceAlias,
        string? targetAlias)
    {
        QueryModelGuard.RequireNullOrNotWhiteSpace(relationshipType, nameof(relationshipType));
        QueryModelGuard.RequireNullOrNotWhiteSpace(sourceAlias, nameof(sourceAlias));
        QueryModelGuard.RequireNullOrNotWhiteSpace(targetAlias, nameof(targetAlias));
        QueryModelGuard.RequireDefinedEnum(direction, nameof(direction));

        if (targetType is not null && relationshipClrType is not null)
        {
            QueryModelGuard.RequireAssignableTo(targetType, typeof(INode), nameof(targetType));
        }

        RelationshipType = relationshipType;
        Direction = direction;
        Depth = depth ?? throw new ArgumentNullException(nameof(depth));
        RelationshipPredicates = QueryModelGuard.CopyRequiredList(relationshipPredicates, nameof(relationshipPredicates));
        TargetType = targetType;
        RelationshipClrType = relationshipClrType;
        IsComplexPropertyTraversal = isComplexPropertyTraversal;
        SourceAlias = sourceAlias;
        TargetAlias = targetAlias;
    }

    /// <summary>
    /// Gets the relationship type label to traverse, or <see langword="null"/> for any relationship type.
    /// </summary>
    public string? RelationshipType { get; }

    /// <summary>
    /// Gets the traversal direction.
    /// </summary>
    public GraphTraversalDirection Direction { get; }

    /// <summary>
    /// Gets the traversal depth range.
    /// </summary>
    public DepthRange Depth { get; }

    /// <summary>
    /// Gets predicates applied to relationships traversed by the step.
    /// </summary>
    public IReadOnlyList<PredicateFragment> RelationshipPredicates { get; }

    /// <summary>
    /// Gets the target node type, if known.
    /// </summary>
    public Type? TargetType { get; }

    /// <summary>
    /// Gets the CLR relationship type, if known.
    /// </summary>
    public Type? RelationshipClrType { get; }

    /// <summary>Gets whether this step represents complex-property navigation.</summary>
    public bool IsComplexPropertyTraversal { get; }

    /// <summary>Gets the semantic source scope for this step, if known.</summary>
    public string? SourceAlias { get; }

    /// <summary>
    /// Gets the semantic scope bound by this step's target, if known. Predicates and ordering keys
    /// whose alias equals this value apply to the traversal target.
    /// </summary>
    public string? TargetAlias { get; }

    /// <summary>Gets or initializes which paths are retained for each source-target pair.</summary>
    public TraversalPathSelection PathSelection { get; init; }

    /// <summary>Gets or initializes predicates that candidate endpoint nodes must satisfy.</summary>
    public IReadOnlyList<PredicateFragment> TargetPredicates
    {
        get => targetPredicates;
        init => targetPredicates = QueryModelGuard.CopyRequiredList(value, nameof(TargetPredicates));
    }
}
