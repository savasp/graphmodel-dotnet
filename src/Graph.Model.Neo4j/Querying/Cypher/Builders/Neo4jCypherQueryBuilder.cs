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

namespace Cvoya.Graph.Model.Neo4j.Querying.Cypher.Builders;

using Cvoya.Graph.Model.Cypher.Querying.Cypher.Builders;
using Cvoya.Graph.Model.Neo4j.Querying.Cypher.Visitors.Core;

/// <summary>
/// Neo4j-specific CypherQueryBuilder that uses the general implementation with Neo4j-specific providers.
/// </summary>
internal class Neo4jCypherQueryBuilder : CypherQueryBuilder
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Neo4jCypherQueryBuilder"/> class.
    /// </summary>
    /// <param name="context">The Neo4j query context.</param>
    public Neo4jCypherQueryBuilder(CypherQueryContext context)
        : base(
            new Neo4jCypherQueryBuilderContext(context),
            new Neo4jCypherExpressionProcessor(context.Scope, context.LoggerFactory),
            new Neo4jCypherCollectionProvider())
    {
        // The base class handles all the logic using the Neo4j-specific providers
    }
}

/// <summary>
/// Implementation of ICypherQueryBuilderContext for Neo4j.
/// </summary>
internal class Neo4jCypherQueryBuilderContext : ICypherQueryBuilderContext
{
    private readonly CypherQueryContext _context;

    public Neo4jCypherQueryBuilderContext(CypherQueryContext context)
    {
        _context = context;
    }

    public ICypherQueryScope Scope => _context.Scope;
    public Microsoft.Extensions.Logging.ILoggerFactory? LoggerFactory => _context.LoggerFactory;
    public ICypherCollectionProvider CollectionProvider => new Neo4jCypherCollectionProvider();
    public ICypherExpressionProcessor ExpressionProcessor => new Neo4jCypherExpressionProcessor(_context.Scope, _context.LoggerFactory);
}