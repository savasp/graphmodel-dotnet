// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Neo4j.Querying.Cypher.Visitors.Core;

using System.Linq.Expressions;
using Cvoya.Graph.Cypher;
using Cvoya.Graph.Cypher.Ast;
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
        CypherStatement statement;
        try
        {
            var model = GraphQueryModelBuilder.Build(node);
            statement = new CypherQueryPlanner(Neo4jDialect.Instance).Plan(model);
        }
        catch (GraphQueryTranslationException exception)
        {
            throw PreserveLegacyProviderException(exception);
        }

        var rendered = new CypherRenderer(Neo4jDialect.Instance).Render(statement);
        (Type Source, Type Relationship, Type Target)? pathTypes = statement.PathTypes is null
            ? null
            : (statement.PathTypes.Source, statement.PathTypes.Relationship, statement.PathTypes.Target);
        Query = new CypherQuery(rendered.Text, rendered.Parameters, pathTypes, rendered.ProjectionColumns);
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
