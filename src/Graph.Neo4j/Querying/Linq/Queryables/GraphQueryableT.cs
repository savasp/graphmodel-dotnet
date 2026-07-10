// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Neo4j.Querying.Linq.Queryables;

using System.Linq.Expressions;
using Cvoya.Graph.Neo4j.Core;
using Cvoya.Graph.Neo4j.Querying.Linq.Providers;


internal sealed class GraphQueryable<T> : GraphQueryableBase<T>, IGraphQueryable<T>, IOrderedGraphQueryable<T>
{
    public GraphQueryable(GraphQueryProvider provider, GraphContext context, GraphTransaction? transaction, Expression expression)
        : base(typeof(T), provider, context, transaction, expression, GraphQueryableKind.General)
    {
    }
}
