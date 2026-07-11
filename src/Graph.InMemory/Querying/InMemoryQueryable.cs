// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.InMemory.Querying;

using System.Collections;
using System.Linq.Expressions;

/// <summary>
/// The single queryable shape the in-memory provider returns from every root and every composed
/// operator. Enumeration routes through <see cref="InMemoryQueryProvider"/>, which compiles the
/// expression to the shared <c>GraphQueryModel</c> and interprets it with LINQ-to-objects.
/// </summary>
internal sealed class InMemoryQueryable<T> : IGraphQueryable<T>, IOrderedGraphQueryable<T>
{
    private readonly InMemoryQueryProvider _provider;

    public InMemoryQueryable(InMemoryQueryProvider provider, Expression? expression = null)
    {
        _provider = provider;
        Expression = expression ?? Expression.Constant(new RootPlaceholder(), typeof(IGraphQueryable<T>));
    }

    public IGraph Graph => _provider.Graph;

    public IGraphQueryProvider Provider => _provider;

    public Type ElementType => typeof(T);

    public Expression Expression { get; }

    IQueryProvider IQueryable.Provider => _provider;

    public IEnumerator<T> GetEnumerator()
    {
        var results = _provider.Execute<IEnumerable<T>>(Expression) ?? [];
        return results.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default) =>
        _provider.Stream<T>(Expression, cancellationToken).GetAsyncEnumerator(cancellationToken);

    /// <summary>
    /// The value inside a root <see cref="ConstantExpression"/>. The shared query model builder
    /// reads only <see cref="IQueryable.ElementType"/> off a root constant; everything else is
    /// deliberately unreachable.
    /// </summary>
    private sealed class RootPlaceholder : IGraphQueryable<T>
    {
        public Type ElementType => typeof(T);

        public IGraph Graph => throw Unreachable();

        public IGraphQueryProvider Provider => throw Unreachable();

        public Expression Expression => throw Unreachable();

        IQueryProvider IQueryable.Provider => throw Unreachable();

        public IEnumerator<T> GetEnumerator() => throw Unreachable();

        IEnumerator IEnumerable.GetEnumerator() => throw Unreachable();

        public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default) =>
            throw Unreachable();

        private static InvalidOperationException Unreachable() =>
            new("The root placeholder only carries the element type; it is never executed.");
    }
}
