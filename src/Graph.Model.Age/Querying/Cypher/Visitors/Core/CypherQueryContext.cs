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

using Cvoya.Graph.Model.Age.Querying.Cypher.Builders;
using Cvoya.Graph.Model.Cypher.Querying.Cypher.Visitors.Core;
using Microsoft.Extensions.Logging;

/// <summary>
/// Aggregates the state and builder dependencies required to translate LINQ expressions into AGE Cypher queries.
/// Uses AGE-specific builder to avoid Neo4j syntax generation.
/// </summary>
internal sealed record CypherQueryContext
{
    public CypherQueryContext(Type rootType, ILoggerFactory? loggerFactory = null, bool useFragmentRenderer = true)
    {
        LoggerFactory = loggerFactory;
        Scope = new CypherQueryScope(rootType);
        Builder = new AgeCypherQueryBuilder(this);
        // Fragment shim support
        FragmentSequence = new List<QueryFragment>();
        AliasManager = new AliasManager();
        UseFragmentRenderer = useFragmentRenderer;
    }

    public CypherQueryScope Scope { get; }

    public AgeCypherQueryBuilder Builder { get; }

    public ILoggerFactory? LoggerFactory { get; }

    // Fragment shim — collects fragments produced during translation (can contain both shared and AGE-specific fragments)
    public List<QueryFragment> FragmentSequence { get; }

    // Lightweight alias manager for fragment shim
    public AliasManager AliasManager { get; }

    /// <summary>
    /// When true, GetQuery() returns fragment-rendered output instead of builder output.
    /// This enables progressive migration from builder-based to fragment-based query generation.
    /// </summary>
    public bool UseFragmentRenderer { get; }

    /// <summary>
    /// Stores the result of the most recent alias-generating operation.
    /// Used for transitioning from implicit (CurrentAlias mutation) to explicit alias passing.
    /// Callers can check this after operations like PathSegments to know what aliases were generated.
    /// </summary>
    public AliasResolutionResult? LastAliasResolution { get; set; }

    public IDisposable PushAlias(string alias) => new AliasScope(this, alias);

    public string GetQuery()
    {
        if (UseFragmentRenderer && FragmentSequence.Count > 0)
        {
            return AgeFragmentRenderer.Render(FragmentSequence);
        }
        
        return Builder.Build().Text;
    }

    public IReadOnlyDictionary<string, object?> GetParameters() => Builder.Build().Parameters;

    private sealed class AliasScope : IDisposable
    {
        private readonly CypherQueryContext context;
        private readonly string previousAlias;

        public AliasScope(CypherQueryContext context, string alias)
        {
            this.context = context;
            previousAlias = context.Scope.CurrentAlias ?? string.Empty;
            context.Scope.PushAlias(alias);
        }

        public void Dispose()
        {
            context.Scope.PopAlias();
            if (string.IsNullOrEmpty(previousAlias))
            {
                context.Scope.CurrentAlias = null;
            }
        }
    }
}
