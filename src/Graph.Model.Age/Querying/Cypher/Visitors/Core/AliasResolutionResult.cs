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

/// <summary>
/// Immutable result of alias resolution during query translation.
/// Contains the aliases that were used/reserved by a visitor operation,
/// enabling explicit alias passing instead of implicit mutation.
/// 
/// <para><strong>Motivation for Explicit Alias Passing</strong></para>
/// The visitor previously relied on implicit state mutation via <c>CypherQueryScope.CurrentAlias</c>.
/// Child visitors would set CurrentAlias, and parent visitors would read it. This created hidden
/// dependencies and made control flow difficult to follow.
/// 
/// <para>Explicit alias passing makes data flow visible and testable:</para>
/// <list type="bullet">
///   <item>Alias generation happens in one place (e.g., HandlePathSegments)</item>
///   <item>Results are stored in <c>CypherQueryContext.LastAliasResolution</c></item>
///   <item>Utilities (Where, OrderBy, Select, GroupBy) receive explicit context</item>
///   <item>No hidden dependencies on CurrentAlias mutation</item>
/// </list>
/// 
/// <para><strong>Dual Support Pattern (Transition Period)</strong></para>
/// During migration, both patterns coexist:
/// <list type="number">
///   <item>Legacy code continues using <c>scope.CurrentAlias</c> mutation</item>
///   <item>New code passes <c>AliasResolutionResult</c> explicitly via utility context records</item>
///   <item>Utilities check <c>ExplicitAliasContext</c> first, fall back to <c>CurrentAlias</c></item>
/// </list>
/// 
/// <para>Once all callers migrate, CurrentAlias can be made internal or deprecated.</para>
/// 
/// <para><strong>Usage Example</strong></para>
/// <code>
/// // In HandlePathSegments: generate and store aliases
/// var aliasResult = AliasResolutionResult.ForPathSegment("src0", "r0", "tgt0", hopNumber: 0);
/// context.LastAliasResolution = aliasResult;
/// context.Scope.CurrentAlias = aliasResult.PrimaryAlias; // Backward compatibility
/// 
/// // In HandleWhere: pass explicit context to utility
/// var whereContext = new WhereContext(
///     Lambda: lambda,
///     QueryContext: context,
///     Logger: logger,
///     ExplicitAliasContext: context.LastAliasResolution // EXPLICIT PASSING
/// );
/// 
/// // Inside WhereExpressionResolver: prefer explicit over implicit
/// var alias = context.ExplicitAliasContext?.PrimaryAlias ?? queryContext.Scope.CurrentAlias;
/// </code>
/// </summary>
internal sealed record AliasResolutionResult
{
    /// <summary>
    /// The primary alias to use for subsequent operations (typically target node).
    /// </summary>
    public required string PrimaryAlias { get; init; }

    /// <summary>
    /// Source node alias (for PathSegment operations).
    /// </summary>
    public string? SourceAlias { get; init; }

    /// <summary>
    /// Relationship alias (for PathSegment operations).
    /// </summary>
    public string? RelationshipAlias { get; init; }

    /// <summary>
    /// Target node alias (for PathSegment operations).
    /// </summary>
    public string? TargetAlias { get; init; }

    /// <summary>
    /// The hop number this resolution applies to (for multi-hop traversals).
    /// </summary>
    public int? HopNumber { get; init; }

    /// <summary>
    /// All aliases reserved/used by this operation.
    /// Enables parent operations to generate new aliases that avoid conflicts.
    /// </summary>
    public IReadOnlySet<string> ReservedAliases { get; init; } = new HashSet<string>();

    /// <summary>
    /// Creates a simple result with just a primary alias.
    /// </summary>
    public static AliasResolutionResult Simple(string alias)
    {
        return new AliasResolutionResult
        {
            PrimaryAlias = alias,
            ReservedAliases = new HashSet<string> { alias }
        };
    }

    /// <summary>
    /// Creates a result for a PathSegment hop with source, relationship, and target aliases.
    /// </summary>
    public static AliasResolutionResult ForPathSegment(
        string sourceAlias,
        string relationshipAlias,
        string targetAlias,
        int hopNumber)
    {
        return new AliasResolutionResult
        {
            PrimaryAlias = targetAlias,
            SourceAlias = sourceAlias,
            RelationshipAlias = relationshipAlias,
            TargetAlias = targetAlias,
            HopNumber = hopNumber,
            ReservedAliases = new HashSet<string> { sourceAlias, relationshipAlias, targetAlias }
        };
    }

    /// <summary>
    /// Creates an empty result (no aliases reserved).
    /// </summary>
    public static AliasResolutionResult Empty(string defaultAlias = "n")
    {
        return new AliasResolutionResult
        {
            PrimaryAlias = defaultAlias,
            ReservedAliases = new HashSet<string>()
        };
    }

    /// <summary>
    /// Combines multiple resolution results into one, merging reserved aliases.
    /// Uses the last result's primary alias.
    /// </summary>
    public static AliasResolutionResult Combine(params AliasResolutionResult[] results)
    {
        if (results.Length == 0)
        {
            return Empty();
        }

        var lastResult = results[^1];
        var allReserved = new HashSet<string>();
        
        foreach (var result in results)
        {
            foreach (var alias in result.ReservedAliases)
            {
                allReserved.Add(alias);
            }
        }

        return new AliasResolutionResult
        {
            PrimaryAlias = lastResult.PrimaryAlias,
            SourceAlias = lastResult.SourceAlias,
            RelationshipAlias = lastResult.RelationshipAlias,
            TargetAlias = lastResult.TargetAlias,
            HopNumber = lastResult.HopNumber,
            ReservedAliases = allReserved
        };
    }
}
