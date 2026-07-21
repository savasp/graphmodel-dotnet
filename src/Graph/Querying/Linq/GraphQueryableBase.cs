// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Querying.Linq;

using System.Collections;
using System.Collections.Generic;
using System.Linq.Expressions;

internal abstract class GraphQueryableBase<T> : IGraphQueryable<T>, IOrderedGraphQueryable<T>, IGraphQueryableKindProvider
{
    protected readonly IStreamingGraphQueryProvider Provider;
    protected readonly Expression Expression;

    protected GraphQueryableBase(
        Type elementType,
        IStreamingGraphQueryProvider provider,
        Expression expression,
        GraphQueryableKind queryableKind)
    {
        ElementType = elementType;
        Provider = provider ?? throw new ArgumentNullException(nameof(provider));
        Expression = expression ?? throw new ArgumentNullException(nameof(expression));
        QueryableKind = queryableKind;
    }

    public GraphQueryableKind QueryableKind { get; }

    #region IQueryable Implementation

    public Type ElementType { get; }
    Expression IQueryable.Expression => Expression;
    IQueryProvider IQueryable.Provider => Provider;

    #endregion

    #region IGraphQueryable Implementation

    public IGraph Graph => Provider.Graph;
    IGraphQueryProvider IGraphQueryable<T>.Provider => Provider;

    #endregion

    #region IEnumerable Implementation

    public IEnumerator<T> GetEnumerator()
    {
        var result = Provider.Execute<IEnumerable<T>>(Expression);

        // Handle the case where Execute returns null (no results)
        if (result is null)
            return Enumerable.Empty<T>().GetEnumerator();

        return result.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    #endregion

    #region IAsyncEnumerable Implementation

    public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default) =>
        Provider.StreamAsync<T>(Expression, cancellationToken).GetAsyncEnumerator(cancellationToken);

    #endregion

}
