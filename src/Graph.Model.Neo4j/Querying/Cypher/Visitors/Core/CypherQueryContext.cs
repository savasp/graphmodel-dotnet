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

namespace Cvoya.Graph.Model.Neo4j.Querying.Cypher.Visitors.Core;

using Cvoya.Graph.Model.Neo4j.Querying.Cypher.Builders;
using Cvoya.Graph.Model.Neo4j.Querying.Cypher.Visitors.Handlers;
using Microsoft.Extensions.Logging;

/// <summary>
/// Context for Cypher query generation that encapsulates all necessary state and services.
/// </summary>
internal record CypherQueryContext
{
    public CypherQueryScope Scope { get; }
    public CypherQueryBuilder Builder { get; }
    public VisitorFactory VisitorFactory { get; }
    public ILoggerFactory? LoggerFactory { get; }
    public MethodHandlerRegistry MethodHandlers { get; }

    public CypherQueryContext(Type rootType, ILoggerFactory? loggerFactory = null)
    {
        LoggerFactory = loggerFactory;
        Scope = new CypherQueryScope(rootType);
        Builder = new CypherQueryBuilder();

        // Set these last in case they access the previous properties in their constructors
        VisitorFactory = new VisitorFactory(this);
        MethodHandlers = MethodHandlerRegistry.Instance;
    }

    /// <summary>
    /// Creates a visitor of the specified type.
    /// </summary>
    public TVisitor CreateVisitor<TVisitor>() where TVisitor : class
    {
        return VisitorFactory.Create<TVisitor>();
    }

    /// <summary>
    /// Pushes a new alias onto the scope.
    /// </summary>
    public IDisposable PushAlias(string alias)
    {
        return new AliasScope(this, alias);
    }

    /// <summary>
    /// Gets the current query string.
    /// </summary>
    public string GetQuery() => Builder.Build().Text;

    /// <summary>
    /// Gets the query parameters.
    /// </summary>
    public IReadOnlyDictionary<string, object?> GetParameters() => Builder.Build().Parameters;

    private class AliasScope : IDisposable
    {
        private readonly CypherQueryContext _context;
        private readonly string _previousAlias;

        public AliasScope(CypherQueryContext context, string alias)
        {
            _context = context;
            _previousAlias = context.Scope.CurrentAlias ?? string.Empty;
            context.Scope.PushAlias(alias);
        }

        public void Dispose()
        {
            _context.Scope.PopAlias();
        }
    }
}