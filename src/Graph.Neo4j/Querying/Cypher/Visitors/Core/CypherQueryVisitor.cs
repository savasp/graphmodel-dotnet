// Copyright CVOYA. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.
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

namespace Cvoya.Graph.Neo4j.Querying.Cypher.Visitors.Core;

using System.Linq.Expressions;
using Cvoya.Graph.Cypher.Planning;
using Cvoya.Graph.Querying;
using Microsoft.Extensions.Logging;

/// <summary>
/// Compatibility adapter that runs the shared semantic-model, planner, and Neo4j renderer pipeline.
/// </summary>
internal sealed class CypherQueryVisitor : ExpressionVisitor
{
    private readonly Type _rootType;
    private readonly ILogger<CypherQueryVisitor>? _logger;

    public CypherQueryVisitor(Type type, ILoggerFactory? loggerFactory = null)
    {
        _rootType = type ?? throw new ArgumentNullException(nameof(type));
        _logger = loggerFactory?.CreateLogger<CypherQueryVisitor>();
    }

    public CypherQuery Query { get; private set; } = CypherQuery.Empty;

    public override Expression? Visit(Expression? node)
    {
        if (node is null)
        {
            return null;
        }

        _logger?.LogDebug("Planning graph query rooted at {RootType}", _rootType);
        GraphQueryModel model;
        try
        {
            model = GraphQueryModelBuilder.Build(node);
        }
        catch (GraphQueryTranslationException exception)
        {
            throw PreserveLegacyProviderException(exception);
        }

        var statement = new CypherQueryPlanner().Plan(model);
        Query = new Neo4jCypherRenderer().Render(statement);
        _logger?.LogDebug(
            "Added Cypher parameter names: {ParameterNames}; count: {ParameterCount}",
            Query.Parameters.Keys.ToArray(),
            Query.Parameters.Count);
        return node;
    }

    private static Exception PreserveLegacyProviderException(GraphQueryTranslationException exception)
    {
        if (exception.Message.Contains("SelectMany", StringComparison.Ordinal))
            return new NotSupportedException("SelectMany is not supported by LINQ-to-Cypher translation yet; see #100.");

        if (exception.Message.Contains("GroupBy", StringComparison.Ordinal))
            return new NotSupportedException("GroupBy is not supported by LINQ-to-Cypher translation yet; see #100.");

        if (exception.Message.Contains("Union", StringComparison.Ordinal))
            return new GraphException("Union operations are not yet fully implemented in the refactored architecture");

        if (exception.Message.Contains("requires an explicit OrderBy", StringComparison.Ordinal))
            return new GraphException(exception.Message);

        if (exception.Message.Contains("chained after 'TraversePaths", StringComparison.Ordinal))
        {
            var methodEnd = exception.Message.IndexOf('(');
            var methodName = methodEnd > 2 ? exception.Message[2..methodEnd] : "operator";
            return new NotSupportedException(
                $"'.{methodName}(...)' chained after 'TraversePaths(...)' is not yet supported by LINQ-to-Cypher translation: " +
                $"TraversePaths returns IGraphPath, whose per-hop RETURN shape has no single row/alias a '{methodName}' " +
                "operator can translate against. Materialize the paths first (e.g. '.ToListAsync()') and continue with " +
                "IReadOnlyList<IGraphPath> client-side (LINQ-to-Objects) instead.");
        }

        return exception;
    }
}
