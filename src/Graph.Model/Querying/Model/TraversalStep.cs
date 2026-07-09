// Copyright 2025 Savas Parastatidis
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//      https://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

namespace Cvoya.Graph.Model.Querying;

/// <summary>
/// Describes one graph traversal step.
/// </summary>
public sealed record TraversalStep
{
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
    {
        QueryModelGuard.RequireNullOrNotWhiteSpace(relationshipType, nameof(relationshipType));
        QueryModelGuard.RequireNullOrNotWhiteSpace(sourceAlias, nameof(sourceAlias));
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
}
