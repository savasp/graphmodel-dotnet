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
using Cvoya.Graph.Model.Cypher.Querying.Cypher.Builders;
using Microsoft.Extensions.Logging;

/// <summary>
/// Aggregates the state and builder dependencies required to translate LINQ expressions into AGE Cypher queries.
/// Mirrors the Neo4j provider's context so shared visitors can remain provider-agnostic.
/// </summary>
internal sealed record CypherQueryContext
{
    public CypherQueryContext(Type rootType, ILoggerFactory? loggerFactory = null)
    {
        LoggerFactory = loggerFactory;
        Scope = new CypherQueryScope(rootType);
        Builder = new AgeCypherQueryBuilder(this);
    }

    public CypherQueryScope Scope { get; }

    public CypherQueryBuilder Builder { get; }

    public ILoggerFactory? LoggerFactory { get; }

    public IDisposable PushAlias(string alias) => new AliasScope(this, alias);

    public string GetQuery() => Builder.Build().Text;

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
