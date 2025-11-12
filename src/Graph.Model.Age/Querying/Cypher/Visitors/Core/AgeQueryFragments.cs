// Copyright 2025 Savas Parastatidis

namespace Cvoya.Graph.Model.Age.Querying.Cypher.Visitors.Core;

using System.Collections.Immutable;
using Cvoya.Graph.Model.Cypher.Querying.Cypher.Visitors.Core;

/// <summary>
/// AGE-specific query fragment base type.
/// Extends the shared QueryFragment from Graph.Model.Cypher.
/// </summary>
internal abstract record AgeQueryFragment : QueryFragment
{
    protected AgeQueryFragment(string kind, ImmutableArray<string> createdAliases, ImmutableArray<string> consumesAliases, string? currentAlias)
        : base(kind, createdAliases, consumesAliases, currentAlias)
    {
    }
}

/// <summary>
/// AGE-specific: Represents a root MATCH clause for a node type.
/// </summary>
internal sealed record MatchRootFragment(string Pattern, string Label, Type NodeType, ImmutableArray<string> CreatedAliases, string CurrentAlias)
    : AgeQueryFragment("MatchRoot", CreatedAliases, ImmutableArray<string>.Empty, CurrentAlias);

/// <summary>
/// AGE-specific: Represents a traversal MATCH segment (path pattern).
/// </summary>
internal sealed record MatchSegmentFragment(string Pattern, Type SourceType, Type RelationshipType, Type TargetType, GraphTraversalDirection Direction, ImmutableArray<string> CreatedAliases, string CurrentAlias)
    : AgeQueryFragment("MatchSegment", CreatedAliases, ImmutableArray<string>.Empty, CurrentAlias);

/// <summary>
/// AGE-specific: Represents an OPTIONAL MATCH clause for complex property loading.
/// </summary>
internal sealed record OptionalMatchFragment(string Pattern, ImmutableArray<string> CreatedAliases, ImmutableArray<string> ConsumesAliases, string? CurrentAlias)
    : AgeQueryFragment("OptionalMatch", CreatedAliases, ConsumesAliases, CurrentAlias);

/// <summary>
/// AGE-specific: Controls complex property hydration for nodes.
/// </summary>
internal sealed record ComplexPropertyLoadingFragment(bool IsEnabled, string? TargetAlias)
    : AgeQueryFragment("ComplexPropertyLoading", ImmutableArray<string>.Empty, ImmutableArray<string>.Empty, TargetAlias);
