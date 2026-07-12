// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Querying.Linq;

using System.Linq.Expressions;

internal sealed class GraphQueryable<T> : GraphQueryableBase<T>, IGraphQueryable<T>, IOrderedGraphQueryable<T>
{
    public GraphQueryable(IStreamingGraphQueryProvider provider, Expression expression)
        : base(typeof(T), provider, expression, GraphQueryableKind.General)
    {
    }
}
