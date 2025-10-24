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

namespace Cvoya.Graph.Model.Age.Querying.Linq.Queryables;

using System.Collections;
using System.Linq.Expressions;
using Cvoya.Graph.Model.Age.Core;
using Cvoya.Graph.Model.Age.Querying.Linq.Providers;

/// <summary>
/// Base class for AGE graph queryables.
/// </summary>
internal abstract class AgeGraphQueryableBase<TElement> : IGraphQueryable<TElement>
{
    protected AgeGraphQueryableBase(
        Type elementType,
        AgeGraphQueryProvider provider,
        AgeGraphContext graphContext,
        AgeGraphTransaction transaction,
        Expression expression)
    {
        ElementType = elementType ?? throw new ArgumentNullException(nameof(elementType));
        Provider = provider ?? throw new ArgumentNullException(nameof(provider));
        GraphContext = graphContext ?? throw new ArgumentNullException(nameof(graphContext));
        Transaction = transaction ?? throw new ArgumentNullException(nameof(transaction));
        Expression = expression ?? throw new ArgumentNullException(nameof(expression));
    }

    public Type ElementType { get; }

    public Expression Expression { get; }

    public IGraphQueryProvider Provider { get; }

    public IGraph Graph => GraphContext.Graph;

    protected AgeGraphContext GraphContext { get; }

    protected AgeGraphTransaction Transaction { get; }

    IQueryProvider IQueryable.Provider => Provider;

    public IEnumerator<TElement> GetEnumerator()
    {
        return Provider.Execute<IEnumerable<TElement>>(Expression).GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    public IAsyncEnumerator<TElement> GetAsyncEnumerator(CancellationToken cancellationToken = default)
    {
        return new AsyncEnumerator(this, cancellationToken);
    }

    private sealed class AsyncEnumerator : IAsyncEnumerator<TElement>
    {
        private readonly AgeGraphQueryableBase<TElement> _queryable;
        private readonly CancellationToken _cancellationToken;
        private IEnumerator<TElement>? _enumerator;

        public AsyncEnumerator(AgeGraphQueryableBase<TElement> queryable, CancellationToken cancellationToken)
        {
            _queryable = queryable;
            _cancellationToken = cancellationToken;
        }

        public TElement Current => _enumerator != null ? _enumerator.Current : throw new InvalidOperationException("Enumerator not initialized");

        public async ValueTask<bool> MoveNextAsync()
        {
            _enumerator ??= await _queryable.Provider
                .ExecuteAsync<IEnumerable<TElement>>(_queryable.Expression, _cancellationToken)
                .ContinueWith(t => t.Result.GetEnumerator(), _cancellationToken);

            return _enumerator.MoveNext();
        }

        public ValueTask DisposeAsync()
        {
            _enumerator?.Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
