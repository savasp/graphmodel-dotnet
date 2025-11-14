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

namespace Cvoya.Graph.Model.Age.Querying.Cypher.Visitors.Core;

using Cvoya.Graph.Model;
using Cvoya.Graph.Model.Cypher.Querying.Cypher.Builders;

/// <summary>
/// Tracks alias and multi-hop traversal state while building Cypher queries for AGE.
/// Implementation aligns with the Neo4j provider's ICypherQueryScope interface.
/// 
/// ALIAS MUTATION PATTERN:
/// This class uses mutable state (CurrentAlias) to track the "current" node/entity
/// being processed during query translation. The pattern follows the inside-out visitor execution:
/// 
/// 1. Innermost expression visits first (e.g., root Nodes&lt;Person&gt;())
/// 2. Child visitor sets CurrentAlias to indicate what it returned (e.g., "p" for Person)
/// 3. Parent visitor reads CurrentAlias to know what to work with
/// 4. Parent may update CurrentAlias for its own result
/// 
/// EXAMPLE FLOW:
/// Query: source.PathSegments&lt;Person, Knows, Person&gt;().Where(ps => ps.EndNode.Age > 30)
/// 
/// 1. PathSegments visits first (inside-out)
///    - Generates: src0, r0, tgt0
///    - Sets CurrentAlias = "tgt0" (the target node)
///    - Stores hop aliases for later reference
/// 
/// 2. Where visits next
///    - Reads CurrentAlias = "tgt0" to know what "ps" refers to
///    - Uses GetHopAliases(CurrentHop - 1) to resolve ps.EndNode, ps.StartNode, ps.Relationship
///    - Generates: WHERE tgt0.Age > $param_0
/// 
/// MULTI-HOP TRACKING:
/// CurrentHop tracks position in chained traversals (0, 1, 2, ...).
/// Each hop stores its aliases via StoreHopAliases(hop, src, rel, tgt).
/// This enables WHERE clauses to reference the correct hop's aliases.
/// 
/// ARCHITECTURAL NOTES:
/// - Essential state (CurrentAlias, hopAliases, hopTypes) must remain for visitor coordination
/// - Traversal modifiers (TraversalDepth/Direction) serve as temporary config for fluent API
/// - Type/alias tracking has been simplified - dead code removed (Steps 1-3)
/// - Traversal state now uses local parameters instead of deep scope reads (Step 4)
/// - No-op stubs (PushAlias/PopAlias) satisfy shared interface with Neo4j provider
/// </summary>
internal sealed class CypherQueryScope(Type rootType) : ICypherQueryScope
{
    private readonly Dictionary<int, (string src, string rel, string tgt)> hopAliases = [];
    private readonly Dictionary<int, (Type src, Type rel, Type tgt)> hopTypes = [];

    public Type RootType { get; } = rootType;

    /// <summary>
    /// The current alias being worked with during query translation.
    /// MUTATION PATTERN: Child visitor sets this to indicate what it returned.
    /// Parent visitor reads this to know what entity to operate on.
    /// 
    /// Example: After PathSegments, CurrentAlias = "tgt0" (target node).
    /// Subsequent Where/Select operations read this to build expressions.
    /// 
    /// WARNING: Mutable state - changes frequently during visitor traversal.
    /// </summary>
    public string? CurrentAlias { get; set; }

    public int? TraversalMinDepth { get; private set; }

    public int? TraversalMaxDepth { get; private set; }

    public GraphTraversalDirection? TraversalDirection { get; private set; }

    /// <summary>
    /// Tracks the Cypher expression from the last Select projection.
    /// Used to resolve OrderBy on parameter-only lambda expressions like OrderBy(x => x).
    /// </summary>
    public string? LastProjectedExpression { get; set; }

    /// <summary>
    /// Tracks the hop number of the most recently processed PathSegments call.
    /// Used by Select projections to determine which hop's aliases to use for ps.StartNode/EndNode.
    /// This is needed when multiple PathSegments exist (e.g., from Traverse which uses PathSegments internally).
    /// </summary>
    public int LastPathSegmentHop { get; set; } = -1;

