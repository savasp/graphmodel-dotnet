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
    {
        QueryModelGuard.RequireNullOrNotWhiteSpace(relationshipType, nameof(relationshipType));
        QueryModelGuard.RequireDefinedEnum(direction, nameof(direction));

        if (targetType is not null)
        {
            QueryModelGuard.RequireAssignableTo(targetType, typeof(INode), nameof(targetType));
        }

        RelationshipType = relationshipType;
        Direction = direction;
        Depth = depth ?? throw new ArgumentNullException(nameof(depth));
        RelationshipPredicates = QueryModelGuard.CopyRequiredList(relationshipPredicates, nameof(relationshipPredicates));
        TargetType = targetType;
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
}
