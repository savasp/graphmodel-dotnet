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

namespace Cvoya.Graph.Model.Cypher.Querying.Cypher.Visitors.Core;

using System.Collections.Immutable;

/// <summary>
/// Base type for query fragments produced during LINQ-to-Cypher translation.
/// Fragments are immutable records that capture semantic query operations
/// and can be rendered into provider-specific Cypher syntax.
/// </summary>
public abstract record QueryFragment
{
    protected QueryFragment(string kind, ImmutableArray<string> createdAliases, ImmutableArray<string> consumesAliases, string? currentAlias)
    {
        Kind = kind;
        CreatedAliases = createdAliases;
        ConsumesAliases = consumesAliases;
        CurrentAlias = currentAlias;
        Metadata = ImmutableDictionary<string, object?>.Empty;
    }

    /// <summary>
    /// The kind of fragment (e.g., "Where", "Projection", "Order").
    /// </summary>
    public string Kind { get; init; }

    /// <summary>
    /// Aliases created by this fragment (e.g., node/relationship aliases in MATCH).
    /// </summary>
    public ImmutableArray<string> CreatedAliases { get; init; }

    /// <summary>
    /// Aliases consumed/referenced by this fragment.
    /// </summary>
    public ImmutableArray<string> ConsumesAliases { get; init; }

    /// <summary>
    /// The primary alias that becomes the "current" focus after this fragment.
    /// Used by subsequent fragments to reference the correct alias.
    /// </summary>
    public string? CurrentAlias { get; init; }

    /// <summary>
    /// Optional metadata for provider-specific or advanced scenarios.
    /// </summary>
    public IImmutableDictionary<string, object?> Metadata { get; init; }
}

/// <summary>
/// Represents a WHERE clause predicate.
/// </summary>
public sealed record WhereFragment(string PredicateText, ImmutableArray<string> ConsumesAliases, string CurrentAlias)
    : QueryFragment("Where", ImmutableArray<string>.Empty, ConsumesAliases, CurrentAlias);

/// <summary>
/// Represents a RETURN projection clause.
/// </summary>
public sealed record ProjectionFragment(ImmutableArray<string> Returns, string CurrentAlias)
    : QueryFragment("Projection", ImmutableArray<string>.Empty, ImmutableArray<string>.Empty, CurrentAlias);

/// <summary>
/// Represents an ORDER BY clause.
/// </summary>
public sealed record OrderFragment(string Expression, bool Descending, string? CurrentAlias = null)
    : QueryFragment("Order", ImmutableArray<string>.Empty, ImmutableArray<string>.Empty, CurrentAlias);

/// <summary>
/// Represents a SKIP clause for pagination.
/// </summary>
public sealed record SkipFragment(int Count, string? CurrentAlias = null)
    : QueryFragment("Skip", ImmutableArray<string>.Empty, ImmutableArray<string>.Empty, CurrentAlias);

/// <summary>
/// Represents a LIMIT clause for pagination.
/// </summary>
public sealed record LimitFragment(int Count, string? CurrentAlias = null)
    : QueryFragment("Limit", ImmutableArray<string>.Empty, ImmutableArray<string>.Empty, CurrentAlias);

/// <summary>
/// Represents the DISTINCT keyword in a RETURN clause.
/// </summary>
public sealed record DistinctFragment(string? CurrentAlias = null)
    : QueryFragment("Distinct", ImmutableArray<string>.Empty, ImmutableArray<string>.Empty, CurrentAlias);

/// <summary>
/// Marker fragment indicating ORDER BY clauses should be reversed.
/// Used for Last() operations.
/// </summary>
public sealed record ReverseOrderFragment()
    : QueryFragment("ReverseOrder", ImmutableArray<string>.Empty, ImmutableArray<string>.Empty, null);

/// <summary>
/// Represents an aggregation operation (Count, Sum, Average, etc.).
/// IsScalar indicates whether this is a scalar aggregation (no grouping) or part of a grouped aggregation.
/// </summary>
public sealed record AggregationFragment(string AggregationType, string Expression, bool IsScalar)
    : QueryFragment("Aggregation", ImmutableArray<string>.Empty, ImmutableArray<string>.Empty, null);

/// <summary>
/// Represents a GROUP BY operation.
/// In Cypher, grouping is often implicit when aggregations are present,
/// but this fragment captures the explicit grouping key.
/// </summary>
public sealed record GroupByFragment(string Expression, string Alias)
    : QueryFragment("GroupBy", ImmutableArray<string>.Empty, ImmutableArray<string>.Empty, Alias);

/// <summary>
/// Deterministic alias manager for generating consistent node and relationship aliases.
/// </summary>
public sealed class AliasManager
{
    private int _nodeCounter = 0;
    private int _relCounter = 0;

    /// <summary>
    /// Resets the alias counters to their initial state.
    /// </summary>
    public void Reset()
    {
        _nodeCounter = 0;
        _relCounter = 0;
    }

    /// <summary>
    /// Reserves a node alias (e.g., "n0", "n1", ...).
    /// </summary>
    /// <param name="clrType">Optional CLR type for future type-aware alias generation.</param>
    /// <returns>A deterministic node alias.</returns>
    public string ReserveNodeAlias(Type? clrType = null)
    {
        var alias = $"n{_nodeCounter++}";
        return alias;
    }

    /// <summary>
    /// Reserves a relationship alias (e.g., "r0", "r1", ...).
    /// </summary>
    /// <param name="clrType">Optional CLR type for future type-aware alias generation.</param>
    /// <returns>A deterministic relationship alias.</returns>
    public string ReserveRelAlias(Type? clrType = null)
    {
        var alias = $"r{_relCounter++}";
        return alias;
    }

    /// <summary>
    /// Reserves aliases for a complete traversal (source node, relationship, target node).
    /// </summary>
    /// <param name="srcType">Optional source node CLR type.</param>
    /// <param name="relType">Optional relationship CLR type.</param>
    /// <param name="tgtType">Optional target node CLR type.</param>
    /// <returns>A tuple of (source, relationship, target) aliases.</returns>
    public (string src, string rel, string tgt) ReserveTraversalAliases(Type? srcType = null, Type? relType = null, Type? tgtType = null)
    {
        var src = ReserveNodeAlias(srcType);
        var rel = ReserveRelAlias(relType);
        var tgt = ReserveNodeAlias(tgtType);
        return (src, rel, tgt);
    }
}
