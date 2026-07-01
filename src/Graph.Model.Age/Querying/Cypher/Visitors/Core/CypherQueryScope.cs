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
using Cvoya.Graph.Model.Cypher.Querying.Cypher.Visitors.Core;

/// <summary>
/// Tracks the state of a single Cypher query translation. This type is NOT thread-safe;
/// each query translation must use its own scope instance. Do not share scope across threads.
/// Alias generation is delegated to <see cref="AliasManager"/> as the single source of truth.
/// </summary>
internal sealed class CypherQueryScope : ICypherQueryScope
{
    private readonly AliasManager _aliasManager;

    public CypherQueryScope(Type rootType, AliasManager aliasManager)
    {
        RootType = rootType;
        _aliasManager = aliasManager ?? throw new ArgumentNullException(nameof(aliasManager));
    }
    /// <summary>
    /// Represents a single hop's aliases in a path segment traversal.
    /// </summary>
    internal sealed record HopAliases(string SourceAlias, string RelationshipAlias, string TargetAlias);

    /// <summary>
    /// Represents a single hop's types in a path segment traversal.
    /// </summary>
    internal sealed record HopTypes(Type SourceType, Type RelationshipType, Type TargetType);

    private readonly Dictionary<int, HopAliases> hopAliases = [];
    private readonly Dictionary<int, HopTypes> hopTypes = [];

    public Type RootType { get; }

    /// <summary>Gets or sets the current alias. NOT thread-safe.</summary>
    public string? CurrentAlias { get; set; }

    public int? TraversalMinDepth { get; private set; }
    public int? TraversalMaxDepth { get; private set; }
    public GraphTraversalDirection? TraversalDirection { get; private set; }

    /// <summary>Gets or sets the last projected expression. NOT thread-safe.</summary>
    public string? LastProjectedExpression { get; set; }

    /// <summary>Gets or sets the last path segment hop index. NOT thread-safe.</summary>
    public int LastPathSegmentHop { get; set; } = -1;

    /// <summary>Gets or sets the current hop index. NOT thread-safe.</summary>
    public int CurrentHop { get; private set; } = 0;

    /// <summary>
    /// Set to true when HandleGroupBy rewrites an identity GroupBy on Traverse target
    /// from tgt0 to src0. The NestedCollectHandler uses this flag to correctly resolve
    /// aliases for collect expressions in Traverse+GroupBy queries.
    /// </summary>
    public bool IdentityGroupByRewritten { get; set; }

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

    public void PushAlias(string alias) { /* AGE manages CurrentAlias directly */ }
    public void PopAlias() { }
    public bool IsInPathSegmentContext() => false;

    public void AdvanceHop() { CurrentHop++; }

    /// <summary>
    /// Generates a hop-based alias by delegating to <see cref="AliasManager.GetHopAlias"/>.
    /// Example: <c>GetNumberedAlias("src")</c> → "src0" (when CurrentHop == 0).
    /// </summary>
    public string GetNumberedAlias(string baseAlias)
        => _aliasManager.GetHopAlias(baseAlias, CurrentHop);

    /// <summary>
    /// Generates a hop-based alias for a specific hop number by delegating to <see cref="AliasManager.GetHopAlias"/>.
    /// Example: <c>GetNumberedAliasForHop("tgt", 1)</c> → "tgt1".
    /// </summary>
    public string GetNumberedAliasForHop(string baseAlias, int hopNumber)
        => _aliasManager.GetHopAlias(baseAlias, hopNumber);

    public void StoreHopAliases(int hopNumber, string sourceAlias, string relationshipAlias, string targetAlias)
    {
        hopAliases[hopNumber] = new HopAliases(sourceAlias, relationshipAlias, targetAlias);
    }

    public HopAliases? GetHopAliases(int hopNumber)
    {
        return hopAliases.TryGetValue(hopNumber, out var aliases) ? aliases : null;
    }

    public void StoreHopTypes(int hopNumber, Type sourceType, Type relationshipType, Type targetType)
    {
        hopTypes[hopNumber] = new HopTypes(sourceType, relationshipType, targetType);
    }

    public HopTypes? GetHopTypes(int hopNumber)
    {
        return hopTypes.TryGetValue(hopNumber, out var types) ? types : null;
    }
}
