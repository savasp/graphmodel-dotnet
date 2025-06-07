// Copyright 2025 Savas Parastatidis
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//      https://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System.Collections;
using System.Linq.Expressions;

namespace Cvoya.Graph.Model.Neo4j.Linq;

internal class GraphQueryable<T> : GraphQueryable, IGraphQueryable<T>, IOrderedQueryable<T>
{
    internal GraphQueryable(
        GraphQueryProvider provider,
        GraphContext graphContext,
        GraphQueryContext queryContext,
        Expression? expression = null,
        GraphTransaction? transaction = null) :
        base(typeof(T), provider, graphContext, queryContext, expression, transaction)
    {
    }

    IGraph IGraphQueryable<T>.Graph => GraphContext.Graph;

    IGraphQueryProvider IGraphQueryable<T>.Provider => Provider;

    IQueryProvider IQueryable.Provider => Provider;

    public Task<bool> AllAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task<bool> AnyAsync(CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task<bool> AnyAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task<int> CountAsync(CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task<int> CountAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task<T> FirstAsync(CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task<T?> FirstOrDefaultAsync(CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public IEnumerator<T> GetEnumerator()
    {
        var result = Provider.Execute<IEnumerable<T>>(Expression);
        return result.GetEnumerator();
    }

    public Task<T> LastAsync(CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task<T?> LastOrDefaultAsync(CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task<T?> MaxAsync(CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task<TResult?> MaxAsync<TResult>(Expression<Func<T, TResult>> selector, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task<T?> MinAsync(CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task<TResult?> MinAsync<TResult>(Expression<Func<T, TResult>> selector, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task<T> SingleAsync(CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task<List<T>> ToListAsync(CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

}

internal class GraphQueryable
{
    public GraphQueryable(
        Type elementType,
        GraphQueryProvider provider,
        GraphContext graphContext,
        GraphQueryContext queryContext,
        Expression? expression = null,
        GraphTransaction? transaction = null
    )
    {
        ElementType = elementType;
        Provider = provider;
        QueryContext = queryContext;
        GraphContext = graphContext;
        Expression = expression ?? Expression.Constant(this);
        Transaction = transaction;
    }

    public Graph Graph => GraphContext.Graph;
    public Type ElementType { get; }
    public GraphQueryProvider Provider { get; }
    public Expression Expression { get; }
    public GraphContext GraphContext { get; }
    public GraphQueryContext QueryContext { get; }
    public GraphTransaction? Transaction { get; }
}