    /// <summary>
    /// Current hop number for multi-hop traversal (0, 1, 2, ...).
    /// 
    /// INCREMENTED BY: HandlePathSegments via AdvanceHop() after processing each hop.
    /// READ BY: Where/OrderBy/Select utilities to determine which hop's aliases to use.
    /// 
    /// IMPORTANT: CurrentHop is incremented AFTER processing the hop, so utilities
    /// typically use (CurrentHop - 1) to reference the most recent hop's aliases.
    /// 
    /// Example: After first PathSegments, CurrentHop = 1 (but hop 0 was just processed).
    /// WHERE clause uses GetHopAliases(CurrentHop - 1) = GetHopAliases(0).
    /// </summary>
    public int CurrentHop { get; private set; } = 0;

    public void SetTraversalDepth(int minDepth, int maxDepth)
    {
        TraversalMinDepth = minDepth;
        TraversalMaxDepth = maxDepth;
    }

    public void ClearTraversalDepth()
    {
        TraversalMinDepth = null;
        TraversalMaxDepth = null;
    }

    public void SetTraversalDirection(GraphTraversalDirection direction)
        => TraversalDirection = direction;

    public void ClearTraversalDirection()
        => TraversalDirection = null;

    /// <summary>
    /// No-op implementation to satisfy ICypherQueryScope interface.
    /// AGE provider doesn't use alias stack - CurrentAlias is managed directly.
    /// </summary>
    public void PushAlias(string alias)
    {
        // No-op: AGE provider manages CurrentAlias directly without a stack
    }

    /// <summary>
    /// No-op implementation to satisfy ICypherQueryScope interface.
    /// AGE provider doesn't use alias stack - CurrentAlias is managed directly.
    /// </summary>
    public void PopAlias()
    {
        // No-op: AGE provider manages CurrentAlias directly without a stack
    }

    public bool IsInPathSegmentContext()
    {
        // This method is kept for interface compatibility but isn't actively used.
        // AGE provider checks FragmentSequence directly for PathSegmentFragment instead.
        return false;
    }

    /// <summary>
    /// Advances to the next hop in multi-hop traversal
    /// </summary>
    public void AdvanceHop()
    {
        CurrentHop++;
    }

    /// <summary>
    /// Gets numbered alias for multi-hop traversal (e.g., "src0", "r1", "tgt2")
    /// </summary>
    public string GetNumberedAlias(string baseAlias)
    {
        return $"{baseAlias}{CurrentHop}";
    }

    /// <summary>
    /// Gets numbered alias for a specific hop (useful for WHERE clause positioning)
    /// </summary>
    public string GetNumberedAliasForHop(string baseAlias, int hopNumber)
    {
        return $"{baseAlias}{hopNumber}";
    }

    /// <summary>
    /// Stores the actual aliases used for a specific hop.
    /// 
    /// CRITICAL: Handles chained patterns where aliases may differ from the default numbered pattern.
    /// 
    /// Example: In chained traversal Person->Person->Person:
    /// - Hop 0: src0 -[r0]-> tgt0
    /// - Hop 1: src1 -[r1]-> src0 (target connects to previous hop's source!)
    /// 
    /// WHERE clauses must use GetHopAliases() to get the actual aliases, not assume
    /// they follow the simple pattern of (srcN, rN, tgtN).
    /// </summary>
    public void StoreHopAliases(int hopNumber, string sourceAlias, string relationshipAlias, string targetAlias)
    {
        hopAliases[hopNumber] = (sourceAlias, relationshipAlias, targetAlias);
    }

    /// <summary>
    /// Gets the actual aliases used for a specific hop
    /// </summary>
    public (string src, string rel, string tgt)? GetHopAliases(int hopNumber)
    {
        return hopAliases.TryGetValue(hopNumber, out var aliases) ? aliases : null;
    }

    /// <summary>
    /// Stores the types used for a specific hop
    /// </summary>
    public void StoreHopTypes(int hopNumber, Type sourceType, Type relationshipType, Type targetType)
    {
        hopTypes[hopNumber] = (sourceType, relationshipType, targetType);
    }

    /// <summary>
    /// Gets the types used for a specific hop
    /// </summary>
    public (Type src, Type rel, Type tgt)? GetHopTypes(int hopNumber)
    {
        return hopTypes.TryGetValue(hopNumber, out var types) ? types : null;
    }
}
