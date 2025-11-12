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

using Cvoya.Graph.Model.Cypher.Querying.Cypher.Builders;

/// <summary>
/// Tracks alias, traversal, and grouping state while building Cypher queries for AGE.
/// Implementation aligns with the Neo4j provider so shared visitors can reuse expectations.
/// 
/// ALIAS MUTATION PATTERN:
/// This class uses mutable state (CurrentAlias, CurrentType) to track the "current" node/entity
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
/// FUTURE REFACTORING:
/// For better architecture, could migrate to explicit alias passing where visitor methods
/// return AliasResolutionResult indicating what aliases they reserved. This would eliminate
/// mutable state but requires updating 100+ call sites. Current pattern works but is implicit.
/// </summary>
internal sealed class CypherQueryScope(Type rootType) : ICypherQueryScope
{
    private readonly Dictionary<Type, string> typeAliases = [];
    private readonly Dictionary<string, Type> aliasTypes = [];
    private readonly Stack<string> aliasStack = new();
    private readonly Dictionary<int, (string src, string rel, string tgt)> hopAliases = [];
    private readonly Dictionary<int, (Type src, Type rel, Type tgt)> hopTypes = [];
    private int aliasCounter;

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

    /// <summary>
    /// The CLR type corresponding to CurrentAlias.
    /// Used for type-specific logic like label generation.
    /// 
    /// WARNING: Mutable state - changes alongside CurrentAlias.
    /// </summary>
    public Type? CurrentType { get; set; }

    public bool IsPathSegmentContext { get; set; }

    public bool PathSegmentPatternsFinalized { get; set; }

    public int? TraversalMinDepth { get; private set; }

    public int? TraversalMaxDepth { get; private set; }

    public TraversalInfo? TraversalInfo { get; private set; }

    public string? GroupByExpression { get; private set; }

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

    /// <summary>
    /// Maximum hop reached (used for generating final aliases).
    /// Tracks the highest hop number seen during traversal.
    /// </summary>
    public int MaxHop { get; private set; } = 0;

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

    /// <summary>
    /// Pushes the current alias onto a stack and sets a new current alias.
    /// Used for nested scopes where we need to temporarily work with a different alias.
    /// 
    /// USAGE: Typically paired with PopAlias() in a using/finally block.
    /// See CypherQueryContext.PushAlias() for the preferred pattern.
    /// 
    /// WARNING: Manual push/pop is error-prone. Prefer using PushAlias() scope pattern.
    /// </summary>
    public void PushAlias(string alias)
    {
        aliasStack.Push(CurrentAlias ?? string.Empty);
        CurrentAlias = alias;
    }

    /// <summary>
    /// Restores the previous alias from the stack.
    /// Must be called after PushAlias() to maintain correct alias context.
    /// </summary>
    public void PopAlias()
    {
        if (aliasStack.Count > 0)
        {
            CurrentAlias = aliasStack.Pop();
            if (string.IsNullOrEmpty(CurrentAlias))
            {
                CurrentAlias = null;
            }
        }
    }

    /// <summary>
    /// Gets or creates an alias for a given type.
    /// 
    /// CACHING: Returns existing alias if type was previously registered.
    /// GENERATION: Creates new alias if type not seen before.
    /// UNIQUENESS: Ensures generated alias doesn't conflict with existing aliases.
    /// 
    /// ALIAS PATTERNS:
    /// - Type-based: PersonNode -> "p", AddressNode -> "a"
    /// - Interface stripping: IPersonNode -> "p" (removes I prefix)
    /// - Conflict resolution: If "p" taken, generates "p_1", "p_2", etc.
    /// 
    /// NOTE: PathSegment handling uses numbered aliases (src0, r0, tgt0) instead,
    /// generated via GetNumberedAlias(). This method is for non-PathSegment nodes.
    /// </summary>
    public string GetOrCreateAlias(Type type, string? preferredAlias = null)
    {
        if (typeAliases.TryGetValue(type, out var alias))
        {
            if (alias == preferredAlias)
            {
                return string.Concat(alias);
            }
        }

        alias = preferredAlias ?? GenerateAlias(type);

        while (aliasTypes.ContainsKey(alias))
        {
            alias = $"{alias}_{++aliasCounter}";
        }

        typeAliases[type] = alias;
        aliasTypes[alias] = type;

        return alias;
    }

    public Type? GetTypeForAlias(string alias)
        => aliasTypes.GetValueOrDefault(alias);

    public string? GetAliasForType(Type type)
        => typeAliases.GetValueOrDefault(type);

    public void SetTraversalInfo(Type sourceType, Type relationshipType, Type targetNodeType)
        => TraversalInfo = new TraversalInfo(sourceType, relationshipType, targetNodeType);

    public void SetGroupByExpression(string expression)
        => GroupByExpression = expression;

    /// <summary>
    /// Manually registers a type-to-alias mapping. Useful when aliases are generated
    /// without using GetOrRegisterAlias (e.g., numbered aliases like src0, src1).
    /// </summary>
    public void RegisterTypeAlias(Type type, string alias)
    {
        typeAliases[type] = alias;
        aliasTypes[alias] = type;
    }

    public bool IsInPathSegmentContext()
        => IsPathSegmentContext;

