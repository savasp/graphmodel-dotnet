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

namespace Cvoya.Graph.Model;

/// <summary>
/// Wraps an IQueryable to provide <see cref="IGraphQueryable{TElement}"/> functionality.
/// </summary>
internal sealed class GraphRelationshipQueryableWrapper<TRel>(
    IQueryable<TRel> queryable,
    IGraph graph,
    IGraphQueryProvider provider) : IGraphRelationshipQueryable<TRel>, IOrderedGraphRelationshipQueryable<TRel>
    where TRel : IRelationship
{
    public IGraph Graph { get; } = graph ?? throw new ArgumentNullException(nameof(graph));
    public IGraphQueryProvider Provider { get; } = provider ?? throw new ArgumentNullException(nameof(provider));

    // IQueryable members
    public Type ElementType => queryable.ElementType;
    public Expression Expression => queryable.Expression;
    IQueryProvider IQueryable.Provider => Provider;

    // IEnumerable members
    public IEnumerator<TRel> GetEnumerator() => queryable.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable)queryable).GetEnumerator();

    // Async execution methods
    public Task<List<TRel>> ToListAsync(CancellationToken cancellationToken = default) =>
        Provider.ExecuteAsync<List<TRel>>(Expression, cancellationToken);

    public Task<TRel?> FirstOrDefaultAsync(CancellationToken cancellationToken = default) =>
        Provider.ExecuteAsync<TRel?>(Expression, cancellationToken);

    public Task<TRel> FirstAsync(CancellationToken cancellationToken = default) =>
        Provider.ExecuteAsync<TRel>(Expression, cancellationToken);

    public Task<bool> AnyAsync(CancellationToken cancellationToken = default) =>
        Provider.ExecuteAsync<bool>(Expression, cancellationToken);

    public Task<int> CountAsync(CancellationToken cancellationToken = default) =>
        Provider.ExecuteAsync<int>(Expression, cancellationToken);

    public Task<TRel> SingleAsync(CancellationToken cancellationToken = default) =>
        Provider.ExecuteAsync<TRel>(Expression, cancellationToken);

    public Task<TRel> LastAsync(CancellationToken cancellationToken = default) =>
        Provider.ExecuteAsync<TRel>(Expression, cancellationToken);

    public Task<TRel?> LastOrDefaultAsync(CancellationToken cancellationToken = default) =>
        Provider.ExecuteAsync<TRel?>(Expression, cancellationToken);

    public Task<bool> AllAsync(Expression<Func<TRel, bool>> predicate, CancellationToken cancellationToken = default) =>
        Provider.ExecuteAsync<bool>(Expression, cancellationToken);

    public Task<int> CountAsync(Expression<Func<TRel, bool>> predicate, CancellationToken cancellationToken = default) =>
        Provider.ExecuteAsync<int>(Expression, cancellationToken);

    public Task<bool> AnyAsync(Expression<Func<TRel, bool>> predicate, CancellationToken cancellationToken = default) =>
        Provider.ExecuteAsync<bool>(Expression, cancellationToken);

    public Task<TRel?> MaxAsync(CancellationToken cancellationToken = default) =>
        Provider.ExecuteAsync<TRel?>(Expression, cancellationToken);

    public Task<TResult?> MaxAsync<TResult>(Expression<Func<TRel, TResult>> selector, CancellationToken cancellationToken = default) =>
        Provider.ExecuteAsync<TResult?>(Expression, cancellationToken);

    public Task<TRel?> MinAsync(CancellationToken cancellationToken = default) =>
        Provider.ExecuteAsync<TRel?>(Expression, cancellationToken);

    public Task<TResult?> MinAsync<TResult>(Expression<Func<TRel, TResult>> selector, CancellationToken cancellationToken = default) =>
        Provider.ExecuteAsync<TResult?>(Expression, cancellationToken);

    public IGraphTraversalQueryable<TSource, TRel, TTarget> Traverse<TSource, TTarget>()
        where TSource : INode
        where TTarget : INode => Provider.CreateTraversalQuery<TSource, TRel, TTarget>(Expression);
}
