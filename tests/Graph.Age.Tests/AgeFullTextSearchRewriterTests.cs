// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Age.Tests;

using System.Linq.Expressions;
using System.Reflection;
using Cvoya.Graph.Age.Querying;
using Cvoya.Graph.CompatibilityTests;

/// <summary>
/// Provider-free tests for the full-text expression rewriter's detection and rewrite halves. Phase 1
/// (the database call) is not exercised here; ids are supplied directly.
/// </summary>
public sealed class AgeFullTextSearchRewriterTests
{
    private static readonly MethodInfo SearchDefinition = typeof(GraphQueryableExtensions)
        .GetMethods(BindingFlags.Public | BindingFlags.Static)
        .Single(m => m.Name == nameof(GraphQueryableExtensions.Search) && m.IsGenericMethodDefinition);

    private static readonly MethodInfo PathSegmentsDefinition = typeof(GraphTraversalExtensions)
        .GetMethods(BindingFlags.Public | BindingFlags.Static)
        .Single(m => m.Name == nameof(GraphTraversalExtensions.PathSegments)
            && m.IsGenericMethodDefinition
            && m.GetParameters().Length == 1);

    private static MethodCallExpression Search<T>(Expression source, string query) =>
        Expression.Call(SearchDefinition.MakeGenericMethod(typeof(T)), source, Expression.Constant(query));

    private static ParameterExpression Root<T>() =>
        Expression.Parameter(typeof(IGraphQueryable<T>), "src");

    [Fact]
    public void LocateSearches_FindsSearchByMethodInfo_NotByName()
    {
        var search = Search<Person>(Root<Person>(), "cloud");

        var located = AgeFullTextSearchRewriter.LocateSearches(search);

        Assert.Single(located);
        Assert.Same(search, located[0]);
        Assert.True(AgeFullTextSearchRewriter.IsSearch(search));
    }

    [Fact]
    public void ApplyRewrite_ReplacesSearchWithWhereContainsOverId_RecoveringElementType()
    {
        var root = Root<Person>();
        var search = Search<Person>(root, "cloud");
        var ids = new[] { "id-1", "id-2" };

        var rewritten = AgeFullTextSearchRewriter.ApplyRewrite(
            search,
            new Dictionary<MethodCallExpression, string[]> { [search] = ids });

        // Search(source, "cloud") becomes Where(source, e => ids.Contains(e.Id)).
        var where = Assert.IsAssignableFrom<MethodCallExpression>(rewritten);
        Assert.Equal(nameof(GraphQueryableExtensions.Where), where.Method.Name);
        Assert.Equal(typeof(Person), where.Method.GetGenericArguments()[0]);
        Assert.Same(root, where.Arguments[0]);

        var predicate = Assert.IsAssignableFrom<LambdaExpression>(StripQuote(where.Arguments[1]));
        Assert.Equal(typeof(Person), predicate.Parameters[0].Type);
        var contains = Assert.IsAssignableFrom<MethodCallExpression>(predicate.Body);
        Assert.Equal(nameof(Enumerable.Contains), contains.Method.Name);
        // The captured id set is the constant first argument; e.Id is the value being tested.
        var constant = Assert.IsAssignableFrom<ConstantExpression>(contains.Arguments[0]);
        Assert.Equal(ids, Assert.IsType<string[]>(constant.Value));
        var idAccess = Assert.IsAssignableFrom<MemberExpression>(contains.Arguments[1]);
        Assert.Equal(nameof(IEntity.Id), idAccess.Member.Name);
    }

    [Fact]
    public void ApplyRewrite_SearchAsTraversalSource_PreservesTraversalOverIdFilter()
    {
        var root = Root<Person>();
        var search = Search<Person>(root, "q");
        var traversal = Expression.Call(
            PathSegmentsDefinition.MakeGenericMethod(typeof(Person), typeof(KnowsWell), typeof(Person)),
            search);
        var ids = new[] { "id-1", "id-2" };

        var rewritten = Assert.IsAssignableFrom<MethodCallExpression>(AgeFullTextSearchRewriter.ApplyRewrite(
            traversal,
            new Dictionary<MethodCallExpression, string[]> { [search] = ids }));

        Assert.Equal(nameof(GraphTraversalExtensions.PathSegments), rewritten.Method.Name);
        var where = Assert.IsAssignableFrom<MethodCallExpression>(rewritten.Arguments[0]);
        Assert.Equal(nameof(GraphQueryableExtensions.Where), where.Method.Name);
        Assert.Same(root, where.Arguments[0]);
    }

    private static Expression StripQuote(Expression expression) =>
        expression is UnaryExpression { NodeType: ExpressionType.Quote } quote ? quote.Operand : expression;
}