    /// <summary>
    /// Advances to the next hop in multi-hop traversal
    /// </summary>
    public void AdvanceHop()
    {
        CurrentHop++;
        MaxHop = Math.Max(MaxHop, CurrentHop);
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

    /// <summary>
    /// Gets all aliases that have been reserved/registered so far.
    /// Used for explicit alias passing to avoid conflicts when generating new aliases.
    /// </summary>
    public IReadOnlySet<string> GetReservedAliases()
    {
        return new HashSet<string>(aliasTypes.Keys);
    }

    /// <summary>
    /// Generates a unique numbered alias that doesn't conflict with existing registrations.
    /// </summary>
    public string GenerateUniqueAlias(string baseAlias)
    {
        var alias = baseAlias;
        var counter = 0;
        
        while (aliasTypes.ContainsKey(alias))
        {
            alias = $"{baseAlias}{++counter}";
        }
        
        return alias;
    }

    /// <summary>
    /// Checks if an alias is already reserved.
    /// </summary>
    public bool IsAliasReserved(string alias)
    {
        return aliasTypes.ContainsKey(alias);
    }

    #region Debug and Validation Helpers

    /// <summary>
    /// Dumps the current alias state for debugging.
    /// Shows all registered aliases, types, hop mappings, and current context.
    /// </summary>
    public string DumpAliasState()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("=== ALIAS STATE DUMP ===");
        sb.AppendLine($"Current Context: Alias={CurrentAlias ?? "(none)"}, Type={CurrentType?.Name ?? "(none)"}, Hop={CurrentHop}, MaxHop={MaxHop}");
        sb.AppendLine();
        
        sb.AppendLine("Type -> Alias Mappings:");
        if (typeAliases.Count == 0)
        {
            sb.AppendLine("  (none)");
        }
        else
        {
            foreach (var kvp in typeAliases.OrderBy(x => x.Key.Name))
            {
                sb.AppendLine($"  {kvp.Key.Name} -> {kvp.Value}");
            }
        }
        sb.AppendLine();
        
        sb.AppendLine("Alias -> Type Mappings:");
        if (aliasTypes.Count == 0)
        {
            sb.AppendLine("  (none)");
        }
        else
        {
            foreach (var kvp in aliasTypes.OrderBy(x => x.Key))
            {
                sb.AppendLine($"  {kvp.Key} -> {kvp.Value.Name}");
            }
        }
        sb.AppendLine();
        
        sb.AppendLine("Hop Aliases:");
        if (hopAliases.Count == 0)
        {
            sb.AppendLine("  (none)");
        }
        else
        {
            foreach (var kvp in hopAliases.OrderBy(x => x.Key))
            {
                sb.AppendLine($"  Hop {kvp.Key}: src={kvp.Value.src}, rel={kvp.Value.rel}, tgt={kvp.Value.tgt}");
            }
        }
        sb.AppendLine();
        
        sb.AppendLine("Hop Types:");
        if (hopTypes.Count == 0)
        {
            sb.AppendLine("  (none)");
        }
        else
        {
            foreach (var kvp in hopTypes.OrderBy(x => x.Key))
            {
                sb.AppendLine($"  Hop {kvp.Key}: src={kvp.Value.src.Name}, rel={kvp.Value.rel.Name}, tgt={kvp.Value.tgt.Name}");
            }
        }
        
        sb.AppendLine("======================");
        return sb.ToString();
    }

    /// <summary>
    /// Validates alias consistency and returns any detected issues.
    /// Checks for:
    /// - Duplicate alias registrations
    /// - Type/alias mapping mismatches
    /// - Orphaned hop references
    /// </summary>
    public List<string> ValidateAliasConsistency()
    {
        var issues = new List<string>();
        
        // Check bidirectional mapping consistency
        foreach (var typeKvp in typeAliases)
        {
            var type = typeKvp.Key;
            var alias = typeKvp.Value;
            
            if (!aliasTypes.TryGetValue(alias, out var reverseType))
            {
                issues.Add($"Type {type.Name} maps to alias '{alias}', but reverse mapping is missing");
            }
            else if (reverseType != type)
            {
                issues.Add($"Type {type.Name} maps to alias '{alias}', but '{alias}' maps back to {reverseType.Name}");
            }
        }
        
        foreach (var aliasKvp in aliasTypes)
        {
            var alias = aliasKvp.Key;
            var type = aliasKvp.Value;
            
            if (!typeAliases.TryGetValue(type, out var reverseAlias))
            {
                issues.Add($"Alias '{alias}' maps to type {type.Name}, but reverse mapping is missing");
            }
            else if (reverseAlias != alias)
            {
                issues.Add($"Alias '{alias}' maps to type {type.Name}, but {type.Name} maps back to '{reverseAlias}'");
            }
        }
        
        // Check hop consistency
        for (int hop = 0; hop < CurrentHop; hop++)
        {
            var hasAliases = hopAliases.ContainsKey(hop);
            var hasTypes = hopTypes.ContainsKey(hop);
            
            if (hasAliases != hasTypes)
            {
                issues.Add($"Hop {hop}: Has aliases={hasAliases}, has types={hasTypes} (should match)");
            }
        }
        
        // Check for gaps in hop sequence
        for (int hop = 0; hop < MaxHop; hop++)
        {
            if (!hopAliases.ContainsKey(hop))
            {
                issues.Add($"Hop {hop}: Missing hop data (gap in sequence up to MaxHop={MaxHop})");
            }
        }
        
        return issues;
    }

    #endregion

    private static string GenerateAlias(Type type)
    {
        var name = type.Name;

        if (name.StartsWith("I", StringComparison.Ordinal) && name.Length > 1 && char.IsUpper(name[1]))
        {
            name = name[1..];
        }

        return char.ToLower(name[0]).ToString();
    }
}
