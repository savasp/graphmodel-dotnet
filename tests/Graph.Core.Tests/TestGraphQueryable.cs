// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Core.Tests;

using System.Collections;
using System.Linq.Expressions;
using Cvoya.Graph.Querying;

// Expression-tree-only test doubles shared by the query-model tests: they let a query be composed
// so its expression can be inspected, and refuse to execute.
internal sealed class TestGraphQueryable<T> : IOrderedGraphQueryable<T>
{
    public TestGraphQueryable()
    {
        Provider = new TestGraphQueryProvider();
        Expression = Expression.Constant(this);
    }

    public TestGraphQueryable(TestGraphQueryProvider provider, Expression expression)
    {
        Provider = provider;
        Expression = expression;
    }

    public Type ElementType => typeof(T);

    public Expression Expression { get; }

    public IGraphQueryProvider Provider { get; }

    IQueryProvider IQueryable.Provider => Provider;

    public IGraph Graph => null!;

    public IEnumerator<T> GetEnumerator() => Enumerable.Empty<T>().GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public async IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        yield break;
    }
}

internal sealed class TestGraphQueryProvider : IGraphQueryProvider
{
    public IGraph Graph => null!;

    public IQueryable CreateQuery(Expression expression)
    {
        var elementType = ExtensionUtils.GetQueryableElementType(expression.Type);
        return (IQueryable)Activator.CreateInstance(
            typeof(TestGraphQueryable<>).MakeGenericType(elementType),
            this,
            expression)!;
    }

    public IQueryable<TElement> CreateQuery<TElement>(Expression expression) =>
        new TestGraphQueryable<TElement>(this, expression);

    IGraphQueryable<TElement> IGraphQueryProvider.CreateQuery<TElement>(Expression expression) =>
        new TestGraphQueryable<TElement>(this, expression);

    public object? Execute(Expression expression) =>
        throw new NotSupportedException();

    public TResult Execute<TResult>(Expression expression) =>
        throw new NotSupportedException();

    public Task<TResult> ExecuteAsync<TResult>(Expression expression, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<object?> ExecuteAsync(Expression expression, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();
}
