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

using Cvoya.Graph.Model.Cypher.Querying.Cypher.Visitors.Core;
using Microsoft.Extensions.Logging;

/// <summary>
/// Aggregates the state required to translate LINQ expressions into AGE Cypher queries.
/// Provides:
/// - Scope: Tracks aliases and multi-hop traversal state
/// - ParameterStore: Manages query parameters
/// - FragmentSequence: Accumulates query fragments for rendering
/// </summary>
internal sealed record CypherQueryContext
{
    public CypherQueryContext(Type rootType, ILoggerFactory? loggerFactory = null)
    {
        LoggerFactory = loggerFactory;
        Scope = new CypherQueryScope(rootType);
        ParameterStore = new QueryParameterStore(loggerFactory);
        FragmentSequence = new List<QueryFragment>();
    }

    public CypherQueryScope Scope { get; }

    public QueryParameterStore ParameterStore { get; }

    public ILoggerFactory? LoggerFactory { get; }

    /// <summary>
    /// Fragment sequence accumulates query fragments during translation.
    /// Fragments are rendered in order by AgeFragmentRenderer.
    /// </summary>
    public List<QueryFragment> FragmentSequence { get; }

    public void AddFragment(QueryFragment fragment)
    {
        FragmentSequence.Add(fragment);
    }

    public bool HasMatchFragments()
        => FragmentSequenceInsights.HasMatchFragments(FragmentSequence);

    public string? GetLastReturnClause()
        => FragmentSequenceInsights.GetLastReturnClause(FragmentSequence);

    public IReadOnlyList<string> GetLatestProjectionReturns()
        => FragmentSequenceInsights.GetLatestProjectionReturns(FragmentSequence);

    public bool HasExplicitReturnFragments()
        => FragmentSequenceInsights.HasExplicitReturnFragments(FragmentSequence);

    public string GetQuery()
    {
        if (FragmentSequence.Count == 0)
        {
            return string.Empty;
        }

        return AgeFragmentRenderer.Render(FragmentSequence);
    }

    public IReadOnlyDictionary<string, object?> GetParameters() => ParameterStore.Snapshot();
}
