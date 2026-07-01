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
    /// <summary>
    /// Initializes a new instance of the <see cref="QueryFragment"/> class.
    /// </summary>
    /// <param name="kind">The kind of fragment (e.g., "Where", "Projection", "Order").</param>
    /// <param name="createdAliases">Aliases created by this fragment.</param>
    /// <param name="consumesAliases">Aliases consumed/referenced by this fragment.</param>
    /// <param name="currentAlias">The primary alias after this fragment.</param>
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
/// </summary>
public sealed record GroupByFragment(string Expression, string Alias)
    : QueryFragment("GroupBy", ImmutableArray<string>.Empty, ImmutableArray<string>.Empty, Alias);

/// <summary>
/// Represents a collect() expression for nested collection projections inside RETURN.
/// Used when a GroupBy→Select chain contains nested .Select().ToList() projections.
/// Generates Cypher like: collect({Name: tgt0.FirstName, Age: tgt0.Age}) AS c_FriendDetails
/// </summary>
/// <param name="CollectExpression">The expression inside collect(), e.g. "src0.FirstName" or "{Name: src0.FirstName, Age: src0.Age}".</param>
/// <param name="Alias">The current Cypher alias (used by the fragment pipeline).</param>
/// <param name="ProjectionColumn">The column alias in the RETURN/WITH clause, e.g. "c_Friends".</param>
/// <param name="GroupByExpression">Optional group-by expression from the parent GroupByFragment, used to emit the WITH clause.</param>
/// <param name="OrderByExpression">Optional ORDER BY expression inside collect(), e.g. "tgt0.Age ASC".</param>
public sealed record CollectFragment(string CollectExpression, string Alias, string ProjectionColumn, string? GroupByExpression = null, string? OrderByExpression = null)
    : QueryFragment("Collect", ImmutableArray<string>.Empty, ImmutableArray<string>.Empty, Alias);

/// <summary>
/// Represents a WITH clause carrying forward aliases and computed expressions.
/// Used for degree queries and other scenarios requiring intermediate WITH clauses
/// between MATCH and WHERE/RETURN.
/// The WithExpression contains comma-separated projection items, e.g.
/// "src0, count(r0) AS degree".
/// </summary>
public sealed record WithFragment(string WithExpression, ImmutableArray<string> ConsumesAliases, string CurrentAlias)
    : QueryFragment("With", ImmutableArray<string>.Empty, ConsumesAliases, CurrentAlias);

/// <summary>
/// Deterministic alias manager for generating consistent node and relationship aliases.
/// Supports two numbering schemes:
/// <list type="bullet">
///   <item><description><b>Independent counters</b> (Neo4j-style): <c>ReserveNodeAlias()</c> → "n0", "n1", …;
///         <c>ReserveRelAlias()</c> → "r0", "r1", …</description></item>
///   <item><description><b>Hop-based</b> (AGE-style): <c>GetHopAlias("src", 0)</c> → "src0";
///         <c>GetHopAlias("r", 0)</c> → "r0"; <c>GetHopAlias("tgt", 0)</c> → "tgt0".
///         All aliases in the same hop share the same number.</description></item>
/// </list>
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
    /// Produces aliases in the Neo4j-style numbering: "n0", "r0", "n1", …
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

    /// <summary>
    /// Generates a hop-based alias using the given <paramref name="prefix"/> and <paramref name="hop"/> number.
    /// All aliases within the same traversal hop share the same hop number.
    /// <para>Example: <c>GetHopAlias("src", 0)</c> → "src0"; <c>GetHopAlias("r", 0)</c> → "r0".</para>
    /// This is the primary alias generation method used by the AGE provider.
    /// </summary>
    /// <param name="prefix">The alias prefix (e.g., "src", "r", "tgt").</param>
    /// <param name="hop">The hop number.</param>
    /// <returns>A hop-based alias string.</returns>
    public string GetHopAlias(string prefix, int hop)
        => $"{prefix}{hop}";

    /// <summary>
    /// Reserves hop-based aliases for a complete traversal (source, relationship, target).
    /// All three aliases share the same hop number.
    /// <para>Example: <c>ReserveHopTraversalAliases("src", "r", "tgt", 0)</c> → ("src0", "r0", "tgt0").</para>
    /// </summary>
    /// <param name="srcPrefix">The source alias prefix (e.g., "src" or "n").</param>
    /// <param name="relPrefix">The relationship alias prefix (e.g., "r").</param>
    /// <param name="tgtPrefix">The target alias prefix (e.g., "tgt" or "n").</param>
    /// <param name="hop">The hop number for all three aliases.</param>
    /// <returns>A tuple of (source, relationship, target) hop-based aliases.</returns>
    public (string src, string rel, string tgt) ReserveHopTraversalAliases(
        string srcPrefix, string relPrefix, string tgtPrefix, int hop)
    {
        return (GetHopAlias(srcPrefix, hop), GetHopAlias(relPrefix, hop), GetHopAlias(tgtPrefix, hop));
    }
}
