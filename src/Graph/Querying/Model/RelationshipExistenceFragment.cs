// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Querying;

/// <summary>Describes a typed relationship-existence filter anchored to a query scope.</summary>
public sealed record RelationshipExistenceFragment
{
    /// <summary>Initializes a relationship-existence filter.</summary>
    public RelationshipExistenceFragment(
        Type relationshipType,
        GraphTraversalDirection direction,
        string sourceAlias,
        PredicateFragment? predicate)
    {
        RelationshipType = relationshipType ?? throw new ArgumentNullException(nameof(relationshipType));
        QueryModelGuard.RequireAssignableTo(relationshipType, typeof(IRelationship), nameof(relationshipType));
        QueryModelGuard.RequireDefinedEnum(direction, nameof(direction));
        QueryModelGuard.RequireNullOrNotWhiteSpace(sourceAlias, nameof(sourceAlias));
        Direction = direction;
        SourceAlias = sourceAlias;
        Predicate = predicate;
    }

    /// <summary>Gets the CLR relationship type.</summary>
    public Type RelationshipType { get; }

    /// <summary>Gets the relationship direction relative to the source scope.</summary>
    public GraphTraversalDirection Direction { get; }

    /// <summary>Gets the source scope alias.</summary>
    public string SourceAlias { get; }

    /// <summary>Gets the optional relationship predicate.</summary>
    public PredicateFragment? Predicate { get; }

    /// <summary>Gets whether the operator appeared after a projection in the LINQ chain.</summary>
    public bool AppliedAfterProjection { get; init; }

    /// <summary>Gets whether the operator appeared after paging in the LINQ chain.</summary>
    public bool AppliedAfterPaging { get; init; }
}
