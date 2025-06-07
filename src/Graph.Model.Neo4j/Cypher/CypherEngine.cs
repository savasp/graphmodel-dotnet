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

using System.Linq.Expressions;
using Cvoya.Graph.Model.Neo4j.Cypher;
using Cvoya.Graph.Model.Neo4j.Linq;
using Microsoft.Extensions.Logging;

namespace Cvoya.Graph.Model.Neo4j;

internal class CypherEngine
{
    private readonly GraphContext _context;
    private readonly ILogger<CypherEngine>? _logger;

    public CypherEngine(GraphContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = _context.LoggerFactory?.CreateLogger<CypherEngine>();
    }

    public async Task<T> ExecuteAsync<T>(string cypher, GraphQueryContext queryContext, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(cypher);
        ArgumentNullException.ThrowIfNull(queryContext);

        _logger?.LogDebug("Executing Cypher query: {Query}", cypher);

        // TODO: Implement execution logic
        throw new NotImplementedException("Execution logic coming soon!");
    }

    public Task<string> ExpressionToCypherVisitor(Expression expression, GraphQueryContext queryContext, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(expression);
        ArgumentNullException.ThrowIfNull(queryContext);

        _logger?.LogDebug("Converting expression to Cypher: {ExpressionType}", expression.NodeType);

        try
        {
            var visitor = new CypherQueryVisitor(queryContext, _logger);
            visitor.Visit(expression);

            var result = visitor.Build();
            queryContext.Parameters = result.Parameters;

            _logger?.LogDebug("Generated Cypher: {Cypher}", result.Cypher);

            return Task.FromResult(result.Cypher);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to convert expression to Cypher");
            throw new InvalidOperationException($"Failed to convert expression to Cypher: {ex.Message}", ex);
        }
    }
}