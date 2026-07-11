// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Age.Querying.Linq.Queryables;

using System.Linq.Expressions;
using Cvoya.Graph.Age.Core;
using Cvoya.Graph.Age.Querying.Linq.Providers;


internal sealed class GraphQueryable<T> : GraphQueryableBase<T>, IGraphQueryable<T>, IOrderedGraphQueryable<T>
{
    public GraphQueryable(GraphQueryProvider provider, AgeGraphContext context, AgeGraphTransaction? transaction, Expression expression)
        : base(typeof(T), provider, context, transaction, expression, GraphQueryableKind.General)
    {
    }
}
