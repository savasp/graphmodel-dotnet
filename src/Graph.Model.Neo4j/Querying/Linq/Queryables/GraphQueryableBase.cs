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

namespace Cvoya.Graph.Model.Neo4j.Querying.Linq.Queryables;

using System.Collections;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;
using Cvoya.Graph.Model.Neo4j.Core;
using Cvoya.Graph.Model.Neo4j.Linq.Helpers;
using Cvoya.Graph.Model.Neo4j.Querying.Linq.Providers;


internal abstract class GraphQueryableBase<T> : IGraphQueryable<T>, IOrderedGraphQueryable<T>, IAsyncEnumerable<T>
{
    protected readonly GraphQueryProvider Provider;
    protected readonly GraphContext Context;
    protected readonly Expression Expression;
    protected GraphTransaction? _transaction;

    protected GraphQueryableBase(
        Type elementType,
        GraphQueryProvider provider,
        GraphContext graphContext,
        GraphTransaction transaction,
        Expression expression)
    {
        ElementType = elementType;
        Provider = provider ?? throw new ArgumentNullException(nameof(provider));
        Context = graphContext ?? throw new ArgumentNullException(nameof(graphContext));
        Expression = expression ?? throw new ArgumentNullException(nameof(expression));
        _transaction = transaction;
    }

    protected GraphTransaction? Transaction => _transaction;

    #region IQueryable Implementation

    public Type ElementType { get; }
    Expression IQueryable.Expression => Expression;
    IQueryProvider IQueryable.Provider => Provider;

    #endregion

    #region IGraphQueryable Implementation

    public IGraph Graph => Context.Graph;
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

    public async IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
    {
        // Execute the query asynchronously
        var results = await Provider.ExecuteAsync<IEnumerable<T>>(Expression, cancellationToken);

        // Yield each result
        foreach (var item in results)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return item;
        }
    }

    #endregion
}
