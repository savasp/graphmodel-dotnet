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

        _logger?.LogDebugCypherQueryVisitor36(_rootType);
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
        var parameters = RewriteFullTextSearchParameters(statement, rendered.Parameters);
        (Type Source, Type Relationship, Type Target)? pathTypes = statement.PathTypes is null
            ? null
            : (statement.PathTypes.Source, statement.PathTypes.Relationship, statement.PathTypes.Target);
        Query = new CypherQuery(rendered.Text, parameters, pathTypes, rendered.ProjectionColumns);
        if (_logger?.IsEnabled(LogLevel.Debug) == true)
        {
            _logger.LogDebugCypherQueryVisitor53(Query.Parameters.Keys.ToArray(), Query.Parameters.Count);
        }
        return node;
    }

    // Lucene classic-parser special characters. Tokens are alphanumeric after tokenization, so
    // escaping is belt-and-braces defence against injection through the raw query string.
    private static readonly char[] LuceneSpecialCharacters =
        ['+', '-', '&', '|', '!', '(', ')', '{', '}', '[', ']', '^', '"', '~', '*', '?', ':', '\\', '/'];

    // A pure-negative Lucene query has no positive clause, so it matches no documents regardless of
    // stored content — the deterministic "match nothing" for an empty term list. Neo4j's Lucene
    // parser throws on an empty string (verified against db.index.fulltext.queryNodes), so we cannot
    // pass one through.
    private const string MatchNothingLuceneQuery = "-cvoyanomatchsentinel";

    /// <summary>
    /// Rewrites each full-text search parameter from the raw user query into a sanitized, AND-joined
    /// Lucene query so the Neo4j lowering honours the <c>FullTextSearch</c> contract (case-insensitive,
    /// whole-word, all-terms-match) instead of inheriting the Lucene parser's default OR combinator and
    /// live metacharacter syntax.
    /// </summary>
    private static IReadOnlyDictionary<string, object?> RewriteFullTextSearchParameters(
        CypherStatement statement,
        IReadOnlyDictionary<string, object?> parameters)
    {
        var searchClauses = new List<FullTextSearchClause>();
        CollectFullTextSearchClauses(statement.Clauses, searchClauses);
        if (searchClauses.Count == 0)
        {
            return parameters;
        }

        var rewritten = new Dictionary<string, object?>(parameters);
        var rewrittenNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var clause in searchClauses)
        {
            var parameterName = clause.Query.Name;

            // The planner's parameter registry dedupes by value, so two search clauses with the
            // same raw query share one parameter. Rewrite it exactly once: a second pass would
            // tokenize the already-rewritten Lucene query ("a AND b" -> "a AND and AND b").
            if (!rewrittenNames.Add(parameterName))
            {
                continue;
            }

            var raw = rewritten.TryGetValue(parameterName, out var value) ? value as string ?? string.Empty : string.Empty;
            rewritten[parameterName] = RewriteToLuceneAndQuery(raw);
        }

        return rewritten;
    }

    // The statement's top-level clause list is not the complete clause tree: set-operation branches
    // (UNION/UNION ALL, nesting for left-associated chains) and CALL subquery bodies carry whole
    // nested clause pipelines. Search clauses in those branches reference branch-prefixed
    // parameters (u0_, u1_, ...) that must be rewritten under the same contract as top-level ones;
    // see #374.
    private static void CollectFullTextSearchClauses(
        IReadOnlyList<ICypherClause> clauses,
        List<FullTextSearchClause> searchClauses)
    {
        foreach (var clause in clauses)
        {
            switch (clause)
            {
                case FullTextSearchClause search:
                    searchClauses.Add(search);
                    break;
                case SetOperationClause setOperation:
                    CollectFullTextSearchClauses(setOperation.First, searchClauses);
                    CollectFullTextSearchClauses(setOperation.Second, searchClauses);
                    break;
                case CallSubqueryClause subquery:
                    CollectFullTextSearchClauses(subquery.Body, searchClauses);
                    break;
            }
        }
    }

    private static string RewriteToLuceneAndQuery(string raw)
    {
        var tokens = FullTextQueryTokenizer.Tokenize(raw);
        return tokens.Count == 0
            ? MatchNothingLuceneQuery
            : string.Join(" AND ", tokens.Select(EscapeLuceneToken));
    }

    private static string EscapeLuceneToken(string token)
    {
        var builder = new System.Text.StringBuilder(token.Length);
        foreach (var character in token)
        {
            if (Array.IndexOf(LuceneSpecialCharacters, character) >= 0)
            {
                builder.Append('\\');
            }

            builder.Append(character);
        }

        return builder.ToString();
    }

    private static Exception PreserveLegacyProviderException(GraphQueryTranslationException exception)
    {
        if (exception.Message.StartsWith(
            "Cannot translate the correlated grouped projection:", StringComparison.Ordinal))
        {
            return exception;
        }

        if (exception.Message.Contains("SelectMany", StringComparison.Ordinal))
            return new NotSupportedException("SelectMany is not supported by LINQ-to-Cypher translation yet; see #100.");

        if (exception.Message.Contains("GroupBy", StringComparison.Ordinal))
            return new NotSupportedException(exception.Message);

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
