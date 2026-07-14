// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Age.Querying;

using System.Linq.Expressions;
using System.Reflection;
using Cvoya.Graph.Age.Querying.Cypher.Execution;

/// <summary>
/// Lowers full-text search out of the LINQ expression tree before the shared Cypher pipeline runs.
/// AGE cannot express full-text in its Cypher subset, so each <c>Search(source, query)</c> call is
/// replaced by an equivalent <c>Where(source, e =&gt; ids.Contains(e.Id))</c>, where <c>ids</c> is the
/// result of phase 1 (a Postgres text-search query, <see cref="AgeFullTextSearch"/>). The residual,
/// search-free expression flows through the completely unchanged shared planner and renderer, so
/// aliases, projections, paging, and composition all come out right by construction.
/// </summary>
/// <remarks>
/// Placement matters: phase 1 is an async database call and the Cypher visitor is synchronous, so the
/// rewrite runs in <c>CypherEngine.ExecuteAsync</c>/<c>StreamAsync</c> — an async context that already
/// holds the transaction — before the query is built. Search matched by <see cref="MethodInfo"/>
/// identity, not by name.
/// </remarks>
internal sealed class AgeFullTextSearchRewriter
{
    private static readonly MethodInfo SearchMethod = ResolveGeneric(
        typeof(GraphQueryableExtensions), nameof(GraphQueryableExtensions.Search), parameterCount: 2);

    private static readonly MethodInfo WhereMethod = ResolveGeneric(
        typeof(GraphQueryableExtensions), nameof(GraphQueryableExtensions.Where), parameterCount: 2);

    private static readonly MethodInfo EnumerableContainsMethod = typeof(Enumerable)
        .GetMethods(BindingFlags.Public | BindingFlags.Static)
        .Single(method => method.Name == nameof(Enumerable.Contains) && method.GetParameters().Length == 2)
        .MakeGenericMethod(typeof(string));

    private readonly SchemaRegistry _schemaRegistry;

    public AgeFullTextSearchRewriter(SchemaRegistry schemaRegistry) =>
        _schemaRegistry = schemaRegistry ?? throw new ArgumentNullException(nameof(schemaRegistry));

    /// <summary>
    /// Returns the search-free equivalent of <paramref name="expression"/>. Unchanged when the
    /// expression contains no <c>Search</c>.
    /// </summary>
    public async Task<Expression> RewriteAsync(
        Expression expression,
        AgeQueryRunner runner,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(expression);
        ArgumentNullException.ThrowIfNull(runner);

        var searches = LocateSearches(expression);
        if (searches.Count == 0)
        {
            return expression;
        }

        var idsBySearch = new Dictionary<MethodCallExpression, string[]>();
        foreach (var search in searches)
        {
            var elementType = search.Method.GetGenericArguments()[0];
            var query = ExtractQuery(search);
            var ids = await AgeFullTextSearch
                .FindMatchingIdsAsync(elementType, query, _schemaRegistry, runner, cancellationToken)
                .ConfigureAwait(false);
            idsBySearch[search] = [.. ids];
        }

        return ApplyRewrite(expression, idsBySearch);
    }

    internal static bool IsSearch(MethodCallExpression node) =>
        node.Method.IsGenericMethod && node.Method.GetGenericMethodDefinition() == SearchMethod;

    /// <summary>Collects every <c>Search</c> operator in the tree. Test seam for the detection half.</summary>
    internal static IReadOnlyList<MethodCallExpression> LocateSearches(Expression expression) =>
        SearchLocator.Locate(expression);

    /// <summary>
    /// Synchronous rewrite half: replaces each located <c>Search(source, q)</c> with
    /// <c>Where(source, e =&gt; ids.Contains(e.Id))</c>. Test seam so the rewrite is exercisable without a
    /// database (phase 1 supplies the ids in production).
    /// </summary>
    internal static Expression ApplyRewrite(
        Expression expression,
        IReadOnlyDictionary<MethodCallExpression, string[]> idsBySearch) =>
        new SearchToWhereRewriter(idsBySearch).Visit(expression);

    /// <summary>Builds the residual id-membership predicate used by both typed and mixed search.</summary>
    internal static MethodCallExpression BuildIdFilter(Expression source, Type elementType, string[] ids)
    {
        var parameter = Expression.Parameter(elementType, "e");
        var idProperty = elementType.GetProperty(nameof(Graph.IEntity.Id))
            ?? typeof(Graph.IEntity).GetProperty(nameof(Graph.IEntity.Id))!;
        var predicateBody = Expression.Call(
            EnumerableContainsMethod,
            Expression.Constant(ids, typeof(IEnumerable<string>)),
            Expression.Property(parameter, idProperty));
        var predicate = Expression.Lambda(predicateBody, parameter);

        return Expression.Call(
            WhereMethod.MakeGenericMethod(elementType),
            source,
            Expression.Quote(predicate));
    }

    private static string ExtractQuery(MethodCallExpression search)
    {
        // The Search operator builds Expression.Constant(searchQuery), so the argument is a constant
        // string. Fall back to evaluation for any other closure shape.
        var argument = search.Arguments[1];
        if (argument is ConstantExpression { Value: string constant })
        {
            return constant;
        }

        return (string)Expression.Lambda(argument).Compile().DynamicInvoke()!;
    }

    private static MethodInfo ResolveGeneric(Type declaringType, string name, int parameterCount) =>
        declaringType
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(method => method.Name == name
                && method.IsGenericMethodDefinition
                && method.GetParameters().Length == parameterCount);

    /// <summary>Locates <c>Search</c> operators.</summary>
    private static class SearchLocator
    {
        public static List<MethodCallExpression> Locate(Expression expression)
        {
            var collector = new SearchCollector();
            collector.Visit(expression);
            return collector.Searches;
        }

        private sealed class SearchCollector : ExpressionVisitor
        {
            public List<MethodCallExpression> Searches { get; } = [];

            protected override Expression VisitMethodCall(MethodCallExpression node)
            {
                if (IsSearch(node))
                {
                    Searches.Add(node);
                }

                return base.VisitMethodCall(node);
            }
        }

    }

    /// <summary>Replaces each located <c>Search(source, query)</c> with <c>Where(source, e =&gt; ids.Contains(e.Id))</c>.</summary>
    private sealed class SearchToWhereRewriter(IReadOnlyDictionary<MethodCallExpression, string[]> idsBySearch)
        : ExpressionVisitor
    {
        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            if (IsSearch(node) && idsBySearch.TryGetValue(node, out var ids))
            {
                // Visit the source first so a nested Search (chained Search calls) is rewritten too.
                var source = Visit(node.Arguments[0]);
                var elementType = node.Method.GetGenericArguments()[0];
                return BuildIdFilter(source, elementType, ids);
            }

            return base.VisitMethodCall(node);
        }
    }
}
