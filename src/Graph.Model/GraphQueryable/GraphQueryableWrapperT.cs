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
internal sealed class GraphQueryableWrapper<T>(
    IQueryable<T> queryable,
    IGraph graph,
    IGraphQueryProvider provider) : IGraphQueryable<T>, IOrderedGraphQueryable<T>
{
    public IGraph Graph { get; } = graph ?? throw new ArgumentNullException(nameof(graph));
    public IGraphQueryProvider Provider { get; } = provider ?? throw new ArgumentNullException(nameof(provider));

    // IQueryable members
    public Type ElementType => queryable.ElementType;
    public Expression Expression => queryable.Expression;
    IQueryProvider IQueryable.Provider => Provider;

    // IEnumerable members
    public IEnumerator<T> GetEnumerator() => queryable.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable)queryable).GetEnumerator();

    // Async execution methods
    public Task<List<T>> ToListAsync(CancellationToken cancellationToken = default) =>
        Provider.ExecuteAsync<List<T>>(Expression, cancellationToken);

    public Task<T?> FirstOrDefaultAsync(CancellationToken cancellationToken = default) =>
        Provider.ExecuteAsync<T?>(Expression, cancellationToken);

    public Task<T> FirstAsync(CancellationToken cancellationToken = default) =>
        Provider.ExecuteAsync<T>(Expression, cancellationToken);

    public Task<bool> AnyAsync(CancellationToken cancellationToken = default) =>
        Provider.ExecuteAsync<bool>(Expression, cancellationToken);

    public Task<int> CountAsync(CancellationToken cancellationToken = default) =>
        Provider.ExecuteAsync<int>(Expression, cancellationToken);

    public Task<T> SingleAsync(CancellationToken cancellationToken = default) =>
        Provider.ExecuteAsync<T>(Expression, cancellationToken);

    public Task<T> LastAsync(CancellationToken cancellationToken = default) =>
        Provider.ExecuteAsync<T>(Expression, cancellationToken);

    public Task<T?> LastOrDefaultAsync(CancellationToken cancellationToken = default) =>
        Provider.ExecuteAsync<T?>(Expression, cancellationToken);

    public Task<bool> AllAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default) =>
        Provider.ExecuteAsync<bool>(Expression, cancellationToken);

    public Task<int> CountAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default) =>
        Provider.ExecuteAsync<int>(Expression, cancellationToken);

    public Task<bool> AnyAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default) =>
        Provider.ExecuteAsync<bool>(Expression, cancellationToken);

    public Task<T?> MaxAsync(CancellationToken cancellationToken = default) =>
        Provider.ExecuteAsync<T?>(Expression, cancellationToken);

    public Task<TResult?> MaxAsync<TResult>(Expression<Func<T, TResult>> selector, CancellationToken cancellationToken = default) =>
        Provider.ExecuteAsync<TResult?>(Expression, cancellationToken);

    public Task<T?> MinAsync(CancellationToken cancellationToken = default) =>
        Provider.ExecuteAsync<T?>(Expression, cancellationToken);

    public Task<TResult?> MinAsync<TResult>(Expression<Func<T, TResult>> selector, CancellationToken cancellationToken = default) =>
        Provider.ExecuteAsync<TResult?>(Expression, cancellationToken);
}